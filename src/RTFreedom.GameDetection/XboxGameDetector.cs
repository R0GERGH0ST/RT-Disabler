using System.Xml.Linq;
using Microsoft.Win32;
using RTFreedom.Core.Enums;
using RTFreedom.Core.Interfaces;
using RTFreedom.Core.Models;

namespace RTFreedom.GameDetection;

/// <summary>
/// Detects Xbox app and Microsoft Store PC game installations across standard Xbox root folders,
/// WindowsApps directories, and GamingServices registry configurations.
/// </summary>
public sealed class XboxGameDetector : IGameDetector
{
    private const string GamingServicesRegKey = @"SOFTWARE\Microsoft\GamingServices";
    private const string PackageVolumesRegKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Appx\PackageVolumes";

    /// <inheritdoc />
    public string DetectorName => "Xbox";

    /// <inheritdoc />
    public GamePlatform Platform => GamePlatform.Xbox;

    /// <inheritdoc />
    public Task<IReadOnlyList<GameInfo>> DetectGamesAsync(CancellationToken ct = default)
    {
        return Task.Run(() => (IReadOnlyList<GameInfo>)ScanGames(ct), ct);
    }

    private List<GameInfo> ScanGames(CancellationToken ct)
    {
        var games = new List<GameInfo>();
        var scannedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var searchRoots = FindXboxSearchDirectories();

            foreach (var root in searchRoots)
            {
                if (ct.IsCancellationRequested)
                {
                    break;
                }

                if (!Directory.Exists(root))
                {
                    continue;
                }

                try
                {
                    // Look through immediate child directories
                    var gameDirectories = Directory.GetDirectories(root);
                    foreach (var gameDir in gameDirectories)
                    {
                        if (ct.IsCancellationRequested)
                        {
                            break;
                        }

                        if (!scannedPaths.Add(gameDir))
                        {
                            continue;
                        }

                        var game = InspectXboxGameDirectory(gameDir);
                        if (game != null)
                        {
                            games.Add(game);
                        }
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    // WindowsApps or protected directory access denied - expected and handled
                }
                catch
                {
                    // Ignore directory enumeration failures
                }
            }
        }
        catch
        {
            // Graceful handling of overall detection errors
        }

        return games;
    }

    /// <summary>
    /// Discovers candidate directories where Xbox PC games can be installed.
    /// </summary>
    /// <returns>Collection of candidate root directories.</returns>
    public HashSet<string> FindXboxSearchDirectories()
    {
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 1. Primary C:\XboxGames
        var defaultXboxGames = @"C:\XboxGames";
        if (Directory.Exists(defaultXboxGames))
        {
            roots.Add(defaultXboxGames);
        }

        // 2. WindowsApps folder on system drive
        var systemApps = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "WindowsApps");
        if (Directory.Exists(systemApps))
        {
            roots.Add(systemApps);
        }

        // 3. Scan all logical drives for <Drive>:\XboxGames and <Drive>:\WindowsApps
        try
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (!drive.IsReady)
                {
                    continue;
                }

                var driveXboxGames = Path.Combine(drive.RootDirectory.FullName, "XboxGames");
                if (Directory.Exists(driveXboxGames))
                {
                    roots.Add(driveXboxGames);
                }

