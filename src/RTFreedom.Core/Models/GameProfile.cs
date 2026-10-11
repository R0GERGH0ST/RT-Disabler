using RTFreedom.Core.Enums;

namespace RTFreedom.Core.Models;

/// <summary>
/// Represents the profile, configuration, and patching state of an individual game.
/// </summary>
public class GameProfile
{
    /// <summary>
    /// Gets or sets the detected game metadata.
    /// </summary>
    public GameInfo Game { get; set; }

    /// <summary>
    /// Gets or sets the evaluated compatibility level for ray tracing bypass.
    /// </summary>
    public CompatibilityLevel Compatibility { get; set; }

    /// <summary>
    /// Gets or sets current status of patches applied to this game.
    /// </summary>
    public PatchStatus Status { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether patches are currently applied to the game installation.
    /// </summary>
    public bool IsPatched { get; set; }

    /// <summary>
    /// Gets or sets the ray tracing analysis result, if analysis has been performed.
    /// </summary>
    public RayTracingAnalysis? Analysis { get; set; }

    /// <summary>
    /// Gets or sets the collection of patches currently applied to the game.
    /// </summary>
    public List<PatchAction> AppliedPatches { get; set; } = [];

    /// <summary>
    /// Gets or sets the timestamp when the profile or its patches were last modified.
    /// </summary>
    public DateTime? LastModified { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the backup manifest associated with the current patched state.
    /// </summary>
    public string? BackupId { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="GameProfile"/> class.
    /// </summary>
    /// <param name="game">The detected game metadata.</param>
    /// <param name="compatibility">The initial compatibility level.</param>
    /// <param name="status">The initial patch status.</param>
    public GameProfile(GameInfo game, CompatibilityLevel compatibility = CompatibilityLevel.Unknown, PatchStatus status = PatchStatus.NotAnalyzed)
    {
        Game = game;
        Compatibility = compatibility;
        Status = status;
        IsPatched = status == PatchStatus.Patched;
    }

    /// <summary>
    /// Parameterless constructor for serialization and reflection.
    /// </summary>
    public GameProfile()
    {
        Game = null!;
        Compatibility = CompatibilityLevel.Unknown;
        Status = PatchStatus.NotAnalyzed;
    }
}
