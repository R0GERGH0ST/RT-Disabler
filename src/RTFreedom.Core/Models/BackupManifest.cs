namespace RTFreedom.Core.Models;

/// <summary>
/// Represents an individual backed-up file entry with verification metadata.
/// </summary>
/// <param name="OriginalPath">Absolute path to the original game file before modification.</param>
/// <param name="BackupPath">Absolute path where the backup copy is preserved.</param>
/// <param name="Hash">Cryptographic hash (e.g. SHA-256) of the original file content.</param>
/// <param name="Size">Size of the file in bytes.</param>
/// <param name="DateTime">Timestamp when the backup entry was recorded.</param>
public record BackupEntry(
    string OriginalPath,
    string BackupPath,
    string Hash,
    long Size,
    DateTime DateTime);

/// <summary>
/// Represents a backup snapshot manifest containing all files backed up prior to patch application.
/// </summary>
public class BackupManifest
{
    /// <summary>
    /// Gets or sets the unique identifier for this backup manifest.
    /// </summary>
    public string BackupId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the game identifier this backup corresponds to.
    /// </summary>
    public string GameId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the directory path where backup files for this manifest are stored.
    /// </summary>
    public string BackupPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the list of individual file entries preserved in this backup.
    /// </summary>
    public List<BackupEntry> Entries { get; set; } = [];

    /// <summary>
    /// Gets or sets the timestamp when this backup manifest was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the version of the game when the backup was taken.
    /// </summary>
    public string GameVersion { get; set; } = string.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="BackupManifest"/> class.
    /// </summary>
    public BackupManifest()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BackupManifest"/> class with specified parameters.
    /// </summary>
    /// <param name="backupId">Unique backup identifier.</param>
    /// <param name="gameId">Associated game identifier.</param>
    /// <param name="backupPath">Path to the backup directory.</param>
    /// <param name="entries">List of file backup entries.</param>
    /// <param name="createdAt">Creation timestamp.</param>
    /// <param name="gameVersion">Game version at backup time.</param>
    public BackupManifest(
        string backupId,
        string gameId,
        string backupPath,
        List<BackupEntry> entries,
        DateTime createdAt,
        string gameVersion)
    {
        BackupId = backupId;
        GameId = gameId;
        BackupPath = backupPath;
        Entries = entries;
        CreatedAt = createdAt;
        GameVersion = gameVersion;
    }
}
