using RTFreedom.Core.Enums;
using RTFreedom.Core.Interfaces;
using RTFreedom.Core.Models;

namespace RTFreedom.Adapters;

/// <summary>
/// Adapter for Gears of War titles (Gears 5, Gears of War: E-Day).
/// Analyzes optional ray tracing features in Unreal Engine and provides raster fallback configuration patches.
/// </summary>
public class GearsOfWarAdapter : BaseGameAdapter
{
    /// <inheritdoc />
    public override string GameId => "gears-of-war";

    /// <inheritdoc />
    public override string GameTitle => "Gears of War Series";

    /// <inheritdoc />
    public override string[] SupportedVersions => ["*"];

    private const string Gears5SteamId = "1097840";
    private const string DefaultIniRelativePath = @"GearGame\Saved\Config\WindowsNoEditor\GameUserSettings.ini";

    /// <inheritdoc />
    public override bool SupportsGame(GameInfo game)
    {
        ArgumentNullException.ThrowIfNull(game);

        if (string.Equals(game.Id, GameId, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(game.Id, "gears-5", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (game.Title.Contains("Gears 5", StringComparison.OrdinalIgnoreCase) ||
            game.Title.Contains("Gears of War", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (game.ExecutablePath.Contains(Gears5SteamId, StringComparison.OrdinalIgnoreCase) ||
            game.InstallPath.Contains(Gears5SteamId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    /// <inheritdoc />
    public override Task<RayTracingAnalysis> AnalyzeAsync(GameInfo game, GpuCapabilities gpu, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(gpu);

        var isGears5 = game.Title.Contains("Gears 5", StringComparison.OrdinalIgnoreCase) ||
                       game.Id.Contains("gears-5", StringComparison.OrdinalIgnoreCase);

        if (isGears5)
        {
            var dependencies = new List<RTDependency>
            {
                new(
                    "Ray-Traced Reflections",
                    RTFeature.RayTracedReflections,
                    IsMandatory: false,
                    HasFallback: true,
                    "Optional screen-space reflection (SSR) fallback exists with zero visual breakage."),
                new(
                    "Ray-Traced Ambient Occlusion",
                    RTFeature.RayTracedAO,
                    IsMandatory: false,
                    HasFallback: true,
                    "Optional DXR ambient occlusion with standard SSAO rasterization fallback.")
            };

            var analysis = new RayTracingAnalysis(
                GameId: GameId,
                Dependencies: dependencies,
                HasNonRTFallback: true,
                RequiresHardwareRT: false,
                FeasibilityAssessment: "Gears 5 utilizes Unreal Engine 4 with full rasterization fallback pipelines. Ray tracing effects (reflections and ambient occlusion) are entirely optional and can be cleanly disabled via GameUserSettings.ini or engine CVars with excellent performance on any modern graphics card.",
                DetectedDxrCalls: ["D3D12CreateRaytracingFallbackDevice", "CreateStateObject"],
                DetectedShaderFeatures: ["RayQuery", "Intersection"],
                RecommendedLevel: CompatibilityLevel.FullyCompatible,
                AnalyzedAt: DateTime.UtcNow);

            return Task.FromResult(analysis);
        }

        // Unknown Gears title (e.g. Gears of War: E-Day or upcoming release)
        var unknownAnalysis = new RayTracingAnalysis(
            GameId: GameId,
            Dependencies: [],
            HasNonRTFallback: false,
            RequiresHardwareRT: false,
            FeasibilityAssessment: "Compatibility for this Gears of War release has not been fully profiled yet. Title marked as Unknown until engine architecture and fallback render paths are analyzed.",
            DetectedDxrCalls: [],
            DetectedShaderFeatures: [],
            RecommendedLevel: CompatibilityLevel.Unknown,
            AnalyzedAt: DateTime.UtcNow);

        return Task.FromResult(unknownAnalysis);
    }

    /// <inheritdoc />
    public override Task<IReadOnlyList<PatchAction>> GetAvailablePatchesAsync(GameInfo game, RayTracingAnalysis analysis)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(analysis);

        var patches = new List<PatchAction>();

        var isGears5 = game.Title.Contains("Gears 5", StringComparison.OrdinalIgnoreCase) ||
                       game.Id.Contains("gears-5", StringComparison.OrdinalIgnoreCase) ||
                       analysis.RecommendedLevel == CompatibilityLevel.FullyCompatible;

        if (isGears5)
        {
            var targetFile = ResolveConfigFile(game);
            patches.Add(new PatchAction(
                Id: "gears5-disable-rt",
                Description: "Disable optional DirectX Raytracing reflections and ambient occlusion, forcing full rasterization fallback.",
                Type: PatchType.ConfigModification,
                TargetFile: targetFile,
                BackupFile: null,
                IsReversible: true,
                AppliedAt: DateTime.MinValue));
        }

        return Task.FromResult<IReadOnlyList<PatchAction>>(patches.AsReadOnly());
    }

    /// <inheritdoc />
    public override async Task<bool> ApplyPatchAsync(GameInfo game, PatchAction patch, IBackupManager backup, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(patch);
        ArgumentNullException.ThrowIfNull(backup);

        var targetConfigFile = patch.TargetFile;

        // Backup existing configuration if it exists
        if (File.Exists(targetConfigFile))
        {
            await backup.CreateBackupAsync(GameId, [targetConfigFile], ct);
        }

        var rtSettings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["r.RayTracing.Reflections"] = "0",
            ["r.RayTracing.AmbientOcclusion"] = "0",
            ["r.RayTracing.Shadows"] = "0",
            ["bRayTracing"] = "False"
        };

        WriteConfigFile(targetConfigFile, rtSettings);
        return true;
    }

    /// <inheritdoc />
    public override Task<bool> ValidatePatchAsync(GameInfo game, PatchAction patch)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(patch);

        if (!File.Exists(patch.TargetFile))
        {
            return Task.FromResult(false);
        }

        var config = ReadConfigFile(patch.TargetFile);
        var isValid = config.TryGetValue("r.RayTracing.Reflections", out var refl) && refl == "0" &&
                      config.TryGetValue("bRayTracing", out var brt) && brt.Equals("False", StringComparison.OrdinalIgnoreCase);

        return Task.FromResult(isValid);
    }

    private string ResolveConfigFile(GameInfo game)
    {
        if (game.ConfigurationFiles.Count > 0 && File.Exists(game.ConfigurationFiles[0]))
        {
            return game.ConfigurationFiles[0];
        }

        // Check local AppData GameUserSettings or game install directory
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appDataIni = Path.Combine(localAppData, "Gears5", "Saved", "Config", "WindowsNoEditor", "GameUserSettings.ini");
        if (File.Exists(appDataIni))
        {
            return appDataIni;
        }

        return Path.Combine(game.InstallPath, DefaultIniRelativePath);
    }
}
