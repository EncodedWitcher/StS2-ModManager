using StS2ModManager.Core;

namespace StS2ModManager.Tests;

public sealed class GameLaunchInfoResolverTests
{
    [Fact]
    public void Resolve_ReturnsCachedLaunchInfo_WhenCommandLineLaunchInfoIsMissing()
    {
        using var temp = new TempDirectory();
        var gameExe = Path.Combine(temp.Path, "game.exe");
        File.WriteAllText(gameExe, string.Empty);
        var config = AppConfig.FromLaunchInfo(new GameLaunchInfo(gameExe, ["--cached"], temp.Path));

        var result = new GameLaunchInfoResolver().Resolve(null, config);

        Assert.NotNull(result);
        Assert.Equal(gameExe, result.ExecutablePath);
        Assert.Equal(["--cached"], result.Arguments);
    }

    [Fact]
    public void Resolve_ReturnsNull_WhenNoUsableLaunchInfoExists()
    {
        using var temp = new TempDirectory();
        var config = new AppConfig
        {
            LastExecutablePath = Path.Combine(temp.Path, "missing.exe"),
            LastWorkingDirectory = temp.Path
        };

        var result = new GameLaunchInfoResolver().Resolve(null, config);

        Assert.Null(result);
    }
}
