using System.Text.RegularExpressions;
using RTFreedom.Core.Enums;
using RTFreedom.Core.Models;

namespace RTFreedom.Graphics;

/// <summary>
/// Evaluation result produced by <see cref="RenderingPathAnalyzer"/>.
/// </summary>
/// <param name="HasNonRTFallback">Whether a viable rasterization fallback path exists in the game.</param>
/// <param name="RequiresHardwareRT">Whether the game strictly mandates hardware ray tracing at engine launch.</param>
/// <param name="RecommendedLevel">The suggested compatibility level for bypass actions.</param>
/// <param name="FeasibilityAssessment">Narrative analysis of bypass feasibility.</param>
/// <param name="Dependencies">Discovered RT dependencies and fallback availability.</param>
public record RenderingPathAssessment(
    bool HasNonRTFallback,
    bool RequiresHardwareRT,
    CompatibilityLevel RecommendedLevel,
    string FeasibilityAssessment,
    List<RTDependency> Dependencies);

/// <summary>
/// Evaluates game rendering pipelines, configuration settings, and shader availability to determine
/// whether non-ray-traced fallback paths exist and are playable.
/// </summary>
public class RenderingPathAnalyzer
{
    private static readonly Regex RtConfigKeyRegex = new(
        @"(r\.RayTracing|r_raytracing|rt_enabled|bEnableRayTracing|RayTracingEnable|RayTracingReflections|RayTracingAO|RayTracingGI|dxr_enabled|rtx_on)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex RasterFallbackConfigRegex = new(
        @"(r\.SSR\.Quality|r_ssao|ScreenSpaceReflections|AmbientOcclusion|CascadeShadowMaps|RasterShadows|LumenScreenTraces)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Analyzes the game's configuration files, binaries, and shaders to determine fallback feasibility.
    /// </summary>
    /// <param name="game">Detected game metadata.</param>
    /// <param name="scanResult">Results of DXR / Vulkan binary scanning.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A comprehensive <see cref="RenderingPathAssessment"/>.</returns>
    public async Task<RenderingPathAssessment> AnalyzeRenderingPathsAsync(
        GameInfo game,
        DxrScanResult scanResult,
        CancellationToken ct = default)
    {
        var dependencies = new List<RTDependency>();
        var (configHasRtToggles, configHasRasterFallbacks) = await AnalyzeConfigFilesAsync(game.ConfigurationFiles, ct).ConfigureAwait(false);

        // Check for specific known game titles with hardcoded engine facts
        var knownAssessment = CheckKnownGames(game);
        if (knownAssessment != null)
        {
            return knownAssessment;
        }

        bool hasDxrCalls = scanResult.DetectedDxrCalls.Count > 0;
        bool hasRtShaders = scanResult.DetectedShaderFeatures.Count > 0;

        // If no RT calls or shaders were detected anywhere, game is raster-only
        if (!hasDxrCalls && !hasRtShaders && !configHasRtToggles)
        {
            return new RenderingPathAssessment(
                HasNonRTFallback: true,
                RequiresHardwareRT: false,
                RecommendedLevel: CompatibilityLevel.FullyCompatible,
                FeasibilityAssessment: "No mandatory ray-tracing dependencies detected in game binaries or shaders. Standard rasterization pipeline is active.",
                Dependencies: []
            );
        }

        // Identify specific dependencies
        bool hasReflections = scanResult.DetectedShaderFeatures.Any(f => f.Contains("closesthit", StringComparison.OrdinalIgnoreCase) || f.Contains("raygen", StringComparison.OrdinalIgnoreCase));
        bool hasAccelerationStruct = scanResult.DetectedDxrCalls.Any(c => c.Contains("AccelerationStructure", StringComparison.OrdinalIgnoreCase));
        bool hasInlineRayQuery = scanResult.DetectedDxrCalls.Any(c => c.Contains("ray_query", StringComparison.OrdinalIgnoreCase)) ||
                                 scanResult.DetectedShaderFeatures.Any(f => f.Contains("RayQuery", StringComparison.OrdinalIgnoreCase));

        if (hasReflections || configHasRtToggles)
        {
            dependencies.Add(new RTDependency(
                Feature: "Ray-Traced Reflections",
                Type: RTFeature.RayTracedReflections,
                IsMandatory: !configHasRasterFallbacks && !configHasRtToggles,
                HasFallback: configHasRasterFallbacks || configHasRtToggles,
                Description: "Screenspace reflections or planar reflections fallback can replace RT reflection passes."
            ));
        }

        if (hasAccelerationStruct)
        {
            dependencies.Add(new RTDependency(
                Feature: "Ray Tracing Acceleration Structures",
                Type: RTFeature.AccelerationStructures,
                IsMandatory: !configHasRtToggles,
                HasFallback: configHasRtToggles,
                Description: "BLAS/TLAS construction required for hardware RT passes. Disabling RT options avoids BVH building overhead."
            ));
        }

        if (hasInlineRayQuery)
        {
            dependencies.Add(new RTDependency(
                Feature: "Inline Ray Tracing / Ray Queries",
                Type: RTFeature.RayQuery,
                IsMandatory: false,
                HasFallback: true,
                Description: "Used in compute/fragment shaders for ambient occlusion or contact shadows."
            ));
        }

        // Evaluate fallback availability
        bool hasNonRtFallback = configHasRasterFallbacks || configHasRtToggles || (!hasRtShaders && hasDxrCalls);
        bool requiresHardwareRt = !hasNonRtFallback && hasDxrCalls && hasRtShaders;

        CompatibilityLevel recommendedLevel;
        string feasibilityNarrative;

        if (hasNonRtFallback && configHasRtToggles)
        {
            recommendedLevel = CompatibilityLevel.FullyCompatible;
            feasibilityNarrative = "Game contains configurable RT toggles and existing rasterization shader paths. RT can be bypassed cleanly via configuration tweaks or DXR stubbing.";
        }
        else if (hasNonRtFallback)
        {
            recommendedLevel = CompatibilityLevel.PartiallyCompatible;
            feasibilityNarrative = "Game contains fallback rendering paths, but some graphical effects (lighting or reflections) may require proxy DLL redirection or shader replacement to eliminate RT dependencies.";
        }
        else if (requiresHardwareRt)
        {
            recommendedLevel = CompatibilityLevel.Incompatible;
            feasibilityNarrative = "The game's rendering pipeline relies exclusively on DXR acceleration structures without raster fallback assets. Ray tracing cannot be safely bypassed without rendering failure.";
        }
        else
        {
            recommendedLevel = CompatibilityLevel.ExperimentalOnly;
            feasibilityNarrative = "Rasterization fallback exists partially. Experimental bypass may produce minor graphical artifacts or missing reflections.";
        }

        return new RenderingPathAssessment(
            HasNonRTFallback: hasNonRtFallback,
            RequiresHardwareRT: requiresHardwareRt,
            RecommendedLevel: recommendedLevel,
            FeasibilityAssessment: feasibilityNarrative,
            Dependencies: dependencies
        );
    }

    private static async Task<(bool HasRtToggles, bool HasRasterFallbacks)> AnalyzeConfigFilesAsync(
        IEnumerable<string> configFiles,
        CancellationToken ct)
    {
        bool hasRtToggles = false;
        bool hasRasterFallbacks = false;

        foreach (var configFile in configFiles)
        {
            if (!File.Exists(configFile)) continue;

            try
            {
                string text = await File.ReadAllTextAsync(configFile, ct).ConfigureAwait(false);
                if (RtConfigKeyRegex.IsMatch(text))
                {
                    hasRtToggles = true;
                }
                if (RasterFallbackConfigRegex.IsMatch(text))
                {
                    hasRasterFallbacks = true;
                }
            }
            catch
            {
                // Graceful fallback for locked or unreadable files
            }
        }

        return (hasRtToggles, hasRasterFallbacks);
    }

    private static RenderingPathAssessment? CheckKnownGames(GameInfo game)
    {
        string title = game.Title.ToLowerInvariant();
        string exec = Path.GetFileName(game.ExecutablePath).ToLowerInvariant();

        // Metro Exodus Enhanced Edition: Pure RT game, absolutely no raster fallback
        if (title.Contains("metro exodus enhanced") || exec.Contains("metroexodus_enhanced"))
        {
            return new RenderingPathAssessment(
                HasNonRTFallback: false,
                RequiresHardwareRT: true,
                RecommendedLevel: CompatibilityLevel.Incompatible,
                FeasibilityAssessment: "Metro Exodus Enhanced Edition is a pure ray-traced engine with all rasterized lighting completely removed. Hardware DXR/Vulkan RT is strictly mandatory and cannot be bypassed.",
                Dependencies:
                [
                    new RTDependency("Global Illumination", RTFeature.RayTracedGI, true, false, "Mandatory pure RT GI pipeline."),
                    new RTDependency("Reflections", RTFeature.RayTracedReflections, true, false, "Mandatory pure RT reflections."),
                    new RTDependency("Acceleration Structures", RTFeature.AccelerationStructures, true, false, "Engine asserts BLAS/TLAS creation on startup.")
                ]
            );
        }

        // Cyberpunk 2077: Full raster fallback available
        if (title.Contains("cyberpunk 2077") || exec.Contains("cyberpunk2077"))
        {
            return new RenderingPathAssessment(
                HasNonRTFallback: true,
                RequiresHardwareRT: false,
                RecommendedLevel: CompatibilityLevel.FullyCompatible,
                FeasibilityAssessment: "Cyberpunk 2077 provides a complete rasterized fallback rendering path. RT and Path Tracing features can be safely disabled through engine configuration.",
                Dependencies:
                [
                    new RTDependency("Ray-Traced Reflections", RTFeature.RayTracedReflections, false, true, "Screen-space reflections fallback available."),
                    new RTDependency("Ray-Traced Lighting", RTFeature.RayTracedGI, false, true, "Baked and dynamic raster lighting fallback available."),
                    new RTDependency("Path Tracing", RTFeature.PathTracing, false, true, "Overdrive path tracing is entirely optional.")
                ]
            );
        }

        // Doom Eternal: Has full raster fallback
        if (title.Contains("doom eternal") || exec.Contains("doometernal"))
        {
            return new RenderingPathAssessment(
                HasNonRTFallback: true,
                RequiresHardwareRT: false,
                RecommendedLevel: CompatibilityLevel.FullyCompatible,
                FeasibilityAssessment: "Doom Eternal's id Tech 7 engine has full rasterization fallbacks. Ray tracing is an optional layer.",
                Dependencies:
                [
                    new RTDependency("Ray-Traced Reflections", RTFeature.RayTracedReflections, false, true, "Standard raster reflections available.")
                ]
            );
        }

        // Alan Wake 2: Requires Mesh Shaders, but RT is optional raster fallback
        if (title.Contains("alan wake 2") || exec.Contains("alanwake2"))
        {
            return new RenderingPathAssessment(
                HasNonRTFallback: true,
                RequiresHardwareRT: false,
                RecommendedLevel: CompatibilityLevel.PartiallyCompatible,
                FeasibilityAssessment: "Alan Wake 2 has rasterization lighting fallbacks (SDFGI and SSR). Ray tracing is toggleable, though Mesh Shaders remain required by the GPU.",
                Dependencies:
                [
                    new RTDependency("Ray-Traced Transparency & Reflections", RTFeature.RayTracedReflections, false, true, "SSR fallback exists."),
                    new RTDependency("Ray-Traced Direct Lighting", RTFeature.RayTracedGI, false, true, "Software SDFGI and raster shadows exist.")
                ]
            );
        }

        // Avatar: Frontiers of Pandora
        if (title.Contains("avatar: frontiers of pandora") || exec.Contains("afop"))
        {
            return new RenderingPathAssessment(
                HasNonRTFallback: true,
                RequiresHardwareRT: false,
                RecommendedLevel: CompatibilityLevel.PartiallyCompatible,
                FeasibilityAssessment: "The Snowdrop engine in Avatar uses a hybrid software BVH and hardware DXR fallback. Disabling hardware RT redirects queries to software BVH.",
                Dependencies:
                [
                    new RTDependency("BVH Traversal", RTFeature.AccelerationStructures, true, true, "Snowdrop software BVH fallback active."),
                    new RTDependency("Diffuse GI", RTFeature.RayTracedGI, true, true, "Software ray tracing fallback available.")
                ]
            );
        }

        return null;
    }
}
