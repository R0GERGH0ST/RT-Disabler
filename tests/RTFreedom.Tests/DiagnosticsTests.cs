using RTFreedom.Core.Enums;
using RTFreedom.Core.Models;
using RTFreedom.Diagnostics;
using Xunit;

namespace RTFreedom.Tests;

public class DiagnosticsTests : IDisposable
{
    private readonly string _testTempDir;

    public DiagnosticsTests()
    {
        _testTempDir = Path.Combine(Path.GetTempPath(), "RTFreedom_DiagTests_" + Guid.NewGuid().ToString("N"));
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
    public void FileLogger_WritesFormattedEntriesAndCreatesFile()
    {
        var logger = new FileLogger(_testTempDir);
        logger.Log(LogLevel.Info, "Application initialized successfully.");
        logger.Log(LogLevel.Warning, "Found experimental configuration.");

        var logPath = logger.GetLogFilePath();
        Assert.True(File.Exists(logPath));

        var logContent = File.ReadAllText(logPath);
        Assert.Contains("[INFO]", logContent);
        Assert.Contains("Application initialized successfully.", logContent);
        Assert.Contains("[WARNING]", logContent);
        Assert.Contains("Found experimental configuration.", logContent);
    }

    [Fact]
    public async Task DiagnosticReportGenerator_GeneratesDetailedReport()
    {
        var generator = new DiagnosticReportGenerator(_testTempDir);
        var game = new GameInfo(
            Id: "indy-steam",
            Title: "Indiana Jones and the Great Circle",
            ExecutablePath: @"C:\Games\Indy\TheGreatCircle.exe",
            InstallPath: @"C:\Games\Indy",
            Version: "1.0.0",
            Publisher: "Bethesda Softworks",
            Platform: GamePlatform.Steam,
            GraphicsApi: RenderingApi.Vulkan,
            ConfigurationFiles: [],
            ShaderFiles: [],
            DetectedAt: DateTime.UtcNow
        );

        var analysis = new RayTracingAnalysis(
            GameId: game.Id,
            Dependencies: [
                new RTDependency("Global Illumination", RTFeature.RayTracedGI, true, false, "Mandatory RT GI")
            ],
            HasNonRTFallback: false,
            RequiresHardwareRT: true,
            FeasibilityAssessment: "Incompatible on non-RT GPUs due to mandatory Vulkan ray tracing GI pipeline.",
            DetectedDxrCalls: ["vkCmdTraceRaysKHR"],
            DetectedShaderFeatures: ["RayGenKHR"],
            RecommendedLevel: CompatibilityLevel.Incompatible,
            AnalyzedAt: DateTime.UtcNow
        );

        var report = await generator.GenerateReportAsync(game, analysis);

        Assert.NotNull(report);
        Assert.Contains("RT FREEDOM DIAGNOSTIC COMPATIBILITY REPORT", report);
        Assert.Contains("Indiana Jones and the Great Circle", report);
        Assert.Contains("Global Illumination", report);
        Assert.Contains("Incompatible", report);
        Assert.Contains("vkCmdTraceRaysKHR", report);
    }
}
