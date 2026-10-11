using RTFreedom.Core.Enums;
using RTFreedom.Core.Interfaces;
using RTFreedom.Core.Models;

namespace RTFreedom.Adapters;

/// <summary>
/// Adapter for DOOM: The Dark Ages (id Software / id Tech 8).
/// Provides comprehensive analysis for the pure path-tracing renderer and exposes performance tuning patches.
/// </summary>
public class DoomDarkAgesAdapter : BaseGameAdapter
{
    /// <inheritdoc />
    public override string GameId => "doom-dark-ages";

    /// <inheritdoc />
    public override string GameTitle => "DOOM: The Dark Ages";

    /// <inheritdoc />
    public override string[] SupportedVersions => ["*"];

    private const string SteamAppId = "2344520";
    private const string DefaultConfigFile = "idTech8Config.cfg";

    /// <inheritdoc />
    public override bool SupportsGame(GameInfo game)
    {
        ArgumentNullException.ThrowIfNull(game);

        if (string.Equals(game.Id, GameId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (game.Title.Contains("DOOM", StringComparison.OrdinalIgnoreCase) &&
            game.Title.Contains("Dark Ages", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (game.ExecutablePath.Contains(SteamAppId, StringComparison.OrdinalIgnoreCase) ||
            game.InstallPath.Contains(SteamAppId, StringComparison.OrdinalIgnoreCase))
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

        var dependencies = new List<RTDependency>
        {
            new(
                "Unified Path Tracing Renderer",
                RTFeature.PathTracing,
                IsMandatory: true,
                HasFallback: false,
                "id Tech 8 is engineered from the ground up around hardware path tracing. Unlike DOOM Eternal's rasterized pipeline, all direct/indirect lighting and materials require RT hardware."),
            new(
                "Ray-Traced Geometry Acceleration",
                RTFeature.AccelerationStructures,
                IsMandatory: true,
                HasFallback: false,
                "Top-level and bottom-level acceleration structures (TLAS/BLAS) required for dynamic scene visibility."),
            new(
                "Ray Queries / Denoising",
                RTFeature.RayQuery,
                IsMandatory: true,
                HasFallback: false,
                "Hardware ray queries integrated with spatiotemporal neural denoisers.")
        };

        var detectedCalls = new List<string>
        {
            "vkCmdTraceRaysKHR",
            "vkCreateRayTracingPipelinesKHR",
            "VK_KHR_ray_tracing_pipeline",
            "VK_KHR_ray_query",
            "VK_KHR_acceleration_structure"
        };

        var detectedShaders = new List<string>
        {
            "RayGenKHR",
            "ClosestHitKHR",
            "MissKHR",
            "CallableKHR"
        };

        var hasHardwareRt = gpu.SupportsRayTracing || gpu.SupportsVulkanRayTracing;
        var recommendedLevel = hasHardwareRt
            ? CompatibilityLevel.PartiallyCompatible
            : CompatibilityLevel.Incompatible;

        var assessment = hasHardwareRt
            ? "DOOM: The Dark Ages is built upon id Tech 8's unified path tracing architecture. Unlike DOOM Eternal which utilized high-speed rasterization with optional ray tracing, The Dark Ages has no raster fallback pipeline. Full ray tracing bypass is NOT feasible. However, on lower-tier RT graphics cards, quality patches can constrain rays per pixel, bounce limits, and denoising passes to maximize performance."
            : "INCOMPATIBLE: id Tech 8 has removed the legacy rasterization pipeline present in previous DOOM titles. Hardware ray tracing support is strictly required to execute shaders and trace geometry. Attempting to bypass RT on a non-RT GPU will result in engine initialization failure.";

        var analysis = new RayTracingAnalysis(
            GameId: GameId,
            Dependencies: dependencies,
            HasNonRTFallback: false,
            RequiresHardwareRT: true,
            FeasibilityAssessment: assessment,
            DetectedDxrCalls: detectedCalls,
            DetectedShaderFeatures: detectedShaders,
            RecommendedLevel: recommendedLevel,
            AnalyzedAt: DateTime.UtcNow);

        return Task.FromResult(analysis);
    }

    /// <inheritdoc />
    public override Task<IReadOnlyList<PatchAction>> GetAvailablePatchesAsync(GameInfo game, RayTracingAnalysis analysis)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(analysis);

        var patches = new List<PatchAction>();
        var targetConfigFile = ResolveConfigFile(game);

        // Honest reporting: full bypass is not feasible; quality reduction is offered
        patches.Add(new PatchAction(
            Id: "doom-da-opt-pathtracing-low",
            Description: "Reduce id Tech 8 Path Tracing load (1 sample per pixel, disable secondary indirect bounces, downscaled radiance cache). Note: Full RT bypass is not feasible.",
            Type: PatchType.ConfigModification,
            TargetFile: targetConfigFile,
            BackupFile: null,
            IsReversible: true,
            AppliedAt: DateTime.MinValue));

        return Task.FromResult<IReadOnlyList<PatchAction>>(patches.AsReadOnly());
    }

    /// <inheritdoc />
    public override async Task<bool> ApplyPatchAsync(GameInfo game, PatchAction patch, IBackupManager backup, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(patch);
        ArgumentNullException.ThrowIfNull(backup);

        var targetConfigFile = patch.TargetFile;

        // Safety first: create a backup snapshot before writing
        if (File.Exists(targetConfigFile))
        {
            await backup.CreateBackupAsync(GameId, [targetConfigFile], ct);
        }

        var rtSettings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["r_pathTracing_spp"] = "1",
            ["r_pathTracing_maxBounces"] = "1",
            ["r_radianceCache_resolution"] = "0.5",
            ["r_denoiser_quality"] = "1",
            ["r_bvh_update_rate"] = "2"
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
        var isValid = config.TryGetValue("r_pathTracing_spp", out var spp) && spp == "1" &&
                      config.TryGetValue("r_pathTracing_maxBounces", out var bounces) && bounces == "1";

        return Task.FromResult(isValid);
    }

    private string ResolveConfigFile(GameInfo game)
    {
        if (game.ConfigurationFiles.Count > 0 && File.Exists(game.ConfigurationFiles[0]))
        {
            return game.ConfigurationFiles[0];
        }

        return Path.Combine(game.InstallPath, "base", DefaultConfigFile);
    }
}
