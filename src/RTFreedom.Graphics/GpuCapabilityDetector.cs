using System.Management;
using System.Text.RegularExpressions;
using RTFreedom.Core.Models;

namespace RTFreedom.Graphics;

/// <summary>
/// Probes system GPU hardware and driver capabilities to assess ray tracing support.
/// </summary>
public class GpuCapabilityDetector
{
    private static readonly Regex NvidiaRtxRegex = new(@"\bRTX\s*(\d{4}|A\d{3,4}|[0-9]{4}M)?\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex NvidiaGtxRegex = new(@"\bGTX\s*(\d{3,4})\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex AmdRxRegex = new(@"\bRX\s*([6-9]\d{3})\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex IntelArcRegex = new(@"\bArc\b.*?\b[AB]\d{3,4}\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Asynchronously detects the primary GPU hardware capabilities.
    /// </summary>
    /// <returns>A <see cref="GpuCapabilities"/> snapshot.</returns>
    public Task<GpuCapabilities> DetectCapabilitiesAsync()
    {
        return Task.Run(DetectCapabilities);
    }

    /// <summary>
    /// Synchronously detects the primary GPU hardware capabilities querying WMI.
    /// </summary>
    /// <returns>A <see cref="GpuCapabilities"/> snapshot.</returns>
    public GpuCapabilities DetectCapabilities()
    {
        try
        {
            var detectedAdapters = QueryVideoControllers();
            if (detectedAdapters.Count > 0)
            {
                // Select best discrete adapter (prioritizing RT-capable and higher VRAM)
                var primary = detectedAdapters
                    .OrderByDescending(a => a.SupportsRayTracing)
                    .ThenByDescending(a => a.VramBytes)
                    .First();

                return primary;
            }
        }
        catch
        {
            // Fall back to safe default if WMI query fails
        }

        return CreateFallbackCapabilities("Unknown Graphics Adapter", "Unknown", "0.0.0.0", 0);
    }

    private List<GpuCapabilities> QueryVideoControllers()
    {
        var capabilitiesList = new List<GpuCapabilities>();

        if (!OperatingSystem.IsWindows())
        {
            return capabilitiesList;
        }

        using var searcher = new ManagementObjectSearcher(
            "SELECT Name, AdapterRAM, DriverVersion, PNPDeviceID, VideoProcessor, Description FROM Win32_VideoController");

        foreach (var queryObj in searcher.Get())
        {
            if (queryObj is not ManagementObject mo)
            {
                continue;
            }

            string name = mo["Name"]?.ToString() ?? mo["Description"]?.ToString() ?? "Generic Display Adapter";
            string driverVersion = mo["DriverVersion"]?.ToString() ?? "Unknown";
            string pnpDeviceId = mo["PNPDeviceID"]?.ToString() ?? string.Empty;

            // Filter out remote desktop, basic software renderers, and virtual GPUs
            if (name.Contains("Microsoft Basic Display", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Remote Desktop", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("VirtualBox", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("VMware", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            long vram = 0;
            if (mo["AdapterRAM"] != null && long.TryParse(mo["AdapterRAM"].ToString(), out var ramVal))
            {
                // Win32_VideoController AdapterRAM is a uint32, clamped at 4GB in some WMI versions.
                // Negative values occur if signed overflow happens.
                vram = ramVal > 0 ? ramVal : (uint)ramVal;
            }

            string vendor = DetectVendor(name, pnpDeviceId);
            var (supportsRt, supportsDxr11, supportsDxr12, supportsVulkanRt, tier, features) = AnalyzeRtSupport(name, vendor);

            capabilitiesList.Add(new GpuCapabilities(
                Name: name,
                Vendor: vendor,
                DriverVersion: driverVersion,
                VramBytes: vram,
                SupportsRayTracing: supportsRt,
                SupportsDxr11: supportsDxr11,
                SupportsDxr12: supportsDxr12,
                SupportsVulkanRayTracing: supportsVulkanRt,
                RayTracingTier: tier,
                SupportedFeatures: features
            ));
        }

        return capabilitiesList;
    }

    /// <summary>
    /// Identifies the GPU vendor from the adapter name or PNP device ID.
    /// </summary>
    /// <param name="name">Adapter name.</param>
    /// <param name="pnpDeviceId">PNP device identifier.</param>
    /// <returns>Vendor string (NVIDIA, AMD, Intel, or Generic).</returns>
    public static string DetectVendor(string name, string pnpDeviceId)
    {
        if (pnpDeviceId.Contains("VEN_10DE", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("GeForce", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Quadro", StringComparison.OrdinalIgnoreCase))
        {
            return "NVIDIA";
        }

        if (pnpDeviceId.Contains("VEN_1002", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("AMD", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Radeon", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("ATI", StringComparison.OrdinalIgnoreCase))
        {
            return "AMD";
        }

        if (pnpDeviceId.Contains("VEN_8086", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Intel", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Arc", StringComparison.OrdinalIgnoreCase))
        {
            return "Intel";
        }

        return "Generic";
    }

    /// <summary>
    /// Analyzes ray tracing support and tier level based on GPU name and vendor.
    /// </summary>
    private static (bool SupportsRt, bool SupportsDxr11, bool SupportsDxr12, bool SupportsVulkanRt, int Tier, List<string> Features)
        AnalyzeRtSupport(string name, string vendor)
    {
        var features = new List<string>();
        bool supportsRt = false;
        bool supportsDxr11 = false;
        bool supportsDxr12 = false;
        bool supportsVulkanRt = false;
        int tier = 0;

        if (vendor.Equals("NVIDIA", StringComparison.OrdinalIgnoreCase))
        {
            // RTX 20xx, 30xx, 40xx, 50xx, TITAN RTX, RTX A-series
            if (NvidiaRtxRegex.IsMatch(name) || name.Contains("TITAN RTX", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("RTX ", StringComparison.OrdinalIgnoreCase))
            {
                supportsRt = true;
                supportsDxr11 = true;
                supportsVulkanRt = true;
                features.Add("DirectX Raytracing (DXR)");
                features.Add("VK_KHR_ray_tracing_pipeline");
                features.Add("VK_KHR_acceleration_structure");
                features.Add("VK_KHR_ray_query");
                features.Add("NVIDIA RT Cores");

                if (name.Contains("40", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("50", StringComparison.OrdinalIgnoreCase))
                {
                    // Ada Lovelace (RTX 40xx) & Blackwell (RTX 50xx)
                    supportsDxr12 = true;
                    tier = 3;
                    features.Add("Shader Execution Reordering (SER)");
                    features.Add("Opacity Micromap (OMM)");
                    features.Add("Displaced Micro-Mesh (DMM)");
                    features.Add("DXR Tier 1.1 Inline Raytracing");
                }
                else if (name.Contains("30", StringComparison.OrdinalIgnoreCase) ||
                         name.Contains("A2000", StringComparison.OrdinalIgnoreCase) ||
                         name.Contains("A4000", StringComparison.OrdinalIgnoreCase) ||
                         name.Contains("A5000", StringComparison.OrdinalIgnoreCase) ||
                         name.Contains("A6000", StringComparison.OrdinalIgnoreCase))
                {
                    // Ampere (RTX 30xx)
                    supportsDxr12 = true;
                    tier = 2;
                    features.Add("DXR Tier 1.1");
                    features.Add("Concurrent RT/Compute");
                }
                else
                {
                    // Turing (RTX 20xx)
                    supportsDxr12 = false;
                    tier = 1;
                    features.Add("DXR Tier 1.0");
                }
            }
            else if (NvidiaGtxRegex.IsMatch(name) || name.Contains("TITAN X", StringComparison.OrdinalIgnoreCase))
            {
                // GTX 1060 6GB+, 1070, 1080, 1660 have driver-level DXR 1.0 compute shader fallback (Driver 425.31+)
                tier = 0;
                supportsRt = false;
                features.Add("DirectX 12 Rasterization");

                string upper = name.ToUpperInvariant();
                bool isComputeDxrCapable = upper.Contains("1060") || upper.Contains("1070") ||
                                          upper.Contains("1080") || upper.Contains("TITAN X") ||
                                          upper.Contains("1660") || upper.Contains("1650 SUPER");
                if (isComputeDxrCapable)
                {
                    features.Add("DXR Software Emulation (Compute Shader Driver 425.31+)");
                    features.Add("Vulkan Compute Ray Query Emulation");
                    features.Add("RT Freedom Experimental Bypass Eligible");
                }
            }
            else
            {
                features.Add("Direct3D Hardware Acceleration");
            }
        }
        else if (vendor.Equals("AMD", StringComparison.OrdinalIgnoreCase))
        {
            var match = AmdRxRegex.Match(name);
            if (match.Success || name.Contains("RDNA2", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("RDNA3", StringComparison.OrdinalIgnoreCase))
            {
                supportsRt = true;
                supportsDxr11 = true;
                supportsVulkanRt = true;
                features.Add("AMD Ray Accelerators");
                features.Add("DirectX Raytracing (DXR)");
                features.Add("VK_KHR_ray_tracing_pipeline");
                features.Add("VK_KHR_ray_query");

                if (match.Success && (match.Groups[1].Value.StartsWith("7") || match.Groups[1].Value.StartsWith("8") || match.Groups[1].Value.StartsWith("9")))
                {
                    // RDNA3 / RDNA4
                    supportsDxr12 = true;
                    tier = 3;
                    features.Add("RDNA3 Raytracing Pipeline");
                    features.Add("DXR Tier 1.1 Inline Raytracing");
                }
                else
                {
                    // RDNA2 (RX 6000 series)
                    supportsDxr12 = true;
                    tier = 2;
                    features.Add("RDNA2 Ray Accelerators");
                    features.Add("DXR Tier 1.1");
                }
            }
            else
            {
                features.Add("Direct3D / Vulkan Rasterization");
            }
        }
        else if (vendor.Equals("Intel", StringComparison.OrdinalIgnoreCase))
        {
            if (IntelArcRegex.IsMatch(name) || name.Contains("Arc", StringComparison.OrdinalIgnoreCase))
            {
                supportsRt = true;
                supportsDxr11 = true;
                supportsDxr12 = true;
                supportsVulkanRt = true;
                tier = 2;
                features.Add("Intel Xe Ray Tracing Units (RTU)");
                features.Add("DirectX Raytracing (DXR)");
                features.Add("VK_KHR_ray_tracing_pipeline");
                features.Add("VK_KHR_ray_query");
                features.Add("DXR Tier 1.1");
            }
            else
            {
                features.Add("Intel Xe / UHD Graphics Rasterization");
            }
        }

        return (supportsRt, supportsDxr11, supportsDxr12, supportsVulkanRt, tier, features);
    }

    private static GpuCapabilities CreateFallbackCapabilities(string name, string vendor, string driver, long vram)
    {
        return new GpuCapabilities(
            Name: name,
            Vendor: vendor,
            DriverVersion: driver,
            VramBytes: vram,
            SupportsRayTracing: false,
            SupportsDxr11: false,
            SupportsDxr12: false,
            SupportsVulkanRayTracing: false,
            RayTracingTier: 0,
            SupportedFeatures: new List<string> { "Standard Rasterization" }
        );
    }
}
