using RTFreedom.Core.Interfaces;
using RTFreedom.Core.Models;

namespace RTFreedom.Graphics;

/// <summary>
/// Implements <see cref="IGraphicsAnalyzer"/> by combining GPU capability probing,
/// binary DXR/Vulkan dependency scanning, and rendering path fallback analysis.
/// </summary>
public class GraphicsAnalysisService : IGraphicsAnalyzer
{
    private readonly GpuCapabilityDetector _gpuDetector;
    private readonly DxrDependencyScanner _dxrScanner;
    private readonly RenderingPathAnalyzer _pathAnalyzer;

    /// <summary>
    /// Initializes a new instance of the <see cref="GraphicsAnalysisService"/> class.
    /// </summary>
    /// <param name="gpuDetector">Optional custom GPU detector instance.</param>
    /// <param name="dxrScanner">Optional custom DXR scanner instance.</param>
    /// <param name="pathAnalyzer">Optional custom rendering path analyzer instance.</param>
    public GraphicsAnalysisService(
        GpuCapabilityDetector? gpuDetector = null,
        DxrDependencyScanner? dxrScanner = null,
        RenderingPathAnalyzer? pathAnalyzer = null)
    {
        _gpuDetector = gpuDetector ?? new GpuCapabilityDetector();
        _dxrScanner = dxrScanner ?? new DxrDependencyScanner();
        _pathAnalyzer = pathAnalyzer ?? new RenderingPathAnalyzer();
    }

    /// <inheritdoc />
    public async Task<GpuCapabilities> DetectGpuCapabilitiesAsync()
    {
        return await _gpuDetector.DetectCapabilitiesAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<RayTracingAnalysis> AnalyzeGameAsync(GameInfo game, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(game);

        // Step 1: Scan game binaries and shaders for DXR / Vulkan RT symbols
        var scanResult = await _dxrScanner.ScanGameDirectoryAsync(
            game.InstallPath,
            game.ExecutablePath,
            ct).ConfigureAwait(false);

        // Step 2: Analyze rendering paths and fallback feasibility
        var assessment = await _pathAnalyzer.AnalyzeRenderingPathsAsync(
            game,
            scanResult,
            ct).ConfigureAwait(false);

        // Step 3: Produce consolidated RayTracingAnalysis model
        return new RayTracingAnalysis(
            GameId: game.Id,
            Dependencies: assessment.Dependencies,
            HasNonRTFallback: assessment.HasNonRTFallback,
            RequiresHardwareRT: assessment.RequiresHardwareRT,
            FeasibilityAssessment: assessment.FeasibilityAssessment,
            DetectedDxrCalls: scanResult.DetectedDxrCalls,
            DetectedShaderFeatures: scanResult.DetectedShaderFeatures,
            RecommendedLevel: assessment.RecommendedLevel,
            AnalyzedAt: DateTime.UtcNow
        );
    }
}
