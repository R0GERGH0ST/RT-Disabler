namespace RTFreedom.Core.Enums;

/// <summary>
/// Status of game patching operations and analysis state.
/// </summary>
public enum PatchStatus
{
    /// <summary>
    /// Game has not yet been analyzed.
    /// </summary>
    NotAnalyzed,

    /// <summary>
    /// Analysis is currently in progress.
    /// </summary>
    Analyzing,

    /// <summary>
    /// Game can be fully patched to run without mandatory ray tracing.
    /// </summary>
    Compatible,

    /// <summary>
    /// Game can be partially patched (some graphical artifacts or limits may apply).
    /// </summary>
    PartiallyCompatible,

    /// <summary>
    /// Game cannot be patched to bypass ray tracing.
    /// </summary>
    Incompatible,

    /// <summary>
    /// Patches have been applied successfully.
    /// </summary>
    Patched,

    /// <summary>
    /// Applying patches encountered an error.
    /// </summary>
    PatchFailed,

    /// <summary>
    /// Game files have been restored to original state from backup.
    /// </summary>
    Restored
}
