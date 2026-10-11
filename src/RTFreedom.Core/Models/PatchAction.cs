using RTFreedom.Core.Enums;

namespace RTFreedom.Core.Models;

/// <summary>
/// Represents an atomic patch or configuration action applied to a game installation.
/// </summary>
/// <param name="Id">Unique identifier for this patch action.</param>
/// <param name="Description">Human-readable description of what this patch performs.</param>
/// <param name="Type">Type of patch action being applied.</param>
/// <param name="TargetFile">Target file path being created, altered, or replaced.</param>
/// <param name="BackupFile">Path to the backup copy of the target file, if one exists.</param>
/// <param name="IsReversible">Whether this patch action can be cleanly reversed or restored.</param>
/// <param name="AppliedAt">Timestamp when the patch action was applied.</param>
public record PatchAction(
    string Id,
    string Description,
    PatchType Type,
    string TargetFile,
    string? BackupFile,
    bool IsReversible,
    DateTime AppliedAt);
