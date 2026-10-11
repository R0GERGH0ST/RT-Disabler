using RTFreedom.Adapters;
using RTFreedom.Core.Enums;
using RTFreedom.Core.Models;
using Xunit;

namespace RTFreedom.Tests;

public class GameAdaptersTests
{
    // Legacy Non-RT GPU without compute DXR emulation (e.g. Maxwell GTX 960)
    private readonly GpuCapabilities _legacyNonRtGpu = new(
        Name: "NVIDIA GeForce GTX 960",
        Vendor: "NVIDIA",
        DriverVersion: "472.12",
        VramBytes: 2147483648,
        SupportsRayTracing: false,
        SupportsDxr11: false,
        SupportsDxr12: false,
        SupportsVulkanRayTracing: false,
        RayTracingTier: 0,
        SupportedFeatures: new List<string> { "Direct3D Hardware Acceleration" }
    );

    // Pascal / Turing GTX GPU with DXR 1.0 compute shader emulation (Driver 425.31+)
    private readonly GpuCapabilities _gtxPascalGpu = new(
        Name: "NVIDIA GeForce GTX 1080",
        Vendor: "NVIDIA",
        DriverVersion: "551.86",
        VramBytes: 8589934592,
        SupportsRayTracing: false,
        SupportsDxr11: false,
        SupportsDxr12: false,
        SupportsVulkanRayTracing: false,
        RayTracingTier: 0,
        SupportedFeatures: new List<string>
        {
            "DirectX 12 Rasterization",
            "DXR Software Emulation (Compute Shader Driver 425.31+)",
            "Vulkan Compute Ray Query Emulation",
            "RT Freedom Experimental Bypass Eligible"
        }
    );

    // Dedicated Hardware RT GPU (RTX 3070)
    private readonly GpuCapabilities _rtGpu = new(
        Name: "NVIDIA GeForce RTX 3070",
        Vendor: "NVIDIA",
        DriverVersion: "551.86",
        VramBytes: 8589934592,
        SupportsRayTracing: true,
        SupportsDxr11: true,
        SupportsDxr12: true,
        SupportsVulkanRayTracing: true,
        RayTracingTier: 2,
        SupportedFeatures: new List<string> { "DXR 1.1", "VK_KHR_ray_tracing_pipeline", "Ray Query" }
    );

    [Fact]
    public void AdapterRegistry_RegistersAndRetrievesBuiltInAdapters()
    {
        var registry = new AdapterRegistry();
        AdapterLoader.RegisterBuiltInAdapters(registry);

        var adapters = registry.GetAllAdapters();
        Assert.Equal(3, adapters.Count);

        var indyGame = new GameInfo("indy-1", "Indiana Jones and the Great Circle", @"C:\Games\Indy\TheGreatCircle.exe", @"C:\Games\Indy", "1.0", "Bethesda", GamePlatform.Steam, RenderingApi.Vulkan, [], [], DateTime.UtcNow);
        var gearsGame = new GameInfo("gears-1", "Gears of War: E-Day", @"C:\Games\Gears\GearGame\Binaries\Win64\GearsEDay.exe", @"C:\Games\Gears", "1.0", "Xbox Game Studios", GamePlatform.Steam, RenderingApi.DirectX12, [], [], DateTime.UtcNow);
        var doomGame = new GameInfo("doom-1", "DOOM: The Dark Ages", @"C:\Games\Doom\DoomTheDarkAges.exe", @"C:\Games\Doom", "1.0", "Bethesda", GamePlatform.Steam, RenderingApi.Vulkan, [], [], DateTime.UtcNow);

        Assert.IsType<IndianaJonesAdapter>(registry.GetAdapter(indyGame));
        Assert.IsType<GearsOfWarAdapter>(registry.GetAdapter(gearsGame));
        Assert.IsType<DoomDarkAgesAdapter>(registry.GetAdapter(doomGame));
    }

    [Fact]
    public async Task IndianaJonesAdapter_MarksLegacyNonRtGpuAsIncompatible()
    {
        var adapter = new IndianaJonesAdapter();
        var game = new GameInfo("indy-1", "Indiana Jones and the Great Circle", @"C:\Games\Indy\TheGreatCircle.exe", @"C:\Games\Indy", "1.0", "Bethesda", GamePlatform.Steam, RenderingApi.Vulkan, [], [], DateTime.UtcNow);

        var analysis = await adapter.AnalyzeAsync(game, _legacyNonRtGpu);

        Assert.Equal(CompatibilityLevel.Incompatible, analysis.RecommendedLevel);
        Assert.False(analysis.HasNonRTFallback);
        Assert.True(analysis.RequiresHardwareRT);
        Assert.Contains("INCOMPATIBLE", analysis.FeasibilityAssessment);
    }

