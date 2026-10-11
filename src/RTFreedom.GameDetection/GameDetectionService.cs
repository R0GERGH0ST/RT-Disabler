using RTFreedom.Core.Enums;
using RTFreedom.Core.Interfaces;
using RTFreedom.Core.Models;

namespace RTFreedom.GameDetection;

/// <summary>
/// Orchestrates game detection across all registered platform detectors and manual game definitions,
/// performing concurrent detection, normalization, deduplication, target matching, and caching.
/// </summary>
public sealed class GameDetectionService
{
    private readonly List<IGameDetector> _detectors;
    private readonly ManualGameManager _manualGameManager;
    private readonly SemaphoreSlim _detectionLock = new(1, 1);

    private IReadOnlyList<GameInfo> _cachedGames = Array.Empty<GameInfo>();
    private DateTime _lastScanTime = DateTime.MinValue;

    /// <summary>
    /// Gets or sets the duration for which detection scan results are cached before an automatic re-scan is performed.
    /// Default is 5 minutes.
    /// </summary>
    public TimeSpan CacheDuration { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Initializes a new instance of <see cref="GameDetectionService"/> with standard default detectors.
    /// </summary>
    public GameDetectionService()
        : this(new IGameDetector[] { new SteamGameDetector(), new EpicGameDetector(), new XboxGameDetector() }, new ManualGameManager())
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="GameDetectionService"/> with custom detectors and manual game repository.
    /// </summary>
    /// <param name="detectors">Collection of platform game detectors.</param>
    /// <param name="manualGameManager">Manual game manager instance.</param>
    public GameDetectionService(IEnumerable<IGameDetector> detectors, ManualGameManager manualGameManager)
    {
        _detectors = detectors?.ToList() ?? new List<IGameDetector>();
        _manualGameManager = manualGameManager ?? throw new ArgumentNullException(nameof(manualGameManager));
    }

    /// <summary>
    /// Gets the manual game manager associated with this detection service.
    /// </summary>
    public ManualGameManager ManualGames => _manualGameManager;

    /// <summary>
    /// Gets all registered platform detectors.
    /// </summary>
    public IReadOnlyList<IGameDetector> Detectors => _detectors;

    /// <summary>
    /// Scans the system using all detectors and manual entries, returning a deduplicated collection of games sorted by title.
    /// </summary>
    /// <param name="forceRefresh">If true, ignores cached results and forces a fresh scan.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A combined, deduplicated, sorted list of detected games.</returns>
    public async Task<IReadOnlyList<GameInfo>> DetectAllGamesAsync(bool forceRefresh = false, CancellationToken ct = default)
    {
        if (!forceRefresh && _cachedGames.Count > 0 && (DateTime.UtcNow - _lastScanTime) < CacheDuration)
        {
            return _cachedGames;
        }

        await _detectionLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // Double check cache inside lock
            if (!forceRefresh && _cachedGames.Count > 0 && (DateTime.UtcNow - _lastScanTime) < CacheDuration)
            {
                return _cachedGames;
            }

            var tasks = new List<Task<IReadOnlyList<GameInfo>>>();

            // 1. Run all platform detectors concurrently
            foreach (var detector in _detectors)
            {
                tasks.Add(SafeDetectAsync(detector, ct));
            }

            // 2. Run manual game manager
            tasks.Add(SafeDetectAsync(_manualGameManager, ct));

            var results = await Task.WhenAll(tasks).ConfigureAwait(false);

            // 3. Deduplicate by normalized install path and executable path
            var allDetected = results.SelectMany(r => r).ToList();
            var deduplicated = DeduplicateGames(allDetected);

            // 4. Sort alphabetically by Title
            var sorted = deduplicated
                .OrderBy(g => g.Title, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            _cachedGames = sorted;
            _lastScanTime = DateTime.UtcNow;

            return _cachedGames;
        }
        finally
        {
            _detectionLock.Release();
        }
    }

    /// <summary>
    /// Retrieves only the games recognized as RT Freedom target titles (e.g. Indiana Jones, Gears 5, DOOM: The Dark Ages).
    /// </summary>
    /// <param name="forceRefresh">Whether to bypass cache and perform a new scan.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Collection of detected target games.</returns>
    public async Task<IReadOnlyList<GameInfo>> GetTargetGamesAsync(bool forceRefresh = false, CancellationToken ct = default)
    {
        var allGames = await DetectAllGamesAsync(forceRefresh, ct).ConfigureAwait(false);
        return FilterTargetGames(allGames);
    }

    /// <summary>
    /// Filters an existing collection of games, returning only those that match known target games.
    /// </summary>
    /// <param name="games">Collection of games to evaluate.</param>
    /// <returns>Games matching RT Freedom target criteria.</returns>
    public IReadOnlyList<GameInfo> FilterTargetGames(IEnumerable<GameInfo> games)
    {
        if (games == null)
        {
            return Array.Empty<GameInfo>();
        }

        return games.Where(IsTargetGame).ToList();
    }

    /// <summary>
    /// Evaluates whether a given game matches any known ray-tracing target games.
    /// </summary>
    /// <param name="game">The game metadata.</param>
    /// <returns>True if recognized as a target game; otherwise, false.</returns>
    public bool IsTargetGame(GameInfo game)
    {
        return GameIdentifier.IdentifyTargetGame(game).HasValue;
    }

    /// <summary>
    /// Identifies the specific target title for a game, if recognized.
    /// </summary>
    /// <param name="game">The game metadata.</param>
    /// <returns>The target game enum value, or null if not a target game.</returns>
    public TargetGame? IdentifyTargetGame(GameInfo game)
    {
        return GameIdentifier.IdentifyTargetGame(game);
    }

    private static async Task<IReadOnlyList<GameInfo>> SafeDetectAsync(IGameDetector detector, CancellationToken ct)
    {
        try
        {
            return await detector.DetectGamesAsync(ct).ConfigureAwait(false);
        }
        catch
        {
            // Detector failures should never break the entire detection pipeline
            return Array.Empty<GameInfo>();
        }
    }

    private static List<GameInfo> DeduplicateGames(IEnumerable<GameInfo> games)
    {
        var pathMap = new Dictionary<string, GameInfo>(StringComparer.OrdinalIgnoreCase);

        foreach (var game in games)
        {
            // Normalize path for accurate deduplication
            var key = NormalizePath(game.InstallPath);
            if (string.IsNullOrWhiteSpace(key))
            {
                key = NormalizePath(game.ExecutablePath);
            }

            if (string.IsNullOrWhiteSpace(key))
            {
                key = game.Id;
            }

            if (!pathMap.TryGetValue(key, out var existing))
            {
                pathMap[key] = game;
            }
            else
            {
                // If a duplicate is encountered, prioritize target games over non-target,
                // and prioritize specific platform detectors over manual definitions
                var existingIsTarget = GameIdentifier.IdentifyTargetGame(existing).HasValue;
                var currentIsTarget = GameIdentifier.IdentifyTargetGame(game).HasValue;

                if (!existingIsTarget && currentIsTarget)
                {
                    pathMap[key] = game;
                }
                else if (existing.Platform == GamePlatform.Manual && game.Platform != GamePlatform.Manual)
                {
                    pathMap[key] = game;
                }
            }
        }

        return pathMap.Values.ToList();
    }

    private static string NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        try
        {
            return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch
        {
            return path.TrimEnd('\\', '/');
        }
    }
}
