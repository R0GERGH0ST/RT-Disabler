using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using RTFreedom.Core.Enums;
using RTFreedom.Core.Interfaces;

namespace RTFreedom.Diagnostics;

/// <summary>
/// Provides global crash handlers and diagnostic dumps for unhandled domain and task exceptions.
/// </summary>
public sealed class CrashDiagnostics : IDisposable
{
    private readonly string _crashReportsDirectory;
    private readonly IDiagnosticsService? _logger;
    private readonly Lock _crashLock = new();
    private string _currentContext = "Idle";
    private bool _isRegistered;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="CrashDiagnostics"/> class.
    /// </summary>
    /// <param name="logger">Optional diagnostics service for logging crash notifications.</param>
    /// <param name="customCrashReportsDirectory">Optional custom directory for crash dumps.</param>
    public CrashDiagnostics(IDiagnosticsService? logger = null, string? customCrashReportsDirectory = null)
    {
        _logger = logger;
        _crashReportsDirectory = customCrashReportsDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RTFreedom",
            "CrashReports");
    }

    /// <summary>
    /// Registers global unhandled exception handlers on <see cref="AppDomain.CurrentDomain"/> and <see cref="TaskScheduler"/>.
    /// </summary>
    public void RegisterGlobalHandlers()
    {
        if (_isRegistered) return;

        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        _isRegistered = true;

        _logger?.Log(LogLevel.Info, "Global crash diagnostic handlers successfully registered.");
    }

    /// <summary>
    /// Unregisters global unhandled exception handlers.
    /// </summary>
    public void UnregisterGlobalHandlers()
    {
        if (!_isRegistered) return;

        AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
        TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
        _isRegistered = false;

        _logger?.Log(LogLevel.Info, "Global crash diagnostic handlers unregistered.");
    }

    /// <summary>
    /// Updates the active application context description (e.g. current game being patched or analyzed).
    /// </summary>
    /// <param name="context">Context description string.</param>
    public void SetCurrentContext(string context)
    {
        _currentContext = string.IsNullOrWhiteSpace(context) ? "Idle" : context;
    }

    /// <summary>
    /// Manually captures and writes a crash diagnostic report for a specific caught exception.
    /// </summary>
    /// <param name="ex">The exception to log and dump.</param>
    /// <param name="contextOverride">Optional contextual notes.</param>
    /// <returns>The path to the generated crash report file.</returns>
    public string CaptureCrash(Exception ex, string? contextOverride = null)
    {
        ArgumentNullException.ThrowIfNull(ex);

        string effectiveContext = contextOverride ?? _currentContext;
        _logger?.Log(LogLevel.Critical, $"Capturing crash report for context '{effectiveContext}': {ex.Message}", ex);

        return WriteCrashDump(ex, isTerminating: false, effectiveContext);
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        try
        {
            var ex = e.ExceptionObject as Exception ?? new Exception($"Non-exception unhandled object: {e.ExceptionObject}");
            _logger?.Log(LogLevel.Critical, $"AppDomain Unhandled Exception (IsTerminating: {e.IsTerminating}): {ex.Message}", ex);
            WriteCrashDump(ex, e.IsTerminating, _currentContext);
        }
        catch
        {
            // Do not throw from unhandled exception callback
        }
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        try
        {
            _logger?.Log(LogLevel.Critical, $"TaskScheduler Unobserved Task Exception: {e.Exception.Message}", e.Exception);
            WriteCrashDump(e.Exception, isTerminating: false, _currentContext);
        }
        catch
        {
            // Do not throw from unobserved task exception callback
        }
    }

    private string WriteCrashDump(Exception ex, bool isTerminating, string context)
    {
        lock (_crashLock)
        {
            try
            {
                if (!Directory.Exists(_crashReportsDirectory))
                {
                    Directory.CreateDirectory(_crashReportsDirectory);
                }

                string timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
                string fileName = $"crash_{timestamp}_{Guid.NewGuid():N}.txt";
                string fullPath = Path.Combine(_crashReportsDirectory, fileName);

                var sb = new StringBuilder();
                sb.AppendLine("================================================================================");
                sb.AppendLine("                           RT FREEDOM CRASH DUMP                                ");
                sb.AppendLine("================================================================================");
                sb.AppendLine($"Timestamp:         {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff UTC}");
                sb.AppendLine($"Process ID:        {Environment.ProcessId}");
                sb.AppendLine($"Is Terminating:    {isTerminating}");
                sb.AppendLine($"Active Context:    {context}");
                sb.AppendLine($"OS Description:    {RuntimeInformation.OSDescription}");
                sb.AppendLine($"Architecture:      {RuntimeInformation.OSArchitecture}");
                sb.AppendLine($"Framework:         {RuntimeInformation.FrameworkDescription}");
                sb.AppendLine($"Working Set (MB):  {Environment.WorkingSet / (1024 * 1024)}");
                sb.AppendLine($"GC Memory (MB):    {GC.GetTotalMemory(false) / (1024 * 1024)}");
                sb.AppendLine();

                sb.AppendLine("--------------------------------------------------------------------------------");
                sb.AppendLine("EXCEPTION DETAILS");
                sb.AppendLine("--------------------------------------------------------------------------------");

                Exception? currentEx = ex;
                int depth = 0;
                while (currentEx != null)
                {
                    sb.AppendLine($"[Exception Level {depth}]");
                    sb.AppendLine($"Type:       {currentEx.GetType().FullName}");
                    sb.AppendLine($"Message:    {currentEx.Message}");
                    sb.AppendLine($"HResult:    0x{currentEx.HResult:X8}");
                    sb.AppendLine($"TargetSite: {currentEx.TargetSite}");
                    sb.AppendLine("StackTrace:");
                    sb.AppendLine(currentEx.StackTrace ?? "  (No stack trace available)");
                    sb.AppendLine();

                    currentEx = currentEx.InnerException;
                    depth++;
                }

                sb.AppendLine("--------------------------------------------------------------------------------");
                sb.AppendLine("LOADED ASSEMBLIES");
                sb.AppendLine("--------------------------------------------------------------------------------");
                try
                {
                    var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                        .OrderBy(a => a.GetName().Name);

                    foreach (var asm in assemblies)
                    {
                        var name = asm.GetName();
                        sb.AppendLine($"{name.Name,-40} {name.Version} (Location: {(asm.IsDynamic ? "<dynamic>" : asm.Location)})");
                    }
                }
                catch
                {
                    sb.AppendLine("(Failed to enumerate loaded assemblies)");
                }

                sb.AppendLine();
                sb.AppendLine("================================================================================");
                sb.AppendLine("                                END OF DUMP                                     ");
                sb.AppendLine("================================================================================");

                File.WriteAllText(fullPath, sb.ToString());
                return fullPath;
            }
            catch (Exception writeEx)
            {
                // Fallback attempt to write to temp directory if AppData fails
                try
                {
                    string fallbackPath = Path.Combine(Path.GetTempPath(), $"rtfreedom_crash_fallback_{Guid.NewGuid():N}.txt");
                    File.WriteAllText(fallbackPath, $"Failed to write crash dump to AppData: {writeEx.Message}\nOriginal Exception: {ex}");
                    return fallbackPath;
                }
                catch
                {
                    return string.Empty;
                }
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        UnregisterGlobalHandlers();
        _disposed = true;
    }
}
