using RTFreedom.Core.Enums;
using RTFreedom.Core.Models;
using RTFreedom.Graphics;
using Xunit;

namespace RTFreedom.Tests;

public class GraphicsAnalysisTests : IDisposable
{
    private readonly string _testTempDir;

    public GraphicsAnalysisTests()
    {
        _testTempDir = Path.Combine(Path.GetTempPath(), "RTFreedom_GfxTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testTempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testTempDir))
            {
                Directory.Delete(_testTempDir, true);
            }
        }
        catch
        {
            // Ignore cleanup exceptions
        }
    }

    [Fact]
    public async Task DxrDependencyScanner_DetectsD3D12AndVulkanRtKeywordsInBinaries()
    {
        var scanner = new DxrDependencyScanner();
        var fakeExePath = Path.Combine(_testTempDir, "GameDemo.exe");

        // Write a binary file containing embedded DXR and Vulkan RT symbol strings
        byte[] buffer = new byte[4096];
        string symbols = "SomeCode...DispatchRays...MoreCode...VK_KHR_ray_tracing_pipeline...vkCmdTraceRaysKHR...BuildRaytracingAccelerationStructure...End";
        System.Text.Encoding.ASCII.GetBytes(symbols, 0, symbols.Length, buffer, 0);
        await File.WriteAllBytesAsync(fakeExePath, buffer);

        var result = await scanner.ScanGameDirectoryAsync(_testTempDir, fakeExePath);

        Assert.NotEmpty(result.DetectedDxrCalls);
        Assert.Contains("DispatchRays", result.DetectedDxrCalls);
        Assert.Contains("VK_KHR_ray_tracing_pipeline", result.DetectedDxrCalls);
        Assert.Contains("vkCmdTraceRaysKHR", result.DetectedDxrCalls);
        Assert.Contains("BuildRaytracingAccelerationStructure", result.DetectedDxrCalls);
    }

    [Fact]
    public async Task RenderingPathAnalyzer_AccuratelyEvaluatesIndianaJonesPureRt()
    {
        var analyzer = new RenderingPathAnalyzer();
        var game = new GameInfo(
            Id: "indy-1",
            Title: "Indiana Jones and the Great Circle",
            ExecutablePath: @"C:\Games\Indy\TheGreatCircle.exe",
            InstallPath: @"C:\Games\Indy",
            Version: "1.0",
            Publisher: "Bethesda Softworks",
            Platform: GamePlatform.Steam,
            GraphicsApi: RenderingApi.Vulkan,
            ConfigurationFiles: [],
            ShaderFiles: [],
            DetectedAt: DateTime.UtcNow
        );

        var scanResult = new DxrScanResult(
            DetectedDxrCalls: ["VK_KHR_ray_tracing_pipeline", "vkCmdTraceRaysKHR"],
            DetectedShaderFeatures: ["RayGenKHR", "ClosestHitKHR"],
            ScannedBinaries: ["TheGreatCircle.exe"],
            ScannedShaders: []
        );

        var assessment = await analyzer.AnalyzeRenderingPathsAsync(game, scanResult);

        Assert.False(assessment.HasNonRTFallback);
        Assert.True(assessment.RequiresHardwareRT);
        Assert.Equal(CompatibilityLevel.Incompatible, assessment.RecommendedLevel);
        Assert.Contains("cannot be safely bypassed", assessment.FeasibilityAssessment);
    }

    [Fact]
    public async Task RenderingPathAnalyzer_AccuratelyEvaluatesGearsOfWarEDaySoftwareLumenFallback()
    {
        var analyzer = new RenderingPathAnalyzer();
        var game = new GameInfo(
            Id: "gears-eday-1",
            Title: "Gears of War: E-Day",
            ExecutablePath: @"C:\Games\Gears\GearGame\Binaries\Win64\GearsEDay.exe",
            InstallPath: @"C:\Games\Gears",
            Version: "1.0",
            Publisher: "Xbox Game Studios",
            Platform: GamePlatform.Steam,
            GraphicsApi: RenderingApi.DirectX12,
            ConfigurationFiles: [],
            ShaderFiles: [],
            DetectedAt: DateTime.UtcNow
        );

        var scanResult = new DxrScanResult(
            DetectedDxrCalls: ["CreateStateObject", "DispatchRays"],
            DetectedShaderFeatures: ["RayQuery"],
            ScannedBinaries: ["GearsEDay.exe"],
            ScannedShaders: []
        );

        var assessment = await analyzer.AnalyzeRenderingPathsAsync(game, scanResult);

        Assert.True(assessment.HasNonRTFallback);
        Assert.False(assessment.RequiresHardwareRT);
        Assert.Equal(CompatibilityLevel.FullyCompatible, assessment.RecommendedLevel);
        Assert.Contains("Software Lumen", assessment.FeasibilityAssessment);
    }
}
