using RTFreedom.Core.Enums;
using RTFreedom.Core.Models;

namespace RTFreedom.Core.Interfaces;

/// <summary>
/// Detects game installations across supported platforms and file systems.
/// </summary>
public interface IGameDetector
{
    /// <summary>
    /// Gets the human-readable name of this game detector.
    /// </summary>
    string DetectorName { get; }

    /// <summary>
    /// Gets the platform targeted by this detector.
    /// </summary>
    GamePlatform Platform { get; }

    /// <summary>
    /// Scans the system for installed games matching this platform.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A collection of detected game metadata records.</returns>
    Task<IReadOnlyList<GameInfo>> DetectGamesAsync(CancellationToken ct = default);
}
