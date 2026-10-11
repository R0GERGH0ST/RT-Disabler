using RTFreedom.Core.Interfaces;
using RTFreedom.Core.Models;

namespace RTFreedom.Backup;

/// <summary>
/// Implements full backup lifecycle operations including SHA-256 snapshotting,
/// verification, atomic restoration, and storage management.
/// </summary>
public class BackupService : IBackupManager
{
    private readonly string _backupRoot;
    private readonly BackupManifestStore _manifestStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="BackupService"/> class with default or custom backup directory.
    /// </summary>
    /// <param name="backupRoot">Optional custom backup root path. If null, uses AppData\Local\RTFreedom\Backups.</param>
    public BackupService(string? backupRoot = null)
    {
        _backupRoot = backupRoot ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RTFreedom",
            "Backups");

        _manifestStore = new BackupManifestStore(_backupRoot);
    }

    /// <summary>
    /// Gets the root directory where all game backups are stored.
    /// </summary>
    public string BackupRoot => _backupRoot;

    /// <inheritdoc />
    public async Task<BackupManifest> CreateBackupAsync(
        string gameId,
        IEnumerable<string> filePaths,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameId);
        ArgumentNullException.ThrowIfNull(filePaths);

        var fileList = filePaths.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var backupId = Guid.NewGuid().ToString("N");
        var backupDir = Path.Combine(_backupRoot, gameId, backupId);

        if (!Directory.Exists(backupDir))
        {
            Directory.CreateDirectory(backupDir);
        }

        var entries = new List<BackupEntry>();
        var fileIndex = 0;

        foreach (var originalPath in fileList)
        {
            ct.ThrowIfCancellationRequested();

            if (!File.Exists(originalPath))
            {
                continue;
            }

            var fileName = Path.GetFileName(originalPath);
            // Prefix index to prevent name collisions if files come from different directories
            var uniqueFileName = $"{fileIndex++}_{fileName}";
            var backupFilePath = Path.Combine(backupDir, uniqueFileName);

            // Compute hash of original file prior to copying
            var hash = await FileIntegrityVerifier.ComputeHashAsync(originalPath, ct).ConfigureAwait(false);
            var fileInfo = new FileInfo(originalPath);

            // Copy file safely to backup directory
            File.Copy(originalPath, backupFilePath, overwrite: true);

            // Verify copied file integrity
            var backupVerified = await FileIntegrityVerifier.VerifyFileAsync(backupFilePath, hash, ct).ConfigureAwait(false);
            if (!backupVerified)
            {
                throw new InvalidOperationException($"Cryptographic verification failed for backed up file '{backupFilePath}'.");
            }

            var entry = new BackupEntry(
                OriginalPath: Path.GetFullPath(originalPath),
                BackupPath: Path.GetFullPath(backupFilePath),
                Hash: hash,
                Size: fileInfo.Length,
                DateTime: DateTime.UtcNow);

            entries.Add(entry);
        }

        var manifest = new BackupManifest(
            backupId: backupId,
            gameId: gameId,
            backupPath: Path.GetFullPath(backupDir),
            entries: entries,
            createdAt: DateTime.UtcNow,
            gameVersion: "1.0.0");

        // Save manifest.json atomically
        await _manifestStore.SaveManifestAsync(manifest, ct).ConfigureAwait(false);

        return manifest;
    }

    /// <inheritdoc />
    public async Task<bool> RestoreBackupAsync(string backupId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(backupId);

        var manifest = await _manifestStore.LoadManifestAsync(backupId, ct).ConfigureAwait(false);
        if (manifest == null)
        {
            return false;
        }

        // Pre-validate all backup files exist and hashes match before starting restoration
        foreach (var entry in manifest.Entries)
        {
            ct.ThrowIfCancellationRequested();

            if (!File.Exists(entry.BackupPath))
            {
                return false;
            }

            var isValid = await FileIntegrityVerifier.VerifyFileAsync(entry.BackupPath, entry.Hash, ct).ConfigureAwait(false);
            if (!isValid)
            {
                return false;
            }
        }

        // Restore each file to its original path safely
        foreach (var entry in manifest.Entries)
        {
            ct.ThrowIfCancellationRequested();

            var originalDir = Path.GetDirectoryName(entry.OriginalPath);
            if (!string.IsNullOrEmpty(originalDir) && !Directory.Exists(originalDir))
            {
                Directory.CreateDirectory(originalDir);
            }

            var tempFilePath = Path.Combine(
                originalDir ?? AppContext.BaseDirectory,
                $"{Path.GetFileName(entry.OriginalPath)}.{Guid.NewGuid():N}.restore.tmp");

            try
            {
                File.Copy(entry.BackupPath, tempFilePath, overwrite: true);

                if (File.Exists(entry.OriginalPath))
                {
                    File.Replace(tempFilePath, entry.OriginalPath, null);
                }
                else
                {
                    File.Move(tempFilePath, entry.OriginalPath);
                }

                // Verify restored file hash matches manifest
                var restoredVerified = await FileIntegrityVerifier.VerifyFileAsync(entry.OriginalPath, entry.Hash, ct).ConfigureAwait(false);
                if (!restoredVerified)
                {
                    return false;
                }
            }
            catch
            {
                if (File.Exists(tempFilePath))
                {
                    try { File.Delete(tempFilePath); } catch { /* best effort cleanup */ }
                }
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BackupManifest>> GetBackupsAsync(string gameId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameId);

        var manifests = await _manifestStore.ListManifestsAsync(gameId).ConfigureAwait(false);
        return manifests.AsReadOnly();
    }

    /// <inheritdoc />
    public async Task<bool> VerifyBackupAsync(string backupId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(backupId);

        var manifest = await _manifestStore.LoadManifestAsync(backupId).ConfigureAwait(false);
        if (manifest == null)
        {
            return false;
        }

        if (manifest.Entries.Count == 0)
        {
            return true;
        }

        foreach (var entry in manifest.Entries)
        {
            if (!File.Exists(entry.BackupPath))
            {
                return false;
            }

            var isValid = await FileIntegrityVerifier.VerifyFileAsync(entry.BackupPath, entry.Hash).ConfigureAwait(false);
            if (!isValid)
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteBackupAsync(string backupId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(backupId);

        var manifest = await _manifestStore.LoadManifestAsync(backupId).ConfigureAwait(false);
        if (manifest == null)
        {
            return false;
        }

        try
        {
            if (Directory.Exists(manifest.BackupPath))
            {
                Directory.Delete(manifest.BackupPath, recursive: true);
            }
            return true;
        }
        catch
        {
            return false;
        }
    }
}
