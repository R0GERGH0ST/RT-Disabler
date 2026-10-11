using System.Text;

namespace RTFreedom.Graphics;

/// <summary>
/// Result of scanning game binaries and shaders for DirectX Raytracing and Vulkan RT dependencies.
/// </summary>
/// <param name="DetectedDxrCalls">List of detected DXR / Vulkan RT API calls or imported symbols.</param>
/// <param name="DetectedShaderFeatures">List of detected RT shader types and bytecode features.</param>
/// <param name="ScannedBinaries">List of binary files (.exe, .dll) scanned.</param>
/// <param name="ScannedShaders">List of shader files (.dxil, .spv, .hlsl, etc.) inspected.</param>
public record DxrScanResult(
    List<string> DetectedDxrCalls,
    List<string> DetectedShaderFeatures,
    List<string> ScannedBinaries,
    List<string> ScannedShaders);

/// <summary>
/// Scans game executables, dynamic libraries, and shader packages for DXR and Vulkan ray tracing dependencies.
/// </summary>
public class DxrDependencyScanner
{
    private static readonly string[] D3D12RtKeywords =
    [
        "CreateStateObject",
        "BuildRaytracingAccelerationStructure",
        "DispatchRays",
        "GetRaytracingAccelerationStructurePrebuildInfo",
        "CopyRaytracingAccelerationStructurePostBuildInfo",
        "EmitRaytracingAccelerationStructurePostbuildInfo",
        "D3D12_RAYTRACING_PIPELINE_CONFIG",
        "D3D12_RAYTRACING_GEOMETRY_DESC",
        "D3D12_RAYTRACING_ACCELERATION_STRUCTURE_POSTBUILD_INFO_COMPACTED_SIZE_DESC",
        "D3D12_RAYTRACING_ACCELERATION_STRUCTURE_BUILD_FLAGS",
        "CreateRaytracingPipeline"
    ];

    private static readonly string[] VulkanRtKeywords =
    [
        "VK_KHR_ray_tracing_pipeline",
        "VK_KHR_acceleration_structure",
        "VK_KHR_ray_query",
        "vkCmdTraceRaysKHR",
        "vkCmdBuildAccelerationStructuresKHR",
        "vkCreateRayTracingPipelinesKHR",
        "vkGetRayTracingShaderGroupHandlesKHR",
        "VK_NV_ray_tracing"
    ];

    private static readonly string[] ShaderRtKeywords =
    [
        "raygeneration",
        "raygen",
        "closesthit",
        "anyhit",
        "miss",
        "intersection",
        "callable",
        "RaytracingAccelerationStructure",
        "RayQuery",
        "TraceRay"
    ];

    private static readonly string[] ShaderExtensions = [".dxil", ".spv", ".hlsl", ".glsl", ".cso"];

    private const int BufferChunkSize = 64 * 1024; // 64 KB chunk
    private const int MaxScanBytesPerBinary = 32 * 1024 * 1024; // Scan up to 32MB per binary file for speed

