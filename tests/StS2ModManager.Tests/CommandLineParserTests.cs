using StS2ModManager.Core;

namespace StS2ModManager.Tests;

public sealed class CommandLineParserTests
{
    [Fact]
    public void Parse_ReturnsNull_WhenNoExecutableArgumentExists()
    {
        var result = CommandLineParser.Parse(["-steam", "123"]);

        Assert.Null(result);
    }

    [Fact]
    public void Parse_ExtractsExecutableAndTrailingArguments()
    {
        var result = CommandLineParser.Parse([
            "--ignored",
            @"D:\Steam\steamapps\common\Slay the Spire 2\game.exe",
            "--fullscreen",
            "seed value"
        ]);

        Assert.NotNull(result);
        Assert.Equal(@"D:\Steam\steamapps\common\Slay the Spire 2\game.exe", result.ExecutablePath);
        Assert.Equal(@"D:\Steam\steamapps\common\Slay the Spire 2", result.WorkingDirectory);
        Assert.Equal(["--fullscreen", "seed value"], result.Arguments);
    }
}
