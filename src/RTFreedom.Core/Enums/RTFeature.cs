namespace RTFreedom.Core.Enums;

/// <summary>
/// Identifies ray tracing techniques and pipeline capabilities.
/// </summary>
public enum RTFeature
{
    /// <summary>
    /// Ray-traced reflections.
    /// </summary>
    RayTracedReflections,

    /// <summary>
    /// Ray-traced global illumination.
    /// </summary>
    RayTracedGI,

    /// <summary>
    /// Ray-traced shadows.
    /// </summary>
    RayTracedShadows,

    /// <summary>
    /// Ray-traced ambient occlusion.
    /// </summary>
    RayTracedAO,

    /// <summary>
    /// Full path tracing / pure ray tracing pipeline.
    /// </summary>
    PathTracing,

    /// <summary>
    /// Inline ray tracing (ray queries in compute/pixel shaders).
    /// </summary>
    InlineRayTracing,

    /// <summary>
    /// Ray tracing acceleration structures (BLAS/TLAS).
    /// </summary>
    AccelerationStructures,

    /// <summary>
    /// Hardware ray query support.
    /// </summary>
    RayQuery
}
