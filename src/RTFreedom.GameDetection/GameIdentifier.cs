using RTFreedom.Core.Enums;
using RTFreedom.Core.Models;

namespace RTFreedom.GameDetection;

/// <summary>
/// Target games with mandatory or intensive ray-tracing requirements supported by RT Freedom.
/// </summary>
public enum TargetGame
{
    /// <summary>
    /// Indiana Jones and the Great Circle (MachineGames / Bethesda).
    /// Mandatory hardware ray tracing / path tracing requirement.
    /// </summary>
    IndianaJonesAndTheGreatCircle,

    /// <summary>
    /// Gears 5 (The Coalition / Xbox Game Studios).
    /// DirectX 12 screen space and ray-traced shadows/reflections.
    /// </summary>
    Gears5,

    /// <summary>
    /// DOOM: The Dark Ages (id Software / Bethesda).
    /// Next-generation id Tech Vulkan RT pipeline.
    /// </summary>
    DoomTheDarkAges
}

/// <summary>
/// Static helper and lookup registry for known ray-tracing target games.
/// Provides platform identification, default rendering APIs, and known configuration paths.
/// </summary>
public static class GameIdentifier
{
    #region Known Platform Identifiers

    // Steam AppIDs
    public const string SteamIdIndianaJones = "2677660";
    public const string SteamIdGears5 = "1097840";
    public const string SteamIdDoomDarkAges = "3017860";

    // Epic Games Store App Names / Catalog Item IDs
    public const string EpicIdIndianaJones = "60ec252ba0e5443a9f0290ca881765c9";
    public const string EpicNameIndianaJones = "GreatCircle";
    public const string EpicIdGears5 = "Gears5";
    public const string EpicIdDoomDarkAges = "DOOMTheDarkAges";

    // Xbox / Windows Store Package Family Names / Prefixes
    public const string XboxPackageIndianaJones = "BethesdaSoftworks.IndianaJonesandtheGreatCircle";
    public const string XboxPackageGears5 = "Microsoft.HalifaxBaseGame";
    public const string XboxPackageDoomDarkAges = "BethesdaSoftworks.DOOMTheDarkAges";

    #endregion

