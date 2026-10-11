using RTFreedom.Core.Enums;
using RTFreedom.Core.Models;

namespace RTFreedom.Core.Interfaces;

/// <summary>
/// Provides logging facilities and diagnostic reporting.
/// </summary>
public interface IDiagnosticsService
{
    /// <summary>
    /// Writes a log message at the specified severity level.
    /// </summary>
    /// <param name="level">Severity level of the log entry.</param>
    /// <param name="message">Informational or diagnostic message.</param>
    /// <param name="ex">Optional exception associated with the log entry.</param>
    void Log(LogLevel level, string message, Exception? ex = null);

    /// <summary>
    /// Generates a comprehensive diagnostic report markdown or text for support and auditing.
    /// </summary>
    /// <param name="game">The game metadata.</param>
    /// <param name="analysis">The ray tracing analysis if available.</param>
    /// <returns>Report text content.</returns>
    Task<string> GenerateReportAsync(GameInfo game, RayTracingAnalysis? analysis);

    /// <summary>
    /// Gets the absolute path to the active log file.
    /// </summary>
    /// <returns>Log file path string.</returns>
    string GetLogFilePath();
}
