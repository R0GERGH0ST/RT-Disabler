using System.Text.Json;
using System.Text.Json.Serialization;
using RTFreedom.Core.Enums;
using RTFreedom.Core.Interfaces;
using RTFreedom.Core.Models;

namespace RTFreedom.GameDetection;

/// <summary>
/// Detects installed Epic Games Store games by parsing JSON manifest files (*.item)
/// located in the Epic Games Launcher ProgramData repository.
/// </summary>
public sealed class EpicGameDetector : IGameDetector
{
    private static readonly string DefaultManifestsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "Epic",
        "EpicGamesLauncher",
        "Data",
        "Manifests");

    private readonly string _manifestsDirectory;

    /// <summary>
    /// Initializes a new instance of the <see cref="EpicGameDetector"/> class with default or custom manifest path.
    /// </summary>
    /// <param name="manifestsDirectory">Optional custom directory path for manifest files.</param>
    public EpicGameDetector(string? manifestsDirectory = null)
    {
        _manifestsDirectory = manifestsDirectory ?? DefaultManifestsPath;
    }

    /// <inheritdoc />
    public string DetectorName => "EpicGames";

    /// <inheritdoc />
    public GamePlatform Platform => GamePlatform.EpicGames;

    /// <inheritdoc />
    public Task<IReadOnlyList<GameInfo>> DetectGamesAsync(CancellationToken ct = default)
    {
        return Task.Run(() => (IReadOnlyList<GameInfo>)ScanGames(ct), ct);
    }

    private List<GameInfo> ScanGames(CancellationToken ct)
    {
        var games = new List<GameInfo>();

        try
        {
            if (!Directory.Exists(_manifestsDirectory))
            {
                return games;
            }

            var itemFiles = Directory.GetFiles(_manifestsDirectory, "*.item", SearchOption.TopDirectoryOnly);

            foreach (var itemPath in itemFiles)
            {
                if (ct.IsCancellationRequested)
                {
                    break;
                }

                try
                {
                    var jsonContent = File.ReadAllText(itemPath);
                    var manifest = JsonSerializer.Deserialize<EpicManifestModel>(jsonContent);

                    if (manifest == null || string.IsNullOrWhiteSpace(manifest.InstallLocation))
                    {
                        continue;
                    }

                    if (!Directory.Exists(manifest.InstallLocation))
                    {
                        continue;
                    }

                    var game = CreateGameInfo(manifest);
                    if (game != null)
                    {
                        games.Add(game);
                    }
                }
                catch
                {
                    // Ignore corrupted or unreadable manifest item
                }
            }
        }
        catch
        {
            // Gracefully handle file system or permission issues
        }

        return games;
    }

    private GameInfo? CreateGameInfo(EpicManifestModel manifest)
    {
        var id = manifest.AppName ?? manifest.MainGameAppName ?? Guid.NewGuid().ToString("N");
        var title = manifest.DisplayName ?? manifest.AppName ?? "Unknown Epic Game";
        var installPath = manifest.InstallLocation!;

        var target = GameIdentifier.IdentifyById(id) ??
                     GameIdentifier.IdentifyByTitle(title) ??
                     GameIdentifier.IdentifyById(manifest.MainGameAppName ?? string.Empty);

        // Graphics API
        var graphicsApi = target.HasValue
            ? GameIdentifier.GetRenderingApi(id)
            : DetectGraphicsApi(installPath);

        // Publisher
        var publisher = target.HasValue
            ? GameIdentifier.GetCanonicalPublisher(target.Value)
            : "Epic Games Store";

        // Display Title
        var displayTitle = target.HasValue
            ? GameIdentifier.GetCanonicalTitle(target.Value)
            : title;

        // Executable Path
        var executablePath = FindExecutable(installPath, manifest.LaunchExecutable, target);

        // Config & Shader files
        var configFiles = FindConfigFiles(installPath, id);
        var shaderFiles = FindShaderFiles(installPath, id);

        return new GameInfo(
            Id: id,
            Title: displayTitle,
            ExecutablePath: executablePath,
            InstallPath: installPath,
            Version: manifest.AppVersionString,
            Publisher: publisher,
            Platform: GamePlatform.EpicGames,
            GraphicsApi: graphicsApi,
            ConfigurationFiles: configFiles,
            ShaderFiles: shaderFiles,
            DetectedAt: DateTime.UtcNow);
    }

    private string FindExecutable(string installPath, string? manifestLaunchExecutable, TargetGame? target)
    {
        try
        {
            // 1. If manifest defines LaunchExecutable and it exists
            if (!string.IsNullOrWhiteSpace(manifestLaunchExecutable))
            {
                var direct = Path.Combine(installPath, manifestLaunchExecutable);
                if (File.Exists(direct))
                {
                    return direct;
                }
            }

            // 2. Known candidate executables if target is recognized
            if (target.HasValue)
            {
                var candidates = GameIdentifier.GetCandidateExecutables(target.Value);
                foreach (var candidate in candidates)
                {
                    var fullCandidate = Path.IsPathRooted(candidate) ? candidate : Path.Combine(installPath, candidate);
                    if (File.Exists(fullCandidate))
                    {
                        return fullCandidate;
                    }
                }
            }

            // 3. Fallback heuristic search
            var exeFiles = Directory.GetFiles(installPath, "*.exe", SearchOption.AllDirectories);
            var filtered = exeFiles
                .Where(f => !Path.GetFileName(f).StartsWith("unins", StringComparison.OrdinalIgnoreCase) &&
                            !Path.GetFileName(f).Contains("crash", StringComparison.OrdinalIgnoreCase) &&
                            !Path.GetFileName(f).Contains("setup", StringComparison.OrdinalIgnoreCase) &&
                            !Path.GetFileName(f).Contains("redist", StringComparison.OrdinalIgnoreCase) &&
                            !Path.GetFileName(f).Contains("dotnet", StringComparison.OrdinalIgnoreCase) &&
                            !Path.GetFileName(f).Contains("vc_redist", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (filtered.Count > 0)
            {
                var folderName = Path.GetFileName(installPath.TrimEnd(Path.DirectorySeparatorChar));
                var matchByName = filtered.FirstOrDefault(f => Path.GetFileNameWithoutExtension(f).Equals(folderName, StringComparison.OrdinalIgnoreCase));
                if (matchByName != null)
                {
                    return matchByName;
                }

                return filtered.OrderByDescending(f =>
                {
                    try { return new FileInfo(f).Length; } catch { return 0L; }
                }).First();
            }
        }
        catch
        {
            // Suppress IO errors
        }

        return string.Empty;
    }

    private List<string> FindConfigFiles(string installPath, string id)
    {
        var result = new List<string>();
        try
        {
            var known = GameIdentifier.GetKnownConfigFiles(id);
            foreach (var rel in known)
            {
                var full = Path.Combine(installPath, rel);
                if (File.Exists(full))
                {
                    result.Add(full);
                }
            }

            if (result.Count == 0)
            {
                var iniFiles = Directory.GetFiles(installPath, "*.ini", SearchOption.AllDirectories).Take(5);
                result.AddRange(iniFiles);
            }
        }
        catch
        {
            // Suppress IO errors
        }

        return result;
    }

    private List<string> FindShaderFiles(string installPath, string id)
    {
        var result = new List<string>();
        try
        {
            var known = GameIdentifier.GetKnownShaderFiles(id);
            foreach (var rel in known)
            {
                var full = Path.Combine(installPath, rel);
                if (File.Exists(full))
                {
                    result.Add(full);
                }
            }
        }
        catch
        {
            // Suppress IO errors
        }

        return result;
    }

    private RenderingApi DetectGraphicsApi(string installPath)
    {
        try
        {
            var files = Directory.GetFiles(installPath, "*.dll", SearchOption.AllDirectories);
            if (files.Any(f => Path.GetFileName(f).Contains("vulkan", StringComparison.OrdinalIgnoreCase)))
            {
                return RenderingApi.Vulkan;
            }

            if (files.Any(f => Path.GetFileName(f).Contains("d3d12", StringComparison.OrdinalIgnoreCase) ||
                               Path.GetFileName(f).Contains("dxil", StringComparison.OrdinalIgnoreCase)))
            {
                return RenderingApi.DirectX12;
            }

            if (files.Any(f => Path.GetFileName(f).Contains("d3d11", StringComparison.OrdinalIgnoreCase)))
            {
                return RenderingApi.DirectX11;
            }
        }
        catch
        {
            // Suppress
        }

        return RenderingApi.Unknown;
    }

    private sealed class EpicManifestModel
    {
        [JsonPropertyName("DisplayName")]
        public string? DisplayName { get; set; }

        [JsonPropertyName("InstallLocation")]
        public string? InstallLocation { get; set; }

        [JsonPropertyName("AppName")]
        public string? AppName { get; set; }

        [JsonPropertyName("MainGameAppName")]
        public string? MainGameAppName { get; set; }

        [JsonPropertyName("LaunchExecutable")]
        public string? LaunchExecutable { get; set; }

        [JsonPropertyName("AppVersionString")]
        public string? AppVersionString { get; set; }
    }
}