    /// <summary>
    /// Identifies whether the specified <see cref="GameInfo"/> matches a known RT Freedom target game.
    /// </summary>
    /// <param name="game">The detected game metadata.</param>
    /// <returns>The recognized <see cref="TargetGame"/> or null if not a target game.</returns>
    public static TargetGame? IdentifyTargetGame(GameInfo game)
    {
        if (game == null)
        {
            return null;
        }

        // Match by Platform ID first
        var byId = IdentifyById(game.Id);
        if (byId.HasValue)
        {
            return byId;
        }

        // Match by Title
        var byTitle = IdentifyByTitle(game.Title);
        if (byTitle.HasValue)
        {
            return byTitle;
        }

        // Match by Executable Name
        if (!string.IsNullOrWhiteSpace(game.ExecutablePath))
        {
            var fileName = Path.GetFileNameWithoutExtension(game.ExecutablePath);
            var byExe = IdentifyByExecutable(fileName);
            if (byExe.HasValue)
            {
                return byExe;
            }
        }

        // Match by Install Folder Name
        if (!string.IsNullOrWhiteSpace(game.InstallPath))
        {
            var folderName = Path.GetFileName(game.InstallPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            var byFolder = IdentifyByTitle(folderName);
            if (byFolder.HasValue)
            {
                return byFolder;
            }
        }

        return null;
    }

    /// <summary>
    /// Identifies a target game by unique platform ID or package name.
    /// </summary>
    /// <param name="gameId">The platform specific identifier or app ID.</param>
    /// <returns>The recognized <see cref="TargetGame"/> or null.</returns>
    public static TargetGame? IdentifyById(string gameId)
    {
        if (string.IsNullOrWhiteSpace(gameId))
        {
            return null;
        }

        var normalized = gameId.Trim();

        // Steam AppIDs
        if (normalized.Equals(SteamIdIndianaJones, StringComparison.OrdinalIgnoreCase))
            return TargetGame.IndianaJonesAndTheGreatCircle;
        if (normalized.Equals(SteamIdGears5, StringComparison.OrdinalIgnoreCase))
            return TargetGame.Gears5;
        if (normalized.Equals(SteamIdDoomDarkAges, StringComparison.OrdinalIgnoreCase))
            return TargetGame.DoomTheDarkAges;

        // Epic App Names / Item IDs
        if (normalized.Contains(EpicIdIndianaJones, StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains(EpicNameIndianaJones, StringComparison.OrdinalIgnoreCase))
            return TargetGame.IndianaJonesAndTheGreatCircle;
        if (normalized.Contains(EpicIdGears5, StringComparison.OrdinalIgnoreCase))
            return TargetGame.Gears5;
        if (normalized.Contains(EpicIdDoomDarkAges, StringComparison.OrdinalIgnoreCase))
            return TargetGame.DoomTheDarkAges;

        // Xbox Package Names
        if (normalized.StartsWith(XboxPackageIndianaJones, StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("IndianaJones", StringComparison.OrdinalIgnoreCase) && normalized.Contains("GreatCircle", StringComparison.OrdinalIgnoreCase))
            return TargetGame.IndianaJonesAndTheGreatCircle;
        if (normalized.StartsWith(XboxPackageGears5, StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("Halifax", StringComparison.OrdinalIgnoreCase))
            return TargetGame.Gears5;
        if (normalized.StartsWith(XboxPackageDoomDarkAges, StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("DOOMTheDarkAges", StringComparison.OrdinalIgnoreCase))
            return TargetGame.DoomTheDarkAges;

        return null;
    }

    /// <summary>
    /// Identifies a target game by its title or folder name.
    /// </summary>
    /// <param name="title">The display title or folder name.</param>
    /// <returns>The recognized <see cref="TargetGame"/> or null.</returns>
    public static TargetGame? IdentifyByTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        var normalized = title.Trim();

        if (normalized.Contains("Indiana Jones", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("The Great Circle", StringComparison.OrdinalIgnoreCase))
        {
            return TargetGame.IndianaJonesAndTheGreatCircle;
        }

        if (normalized.Contains("Gears 5", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("Gears5", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("Gears of War 5", StringComparison.OrdinalIgnoreCase))
        {
            return TargetGame.Gears5;
        }

        if (normalized.Contains("Dark Ages", StringComparison.OrdinalIgnoreCase) ||
            (normalized.Contains("DOOM", StringComparison.OrdinalIgnoreCase) && normalized.Contains("Dark", StringComparison.OrdinalIgnoreCase)))
        {
            return TargetGame.DoomTheDarkAges;
        }

        return null;
    }

    /// <summary>
    /// Identifies a target game by executable filename.
    /// </summary>
    /// <param name="executableName">The filename of the executable (with or without .exe).</param>
    /// <returns>The recognized <see cref="TargetGame"/> or null.</returns>
    public static TargetGame? IdentifyByExecutable(string? executableName)
    {
        if (string.IsNullOrWhiteSpace(executableName))
        {
            return null;
        }

        var normalized = Path.GetFileNameWithoutExtension(executableName);

        if (normalized.Equals("TheGreatCircle", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("IndianaJones", StringComparison.OrdinalIgnoreCase))
        {
            return TargetGame.IndianaJonesAndTheGreatCircle;
        }

        if (normalized.Equals("Gears5", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("GearGame", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("GearGame-Win64-Shipping", StringComparison.OrdinalIgnoreCase))
        {
            return TargetGame.Gears5;
        }

        if (normalized.Equals("DoomTheDarkAges", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("DoomDarkAges", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("doom_dark_ages", StringComparison.OrdinalIgnoreCase))
        {
            return TargetGame.DoomTheDarkAges;
        }

        return null;
    }

    /// <summary>
    /// Gets the standard graphics rendering API utilized by the target game.
    /// </summary>
    /// <param name="gameId">The game identifier, title, or target enum string representation.</param>
    /// <returns>The <see cref="RenderingApi"/> used by the game, or Unknown.</returns>
    public static RenderingApi GetRenderingApi(string gameId)
    {
        var target = IdentifyById(gameId) ?? IdentifyByTitle(gameId) ?? IdentifyByExecutable(gameId);
        if (!target.HasValue && Enum.TryParse<TargetGame>(gameId, true, out var parsed))
        {
            target = parsed;
        }

        return target switch
        {
            TargetGame.IndianaJonesAndTheGreatCircle => RenderingApi.Vulkan,
            TargetGame.Gears5 => RenderingApi.DirectX12,
            TargetGame.DoomTheDarkAges => RenderingApi.Vulkan,
            _ => RenderingApi.Unknown
        };
    }

    /// <summary>
    /// Gets known configuration file paths relative to the game's installation directory.
    /// </summary>
    /// <param name="gameId">The game identifier, title, or target enum string representation.</param>
    /// <returns>A list of relative configuration file paths.</returns>
    public static IReadOnlyList<string> GetKnownConfigFiles(string gameId)
    {
        var target = IdentifyById(gameId) ?? IdentifyByTitle(gameId) ?? IdentifyByExecutable(gameId);
        if (!target.HasValue && Enum.TryParse<TargetGame>(gameId, true, out var parsed))
        {
            target = parsed;
        }

        return target switch
        {
            TargetGame.IndianaJonesAndTheGreatCircle => new[]
            {
                Path.Combine("base", "TheGreatCircleConfig.local"),
                Path.Combine("base", "default.cfg"),
                "TheGreatCircle.ini"
            },
            TargetGame.Gears5 => new[]
            {
                Path.Combine("GearGame", "Config", "DefaultEngine.ini"),
                Path.Combine("GearGame", "Config", "DefaultScalability.ini"),
                Path.Combine("GearGame", "Config", "DefaultGame.ini")
            },
            TargetGame.DoomTheDarkAges => new[]
            {
                Path.Combine("base", "doom_config.cfg"),
                Path.Combine("base", "darkages_config.local"),
                Path.Combine("base", "default.cfg")
            },
            _ => Array.Empty<string>()
        };
    }

    /// <summary>
    /// Gets known shader cache or compiled binary paths relative to the game's installation directory.
    /// </summary>
    /// <param name="gameId">The game identifier, title, or target enum string representation.</param>
    /// <returns>A list of relative shader file paths.</returns>
    public static IReadOnlyList<string> GetKnownShaderFiles(string gameId)
    {
        var target = IdentifyById(gameId) ?? IdentifyByTitle(gameId) ?? IdentifyByExecutable(gameId);
        if (!target.HasValue && Enum.TryParse<TargetGame>(gameId, true, out var parsed))
        {
            target = parsed;
        }

        return target switch
        {
            TargetGame.IndianaJonesAndTheGreatCircle => new[]
            {
                Path.Combine("base", "renderprogs", "vk_pipeline_cache.bin"),
                Path.Combine("base", "renderprogs", "vulkan_pc.spv")
            },
            TargetGame.Gears5 => new[]
            {
                Path.Combine("GearGame", "Content", "ShaderCache.upk"),
                Path.Combine("GearGame", "Content", "D3D12ShaderCache.bin")
            },
            TargetGame.DoomTheDarkAges => new[]
            {
                Path.Combine("base", "renderprogs", "vk_pipeline_cache.bin"),
                Path.Combine("base", "renderprogs", "doom_spv.bin")
            },
            _ => Array.Empty<string>()
        };
    }

    /// <summary>
    /// Gets candidate executable file names (relative or filename) for a target game.
    /// </summary>
    /// <param name="target">The target game.</param>
    /// <returns>Candidate executable paths.</returns>
    public static IReadOnlyList<string> GetCandidateExecutables(TargetGame target)
    {
        return target switch
        {
            TargetGame.IndianaJonesAndTheGreatCircle => new[]
            {
                "TheGreatCircle.exe",
                "IndianaJones.exe",
                Path.Combine("Binaries", "Win64", "TheGreatCircle.exe")
            },
            TargetGame.Gears5 => new[]
            {
                Path.Combine("GearGame", "Binaries", "Win64", "Gears5.exe"),
                Path.Combine("GearGame", "Binaries", "Win64", "GearGame.exe"),
                "Gears5.exe"
            },
            TargetGame.DoomTheDarkAges => new[]
            {
                "DoomTheDarkAges.exe",
                "doom_dark_ages.exe",
                Path.Combine("base", "DoomTheDarkAges.exe")
            },
            _ => Array.Empty<string>()
        };
    }

    /// <summary>
    /// Gets canonical publisher/developer for a target game.
    /// </summary>
    /// <param name="target">The target game.</param>
    /// <returns>Canonical publisher name.</returns>
    public static string GetCanonicalPublisher(TargetGame target)
    {
        return target switch
        {
            TargetGame.IndianaJonesAndTheGreatCircle => "Bethesda Softworks / MachineGames",
            TargetGame.Gears5 => "Xbox Game Studios / The Coalition",
            TargetGame.DoomTheDarkAges => "Bethesda Softworks / id Software",
            _ => "Unknown"
        };
    }

    /// <summary>
    /// Gets the canonical friendly display title for a target game.
    /// </summary>
    /// <param name="target">The target game.</param>
    /// <returns>Friendly title.</returns>
    public static string GetCanonicalTitle(TargetGame target)
    {
        return target switch
        {
            TargetGame.IndianaJonesAndTheGreatCircle => "Indiana Jones and the Great Circle",
            TargetGame.Gears5 => "Gears 5",
            TargetGame.DoomTheDarkAges => "DOOM: The Dark Ages",
            _ => target.ToString()
        };
    }
}
