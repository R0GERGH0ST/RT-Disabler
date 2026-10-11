using System.IO;
using System.Text.Json;
using RTFreedom.Adapters;
using RTFreedom.Backup;
using RTFreedom.Core.Interfaces;
using RTFreedom.Core.Models;
using RTFreedom.Diagnostics;
using RTFreedom.GameDetection;
using RTFreedom.Graphics;

namespace RTFreedom.App.Services;

/// <summary>
/// Central service locator providing application-wide access to core services and configurations.
/// </summary>
public static class ServiceLocator
{
    private static readonly string SettingsFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RTFreedom",
        "settings.json");

    /// <summary>
    /// Gets the game detection and platform scanning service.
    /// </summary>
    public static GameDetectionService GameDetection { get; private set; } = null!;

    /// <summary>
    /// Gets the graphics hardware and ray tracing pipeline analyzer.
    /// </summary>
    public static IGraphicsAnalyzer GraphicsAnalyzer { get; private set; } = null!;

    /// <summary>
    /// Gets the game adapter registry.
    /// </summary>
    public static IAdapterRegistry AdapterRegistry { get; private set; } = null!;

    /// <summary>
    /// Gets the file backup and restore manager.
    /// </summary>
    public static IBackupManager BackupManager { get; private set; } = null!;

    /// <summary>
    /// Gets the diagnostic logging and report generator service.
    /// </summary>
    public static IDiagnosticsService Diagnostics { get; private set; } = null!;

    /// <summary>
    /// Gets the current application configuration.
    /// </summary>
    public static AppConfiguration Configuration { get; set; } = new();

    /// <summary>
    /// Initializes all singleton services and registries required by RT Freedom.
    /// Called during application startup.
    /// </summary>
    public static void Initialize()
    {
        // 1. Diagnostics / Logging
        Diagnostics = new FileLogger();

        // 2. Load Configuration
        LoadConfiguration();

        // 3. Backup Management
        var backupRoot = !string.IsNullOrWhiteSpace(Configuration.BackupRootPath)
            ? Configuration.BackupRootPath
            : null;
        BackupManager = new BackupService(backupRoot);

        // 4. Graphics Capability Analyzer
        GraphicsAnalyzer = new GraphicsAnalysisService();

        // 5. Adapter Registry & Built-in Adapters
        var adapterRegistry = new AdapterRegistry();
        AdapterLoader.RegisterBuiltInAdapters(adapterRegistry);
        AdapterRegistry = adapterRegistry;

        // 6. Game Detection Service
        GameDetection = new GameDetectionService();

        Diagnostics.Log(RTFreedom.Core.Enums.LogLevel.Info, "ServiceLocator initialized successfully.");
    }

    /// <summary>
    /// Saves current application configuration to disk.
    /// </summary>
    public static void SaveConfiguration()
    {
        try
        {
            var dir = Path.GetDirectoryName(SettingsFilePath);
            if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var json = JsonSerializer.Serialize(Configuration, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsFilePath, json);
        }
        catch (Exception ex)
        {
            Diagnostics?.Log(RTFreedom.Core.Enums.LogLevel.Error, "Failed to persist application configuration", ex);
        }
    }

    private static void LoadConfiguration()
    {
        try
        {
            if (File.Exists(SettingsFilePath))
            {
                var json = File.ReadAllText(SettingsFilePath);
                var loaded = JsonSerializer.Deserialize<AppConfiguration>(json);
                if (loaded != null)
                {
                    Configuration = loaded;
                }
            }
        }
        catch (Exception ex)
        {
            Diagnostics?.Log(RTFreedom.Core.Enums.LogLevel.Warning, "Could not load settings; default configuration used", ex);
        }

        if (string.IsNullOrWhiteSpace(Configuration.BackupRootPath))
        {
            Configuration.BackupRootPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "RTFreedom",
                "Backups");
        }
    }
}
