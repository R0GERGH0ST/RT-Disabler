using System.Text.Json;
using RTFreedom.Core.Models;

namespace RTFreedom.Backup;

/// <summary>
/// Provides atomic persistence, serialization, and retrieval for backup manifests using System.Text.Json.
/// </summary>
public class BackupManifestStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _backupRoot;

    /// <summary>
    /// Initializes a new instance of the <see cref="BackupManifestStore"/> class.
    /// </summary>
    /// <param name="backupRoot">Root directory where game backups are stored. If null, defaults to AppData\Local\RTFreedom\Backups.</param>
    public BackupManifestStore(string? backupRoot = null)
    {
        _backupRoot = backupRoot ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RTFreedom",
            "Backups");
    }

    /// <summary>
    /// Gets the root directory path where backups are stored.
    /// </summary>
    public string BackupRoot => _backupRoot;

    /// <summary>
    /// Persists a backup manifest atomically to disk at the manifest's BackupPath.
    /// </summary>
    /// <param name="manifest">The backup manifest to save.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task SaveManifestAsync(BackupManifest manifest, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentException.ThrowIfNullOrWhiteSpace(manifest.BackupPath);

        if (!Directory.Exists(manifest.BackupPath))
        {
            Directory.CreateDirectory(manifest.BackupPath);
        }

        var manifestFilePath = Path.Combine(manifest.BackupPath, "manifest.json");
        await SaveManifestToFileAsync(manifest, manifestFilePath, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Persists a backup manifest atomically to a specific file destination.
    /// </summary>
    /// <param name="manifest">The backup manifest to save.</param>
    /// <param name="destinationFilePath">Full destination path to the manifest JSON file.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task SaveManifestToFileAsync(BackupManifest manifest, string destinationFilePath, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationFilePath);

        var destinationDir = Path.GetDirectoryName(destinationFilePath);
        if (!string.IsNullOrEmpty(destinationDir) && !Directory.Exists(destinationDir))
        {
            Directory.CreateDirectory(destinationDir);
        }

        var tempFilePath = Path.Combine(
            destinationDir ?? AppContext.BaseDirectory,
            $"{Path.GetFileName(destinationFilePath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            await using (var fileStream = new FileStream(
                tempFilePath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                useAsync: true))
            {
                await JsonSerializer.SerializeAsync(fileStream, manifest, JsonOptions, ct).ConfigureAwait(false);
            }

            if (File.Exists(destinationFilePath))
            {
                File.Replace(tempFilePath, destinationFilePath, null);
            }
            else
            {
                File.Move(tempFilePath, destinationFilePath);
            }
        }
        catch
        {
            if (File.Exists(tempFilePath))
            {
                try { File.Delete(tempFilePath); } catch { /* best effort cleanup */ }
            }
            throw;
        }
    }

    /// <summary>
    /// Loads a manifest from disk for a given backup ID.
    /// Searches across game backup directories under the configured root.
    /// </summary>
    /// <param name="backupId">Unique identifier of the backup manifest.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The deserialized <see cref="BackupManifest"/>, or null if not found.</returns>
    public async Task<BackupManifest?> LoadManifestAsync(string backupId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(backupId);

        if (!Directory.Exists(_backupRoot))
        {
            return null;
        }

        // Look for any folder named backupId or check manifest.json files
        var manifestFiles = Directory.GetFiles(_backupRoot, "manifest.json", SearchOption.AllDirectories);

        foreach (var manifestPath in manifestFiles)
        {
            var manifest = await LoadManifestFromFileAsync(manifestPath, ct).ConfigureAwait(false);
            if (manifest != null && string.Equals(manifest.BackupId, backupId, StringComparison.OrdinalIgnoreCase))
            {
                return manifest;
            }
        }

        return null;
    }

    /// <summary>
    /// Loads and deserializes a backup manifest directly from a file path.
    /// </summary>
    /// <param name="manifestFilePath">Path to the manifest.json file.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The deserialized <see cref="BackupManifest"/>, or null if loading failed.</returns>
    public async Task<BackupManifest?> LoadManifestFromFileAsync(string manifestFilePath, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestFilePath);

        if (!File.Exists(manifestFilePath))
        {
            return null;
        }

        try
        {
            await using var fileStream = new FileStream(
                manifestFilePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                useAsync: true);

            return await JsonSerializer.DeserializeAsync<BackupManifest>(fileStream, JsonOptions, ct).ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Lists all backup manifests available for a specific game, ordered newest first.
    /// </summary>
    /// <param name="gameId">Unique identifier of the game.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of loaded backup manifests for the game.</returns>
    public async Task<List<BackupManifest>> ListManifestsAsync(string gameId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameId);

        var manifests = new List<BackupManifest>();
        var gameDirectory = Path.Combine(_backupRoot, gameId);

        if (!Directory.Exists(gameDirectory))
        {
            return manifests;
        }

        var manifestFiles = Directory.GetFiles(gameDirectory, "manifest.json", SearchOption.AllDirectories);

        foreach (var manifestPath in manifestFiles)
        {
            var manifest = await LoadManifestFromFileAsync(manifestPath, ct).ConfigureAwait(false);
            if (manifest != null)
            {
                manifests.Add(manifest);
            }
        }

        return manifests.OrderByDescending(m => m.CreatedAt).ToList();
    }
}
