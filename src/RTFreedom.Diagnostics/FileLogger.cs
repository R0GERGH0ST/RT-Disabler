using RTFreedom.Core.Enums;
using RTFreedom.Core.Interfaces;
using RTFreedom.Core.Models;

namespace RTFreedom.Diagnostics;

/// <summary>
/// Provides thread-safe file logging with rolling date-based log files in AppData.
/// Implements <see cref="IDiagnosticsService"/>.
/// </summary>
public class FileLogger : IDiagnosticsService
{
    private readonly string _logsDirectory;
    private readonly Lock _writeLock = new();
    private readonly DiagnosticReportGenerator _reportGenerator;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileLogger"/> class.
    /// </summary>
    /// <param name="customLogsDirectory">Optional custom directory for log files.</param>
    /// <param name="reportGenerator">Optional custom report generator instance.</param>
    public FileLogger(string? customLogsDirectory = null, DiagnosticReportGenerator? reportGenerator = null)
    {
        _logsDirectory = customLogsDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RTFreedom",
            "Logs");

        _reportGenerator = reportGenerator ?? new DiagnosticReportGenerator();

        EnsureDirectoryExists();
    }

    /// <inheritdoc />
    public void Log(LogLevel level, string message, Exception? ex = null)
    {
        try
        {
            EnsureDirectoryExists();
            string logFilePath = GetLogFilePath();
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            string levelTag = level.ToString().ToUpperInvariant();

            string entry;
            if (ex != null)
            {
                entry = $"[{timestamp}] [{levelTag}] {message}{Environment.NewLine}Exception: {ex.GetType().FullName}: {ex.Message}{Environment.NewLine}{ex.StackTrace}{Environment.NewLine}";
            }
            else
            {
                entry = $"[{timestamp}] [{levelTag}] {message}{Environment.NewLine}";
            }

            lock (_writeLock)
            {
                File.AppendAllText(logFilePath, entry);
            }
        }
        catch
        {
            // Logging failure must never crash the host application
        }
    }

    /// <inheritdoc />
    public Task<string> GenerateReportAsync(GameInfo game, RayTracingAnalysis? analysis)
    {
        return _reportGenerator.GenerateReportAsync(game, analysis);
    }

    /// <inheritdoc />
    public string GetLogFilePath()
    {
        string dateSuffix = DateTime.Now.ToString("yyyyMMdd");
        string fileName = $"rtfreedom_{dateSuffix}.log";
        return Path.Combine(_logsDirectory, fileName);
    }

    private void EnsureDirectoryExists()
    {
        try
        {
            if (!Directory.Exists(_logsDirectory))
            {
                Directory.CreateDirectory(_logsDirectory);
            }
        }
        catch
        {
            // Directory creation failure handling
        }
    }
}
