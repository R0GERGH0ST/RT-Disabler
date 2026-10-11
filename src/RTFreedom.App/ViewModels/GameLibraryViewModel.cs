using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using RTFreedom.App.Services;
using RTFreedom.Core.Enums;
using RTFreedom.Core.Models;
using RTFreedom.GameDetection;

namespace RTFreedom.App.ViewModels;

/// <summary>
/// ViewModel managing the game library catalog, scanning, filtering, and manual additions.
/// </summary>
public partial class GameLibraryViewModel : ViewModelBase
{
    private readonly Action<GameProfile>? _onOpenGameDetail;
    private readonly List<GameCardViewModel> _allGameCards = [];

    [ObservableProperty]
    private ObservableCollection<GameCardViewModel> _games = [];

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private string _filterText = string.Empty;

    [ObservableProperty]
    private GameCardViewModel? _selectedGame;

    [ObservableProperty]
    private bool _hasNoGames;

    /// <summary>
    /// Initializes a new instance of the <see cref="GameLibraryViewModel"/> class.
    /// </summary>
    /// <param name="onOpenGameDetail">Action invoked when user selects a game card to view details.</param>
    public GameLibraryViewModel(Action<GameProfile>? onOpenGameDetail = null)
    {
        _onOpenGameDetail = onOpenGameDetail;
    }

    partial void OnFilterTextChanged(string value)
    {
        ApplyFilter();
    }

    partial void OnSelectedGameChanged(GameCardViewModel? value)
    {
        if (value != null)
        {
            _onOpenGameDetail?.Invoke(value.Profile);
        }
    }

    /// <summary>
    /// Loads and discovers installed games via <see cref="GameDetectionService"/>.
    /// </summary>
    /// <param name="forceRefresh">Whether to bypass cache and force a new filesystem/registry scan.</param>
    [RelayCommand]
    public async Task RefreshGamesAsync(bool forceRefresh = true)
    {
        if (IsScanning)
        {
            return;
        }

        IsScanning = true;
        SetBusy("Scanning system for games (Steam, Epic, Xbox, Custom)...");

        try
        {
            var detectedGames = await ServiceLocator.GameDetection.DetectAllGamesAsync(forceRefresh).ConfigureAwait(false);

            var newCards = new List<GameCardViewModel>();
            foreach (var game in detectedGames)
            {
                var profile = new GameProfile(game);

                // Auto-evaluate known titles or target games
                var target = ServiceLocator.GameDetection.IdentifyTargetGame(game);
                if (target.HasValue)
                {
                    profile.Compatibility = target.Value switch
                    {
                        TargetGame.IndianaJonesAndTheGreatCircle => CompatibilityLevel.Incompatible,
                        TargetGame.Gears5 => CompatibilityLevel.FullyCompatible,
                        TargetGame.DoomTheDarkAges => CompatibilityLevel.Incompatible,
                        _ => CompatibilityLevel.Unknown
                    };
                }

                var cardVm = new GameCardViewModel(profile, card =>
                {
                    _onOpenGameDetail?.Invoke(card.Profile);
                });

                newCards.Add(cardVm);
            }

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                _allGameCards.Clear();
                _allGameCards.AddRange(newCards);
                ApplyFilter();
            });

            ServiceLocator.Diagnostics.Log(LogLevel.Info, $"Scanned {newCards.Count} games total.");
        }
        catch (Exception ex)
        {
            ServiceLocator.Diagnostics.Log(LogLevel.Error, "Failed while scanning games", ex);
            StatusMessage = $"Scan error: {ex.Message}";
        }
        finally
        {
            IsScanning = false;
            ClearBusy();
        }
    }

    /// <summary>
    /// Displays a file dialog allowing the user to select an executable file and add it to the library.
    /// </summary>
    [RelayCommand]
    public async Task AddGameManuallyAsync()
    {
        var openFileDialog = new OpenFileDialog
        {
            Title = "Select Game Executable",
            Filter = "Game Executable (*.exe)|*.exe|All Files (*.*)|*.*",
            CheckFileExists = true
        };

        if (openFileDialog.ShowDialog() == true)
        {
            var exePath = openFileDialog.FileName;
            var installDir = Path.GetDirectoryName(exePath) ?? string.Empty;
            var title = Path.GetFileNameWithoutExtension(exePath);

            var id = $"manual_{Guid.NewGuid():N}";
            var manualGame = new GameInfo(
                Id: id,
                Title: title,
                ExecutablePath: exePath,
                InstallPath: installDir,
                Version: "1.0",
                Publisher: "Manual Import",
                Platform: GamePlatform.Manual,
                GraphicsApi: RenderingApi.Unknown,
                ConfigurationFiles: [],
                ShaderFiles: [],
                DetectedAt: DateTime.UtcNow
            );

            ServiceLocator.GameDetection.ManualGames.AddGame(manualGame);
            await ServiceLocator.GameDetection.ManualGames.SaveAsync();

            await RefreshGamesAsync(forceRefresh: true);
        }
    }

    private void ApplyFilter()
    {
        var query = FilterText?.Trim();

        IEnumerable<GameCardViewModel> filtered = _allGameCards;
        if (!string.IsNullOrWhiteSpace(query))
        {
            filtered = filtered.Where(g =>
                g.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                g.PlatformText.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                g.StatusText.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        Games.Clear();
        foreach (var card in filtered)
        {
            Games.Add(card);
        }

        HasNoGames = Games.Count == 0 && !IsScanning;
    }
}
