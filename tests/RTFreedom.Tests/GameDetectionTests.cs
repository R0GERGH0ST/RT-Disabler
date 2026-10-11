using RTFreedom.Core.Enums;
using RTFreedom.Core.Models;
using RTFreedom.GameDetection;
using Xunit;

namespace RTFreedom.Tests;

public class GameDetectionTests
{
    [Fact]
    public void VdfParser_ParsesNestedKeyValuePairs()
    {
        string vdfContent = """
        "AppState"
        {
            "appid" "2677660"
            "Universe" "1"
            "name" "Indiana Jones and the Great Circle"
            "installdir" "Indiana Jones and the Great Circle"
            "UserConfig"
            {
                "language" "english"
            }
        }
        """;

        var node = VdfParser.ParseText(vdfContent);

        Assert.NotNull(node);
        Assert.NotNull(node["AppState"]);
        Assert.Equal("AppState", node["AppState"]!.Key);
        Assert.Equal("2677660", node.GetValue("appid"));
        Assert.Equal("Indiana Jones and the Great Circle", node.GetValue("name"));
        Assert.Equal("Indiana Jones and the Great Circle", node.GetValue("installdir"));
        Assert.Equal("english", node.GetValue("UserConfig.language"));
    }

    [Fact]
    public void GameIdentifier_IdentifiesIndianaJones()
    {
        var game = new GameInfo(
            Id: "steam-2677660",
            Title: "Indiana Jones and the Great Circle",
            ExecutablePath: @"C:\Games\Steam\steamapps\common\Indiana Jones and the Great Circle\TheGreatCircle.exe",
            InstallPath: @"C:\Games\Steam\steamapps\common\Indiana Jones and the Great Circle",
            Version: "1.0.0",
            Publisher: "Bethesda Softworks",
            Platform: GamePlatform.Steam,
            GraphicsApi: RenderingApi.Vulkan,
            ConfigurationFiles: new List<string>(),
            ShaderFiles: new List<string>(),
            DetectedAt: DateTime.UtcNow
        );

        var recognized = GameIdentifier.IdentifyTargetGame(game);
        Assert.NotNull(recognized);
        Assert.Equal(TargetGame.IndianaJonesAndTheGreatCircle, recognized.Value);
        Assert.Equal(RenderingApi.Vulkan, GameIdentifier.GetRenderingApi(recognized.Value.ToString()));
    }

    [Fact]
    public void GameIdentifier_IdentifiesGears5()
    {
        var game = new GameInfo(
            Id: "steam-1097840",
            Title: "Gears 5",
            ExecutablePath: @"C:\Games\Steam\steamapps\common\Gears5\GearGame\Binaries\Win64\Gears5.exe",
            InstallPath: @"C:\Games\Steam\steamapps\common\Gears5",
            Version: "1.1.0",
            Publisher: "Xbox Game Studios",
            Platform: GamePlatform.Steam,
            GraphicsApi: RenderingApi.DirectX12,
            ConfigurationFiles: new List<string>(),
            ShaderFiles: new List<string>(),
            DetectedAt: DateTime.UtcNow
        );

        var recognized = GameIdentifier.IdentifyTargetGame(game);
        Assert.NotNull(recognized);
        Assert.Equal(TargetGame.Gears5, recognized.Value);
        Assert.Equal(RenderingApi.DirectX12, GameIdentifier.GetRenderingApi(recognized.Value.ToString()));
    }

    [Fact]
    public void GameIdentifier_IdentifiesDoomDarkAges()
    {
        var game = new GameInfo(
            Id: "steam-3017860",
            Title: "DOOM: The Dark Ages",
            ExecutablePath: @"C:\Games\Steam\steamapps\common\DOOM The Dark Ages\DoomTheDarkAges.exe",
            InstallPath: @"C:\Games\Steam\steamapps\common\DOOM The Dark Ages",
            Version: "1.0.0",
            Publisher: "Bethesda Softworks",
            Platform: GamePlatform.Steam,
            GraphicsApi: RenderingApi.Vulkan,
            ConfigurationFiles: new List<string>(),
            ShaderFiles: new List<string>(),
            DetectedAt: DateTime.UtcNow
        );

        var recognized = GameIdentifier.IdentifyTargetGame(game);
        Assert.NotNull(recognized);
        Assert.Equal(TargetGame.DoomTheDarkAges, recognized.Value);
        Assert.Equal(RenderingApi.Vulkan, GameIdentifier.GetRenderingApi(recognized.Value.ToString()));
    }

    [Fact]
    public void GameIdentifier_UnknownGameReturnsNull()
    {
        var game = new GameInfo(
            Id: "steam-12345",
            Title: "Some Non-Target Game",
            ExecutablePath: @"C:\Games\Steam\steamapps\common\NonTarget\game.exe",
            InstallPath: @"C:\Games\Steam\steamapps\common\NonTarget",
            Version: "1.0.0",
            Publisher: "Indie Dev",
            Platform: GamePlatform.Steam,
            GraphicsApi: RenderingApi.DirectX11,
            ConfigurationFiles: new List<string>(),
            ShaderFiles: new List<string>(),
            DetectedAt: DateTime.UtcNow
        );

        var recognized = GameIdentifier.IdentifyTargetGame(game);
        Assert.Null(recognized);
    }
}
