using System.Text.Json;
using RTFreedom.Core.Enums;
using RTFreedom.Core.Interfaces;
using RTFreedom.Core.Models;

namespace RTFreedom.GameDetection;

/// <summary>
/// Manages user-configured custom and standalone game installations, persisting them
/// to local application storage in JSON format.
/// </summary>
public sealed class ManualGameManager : IGameDetector
{
    private static readonly string DefaultStoragePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RTFreedom",
        "manual_games.json");

    private readonly string _storageFilePath;
    private readonly SemaphoreSlim _fileLock = new(1, 1);
    private readonly Dictionary<string, GameInfo> _games = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Initializes a new instance of the <see cref="ManualGameManager"/> class with the default storage file.
    /// </summary>
    public ManualGameManager() : this(DefaultStoragePath)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ManualGameManager"/> class with a custom storage path.
    /// </summary>
    /// <param name="storageFilePath">Custom JSON file path for manual game entries.</param>
    public ManualGameManager(string storageFilePath)
    {
        _storageFilePath = storageFilePath ?? throw new ArgumentNullException(nameof(storageFilePath));
    }

    /// <inheritdoc />
    public string DetectorName => "Manual";

    /// <inheritdoc />
    public GamePlatform Platform => GamePlatform.Manual;

    /// <inheritdoc />
    public async Task<IReadOnlyList<GameInfo>> DetectGamesAsync(CancellationToken ct = default)
    {
        await LoadAsync(ct).ConfigureAwait(false);
        return GetGames();
    }

    /// <summary>
    /// Adds or updates a manually configured game after validating that its executable exists.
    /// </summary>
    /// <param name="game">The game metadata to add.</param>
    /// <exception cref="ArgumentNullException">Thrown if game is null.</exception>
    /// <exception cref="FileNotFoundException">Thrown if the executable path does not exist on disk.</exception>
    public void AddGame(GameInfo game)
    {
        ArgumentNullException.ThrowIfNull(game);

        if (string.IsNullOrWhiteSpace(game.ExecutablePath) || !File.Exists(game.ExecutablePath))
        {
            throw new FileNotFoundException($"The specified game executable does not exist: '{game.ExecutablePath}'", game.ExecutablePath);
        }

        // Ensure Platform is set to Manual if not already specified
        var manualGame = game.Platform == GamePlatform.Manual ? game : game with { Platform = GamePlatform.Manual };

        lock (_games)
        {
            _games[manualGame.Id] = manualGame;
        }
    }

    /// <summary>
    /// Attempts to add a manually configured game, returning false with an error message if invalid.
    /// </summary>
    /// <param name="game">The game metadata.</param>
    /// <param name="errorMessage">Error description if addition failed.</param>
    /// <returns>True if the game was successfully added; otherwise, false.</returns>
    public bool TryAddGame(GameInfo game, out string? errorMessage)
    {
        if (game == null)
        {
            errorMessage = "Game cannot be null.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(game.ExecutablePath) || !File.Exists(game.ExecutablePath))
        {
            errorMessage = $"The executable was not found at: '{game.ExecutablePath}'";
            return false;
        }

        try
        {
            AddGame(game);
            errorMessage = null;
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Removes a game by its identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the game to remove.</param>
    /// <returns>True if the game was removed; false if not found.</returns>
    public bool RemoveGame(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        lock (_games)
        {
            return _games.Remove(id);
        }
    }

    /// <summary>
    /// Gets all currently configured manual games.
    /// </summary>
    /// <returns>A read-only snapshot list of manual games.</returns>
    public IReadOnlyList<GameInfo> GetGames()
    {
        lock (_games)
        {
            return _games.Values.ToList();
        }
    }

    /// <summary>
    /// Asynchronously saves manual games to the local JSON configuration file.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    public async Task SaveAsync(CancellationToken ct = default)
    {
        await _fileLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            List<GameInfo> snapshot;
            lock (_games)
            {
                snapshot = _games.Values.ToList();
            }

            var dir = Path.GetDirectoryName(_storageFilePath);
            if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNameCaseInsensitive = true
            };

            await using var stream = new FileStream(_storageFilePath, FileMode.Create, FileAccess.Write, FileShare.None);
            await JsonSerializer.SerializeAsync(stream, snapshot, options, ct).ConfigureAwait(false);
        }
        catch
        {
            // Do not throw on persistence failures
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <summary>
    /// Asynchronously loads manual games from the local JSON configuration file.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The loaded collection of games.</returns>
    public async Task<IReadOnlyList<GameInfo>> LoadAsync(CancellationToken ct = default)
    {
        if (!File.Exists(_storageFilePath))
        {
            return GetGames();
        }

        await _fileLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            await using var stream = new FileStream(_storageFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var loaded = await JsonSerializer.DeserializeAsync<List<GameInfo>>(stream, options, ct).ConfigureAwait(false);

            if (loaded != null)
            {
                lock (_games)
                {
                    _games.Clear();
                    foreach (var game in loaded)
                    {
                        // Verify that the executable still exists
                        if (File.Exists(game.ExecutablePath))
                        {
                            _games[game.Id] = game;
                        }
                    }
                }
            }
        }
        catch
        {
            // Do not throw on corrupted storage; retain existing state
        }
        finally
        {
            _fileLock.Release();
        }

        return GetGames();
    }
}
