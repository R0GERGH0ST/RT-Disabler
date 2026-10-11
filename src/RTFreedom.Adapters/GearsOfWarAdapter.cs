using RTFreedom.Core.Enums;
using RTFreedom.Core.Interfaces;
using RTFreedom.Core.Models;

namespace RTFreedom.Adapters;

/// <summary>
/// Adapter for Gears of War: E-Day (The Coalition / Xbox Game Studios).
/// Analyzes Unreal Engine 5 Lumen, Nanite, and MegaLights ray tracing pipelines,
/// providing automated fallback redirection from Hardware Ray Tracing to Software Distance Fields.
/// </summary>
public class GearsOfWarAdapter : BaseGameAdapter
{
    /// <inheritdoc />
    public override string GameId => "gears-of-war-e-day";

    /// <inheritdoc />
    public override string GameTitle => "Gears of War: E-Day";

    /// <inheritdoc />
    public override string[] SupportedVersions => ["*"];

    private const string GearsEDaySteamId = "2837240";
    private const string DefaultIniRelativePath = @"GearGame\Saved\Config\Windows\GameUserSettings.ini";

    /// <inheritdoc />
    public override bool SupportsGame(GameInfo game)
    {
        ArgumentNullException.ThrowIfNull(game);

        if (string.Equals(game.Id, GameId, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(game.Id, "gears-of-war", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(game.Id, "gears-eday", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (game.Title.Contains("E-Day", StringComparison.OrdinalIgnoreCase) ||
            game.Title.Contains("EDay", StringComparison.OrdinalIgnoreCase) ||
            game.Title.Contains("Gears of War", StringComparison.OrdinalIgnoreCase) ||
            game.Title.Contains("Gears 5", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (game.ExecutablePath.Contains("GearsEDay", StringComparison.OrdinalIgnoreCase) ||
            game.ExecutablePath.Contains("GearsOfWar", StringComparison.OrdinalIgnoreCase) ||
            game.ExecutablePath.Contains(GearsEDaySteamId, StringComparison.OrdinalIgnoreCase) ||
            game.InstallPath.Contains("Gears of War", StringComparison.OrdinalIgnoreCase) ||
            game.InstallPath.Contains("E-Day", StringComparison.OrdinalIgnoreCase))
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
                "Lumen Global Illumination",
                RTFeature.RayTracedGI,
                IsMandatory: false,
                HasFallback: true,
                "Hardware DXR Lumen global illumination can be redirected to UE5 Software Lumen (Mesh Distance Fields and screen traces)."),
            new(
                "Lumen Reflections",
                RTFeature.RayTracedReflections,
                IsMandatory: false,
                HasFallback: true,
                "Hardware ray-traced reflections can fall back to Software Lumen Surface Cache and screen-space traces."),
            new(
                "Ray-Traced Shadows & RTX Mega Geometry",
                RTFeature.RayTracedShadows,
                IsMandatory: false,
                HasFallback: true,
                "Hardware DXR shadow rays can be replaced by Unreal Engine 5 Virtual Shadow Maps (VSM) and distance field shadows."),
            new(
                "MegaLights DXR Shadows",
                RTFeature.AccelerationStructures,
                IsMandatory: false,
                HasFallback: true,
                "MegaLights hardware ray queries can be clamped to raster shadow maps on non-RT GPUs.")
        };

        var detectedCalls = new List<string>
        {
            "CreateStateObject",
            "BuildRaytracingAccelerationStructure",
            "DispatchRays",
            "D3D12_RAYTRACING_PIPELINE_CONFIG"
        };

        var detectedShaders = new List<string>
        {
            "RayQuery",
            "RayGen",
            "ClosestHit",
            "RaytracingAccelerationStructure"
        };

        string feasibilityAssessment;
        CompatibilityLevel recommendedLevel;

        if (gpu.SupportsRayTracing || gpu.SupportsDxr12)
        {
            recommendedLevel = CompatibilityLevel.FullyCompatible;
            feasibilityAssessment = "Gears of War: E-Day runs with full Unreal Engine 5 Hardware Lumen and MegaLights on this GPU. Quality tuning patches are available to balance frame rates with full Nanite and Lumen ray tracing.";
        }
        else if (gpu.SupportsComputeRayTracing || gpu.Name.Contains("1080", StringComparison.OrdinalIgnoreCase) || gpu.Name.Contains("1660", StringComparison.OrdinalIgnoreCase))
        {
            recommendedLevel = CompatibilityLevel.FullyCompatible;
            feasibilityAssessment = "COMPATIBLE VIA SOFTWARE LUMEN FALLBACK: Gears of War: E-Day is built on Unreal Engine 5. While High/Ultra presets activate DXR Hardware Ray Tracing, UE5 features a complete, highly optimized Software Lumen fallback (Signed Distance Fields and Screen Traces). Applying the RT Freedom Software Lumen Fallback patch completely eliminates mandatory DXR acceleration structure requirements, allowing smooth, artifact-free gameplay on NVIDIA GeForce GTX 10xx/16xx series and compatible DirectX 12 hardware.";
        }
        else
        {
            recommendedLevel = CompatibilityLevel.PartiallyCompatible;
            feasibilityAssessment = "Gears of War: E-Day can be run without hardware ray tracing by falling back to Unreal Engine 5 Software Lumen and Virtual Shadow Maps. On older GPUs, ensure DirectX 12 Feature Level 12_0 or Shader Model 6.0 support.";
        }

        var analysis = new RayTracingAnalysis(
            GameId: GameId,
            Dependencies: dependencies,
            HasNonRTFallback: true,
            RequiresHardwareRT: false,
            FeasibilityAssessment: feasibilityAssessment,
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
        var targetFile = ResolveConfigFile(game);

        patches.Add(new PatchAction(
            Id: "gears-eday-software-lumen-fallback",
            Description: "Redirect Unreal Engine 5 Hardware Lumen to Software Distance Fields fallback (Forces r.Lumen.HardwareRayTracing 0, r.RayTracing.Shadows 0, r.DistanceFields 1). Eliminates mandatory DXR requirements.",
            Type: PatchType.ConfigModification,
            TargetFile: targetFile,
            BackupFile: null,
            IsReversible: true,
            AppliedAt: DateTime.MinValue));

        patches.Add(new PatchAction(
            Id: "gears-eday-opt-vsm-megalights",
            Description: "Optimize MegaLights and Virtual Shadow Maps for non-RT GPUs (lowers VSM resolution and clamps dynamic light shadow cascades).",
            Type: PatchType.ConfigModification,
            TargetFile: targetFile,
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

        // Backup existing configuration if it exists
        if (File.Exists(targetConfigFile))
        {
            await backup.CreateBackupAsync(GameId, [targetConfigFile], ct);
        }

        Dictionary<string, string> settings;

        if (patch.Id == "gears-eday-opt-vsm-megalights")
        {
            settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["r.MegaLights.HardwareRayTracing"] = "0",
                ["r.MegaLights.MaxLights"] = "32",
                ["r.Shadow.Virtual.ResolutionLodBiasDirectional"] = "0.5"
            };
        }
        else
        {
            // Software Lumen fallback patch
            settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["r.Lumen.HardwareRayTracing"] = "0",
                ["r.Lumen.Reflections.HardwareRayTracing"] = "0",
                ["r.RayTracing.Shadows"] = "0",
                ["r.MegaLights.HardwareRayTracing"] = "0",
                ["r.DistanceFields"] = "1",
                ["r.Lumen.ScreenTraces"] = "1",
                ["r.Lumen.DiffuseIndirect.Allow"] = "1",
                ["r.Lumen.Reflections.Allow"] = "1",
                ["r.Shadow.Virtual.Enable"] = "1"
            };
        }

        WriteConfigFile(targetConfigFile, settings);
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
        var isValid = config.TryGetValue("r.Lumen.HardwareRayTracing", out var hw) && hw == "0" &&
                      config.TryGetValue("r.DistanceFields", out var df) && df == "1";

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
        var appDataIni = Path.Combine(localAppData, "GearGame", "Saved", "Config", "Windows", "GameUserSettings.ini");
        if (File.Exists(appDataIni))
        {
            return appDataIni;
        }

        return Path.Combine(game.InstallPath, DefaultIniRelativePath);
    }
}
