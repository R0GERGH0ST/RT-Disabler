using RTFreedom.Core.Models;

namespace RTFreedom.Core.Interfaces;

/// <summary>
/// Manages file backup snapshots, integrity verification, and restoration for game patches.
/// </summary>
public interface IBackupManager
{
    /// <summary>
    /// Creates a backup snapshot of the specified files prior to modification.
    /// </summary>
    /// <param name="gameId">Unique identifier of the game.</param>
    /// <param name="filePaths">Absolute paths of files to back up.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created backup manifest.</returns>
    Task<BackupManifest> CreateBackupAsync(string gameId, IEnumerable<string> filePaths, CancellationToken ct = default);

    /// <summary>
    /// Restores all original files from the specified backup snapshot.
    /// </summary>
    /// <param name="backupId">Identifier of the backup manifest to restore.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><c>true</c> if all files were restored successfully; otherwise, <c>false</c>.</returns>
    Task<bool> RestoreBackupAsync(string backupId, CancellationToken ct = default);

    /// <summary>
    /// Retrieves all backup manifests associated with a specific game.
    /// </summary>
    /// <param name="gameId">Unique identifier of the game.</param>
    /// <returns>A collection of backup manifests.</returns>
    Task<IReadOnlyList<BackupManifest>> GetBackupsAsync(string gameId);

    /// <summary>
    /// Verifies the cryptographic integrity of files within a backup manifest.
    /// </summary>
    /// <param name="backupId">Identifier of the backup manifest to verify.</param>
    /// <returns><c>true</c> if all file hashes match; otherwise, <c>false</c>.</returns>
    Task<bool> VerifyBackupAsync(string backupId);

    /// <summary>
    /// Deletes a backup snapshot and its associated files from disk.
    /// </summary>
    /// <param name="backupId">Identifier of the backup manifest to delete.</param>
    /// <returns><c>true</c> if the backup was deleted; otherwise, <c>false</c>.</returns>
    Task<bool> DeleteBackupAsync(string backupId);
}
