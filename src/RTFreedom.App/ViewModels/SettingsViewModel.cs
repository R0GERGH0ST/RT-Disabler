using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RTFreedom.App.Services;
using RTFreedom.Core.Enums;
using RTFreedom.Core.Models;

namespace RTFreedom.App.ViewModels;

/// <summary>
/// ViewModel managing global application preferences, backup locations, and diagnostic logs.
/// </summary>
public partial class SettingsViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _backupPath = string.Empty;

    [ObservableProperty]
    private bool _autoDetectGames = true;

    [ObservableProperty]
    private bool _showExperimentalFeatures;

    [ObservableProperty]
    private string _selectedCustomPath = string.Empty;

    [ObservableProperty]
    private string _newCustomPathInput = string.Empty;

    /// <summary>
    /// Gets the collection of custom directory paths scanned for games.
    /// </summary>
    public ObservableCollection<string> CustomGamePaths { get; } = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsViewModel"/> class.
    /// </summary>
    public SettingsViewModel()
    {
        LoadSettings();
    }

    /// <summary>
    /// Loads settings from current configuration state.
    /// </summary>
    public void LoadSettings()
    {
        var config = ServiceLocator.Configuration;
        BackupPath = config.BackupRootPath;
        AutoDetectGames = config.AutoDetectGames;
        ShowExperimentalFeatures = config.ShowExperimentalFeatures;

        CustomGamePaths.Clear();
        foreach (var p in config.CustomGamePaths)
        {
            CustomGamePaths.Add(p);
        }
    }

    /// <summary>
    /// Saves modified settings back to disk.
    /// </summary>
    [RelayCommand]
    public void Save()
    {
        var config = ServiceLocator.Configuration;
        config.BackupRootPath = BackupPath;
        config.AutoDetectGames = AutoDetectGames;
        config.ShowExperimentalFeatures = ShowExperimentalFeatures;
        config.CustomGamePaths = CustomGamePaths.ToList();

        ServiceLocator.SaveConfiguration();
        SetBusy("Settings saved successfully.");
        ServiceLocator.Diagnostics.Log(LogLevel.Info, "Application settings updated.");
    }

    /// <summary>
    /// Resets settings back to recommended factory defaults.
    /// </summary>
    [RelayCommand]
    public void Reset()
    {
        BackupPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RTFreedom",
            "Backups");
        AutoDetectGames = true;
        ShowExperimentalFeatures = false;
        CustomGamePaths.Clear();

        Save();
    }

    /// <summary>
    /// Adds a folder path to the custom search locations.
    /// </summary>
    [RelayCommand]
    public void AddCustomPath()
    {
        if (string.IsNullOrWhiteSpace(NewCustomPathInput))
        {
            return;
        }

        var trimmed = NewCustomPathInput.Trim();
        if (Directory.Exists(trimmed) && !CustomGamePaths.Contains(trimmed, StringComparer.OrdinalIgnoreCase))
        {
            CustomGamePaths.Add(trimmed);
            NewCustomPathInput = string.Empty;
        }
    }

    /// <summary>
    /// Removes the selected custom folder path from the list.
    /// </summary>
    [RelayCommand]
    public void RemoveCustomPath(string? path)
    {
        var target = path ?? SelectedCustomPath;
        if (!string.IsNullOrWhiteSpace(target) && CustomGamePaths.Contains(target))
        {
            CustomGamePaths.Remove(target);
        }
    }

    /// <summary>
    /// Opens the backup directory folder in Windows File Explorer.
    /// </summary>
    [RelayCommand]
    public void OpenBackupFolder()
    {
        try
        {
            if (!Directory.Exists(BackupPath))
            {
                Directory.CreateDirectory(BackupPath);
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = BackupPath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            ServiceLocator.Diagnostics.Log(LogLevel.Warning, "Could not open backup directory in Explorer", ex);
        }
    }

    /// <summary>
    /// Clears historical application diagnostic log files.
    /// </summary>
    [RelayCommand]
    public void ClearLogs()
    {
        try
        {
            var logPath = ServiceLocator.Diagnostics.GetLogFilePath();
            var dir = Path.GetDirectoryName(logPath);
            if (Directory.Exists(dir))
            {
                var files = Directory.GetFiles(dir, "*.log");
                foreach (var f in files)
                {
                    try { File.Delete(f); } catch { /* skip in-use active log file */ }
                }
            }

            SetBusy("Diagnostic logs cleared.");
        }
        catch (Exception ex)
        {
            ServiceLocator.Diagnostics.Log(LogLevel.Warning, "Failed to clear log directory", ex);
        }
    }
}
