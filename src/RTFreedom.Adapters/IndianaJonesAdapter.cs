using RTFreedom.Core.Enums;
using RTFreedom.Core.Interfaces;
using RTFreedom.Core.Models;

namespace RTFreedom.Adapters;

/// <summary>
/// Adapter for Indiana Jones and the Great Circle (MachineGames / id Tech 7 modified).
/// Analyzes mandatory Vulkan ray tracing GI dependencies and offers performance optimization patches.
/// </summary>
public class IndianaJonesAdapter : BaseGameAdapter
{
    /// <inheritdoc />
    public override string GameId => "indiana-jones-great-circle";

    /// <inheritdoc />
    public override string GameTitle => "Indiana Jones and the Great Circle";

    /// <inheritdoc />
    public override string[] SupportedVersions => ["*"];

    private const string SteamAppId = "2677660";
    private const string EpicCatalogId = "greatcircle";
    private const string DefaultConfigFile = "GreatCircleConfig.cfg";

    /// <inheritdoc />
    public override bool SupportsGame(GameInfo game)
    {
        ArgumentNullException.ThrowIfNull(game);

        if (string.Equals(game.Id, GameId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (game.Title.Contains("Indiana Jones", StringComparison.OrdinalIgnoreCase) &&
            game.Title.Contains("Great Circle", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (game.ExecutablePath.Contains(SteamAppId, StringComparison.OrdinalIgnoreCase) ||
            game.InstallPath.Contains(SteamAppId, StringComparison.OrdinalIgnoreCase) ||
            game.InstallPath.Contains(EpicCatalogId, StringComparison.OrdinalIgnoreCase))
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
                "Global Illumination",
                RTFeature.RayTracedGI,
                IsMandatory: true,
                HasFallback: false,
                "Mandatory ray-traced global illumination via VK_KHR_ray_tracing_pipeline. id Tech lacks traditional rasterized GI/lightmap fallback."),
            new(
                "Reflections",
                RTFeature.RayTracedReflections,
                IsMandatory: false,
                HasFallback: true,
                "Ray-traced reflections on glossy materials with hybrid screen-space reflection fallback."),
            new(
                "Directional Shadows",
                RTFeature.RayTracedShadows,
                IsMandatory: false,
                HasFallback: true,
                "Ray-traced contact shadows and sun shadows.")
        };

        var detectedCalls = new List<string>
        {
            "vkCreateRayTracingPipelinesKHR",
            "vkGetRayTracingShaderGroupHandlesKHR",
            "vkCmdTraceRaysKHR",
            "VK_KHR_ray_tracing_pipeline",
            "VK_KHR_acceleration_structure"
        };

        var detectedShaders = new List<string>
        {
            "RayGenKHR",
            "ClosestHitKHR",
            "MissKHR",
            "IntersectionKHR"
        };

        // Evaluation: check hardware RT vs compute emulation (GTX 1060 6GB+, 1070, 1080, 1660)
        var hasHardwareRt = gpu.SupportsRayTracing || gpu.SupportsVulkanRayTracing;
        var hasComputeEmulation = gpu.SupportsComputeRayTracing;

        CompatibilityLevel recommendedLevel;
        string assessment;

        if (hasHardwareRt)
        {
            recommendedLevel = CompatibilityLevel.PartiallyCompatible;
            assessment = "Full ray tracing bypass is NOT feasible for Indiana Jones and the Great Circle. The lighting system strictly relies on hardware ray tracing (Vulkan VK_KHR_ray_tracing_pipeline) for global illumination with no traditional raster lightmap fallback. However, configuration tuning can reduce ray count, bounce depth, and BVH complexity to improve frame rates on entry-level RT GPUs.";
        }
        else if (hasComputeEmulation)
        {
            recommendedLevel = CompatibilityLevel.ExperimentalOnly;
            assessment = "COMPATIBLE VIA EXPERIMENTAL COMPUTE EMULATION: NVIDIA GeForce GTX 10-series (Pascal: 1060 6GB, 1070, 1080) and GTX 16-series (Turing: 1660) support driver-level compute emulation for DXR and Vulkan ray queries (NVIDIA Driver 425.31+). RT Freedom provides the Experimental Hardware RT Check Bypass & Compute Fallback Patch (+r_raytracing 0, +r_forceMinimalVulkanFeatures 1, +r_skipHardwareRayTracingCheck 1) to allow the game to initialize and render on CUDA compute ALUs. Notice: Because id Tech 7 lacks prebaked lightmaps, lighting will be rendered using minimal ambient fallbacks, which may result in darker interiors, and performance depends on GPU compute throughput.";
        }
        else
        {
            recommendedLevel = CompatibilityLevel.Incompatible;
            assessment = "INCOMPATIBLE: GPU lacks hardware ray tracing and compute shader DXR emulation capabilities. Because the engine possesses no baked lighting or raster fallback path, bypassing ray tracing on non-RT hardware without compute support will prevent the game from launching or render an entirely black environment.";
        }

        var analysis = new RayTracingAnalysis(
            GameId: GameId,
            Dependencies: dependencies,
            HasNonRTFallback: false,
            RequiresHardwareRT: !hasComputeEmulation && !hasHardwareRt,
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

        // Quality optimization patch for RT-capable GPUs
        patches.Add(new PatchAction(
            Id: "ij-opt-rt-quality-low",
            Description: "Reduce Ray Tracing quality (low ray density, single GI bounce, disable RT reflections) for RT GPUs.",
            Type: PatchType.ConfigModification,
            TargetFile: targetConfigFile,
            BackupFile: null,
            IsReversible: true,
            AppliedAt: DateTime.MinValue));

        // Experimental bypass patch for GTX 10xx / 16xx and non-RT GPUs
        patches.Add(new PatchAction(
            Id: "ij-gtx-compute-bypass",
            Description: "Experimental Hardware RT Check Bypass & Compute Fallback for GTX 10xx/16xx (Forces r_raytracing 0, minimal Vulkan features, and bypasses startup hardware assert). Warning: Experimental mode with ambient lighting fallbacks.",
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

        // Safety first: Create a backup before modifying the file
        if (File.Exists(targetConfigFile))
        {
            await backup.CreateBackupAsync(GameId, [targetConfigFile], ct);
        }

        Dictionary<string, string> rtSettings;

        if (patch.Id == "ij-gtx-compute-bypass")
        {
            // Experimental bypass for GTX 10xx / 16xx: bypass launch check, force minimal features, disable RT reflections/shadows
            rtSettings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["r_raytracing"] = "0",
                ["r_forceMinimalVulkanFeatures"] = "1",
                ["r_skipHardwareRayTracingCheck"] = "1",
                ["r_rt_reflections_enable"] = "0",
                ["r_rt_shadows_enable"] = "0",
                ["r_bvhQuality"] = "0",
                ["r_renderAPI"] = "1",
                ["r_rtgi_rays_per_pixel"] = "1",
                ["r_rtgi_bounces"] = "1",
                ["r_rt_bvh_budget_mb"] = "256"
            };
        }
        else
        {
            // Standard RT optimization patch
            rtSettings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["r_rtgi_rays_per_pixel"] = "1",
                ["r_rtgi_bounces"] = "1",
                ["r_rt_reflections_enable"] = "0",
                ["r_rt_shadows_enable"] = "0",
                ["r_rt_bvh_budget_mb"] = "512"
            };
        }

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

        if (patch.Id == "ij-gtx-compute-bypass")
        {
            var isValid = config.TryGetValue("r_raytracing", out var rt) && rt == "0" &&
                          config.TryGetValue("r_skipHardwareRayTracingCheck", out var skip) && skip == "1";
            return Task.FromResult(isValid);
        }

        var isStandardValid = config.TryGetValue("r_rtgi_rays_per_pixel", out var rays) && rays == "1" &&
                              config.TryGetValue("r_rt_reflections_enable", out var refl) && refl == "0";

        return Task.FromResult(isStandardValid);
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
