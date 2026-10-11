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
    List<string> SupportedFeatures);
