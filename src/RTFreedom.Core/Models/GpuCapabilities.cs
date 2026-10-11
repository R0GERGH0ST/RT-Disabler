namespace RTFreedom.Core.Models;

/// <summary>
/// Details the hardware and driver capabilities of the active GPU.
/// </summary>
/// <param name="Name">Device name or model of the GPU adapter.</param>
/// <param name="Vendor">Hardware vendor name (e.g. NVIDIA, AMD, Intel).</param>
/// <param name="DriverVersion">Installed graphics driver version string.</param>
/// <param name="VramBytes">Dedicated video memory in bytes.</param>
/// <param name="SupportsRayTracing">Whether the GPU supports any form of hardware ray tracing.</param>
/// <param name="SupportsDxr11">Whether DXR 1.1 tier features are supported.</param>
/// <param name="SupportsDxr12">Whether DirectX 12 ray tracing tier 1.1+ is supported.</param>
/// <param name="SupportsVulkanRayTracing">Whether Vulkan VK_KHR_ray_tracing extensions are supported.</param>
/// <param name="RayTracingTier">Ray tracing capability tier (0=none, 1=basic DXR1.0, 2=DXR1.1, 3=full inline RT).</param>
/// <param name="SupportedFeatures">List of specific graphics and ray tracing extension names supported.</param>
public record GpuCapabilities(
    string Name,
    string Vendor,
    string DriverVersion,
    long VramBytes,
    bool SupportsRayTracing,
    bool SupportsDxr11,
    bool SupportsDxr12,
    bool SupportsVulkanRayTracing,
    int RayTracingTier,
    List<string> SupportedFeatures)
{
    /// <summary>
    /// Gets whether this GPU supports driver-level compute emulation for DXR or ray queries
    /// (e.g. NVIDIA GTX 1060 6GB+, 1070, 1080, 1080 Ti, Titan X, GTX 1660 series with Driver 425.31+).
    /// </summary>
    public bool SupportsComputeRayTracing =>
        SupportsRayTracing ||
        SupportsVulkanRayTracing ||
        SupportedFeatures.Any(f => f.Contains("Compute Shader", StringComparison.OrdinalIgnoreCase) ||
                                  f.Contains("DXR Software Emulation", StringComparison.OrdinalIgnoreCase)) ||
        IsPascalOrTuringComputeCapable();

    private bool IsPascalOrTuringComputeCapable()
    {
        if (!Vendor.Equals("NVIDIA", StringComparison.OrdinalIgnoreCase)) return false;
        string n = Name.ToUpperInvariant();
        return (n.Contains("1060") && VramBytes >= 5L * 1024 * 1024 * 1024) ||
               n.Contains("1070") || n.Contains("1080") || n.Contains("TITAN X") ||
               n.Contains("1660") || n.Contains("1650 SUPER");
    }
}
