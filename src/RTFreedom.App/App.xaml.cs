using System.IO;
using System.Windows;
using System.Windows.Threading;
using RTFreedom.App.Services;
using RTFreedom.App.ViewModels;
using RTFreedom.App.Views;
using RTFreedom.Core.Enums;

namespace RTFreedom.App;

/// <summary>
/// Interaction logic for App.xaml.
/// Configures dependency wiring, error boundaries, and application lifecycle.
/// </summary>
public partial class App : Application
{
    /// <inheritdoc />
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 1. Setup Crash Diagnostics & Exception Hooks
        SetupCrashDiagnostics();

        try
        {
            // 2. Initialize Core Services via ServiceLocator
            ServiceLocator.Initialize();

            // 3. Create Root ViewModel and Show MainWindow
            var mainViewModel = new MainViewModel();
            var mainWindow = new MainWindow(mainViewModel);
            MainWindow = mainWindow;
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            HandleFatalStartupException(ex);
        }
    }

    private void SetupCrashDiagnostics()
    {
        // UI Dispatcher Unhandled Exceptions
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        // AppDomain Unhandled Exceptions
        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;

        // TaskScheduler Unobserved Task Exceptions
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        LogCrash("DispatcherUnhandledException", e.Exception);
        MessageBox.Show(
            $"An unexpected error occurred in RT Freedom:\n\n{e.Exception.Message}\n\nDiagnostic details have been recorded in the application log.",
            "RT Freedom - Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        e.Handled = true;
    }

    private void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            LogCrash("AppDomainUnhandledException", ex);
        }
        else
        {
            LogCrash("AppDomainUnhandledException", new Exception($"Non-exception object: {e.ExceptionObject}"));
        }
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        LogCrash("UnobservedTaskException", e.Exception);
        e.SetObserved();
    }

    private static void LogCrash(string context, Exception ex)
    {
        try
        {
            ServiceLocator.Diagnostics?.Log(LogLevel.Critical, $"FATAL: {context}: {ex.Message}", ex);
        }
        catch
        {
            // Fallback emergency crash log writing if ServiceLocator is unavailable
            try
            {
                var crashLogDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "RTFreedom",
                    "Logs");
                Directory.CreateDirectory(crashLogDir);
                var filePath = Path.Combine(crashLogDir, "emergency_crash.log");
                var message = $"[{DateTime.UtcNow:u}] [{context}] {ex.Message}\n{ex.StackTrace}\n\n";
                File.AppendAllText(filePath, message);
            }
            catch
            {
                // Last-resort suppression
            }
        }
    }

    private static void HandleFatalStartupException(Exception ex)
    {
        LogCrash("FatalStartupException", ex);
        MessageBox.Show(
            $"RT Freedom could not start due to an initialization failure:\n\n{ex.Message}",
            "RT Freedom - Fatal Startup Error",
            MessageBoxButton.OK,
            MessageBoxImage.Stop);
        Current.Shutdown(-1);
    }
}
