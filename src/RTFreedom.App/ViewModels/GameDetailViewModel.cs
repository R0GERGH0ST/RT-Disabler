using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RTFreedom.App.Services;
using RTFreedom.Core.Enums;
using RTFreedom.Core.Interfaces;
using RTFreedom.Core.Models;

namespace RTFreedom.App.ViewModels;

/// <summary>
/// ViewModel managing detailed game inspection, ray-tracing analysis, patching actions, and backups.
/// </summary>
public partial class GameDetailViewModel : ViewModelBase
{
    private readonly Action? _onBackToLibrary;

    [ObservableProperty]
    private GameProfile _profile;

    [ObservableProperty]
    private RayTracingAnalysis? _analysisResult;

    [ObservableProperty]
    private bool _isAnalyzing;

    [ObservableProperty]
    private bool _enableExperimentalMode;

    [ObservableProperty]
    private string _incompatibilityWarning = string.Empty;

    [ObservableProperty]
    private string _reportContent = string.Empty;

    [ObservableProperty]
    private bool _showReportDialog;

    /// <summary>
    /// Gets the list of available patch actions for this game.
    /// </summary>
    public ObservableCollection<PatchAction> AvailablePatches { get; } = [];

    /// <summary>
    /// Gets the list of compatibility warnings and notices.
    /// </summary>
    public ObservableCollection<string> Warnings { get; } = [];

    /// <summary>
    /// Gets historical backup snapshots for this game.
    /// </summary>
    public ObservableCollection<BackupManifest> BackupList { get; } = [];

    /// <summary>
    /// Gets the detected game metadata.
    /// </summary>
    public GameInfo Game => Profile.Game;