    /// <summary>
    /// Scans a game directory and executable for ray tracing API dependencies and shaders.
    /// </summary>
    /// <param name="installPath">Root game directory path.</param>
    /// <param name="executablePath">Optional path to the game executable.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="DxrScanResult"/> detailing detected calls and features.</returns>
    public async Task<DxrScanResult> ScanGameDirectoryAsync(
        string installPath,
        string? executablePath = null,
        CancellationToken ct = default)
    {
        var detectedCalls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var detectedShaderFeatures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var scannedBinaries = new List<string>();
        var scannedShaders = new List<string>();

        if (!Directory.Exists(installPath) && string.IsNullOrEmpty(executablePath))
        {
            return new DxrScanResult([], [], [], []);
        }

        var filesToScan = new List<string>();
        if (!string.IsNullOrEmpty(executablePath) && File.Exists(executablePath))
        {
            filesToScan.Add(executablePath);
        }

        if (Directory.Exists(installPath))
        {
            try
            {
                // Gather binaries (exe, dll) near the executable or root, up to 2 levels deep
                var binFiles = Directory.EnumerateFiles(installPath, "*.*", SearchOption.AllDirectories)
                    .Where(f =>
                    {
                        string ext = Path.GetExtension(f).ToLowerInvariant();
                        return ext is ".exe" or ".dll";
                    })
                    .Take(25); // Cap to avoid scanning hundreds of bundled dependency DLLs

                foreach (var bf in binFiles)
                {
                    if (!filesToScan.Contains(bf, StringComparer.OrdinalIgnoreCase))
                    {
                        filesToScan.Add(bf);
                    }
                }

                // Gather shader files
                var shaderFiles = Directory.EnumerateFiles(installPath, "*.*", SearchOption.AllDirectories)
                    .Where(f => ShaderExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                    .Take(50);

                foreach (var sf in shaderFiles)
                {
                    scannedShaders.Add(sf);
                }
            }
            catch
            {
                // Directory enumeration error handling
            }
        }

        // Scan binaries
        foreach (var binaryPath in filesToScan)
        {
            ct.ThrowIfCancellationRequested();
            scannedBinaries.Add(binaryPath);

            var (calls, shaders) = await ScanBinaryFileAsync(binaryPath, ct).ConfigureAwait(false);
            foreach (var c in calls) detectedCalls.Add(c);
            foreach (var s in shaders) detectedShaderFeatures.Add(s);
        }

        // Scan detected shader files
        foreach (var shaderPath in scannedShaders)
        {
            ct.ThrowIfCancellationRequested();
            var shaderFeatures = await ScanShaderFileAsync(shaderPath, ct).ConfigureAwait(false);
            foreach (var sf in shaderFeatures)
            {
                detectedShaderFeatures.Add(sf);
            }
        }

        return new DxrScanResult(
            DetectedDxrCalls: detectedCalls.OrderBy(c => c).ToList(),
            DetectedShaderFeatures: detectedShaderFeatures.OrderBy(f => f).ToList(),
            ScannedBinaries: scannedBinaries,
            ScannedShaders: scannedShaders
        );
    }

    /// <summary>
    /// Scans a single binary (EXE/DLL) for ASCII and UTF-8 strings matching DXR and Vulkan RT keywords.
    /// </summary>
    /// <param name="filePath">Target binary file path.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Tuple of detected RT calls and shader features.</returns>
    public async Task<(List<string> Calls, List<string> Shaders)> ScanBinaryFileAsync(string filePath, CancellationToken ct = default)
    {
        var foundCalls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var foundShaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!File.Exists(filePath))
        {
            return ([], []);
        }

        try
        {
            await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, BufferChunkSize, useAsync: true);
            var buffer = new byte[BufferChunkSize];
            int overlapSize = 128; // Overlap buffer to avoid missing keywords split across chunks
            var overlap = new byte[overlapSize];
            int overlapLength = 0;
            long bytesReadTotal = 0;

            while (bytesReadTotal < MaxScanBytesPerBinary)
            {
                ct.ThrowIfCancellationRequested();
                int read = await stream.ReadAsync(buffer.AsMemory(0, BufferChunkSize), ct).ConfigureAwait(false);
                if (read <= 0) break;

                bytesReadTotal += read;

                // Combine overlap + current buffer chunk
                var combined = new byte[overlapLength + read];
                if (overlapLength > 0)
                {
                    Buffer.BlockCopy(overlap, 0, combined, 0, overlapLength);
                }
                Buffer.BlockCopy(buffer, 0, combined, overlapLength, read);

                string asciiText = Encoding.ASCII.GetString(combined);

                // Check D3D12 RT keywords
                foreach (var kw in D3D12RtKeywords)
                {
                    if (asciiText.Contains(kw, StringComparison.OrdinalIgnoreCase))
                    {
                        foundCalls.Add(kw);
                    }
                }

                // Check Vulkan RT keywords
                foreach (var kw in VulkanRtKeywords)
                {
                    if (asciiText.Contains(kw, StringComparison.OrdinalIgnoreCase))
                    {
                        foundCalls.Add(kw);
                    }
                }

                // Check Shader RT keywords
                foreach (var kw in ShaderRtKeywords)
                {
                    if (asciiText.Contains(kw, StringComparison.OrdinalIgnoreCase))
                    {
                        foundShaders.Add($"Shader_{kw}");
                    }
                }

                // Save end of current buffer as overlap
                overlapLength = Math.Min(read, overlapSize);
                Buffer.BlockCopy(buffer, read - overlapLength, overlap, 0, overlapLength);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // Fallback gracefully on read errors (locked files, permissions)
        }

        return (foundCalls.ToList(), foundShaders.ToList());
    }

    /// <summary>
    /// Scans a shader file for RT-specific shader stages and instructions.
    /// </summary>
    /// <param name="shaderPath">Path to shader file.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of detected shader stages or features.</returns>
    public async Task<List<string>> ScanShaderFileAsync(string shaderPath, CancellationToken ct = default)
    {
        var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!File.Exists(shaderPath))
        {
            return [];
        }

        try
        {
            string ext = Path.GetExtension(shaderPath).ToLowerInvariant();
            byte[] bytes = await File.ReadAllBytesAsync(shaderPath, ct).ConfigureAwait(false);
            string text = Encoding.ASCII.GetString(bytes);

            foreach (var kw in ShaderRtKeywords)
            {
                if (text.Contains(kw, StringComparison.OrdinalIgnoreCase))
                {
                    found.Add($"RTStage_{kw}_{ext.TrimStart('.')}");
                }
            }

            // Check SPIR-V OpCapability RayTracingKHR or RayQueryKHR
            if (ext == ".spv" && bytes.Length >= 4)
            {
                // SPIR-V magic number 0x07230203
                if ((bytes[0] == 0x03 && bytes[1] == 0x02 && bytes[2] == 0x23 && bytes[3] == 0x07) ||
                    (bytes[0] == 0x07 && bytes[1] == 0x23 && bytes[2] == 0x02 && bytes[3] == 0x03))
                {
                    if (text.Contains("RayTracingKHR", StringComparison.OrdinalIgnoreCase) ||
                        text.Contains("RayQueryKHR", StringComparison.OrdinalIgnoreCase))
                    {
                        found.Add("SPIR-V_RayTracingExtension");
                    }
                }
            }

            // Check DXIL container header 'DXBC'
            if (ext is ".dxil" or ".cso" && bytes.Length >= 4)
            {
                if (bytes[0] == (byte)'D' && bytes[1] == (byte)'X' && bytes[2] == (byte)'B' && bytes[3] == (byte)'C')
                {
                    if (text.Contains("lib_6_", StringComparison.OrdinalIgnoreCase) ||
                        text.Contains("raygeneration", StringComparison.OrdinalIgnoreCase))
                    {
                        found.Add("DXIL_RaytracingLibrary");
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // Ignore file access issues gracefully
        }

        return found.ToList();
    }
}
