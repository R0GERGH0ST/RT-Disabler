using Microsoft.Win32;
using RTFreedom.Core.Enums;
using RTFreedom.Core.Interfaces;
using RTFreedom.Core.Models;

namespace RTFreedom.GameDetection;

/// <summary>
/// Detects installed Steam games by querying Windows registry keys, parsing Steam library
/// folder definitions, and inspecting ACF application manifests.
/// </summary>
public sealed class SteamGameDetector : IGameDetector
{
    private const string SteamRegKeyWow64 = @"SOFTWARE\WOW6432Node\Valve\Steam";
    private const string SteamRegKeyDirect = @"SOFTWARE\Valve\Steam";

    /// <inheritdoc />
    public string DetectorName => "Steam";

    /// <inheritdoc />
    public GamePlatform Platform => GamePlatform.Steam;

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
            var steamPath = FindSteamInstallPath();
            if (string.IsNullOrWhiteSpace(steamPath) || !Directory.Exists(steamPath))
            {
                return games;
            }

            var libraryPaths = FindLibraryFolders(steamPath);

            foreach (var libraryPath in libraryPaths)
            {
                if (ct.IsCancellationRequested)
                {
                    break;
                }

                var steamAppsDir = Path.Combine(libraryPath, "steamapps");
                if (!Directory.Exists(steamAppsDir))
                {
                    continue;
                }

                try
                {
                    var manifestFiles = Directory.GetFiles(steamAppsDir, "appmanifest_*.acf", SearchOption.TopDirectoryOnly);

                    foreach (var manifestPath in manifestFiles)
                    {
                        if (ct.IsCancellationRequested)
                        {
                            break;
                        }

                        var game = ParseManifest(manifestPath, steamAppsDir);
                        if (game != null)
                        {
                            games.Add(game);
                        }
                    }
                }
                catch
                {
                    // Ignore inaccessible library folders or directory enumeration errors
                }
            }
        }
        catch
        {
            // Graceful handling of unexpected scanning errors
        }

        return games;
    }

    /// <summary>
    /// Searches 64-bit and 32-bit registry hives for the Steam client installation path.
    /// </summary>
    /// <returns>Steam install directory path, or null if not found.</returns>
    public string? FindSteamInstallPath()
    {
        try
        {
            // Check HKLM WOW6432Node
            using (var key = Registry.LocalMachine.OpenSubKey(SteamRegKeyWow64))
            {
                var val = key?.GetValue("InstallPath") as string;
                if (!string.IsNullOrWhiteSpace(val) && Directory.Exists(val))
                {
                    return val;
                }
            }

            // Check HKCU
            using (var key = Registry.CurrentUser.OpenSubKey(SteamRegKeyDirect))
            {
                var val = key?.GetValue("SteamPath") as string;
                if (!string.IsNullOrWhiteSpace(val))
                {
                    // SteamPath in HKCU often has forward slashes
                    var normalized = val.Replace('/', '\\');
                    if (Directory.Exists(normalized))
                    {
                        return normalized;
                    }
                }
            }

            // Check HKLM 64-bit / standard
            using (var key = Registry.LocalMachine.OpenSubKey(SteamRegKeyDirect))
            {
                var val = key?.GetValue("InstallPath") as string;
                if (!string.IsNullOrWhiteSpace(val) && Directory.Exists(val))
                {
                    return val;
                }
            }

            // Common default path fallback
            var defaultPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam");
            if (Directory.Exists(defaultPath))
            {
                return defaultPath;
            }
        }
        catch
        {
            // Suppress registry access exceptions
        }

        return null;
    }

    /// <summary>
    /// Locates all configured Steam library folders from libraryfolders.vdf.
    /// </summary>
    /// <param name="steamPath">The base Steam installation directory.</param>
    /// <returns>A collection of verified directory paths containing Steam libraries.</returns>
    public HashSet<string> FindLibraryFolders(string steamPath)
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { steamPath };

        var libraryFoldersVdf = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
        if (!File.Exists(libraryFoldersVdf))
        {
            return paths;
        }

        try
        {
            var root = VdfParser.Parse(libraryFoldersVdf);
            var libraryFoldersNode = root["libraryfolders"] ?? root;

            foreach (var child in libraryFoldersNode.Children.Values)
            {
                // Each child is either an index ("0", "1", ...) or "path"
                var folderPath = child["path"]?.Value ?? child.Value;
                if (!string.IsNullOrWhiteSpace(folderPath))
                {
                    // Normalize slashes
                    folderPath = folderPath.Replace('/', '\\');
                    if (Directory.Exists(folderPath))
                    {
                        paths.Add(folderPath);
                    }
                }
            }
        }
        catch
        {
            // Graceful error handling for corrupted libraryfolders.vdf
        }

        return paths;
    }

    private GameInfo? ParseManifest(string manifestPath, string steamAppsDir)
    {
        try
        {
            var root = VdfParser.Parse(manifestPath);
            var appState = root["AppState"] ?? root;

            var appId = appState["appid"]?.Value;
            var name = appState["name"]?.Value;
            var installDirName = appState["installdir"]?.Value;

            if (string.IsNullOrWhiteSpace(appId) || string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            // Filter out Steam Common Redistributables or SteamVR drivers / tools if not target
            if (appId == "228980" || appId == "250820")
            {
                return null;
            }

            var commonDir = Path.Combine(steamAppsDir, "common");
            var installPath = !string.IsNullOrWhiteSpace(installDirName)
                ? Path.Combine(commonDir, installDirName)
                : commonDir;

            if (!Directory.Exists(installPath))
            {
                return null;
            }

            var target = GameIdentifier.IdentifyById(appId) ?? GameIdentifier.IdentifyByTitle(name);

            // Determine API
            var graphicsApi = target.HasValue
                ? GameIdentifier.GetRenderingApi(appId)
                : DetectGraphicsApi(installPath);

            // Determine Publisher
            var publisher = target.HasValue
                ? GameIdentifier.GetCanonicalPublisher(target.Value)
                : appState["UserConfig"]?["Publisher"]?.Value;

            // Locate Executable
            var executablePath = FindExecutable(installPath, target);

            // Locate Config and Shader files
            var configFiles = FindConfigFiles(installPath, appId);
            var shaderFiles = FindShaderFiles(installPath, appId);

            return new GameInfo(
                Id: appId,
                Title: target.HasValue ? GameIdentifier.GetCanonicalTitle(target.Value) : name,
                ExecutablePath: executablePath,
                InstallPath: installPath,
                Version: appState["buildid"]?.Value,
                Publisher: publisher,
                Platform: GamePlatform.Steam,
                GraphicsApi: graphicsApi,
                ConfigurationFiles: configFiles,
                ShaderFiles: shaderFiles,
                DetectedAt: DateTime.UtcNow);
        }
        catch
        {
            return null;
        }
    }

    private string FindExecutable(string installPath, TargetGame? target)
    {
        try
        {
            // Check candidate executables if target is known
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

            // Heuristic search for game executable
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
                // Prefer executable that shares name with folder or largest executable
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
            // Fallback
        }

        return string.Empty;
    }

    private List<string> FindConfigFiles(string installPath, string appId)
    {
        var result = new List<string>();
        try
        {
            var known = GameIdentifier.GetKnownConfigFiles(appId);
            foreach (var rel in known)
            {
                var full = Path.Combine(installPath, rel);
                if (File.Exists(full))
                {
                    result.Add(full);
                }
            }

            // Also search for standard engine config files if empty
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

    private List<string> FindShaderFiles(string installPath, string appId)
    {
        var result = new List<string>();
        try
        {
            var known = GameIdentifier.GetKnownShaderFiles(appId);
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
}
