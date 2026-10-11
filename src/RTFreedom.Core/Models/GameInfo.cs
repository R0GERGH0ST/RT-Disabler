using RTFreedom.Core.Enums;

namespace RTFreedom.Core.Models;

/// <summary>
/// Represents detected information about an installed game.
/// </summary>
/// <param name="Id">Unique identifier for the game.</param>
/// <param name="Title">Display title of the game.</param>
/// <param name="ExecutablePath">Absolute path to the game executable file.</param>
/// <param name="InstallPath">Root directory path where the game is installed.</param>
/// <param name="Version">Detected version string of the game, or null if unknown.</param>
/// <param name="Publisher">Publisher or developer name, or null if unknown.</param>
/// <param name="Platform">Game distribution platform.</param>
/// <param name="GraphicsApi">Primary graphics rendering API.</param>
/// <param name="ConfigurationFiles">List of detected configuration file paths.</param>
/// <param name="ShaderFiles">List of detected shader cache or package file paths.</param>
/// <param name="DetectedAt">Timestamp when the game was detected.</param>
public record GameInfo(
    string Id,
    string Title,
    string ExecutablePath,
    string InstallPath,
    string? Version,
    string? Publisher,
    GamePlatform Platform,
    RenderingApi GraphicsApi,
    List<string> ConfigurationFiles,
    List<string> ShaderFiles,
    DateTime DetectedAt);