    /// <summary>
    /// Gets the friendly platform title.
    /// </summary>
    public string PlatformDisplay => Game.Platform switch
    {
        GamePlatform.Steam => "Steam",
        GamePlatform.EpicGames => "Epic Games Store",
        GamePlatform.Xbox => "Xbox / Windows Store",
        GamePlatform.Manual => "Custom Local Directory",
        _ => Game.Platform.ToString()
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="GameDetailViewModel"/> class.
    /// </summary>
    /// <param name="profile">The game profile.</param>
    /// <param name="onBackToLibrary">Callback to navigate back to library view.</param>
    public GameDetailViewModel(GameProfile profile, Action? onBackToLibrary = null)
    {
        _profile = profile ?? throw new ArgumentNullException(nameof(profile));
        _onBackToLibrary = onBackToLibrary;
        _analysisResult = profile.Analysis;

        UpdateWarnings();
        _ = LoadBackupsAsync();
        _ = LoadAvailablePatchesAsync();
    }

    /// <summary>
    /// Command to return to the game library view.
    /// </summary>
    [RelayCommand]
    private void BackToLibrary()
    {
        _onBackToLibrary?.Invoke();
    }

    /// <summary>
    /// Executes ray-tracing analysis on the game.
    /// </summary>
    [RelayCommand]
    public async Task AnalyzeGameAsync()
    {
        IsAnalyzing = true;
        SetBusy($"Analyzing ray tracing pipeline for {Game.Title}...");

        try
        {
            ServiceLocator.Diagnostics.Log(LogLevel.Info, $"Starting analysis for {Game.Title} ({Game.Id})");

            var gpu = await ServiceLocator.GraphicsAnalyzer.DetectGpuCapabilitiesAsync().ConfigureAwait(false);
            var adapter = ServiceLocator.AdapterRegistry.GetAdapter(Game);

            RayTracingAnalysis analysis;
            if (adapter != null)
            {
                analysis = await adapter.AnalyzeAsync(Game, gpu).ConfigureAwait(false);
            }
            else
            {
                analysis = await ServiceLocator.GraphicsAnalyzer.AnalyzeGameAsync(Game).ConfigureAwait(false);
            }

            AnalysisResult = analysis;
            Profile.Analysis = analysis;
            Profile.Compatibility = analysis.RecommendedLevel;
            Profile.Status = analysis.RecommendedLevel switch
            {
                CompatibilityLevel.Incompatible => PatchStatus.Incompatible,
                CompatibilityLevel.FullyCompatible => PatchStatus.Compatible,
                CompatibilityLevel.PartiallyCompatible => PatchStatus.PartiallyCompatible,
                CompatibilityLevel.ExperimentalOnly => PatchStatus.PartiallyCompatible,
                _ => PatchStatus.NotAnalyzed
            };

            await Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                UpdateWarnings();
                await LoadAvailablePatchesAsync();
            });

            ServiceLocator.Diagnostics.Log(LogLevel.Info, $"Analysis complete for {Game.Title}. Result: {analysis.RecommendedLevel}");
        }
        catch (Exception ex)
        {
            ServiceLocator.Diagnostics.Log(LogLevel.Error, $"Error during analysis of {Game.Title}", ex);
            StatusMessage = $"Analysis failed: {ex.Message}";
        }
        finally
        {
            IsAnalyzing = false;
            ClearBusy();
            OnPropertyChanged(nameof(Profile));
        }
    }

    /// <summary>
    /// Applies a specific patch action to the game safely with backup.
    /// </summary>
    /// <param name="patch">The patch action to apply.</param>
    [RelayCommand]
    public async Task ApplyPatchAsync(PatchAction? patch)
    {
        if (patch == null)
        {
            return;
        }

        SetBusy($"Applying patch: {patch.Description}...");
        try
        {
            var adapter = ServiceLocator.AdapterRegistry.GetAdapter(Game);
            if (adapter == null)
            {
                StatusMessage = "No compatible adapter found for this title.";
                return;
            }

            var success = await adapter.ApplyPatchAsync(Game, patch, ServiceLocator.BackupManager).ConfigureAwait(false);

            if (success)
            {
                Profile.Status = PatchStatus.Patched;
                Profile.IsPatched = true;
                if (!Profile.AppliedPatches.Any(p => p.Id == patch.Id))
                {
                    Profile.AppliedPatches.Add(patch);
                }
                Profile.LastModified = DateTime.UtcNow;

                await Application.Current.Dispatcher.InvokeAsync(async () =>
                {
                    await LoadBackupsAsync();
                    await LoadAvailablePatchesAsync();
                });

                StatusMessage = $"Patch '{patch.Description}' applied successfully.";
                ServiceLocator.Diagnostics.Log(LogLevel.Info, $"Applied patch '{patch.Id}' to {Game.Title}");
            }
            else
            {
                Profile.Status = PatchStatus.PatchFailed;
                StatusMessage = $"Failed to apply patch: '{patch.Description}'. Check logs.";
                ServiceLocator.Diagnostics.Log(LogLevel.Warning, $"Failed applying patch '{patch.Id}' to {Game.Title}");
            }
        }
        catch (Exception ex)
        {
            Profile.Status = PatchStatus.PatchFailed;
            StatusMessage = $"Error applying patch: {ex.Message}";
            ServiceLocator.Diagnostics.Log(LogLevel.Error, $"Exception applying patch to {Game.Title}", ex);
        }
        finally
        {
            ClearBusy();
            OnPropertyChanged(nameof(Profile));
        }
    }

    /// <summary>
    /// Applies all recommended patches for this game.
    /// </summary>
    [RelayCommand]
    public async Task ApplyAllPatchesAsync()
    {
        if (AvailablePatches.Count == 0)
        {
            await LoadAvailablePatchesAsync();
        }

        foreach (var patch in AvailablePatches.ToList())
        {
            await ApplyPatchAsync(patch);
        }
    }

    /// <summary>
    /// Restores original game files from the latest backup.
    /// </summary>
    [RelayCommand]
    public async Task RestorePatchAsync()
    {
        SetBusy("Restoring game files from backup...");
        try
        {
            var backups = await ServiceLocator.BackupManager.GetBackupsAsync(Game.Id).ConfigureAwait(false);
            if (backups.Count == 0)
            {
                StatusMessage = "No backup found to restore.";
                return;
            }

            var latest = backups.OrderByDescending(b => b.CreatedAt).First();
            var success = await ServiceLocator.BackupManager.RestoreBackupAsync(latest.BackupId).ConfigureAwait(false);

            if (success)
            {
                Profile.Status = PatchStatus.Restored;
                Profile.IsPatched = false;
                Profile.AppliedPatches.Clear();
                Profile.LastModified = DateTime.UtcNow;

                StatusMessage = "Original files restored successfully.";
                ServiceLocator.Diagnostics.Log(LogLevel.Info, $"Restored files from backup {latest.BackupId} for {Game.Title}");

                await Application.Current.Dispatcher.InvokeAsync(async () =>
                {
                    await LoadAvailablePatchesAsync();
                });
            }
            else
            {
                StatusMessage = "Restoration failed. Files may be locked or modified.";
                ServiceLocator.Diagnostics.Log(LogLevel.Error, $"Restoration failed for {Game.Title}");
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Restoration error: {ex.Message}";
            ServiceLocator.Diagnostics.Log(LogLevel.Error, $"Exception during restoration for {Game.Title}", ex);
        }
        finally
        {
            ClearBusy();
            OnPropertyChanged(nameof(Profile));
        }
    }

    /// <summary>
    /// Manually creates a new backup snapshot of known config and shader files.
    /// </summary>
    [RelayCommand]
    public async Task CreateBackupAsync()
    {
        SetBusy("Creating file snapshot backup...");
        try
        {
            var filesToBackup = new List<string>();
            filesToBackup.AddRange(Game.ConfigurationFiles);
            filesToBackup.AddRange(Game.ShaderFiles);

            if (filesToBackup.Count == 0 && File.Exists(Game.ExecutablePath))
            {
                filesToBackup.Add(Game.ExecutablePath);
            }

            var manifest = await ServiceLocator.BackupManager.CreateBackupAsync(Game.Id, filesToBackup).ConfigureAwait(false);
            Profile.BackupId = manifest.BackupId;

            await Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                await LoadBackupsAsync();
            });

            StatusMessage = $"Backup snapshot created ({manifest.Entries.Count} files).";
            ServiceLocator.Diagnostics.Log(LogLevel.Info, $"Manual backup created for {Game.Title}: {manifest.BackupId}");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to create backup: {ex.Message}";
            ServiceLocator.Diagnostics.Log(LogLevel.Error, $"Failed manual backup for {Game.Title}", ex);
        }
        finally
        {
            ClearBusy();
        }
    }

    /// <summary>
    /// Restores a specific backup manifest snapshot by ID.
    /// </summary>
    /// <param name="manifest">The backup manifest to restore.</param>
    [RelayCommand]
    public async Task RestoreBackupAsync(BackupManifest? manifest)
    {
        if (manifest == null)
        {
            return;
        }

        SetBusy($"Restoring snapshot from {manifest.CreatedAt:g}...");
        try
        {
            var success = await ServiceLocator.BackupManager.RestoreBackupAsync(manifest.BackupId).ConfigureAwait(false);
            if (success)
            {
                Profile.Status = PatchStatus.Restored;
                Profile.IsPatched = false;
                StatusMessage = "Snapshot restored successfully.";
                ServiceLocator.Diagnostics.Log(LogLevel.Info, $"Snapshot {manifest.BackupId} restored for {Game.Title}");
            }
            else
            {
                StatusMessage = "Restoration from snapshot failed.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Restore failed: {ex.Message}";
        }
        finally
        {
            ClearBusy();
            OnPropertyChanged(nameof(Profile));
        }
    }

    /// <summary>
    /// Generates and displays the diagnostic markdown report for this game.
    /// </summary>
    [RelayCommand]
    public async Task ViewReportAsync()
    {
        SetBusy("Generating diagnostic analysis report...");
        try
        {
            var report = await ServiceLocator.Diagnostics.GenerateReportAsync(Game, AnalysisResult).ConfigureAwait(false);
            ReportContent = report;
            ShowReportDialog = true;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error generating report: {ex.Message}";
        }
        finally
        {
            ClearBusy();
        }
    }

    /// <summary>
    /// Closes the markdown report modal dialog.
    /// </summary>
    [RelayCommand]
    private void CloseReport()
    {
        ShowReportDialog = false;
    }

    private async Task LoadBackupsAsync()
    {
        try
        {
            var backups = await ServiceLocator.BackupManager.GetBackupsAsync(Game.Id).ConfigureAwait(false);
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                BackupList.Clear();
                foreach (var b in backups.OrderByDescending(x => x.CreatedAt))
                {
                    BackupList.Add(b);
                }
            });
        }
        catch
        {
            // Suppress backup list enumeration errors
        }
    }

    private async Task LoadAvailablePatchesAsync()
    {
        try
        {
            var adapter = ServiceLocator.AdapterRegistry.GetAdapter(Game);
            if (adapter != null && AnalysisResult != null)
            {
                var patches = await adapter.GetAvailablePatchesAsync(Game, AnalysisResult).ConfigureAwait(false);
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    AvailablePatches.Clear();
                    foreach (var p in patches)
                    {
                        AvailablePatches.Add(p);
                    }
                });
            }
            else
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    AvailablePatches.Clear();
                });
            }
        }
        catch
        {
            // Suppress patch generation errors
        }
    }

    private void UpdateWarnings()
    {
        Warnings.Clear();

        if (Profile.Compatibility == CompatibilityLevel.Incompatible)
        {
            IncompatibilityWarning = "This game strictly requires hardware ray tracing (DXR/Vulkan). The game engine contains no rasterized fallback paths. Bypassing will result in black screens, rendering crashes, or unrendered lighting.";
            Warnings.Add(IncompatibilityWarning);
        }
        else if (Profile.Compatibility == CompatibilityLevel.ExperimentalOnly)
        {
            IncompatibilityWarning = "Experimental bypass only. Graphical artifacts (missing ambient occlusion, reflections, or incorrect illumination) are expected.";
            Warnings.Add(IncompatibilityWarning);
        }
        else if (Profile.Compatibility == CompatibilityLevel.PartiallyCompatible)
        {
            IncompatibilityWarning = "Partial bypass supported. Fallback rasterization paths will be used, but high-end ray-traced reflections and lighting will be disabled.";
            Warnings.Add(IncompatibilityWarning);
        }
        else
        {
            IncompatibilityWarning = string.Empty;
        }

        if (Game.ConfigurationFiles.Count == 0)
        {
            Warnings.Add("No local configuration files detected for this title. Launch the game at least once to create standard config files.");
        }
    }
}
