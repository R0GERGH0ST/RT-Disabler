using RTFreedom.Core.Enums;

namespace RTFreedom.Core.Models;

/// <summary>
/// Represents global application settings and user preferences.
/// </summary>
public class AppConfiguration
{
    /// <summary>
    /// Gets or sets the root directory where all backups are stored.
    /// </summary>
    public string BackupRootPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets user-specified directories to scan for custom game installations.
    /// </summary>
    public List<string> CustomGamePaths { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether games should be detected automatically at startup.
    /// </summary>
    public bool AutoDetectGames { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether experimental patches and bypasses should be shown.
    /// </summary>
    public bool ShowExperimentalFeatures { get; set; } = false;

    /// <summary>
    /// Gets or sets the minimum log level for application diagnostics.
    /// </summary>
    public LogLevel MinimumLogLevel { get; set; } = LogLevel.Info;

    /// <summary>
    /// Gets or sets the accent color hex code or resource key used in the UI theme.
    /// </summary>
    public string ThemeAccentColor { get; set; } = "#0078D7";
}
