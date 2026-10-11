namespace RTFreedom.Core.Enums;

/// <summary>
/// Indicates the feasibility and stability level of bypassing ray tracing for a game.
/// </summary>
public enum CompatibilityLevel
{
    /// <summary>
    /// Compatibility has not yet been evaluated.
    /// </summary>
    Unknown,

    /// <summary>
    /// Fully compatible with fallback rendering methods.
    /// </summary>
    FullyCompatible,

    /// <summary>
    /// Partially compatible; playable but minor visual artifacts or features disabled.
    /// </summary>
    PartiallyCompatible,

    /// <summary>
    /// Experimental bypass available; may cause rendering bugs or instability.
    /// </summary>
    ExperimentalOnly,

    /// <summary>
    /// Incompatible; mandatory ray tracing pipeline cannot be bypassed.
    /// </summary>
    Incompatible
}
