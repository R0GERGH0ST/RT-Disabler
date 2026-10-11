using RTFreedom.Core.Enums;

namespace RTFreedom.Core.Models;

/// <summary>
/// Represents a specific ray tracing dependency or feature utilized by a game engine.
/// </summary>
/// <param name="Feature">Readable name of the feature (e.g. "Global Illumination", "Reflections").</param>
/// <param name="Type">Ray tracing feature category.</param>
/// <param name="IsMandatory">Indicates whether the feature is mandatory for the engine to render.</param>
/// <param name="HasFallback">Indicates whether a non-ray-traced fallback implementation exists.</param>
/// <param name="Description">Detailed description of the dependency and its visual impact.</param>
public record RTDependency(
    string Feature,
    RTFeature Type,
    bool IsMandatory,
    bool HasFallback,
    string Description);
