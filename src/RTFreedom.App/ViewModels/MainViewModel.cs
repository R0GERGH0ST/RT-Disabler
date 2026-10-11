using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RTFreedom.App.Services;
using RTFreedom.Core.Enums;
using RTFreedom.Core.Models;

namespace RTFreedom.App.ViewModels;

/// <summary>
/// Root ViewModel for the RT Freedom main application window, managing navigation and status bar.
/// </summary>
public partial class MainViewModel : ViewModelBase
{
    [ObservableProperty]
    private ViewModelBase _currentView = null!;

    [ObservableProperty]
    private GameLibraryViewModel _gameLibraryVM = null!;

    [ObservableProperty]
    private SettingsViewModel _settingsVM = null!;

    [ObservableProperty]
    private AboutViewModel _aboutVM = null!;

    [ObservableProperty]
    private string _gpuName = "Detecting GPU...";

    [ObservableProperty]
    private string _gpuDriverVersion = string.Empty;

    [ObservableProperty]
    private string _gpuVramText = string.Empty;

    [ObservableProperty]
    private string _rtSupportText = "Checking Ray Tracing...";

    [ObservableProperty]
    private bool _isRtSupported;

    [ObservableProperty]
    private GpuCapabilities? _gpuInfo;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainViewModel"/> class.
    /// </summary>
    public MainViewModel()
    {
        GameLibraryVM = new GameLibraryViewModel(onOpenGameDetail: OnOpenGameDetail);
        SettingsVM = new SettingsViewModel();
        AboutVM = new AboutViewModel();

        CurrentView = GameLibraryVM;

        _ = InitializeAsync();
    }

    /// <summary>
    /// Initializes hardware detection and loads installed games.
    /// </summary>
    public async Task InitializeAsync()
    {
        SetBusy("Initializing graphics hardware detection...");

        try
        {
            // 1. Detect GPU Capabilities
            var gpu = await ServiceLocator.GraphicsAnalyzer.DetectGpuCapabilitiesAsync().ConfigureAwait(false);
            GpuInfo = gpu;

            var vramMb = gpu.VramBytes / (1024 * 1024);
            var vramGb = vramMb >= 1024 ? $"{(vramMb / 1024.0):F1} GB" : $"{vramMb} MB";

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                GpuName = $"{gpu.Vendor} {gpu.Name}";
                GpuDriverVersion = $"Driver {gpu.DriverVersion}";
                GpuVramText = $"VRAM: {vramGb}";
                IsRtSupported = gpu.SupportsRayTracing;
                RtSupportText = gpu.SupportsRayTracing
                    ? $"Hardware RT: Supported (Tier {gpu.RayTracingTier})"
                    : "Hardware RT: Not Supported (Raster Only)";
            });

            ServiceLocator.Diagnostics.Log(LogLevel.Info, $"GPU detected: {gpu.Name}, RT={gpu.SupportsRayTracing}");

            // 2. Load Games Catalog
            await GameLibraryVM.RefreshGamesAsync(forceRefresh: false);
        }
        catch (Exception ex)
        {
            ServiceLocator.Diagnostics.Log(LogLevel.Error, "Error during MainViewModel initialization", ex);
            StatusMessage = $"Init warning: {ex.Message}";
        }
        finally
        {
            ClearBusy();
        }
    }

    /// <summary>
    /// Navigates to the Game Library view.
    /// </summary>
    [RelayCommand]
    public void NavigateToLibrary()
    {
        CurrentView = GameLibraryVM;
    }

    /// <summary>
    /// Navigates to the Settings view.
    /// </summary>
    [RelayCommand]
    public void NavigateToSettings()
    {
        SettingsVM.LoadSettings();
        CurrentView = SettingsVM;
    }

    /// <summary>
    /// Navigates to the About / Disclaimers view.
    /// </summary>
    [RelayCommand]
    public void NavigateToAbout()
    {
        CurrentView = AboutVM;
    }

    private void OnOpenGameDetail(GameProfile profile)
    {
        var detailVm = new GameDetailViewModel(profile, onBackToLibrary: () =>
        {
            CurrentView = GameLibraryVM;
        });

        CurrentView = detailVm;
    }
}
