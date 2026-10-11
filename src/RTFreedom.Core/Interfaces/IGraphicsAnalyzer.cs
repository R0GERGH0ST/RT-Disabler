using RTFreedom.Core.Models;

namespace RTFreedom.Core.Interfaces;

/// <summary>
/// Provides hardware capability detection and game graphics pipeline analysis.
/// </summary>
public interface IGraphicsAnalyzer
{
    /// <summary>
    /// Probes the system for GPU hardware details and ray tracing capabilities.
    /// </summary>
    /// <returns>A snapshot of GPU capabilities.</returns>
    Task<GpuCapabilities> DetectGpuCapabilitiesAsync();

    /// <summary>
    /// Analyzes a game installation for ray tracing dependencies and fallback feasibility.
    /// </summary>
    /// <param name="game">The game metadata to inspect.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The ray tracing analysis result.</returns>
    Task<RayTracingAnalysis> AnalyzeGameAsync(GameInfo game, CancellationToken ct = default);
}
