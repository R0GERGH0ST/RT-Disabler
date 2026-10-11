using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RTFreedom.Core.Enums;
using RTFreedom.Core.Models;

namespace RTFreedom.App.ViewModels;

/// <summary>
/// ViewModel representing an individual game card within the library grid.
/// </summary>
public partial class GameCardViewModel : ViewModelBase
{
    private readonly Action<GameCardViewModel>? _onOpenDetail;

    /// <summary>
    /// Gets the backing game profile and state.
    /// </summary>
    public GameProfile Profile { get; }

    /// <summary>
    /// Gets the game information metadata.
    /// </summary>
    public GameInfo Game => Profile.Game;

    /// <summary>
    /// Gets the game display title.
    /// </summary>
    public string Title => Game.Title;

    /// <summary>
    /// Gets the platform source (Steam, Epic, Xbox, Manual).
    /// </summary>
    public GamePlatform Platform => Game.Platform;

    /// <summary>
    /// Gets the current patch status.
    /// </summary>
    public PatchStatus Status => Profile.Status;

    /// <summary>
    /// Gets the compatibility level.
    /// </summary>
    public CompatibilityLevel Compatibility => Profile.Compatibility;

    /// <summary>
    /// Gets whether patches are actively applied.
    /// </summary>
    public bool IsPatched => Profile.IsPatched;

    /// <summary>
    /// Gets the platform name for display badge.
    /// </summary>
    public string PlatformText => Platform switch
    {
        GamePlatform.Steam => "Steam",
        GamePlatform.EpicGames => "Epic Games",
        GamePlatform.Xbox => "Xbox / PC Game Pass",
        GamePlatform.Manual => "Custom",
        _ => Platform.ToString()
    };

    /// <summary>
    /// Gets human-readable status text for the badge.
    /// </summary>
    public string StatusText => Status switch
    {
        PatchStatus.NotAnalyzed => "Not Analyzed",
        PatchStatus.Analyzing => "Analyzing...",
        PatchStatus.Compatible => "Compatible",
        PatchStatus.PartiallyCompatible => "Partial Bypass",
        PatchStatus.Incompatible => "RT Mandatory",
        PatchStatus.Patched => "Patched",
        PatchStatus.PatchFailed => "Patch Error",
        PatchStatus.Restored => "Restored",
        _ => Status.ToString()
    };

    /// <summary>
    /// Gets hex color representing the patch status.
    /// </summary>
    public string StatusColor => Status switch
    {
        PatchStatus.NotAnalyzed => "#8A9BB5",
        PatchStatus.Analyzing => "#00E5FF",
        PatchStatus.Compatible => "#00E676",
        PatchStatus.PartiallyCompatible => "#FFD600",
        PatchStatus.Incompatible => "#FF1744",
        PatchStatus.Patched => "#00E676",
        PatchStatus.PatchFailed => "#FF1744",
        PatchStatus.Restored => "#7C4DFF",
        _ => "#8A9BB5"
    };

    /// <summary>
    /// Gets human-readable compatibility text.
    /// </summary>
    public string CompatibilityText => Compatibility switch
    {
        CompatibilityLevel.FullyCompatible => "Full RT Bypass Possible",
        CompatibilityLevel.PartiallyCompatible => "Partial RT Bypass",
        CompatibilityLevel.ExperimentalOnly => "Experimental Only",
        CompatibilityLevel.Incompatible => "Incompatible (RT Required)",
        _ => "Unknown Feasibility"
    };

    /// <summary>
    /// Gets hex color representing the compatibility level.
    /// </summary>
    public string CompatibilityColor => Compatibility switch
    {
        CompatibilityLevel.FullyCompatible => "#00E676",
        CompatibilityLevel.PartiallyCompatible => "#FFD600",
        CompatibilityLevel.ExperimentalOnly => "#FF9100",
        CompatibilityLevel.Incompatible => "#FF1744",
        _ => "#8A9BB5"
    };

    /// <summary>
    /// Gets vector path data or short badge symbol for the platform.
    /// </summary>
    public string PlatformBadgeShort => Platform switch
    {
        GamePlatform.Steam => "STM",
        GamePlatform.EpicGames => "EGS",
        GamePlatform.Xbox => "XBX",
        GamePlatform.Manual => "DIR",
        _ => "APP"
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="GameCardViewModel"/> class.
    /// </summary>
    /// <param name="profile">The game profile.</param>
    /// <param name="onOpenDetail">Callback when card is opened/clicked.</param>
    public GameCardViewModel(GameProfile profile, Action<GameCardViewModel>? onOpenDetail = null)
    {
        Profile = profile ?? throw new ArgumentNullException(nameof(profile));
        _onOpenDetail = onOpenDetail;
    }

    /// <summary>
    /// Refreshes notification of all profile-dependent computed properties.
    /// </summary>
    public void RefreshState()
    {
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(Compatibility));
        OnPropertyChanged(nameof(IsPatched));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(StatusColor));
        OnPropertyChanged(nameof(CompatibilityText));
        OnPropertyChanged(nameof(CompatibilityColor));
    }

    /// <summary>
    /// Command invoked to navigate to the detailed view of this game.
    /// </summary>
    [RelayCommand]
    private void OpenGameDetail()
    {
        _onOpenDetail?.Invoke(this);
    }
}
