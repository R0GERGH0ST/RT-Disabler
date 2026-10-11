using RTFreedom.Core.Models;

namespace RTFreedom.Core.Interfaces;

/// <summary>
/// Provides game-specific ray tracing analysis, patch generation, and patch execution.
/// </summary>
public interface IGameAdapter
{
    /// <summary>
    /// Gets the unique identifier of the game supported by this adapter.
    /// </summary>
    string GameId { get; }

    /// <summary>
    /// Gets the display title of the game supported by this adapter.
    /// </summary>
    string GameTitle { get; }

    /// <summary>
    /// Gets the array of game versions supported by this adapter.
    /// </summary>
    string[] SupportedVersions { get; }

    /// <summary>
    /// Analyzes ray tracing dependencies and feasibility for the specific game under given GPU capabilities.
    /// </summary>
    /// <param name="game">The detected game metadata.</param>
    /// <param name="gpu">The detected GPU capabilities.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A detailed ray tracing analysis report.</returns>
    Task<RayTracingAnalysis> AnalyzeAsync(GameInfo game, GpuCapabilities gpu, CancellationToken ct = default);

    /// <summary>
    /// Evaluates which patches are available and recommended based on the analysis.
    /// </summary>
    /// <param name="game">The game metadata.</param>
    /// <param name="analysis">The ray tracing analysis result.</param>
    /// <returns>A collection of available patch actions.</returns>
    Task<IReadOnlyList<PatchAction>> GetAvailablePatchesAsync(GameInfo game, RayTracingAnalysis analysis);

    /// <summary>
    /// Applies a specific patch action to the game installation safely via the backup manager.
    /// </summary>
    /// <param name="game">The game metadata.</param>
    /// <param name="patch">The patch action to apply.</param>
    /// <param name="backup">Backup manager to safeguard original files before modification.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><c>true</c> if the patch applied successfully; otherwise, <c>false</c>.</returns>
    Task<bool> ApplyPatchAsync(GameInfo game, PatchAction patch, IBackupManager backup, CancellationToken ct = default);

    /// <summary>
    /// Validates whether a patch is currently properly applied and intact.
    /// </summary>
    /// <param name="game">The game metadata.</param>
    /// <param name="patch">The patch action to validate.</param>
    /// <returns><c>true</c> if the patch is valid and active; otherwise, <c>false</c>.</returns>
    Task<bool> ValidatePatchAsync(GameInfo game, PatchAction patch);

    /// <summary>
    /// Determines whether this adapter supports the given game installation.
    /// </summary>
    /// <param name="game">The game metadata to check.</param>
    /// <returns><c>true</c> if supported; otherwise, <c>false</c>.</returns>
    bool SupportsGame(GameInfo game);
}