    [Fact]
    public async Task IndianaJonesAdapter_MarksGtx10xxAsExperimentalWithBypassPatch()
    {
        var adapter = new IndianaJonesAdapter();
        var game = new GameInfo("indy-1", "Indiana Jones and the Great Circle", @"C:\Games\Indy\TheGreatCircle.exe", @"C:\Games\Indy", "1.0", "Bethesda", GamePlatform.Steam, RenderingApi.Vulkan, [], [], DateTime.UtcNow);

        var analysis = await adapter.AnalyzeAsync(game, _gtxPascalGpu);

        Assert.Equal(CompatibilityLevel.ExperimentalOnly, analysis.RecommendedLevel);
        Assert.Contains("EXPERIMENTAL COMPUTE EMULATION", analysis.FeasibilityAssessment);

        var patches = await adapter.GetAvailablePatchesAsync(game, analysis);
        Assert.Contains(patches, p => p.Id == "ij-gtx-compute-bypass");
    }

    [Fact]
    public async Task IndianaJonesAdapter_MarksRtGpuAsPartiallyCompatibleWithTuning()
    {
        var adapter = new IndianaJonesAdapter();
        var game = new GameInfo("indy-1", "Indiana Jones and the Great Circle", @"C:\Games\Indy\TheGreatCircle.exe", @"C:\Games\Indy", "1.0", "Bethesda", GamePlatform.Steam, RenderingApi.Vulkan, [], [], DateTime.UtcNow);

        var analysis = await adapter.AnalyzeAsync(game, _rtGpu);

        Assert.Equal(CompatibilityLevel.PartiallyCompatible, analysis.RecommendedLevel);
        var patches = await adapter.GetAvailablePatchesAsync(game, analysis);
        Assert.NotEmpty(patches);
    }

    [Fact]
    public async Task GearsOfWarAdapter_MarksEDayAsFullyCompatibleViaSoftwareLumen()
    {
        var adapter = new GearsOfWarAdapter();
        var game = new GameInfo("gears-eday-1", "Gears of War: E-Day", @"C:\Games\Gears\GearGame\Binaries\Win64\GearsEDay.exe", @"C:\Games\Gears", "1.0", "Xbox Game Studios", GamePlatform.Steam, RenderingApi.DirectX12, [], [], DateTime.UtcNow);

        var analysis = await adapter.AnalyzeAsync(game, _gtxPascalGpu);

        Assert.Equal(CompatibilityLevel.FullyCompatible, analysis.RecommendedLevel);
        Assert.True(analysis.HasNonRTFallback);
        Assert.False(analysis.RequiresHardwareRT);
        Assert.Contains("SOFTWARE LUMEN FALLBACK", analysis.FeasibilityAssessment);

        var patches = await adapter.GetAvailablePatchesAsync(game, analysis);
        Assert.NotEmpty(patches);
        Assert.Contains(patches, p => p.Id == "gears-eday-software-lumen-fallback");
    }

    [Fact]
    public async Task DoomDarkAgesAdapter_MarksLegacyNonRtGpuAsIncompatible()
    {
        var adapter = new DoomDarkAgesAdapter();
        var game = new GameInfo("doom-1", "DOOM: The Dark Ages", @"C:\Games\Doom\DoomTheDarkAges.exe", @"C:\Games\Doom", "1.0", "Bethesda", GamePlatform.Steam, RenderingApi.Vulkan, [], [], DateTime.UtcNow);

        var analysis = await adapter.AnalyzeAsync(game, _legacyNonRtGpu);

        Assert.Equal(CompatibilityLevel.Incompatible, analysis.RecommendedLevel);
        Assert.False(analysis.HasNonRTFallback);
        Assert.True(analysis.RequiresHardwareRT);
        Assert.Contains("INCOMPATIBLE", analysis.FeasibilityAssessment);
    }

    [Fact]
    public async Task DoomDarkAgesAdapter_MarksGtx10xxAsExperimentalWithFallbackPatch()
    {
        var adapter = new DoomDarkAgesAdapter();
        var game = new GameInfo("doom-1", "DOOM: The Dark Ages", @"C:\Games\Doom\DoomTheDarkAges.exe", @"C:\Games\Doom", "1.0", "Bethesda", GamePlatform.Steam, RenderingApi.Vulkan, [], [], DateTime.UtcNow);

        var analysis = await adapter.AnalyzeAsync(game, _gtxPascalGpu);

        Assert.Equal(CompatibilityLevel.ExperimentalOnly, analysis.RecommendedLevel);
        Assert.Contains("EXPERIMENTAL COMPUTE FALLBACK", analysis.FeasibilityAssessment);

        var patches = await adapter.GetAvailablePatchesAsync(game, analysis);
        Assert.Contains(patches, p => p.Id == "doom-da-gtx-compute-fallback");
    }
}
