namespace RTFreedom.Core.Enums;

/// <summary>
/// Severity levels for logging operations.
/// </summary>
public enum LogLevel
{
    /// <summary>
    /// Verbose diagnostic information.
    /// </summary>
    Debug,

    /// <summary>
    /// General informational events.
    /// </summary>
    Info,

    /// <summary>
    /// Non-fatal warnings indicating potential issues.
    /// </summary>
    Warning,

    /// <summary>
    /// Errors encountered during execution.
    /// </summary>
    Error,

    /// <summary>
    /// Critical failures causing immediate abort of operation.
    /// </summary>
    Critical
}
