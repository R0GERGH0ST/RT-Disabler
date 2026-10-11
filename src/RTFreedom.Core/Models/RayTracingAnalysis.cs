using RTFreedom.Core.Enums;

namespace RTFreedom.Core.Models;

/// <summary>
/// Contains the complete analysis of a game's ray tracing usage and bypass feasibility.
/// </summary>
/// <param name="GameId">Unique identifier of the analyzed game.</param>
/// <param name="Dependencies">List of ray tracing dependencies found in the game.</param>
/// <param name="HasNonRTFallback">Indicates whether a viable non-RT rasterized fallback path exists.</param>
/// <param name="RequiresHardwareRT">Indicates if the game strictly mandates hardware ray tracing capabilities.</param>
/// <param name="FeasibilityAssessment">Detailed narrative evaluation of bypass feasibility.</param>
/// <param name="DetectedDxrCalls">List of detected DirectX Raytracing API calls or symbols.</param>
/// <param name="DetectedShaderFeatures">List of detected RT shader bytecode features or instruction sets.</param>
/// <param name="RecommendedLevel">Recommended compatibility level for patching.</param>
/// <param name="AnalyzedAt">Timestamp when the analysis was performed.</param>
public record RayTracingAnalysis(
    string GameId,
    List<RTDependency> Dependencies,
    bool HasNonRTFallback,
    bool RequiresHardwareRT,
    string FeasibilityAssessment,
    List<string> DetectedDxrCalls,
    List<string> DetectedShaderFeatures,
    CompatibilityLevel RecommendedLevel,
    DateTime AnalyzedAt);