                var driveWindowsApps = Path.Combine(drive.RootDirectory.FullName, "WindowsApps");
                if (Directory.Exists(driveWindowsApps))
                {
                    roots.Add(driveWindowsApps);
                }
            }
        }
        catch
        {
            // Suppress drive enumeration errors
        }

        // 4. Registry lookup for GamingServices root dirs and package volumes
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(GamingServicesRegKey);
            if (key != null)
            {
                // Inspect values or subkeys
                var subKeys = key.GetSubKeyNames();
                foreach (var sub in subKeys)
                {
                    using var subKey = key.OpenSubKey(sub);
                    var pathVal = subKey?.GetValue("Path") as string ?? subKey?.GetValue("InstallLocation") as string;
                    if (!string.IsNullOrWhiteSpace(pathVal) && Directory.Exists(pathVal))
                    {
                        roots.Add(pathVal);
                    }
                }
            }
        }
        catch
        {
            // Suppress registry errors
        }

        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(PackageVolumesRegKey);
            if (key != null)
            {
                foreach (var sub in key.GetSubKeyNames())
                {
                    using var volKey = key.OpenSubKey(sub);
                    var volPath = volKey?.GetValue("Path") as string;
                    if (!string.IsNullOrWhiteSpace(volPath))
                    {
                        var gamesPath = Path.Combine(volPath, "XboxGames");
                        if (Directory.Exists(gamesPath))
                        {
                            roots.Add(gamesPath);
                        }

                        var appsPath = Path.Combine(volPath, "WindowsApps");
                        if (Directory.Exists(appsPath))
                        {
                            roots.Add(appsPath);
                        }
                    }
                }
            }
        }
        catch
        {
            // Suppress registry errors
        }

        return roots;
    }

    private GameInfo? InspectXboxGameDirectory(string gameDir)
    {
        try
        {
            var dirName = Path.GetFileName(gameDir);

            // Ignore system or non-game Appx packages
            if (dirName.StartsWith("Microsoft.NET.", StringComparison.OrdinalIgnoreCase) ||
                dirName.StartsWith("Microsoft.VCLibs", StringComparison.OrdinalIgnoreCase) ||
                dirName.StartsWith("Microsoft.UI.", StringComparison.OrdinalIgnoreCase) ||
                dirName.StartsWith("Deleted", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            string? title = null;
            string? id = null;
            string? version = null;
            string? publisher = null;
            string? launchExe = null;

            // Check for MicrosoftGame.config (Standard Xbox GDK configuration)
            var gdkConfig = Path.Combine(gameDir, "Content", "MicrosoftGame.config");
            if (!File.Exists(gdkConfig))
            {
                gdkConfig = Path.Combine(gameDir, "MicrosoftGame.config");
            }

            if (File.Exists(gdkConfig))
            {
                ParseMicrosoftGameConfig(gdkConfig, ref title, ref id, ref launchExe, ref version, ref publisher);
            }

            // Check for AppxManifest.xml
            var appxManifest = Path.Combine(gameDir, "AppxManifest.xml");
            if (File.Exists(appxManifest))
            {
                ParseAppxManifest(appxManifest, ref title, ref id, ref launchExe, ref version, ref publisher);
            }

            id ??= dirName;
            title ??= FormatTitleFromDirName(dirName);

            var target = GameIdentifier.IdentifyById(id) ??
                         GameIdentifier.IdentifyByTitle(title) ??
                         GameIdentifier.IdentifyByTitle(dirName);

            if (target.HasValue)
            {
                title = GameIdentifier.GetCanonicalTitle(target.Value);
                publisher = GameIdentifier.GetCanonicalPublisher(target.Value);
            }

            var graphicsApi = target.HasValue
                ? GameIdentifier.GetRenderingApi(id)
                : DetectGraphicsApi(gameDir);

            var executablePath = FindExecutable(gameDir, launchExe, target);

            var configFiles = FindConfigFiles(gameDir, id);
            var shaderFiles = FindShaderFiles(gameDir, id);

            return new GameInfo(
                Id: id,
                Title: title,
                ExecutablePath: executablePath,
                InstallPath: gameDir,
                Version: version,
                Publisher: publisher ?? "Xbox / Microsoft Store",
                Platform: GamePlatform.Xbox,
                GraphicsApi: graphicsApi,
                ConfigurationFiles: configFiles,
                ShaderFiles: shaderFiles,
                DetectedAt: DateTime.UtcNow);
        }
        catch
        {
            // Access denied or parsing error
            return null;
        }
    }

    private void ParseMicrosoftGameConfig(string configPath, ref string? title, ref string? id, ref string? launchExe, ref string? version, ref string? publisher)
    {
        try
        {
            var doc = XDocument.Load(configPath);
            var root = doc.Root;
            if (root == null) return;

            var identity = root.Element("Identity");
            if (identity != null)
            {
                id ??= identity.Attribute("Name")?.Value;
                version ??= identity.Attribute("Version")?.Value;
                publisher ??= identity.Attribute("Publisher")?.Value;
            }

            var exe = root.Descendants("Executable").FirstOrDefault();
            if (exe != null)
            {
                launchExe ??= exe.Attribute("Name")?.Value;
                title ??= exe.Attribute("DisplayName")?.Value;
            }
        }
        catch
        {
            // Ignore XML parse errors
        }
    }

    private void ParseAppxManifest(string manifestPath, ref string? title, ref string? id, ref string? launchExe, ref string? version, ref string? publisher)
    {
        try
        {
            var doc = XDocument.Load(manifestPath);
            var ns = doc.Root?.Name.Namespace ?? XNamespace.None;

            var identity = doc.Root?.Element(ns + "Identity");
            if (identity != null)
            {
                id ??= identity.Attribute("Name")?.Value;
                version ??= identity.Attribute("Version")?.Value;
                publisher ??= identity.Attribute("Publisher")?.Value;
            }

            var app = doc.Root?.Descendants(ns + "Application").FirstOrDefault();
            if (app != null)
            {
                launchExe ??= app.Attribute("Executable")?.Value;
                var visualElements = app.Descendants(ns + "VisualElements").FirstOrDefault();
                title ??= visualElements?.Attribute("DisplayName")?.Value;
            }
        }
        catch
        {
            // Ignore XML parse errors
        }
    }

    private string FindExecutable(string gameDir, string? manifestLaunchExe, TargetGame? target)
    {
        try
        {
            // 1. If explicit manifest launch executable specified
            if (!string.IsNullOrWhiteSpace(manifestLaunchExe))
            {
                var direct = Path.Combine(gameDir, manifestLaunchExe);
                if (File.Exists(direct))
                {
                    return direct;
                }

                var contentDir = Path.Combine(gameDir, "Content", manifestLaunchExe);
                if (File.Exists(contentDir))
                {
                    return contentDir;
                }
            }

            // 2. Known candidate executables for target games
            if (target.HasValue)
            {
                var candidates = GameIdentifier.GetCandidateExecutables(target.Value);
                foreach (var candidate in candidates)
                {
                    var fullCandidate = Path.IsPathRooted(candidate) ? candidate : Path.Combine(gameDir, candidate);
                    if (File.Exists(fullCandidate))
                    {
                        return fullCandidate;
                    }

                    var contentCandidate = Path.Combine(gameDir, "Content", candidate);
                    if (File.Exists(contentCandidate))
                    {
                        return contentCandidate;
                    }
                }
            }

            // 3. Fallback scan
            var exeFiles = Directory.GetFiles(gameDir, "*.exe", SearchOption.AllDirectories);
            var filtered = exeFiles
                .Where(f => !Path.GetFileName(f).StartsWith("unins", StringComparison.OrdinalIgnoreCase) &&
                            !Path.GetFileName(f).Contains("crash", StringComparison.OrdinalIgnoreCase) &&
                            !Path.GetFileName(f).Contains("gamelaunchhelper", StringComparison.OrdinalIgnoreCase) &&
                            !Path.GetFileName(f).Contains("redist", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (filtered.Count > 0)
            {
                return filtered.OrderByDescending(f =>
                {
                    try { return new FileInfo(f).Length; } catch { return 0L; }
                }).First();
            }
        }
        catch
        {
            // Suppress
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
                var contentFull = Path.Combine(installPath, "Content", rel);
                if (File.Exists(contentFull))
                {
                    result.Add(contentFull);
                }
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
                var contentFull = Path.Combine(installPath, "Content", rel);
                if (File.Exists(contentFull))
                {
                    result.Add(contentFull);
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

            if (files.Any(f => Path.GetFileName(f).Contains("d3d12", StringComparison.OrdinalIgnoreCase)))
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

    private static string FormatTitleFromDirName(string dirName)
    {
        // Strip package suffixes like _8wekyb3d8bbwe
        var underscoreIndex = dirName.IndexOf('_');
        if (underscoreIndex > 0)
        {
            var prefix = dirName[..underscoreIndex];
            var dotIndex = prefix.LastIndexOf('.');
            if (dotIndex >= 0 && dotIndex < prefix.Length - 1)
            {
                return prefix[(dotIndex + 1)..];
            }
            return prefix;
        }

        return dirName;
    }
}
