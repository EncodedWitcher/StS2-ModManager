using StS2ModManager.Core;

namespace StS2ModManager.Tests;

public sealed class GameInstallLocatorTests
{
    [Fact]
    public void TryCreateLaunchInfo_ReturnsNull_WhenDirectoryHasNoExecutable()
    {
        using var temp = new TempDirectory();
        var gameRoot = Path.Combine(temp.Path, "Slay the Spire 2");
        Directory.CreateDirectory(gameRoot);

        var result = new GameInstallLocator().TryCreateLaunchInfo(gameRoot);

        Assert.Null(result);
    }

    [Fact]
    public void TryCreateLaunchInfo_ReturnsLaunchInfo_WhenDirectoryHasExecutable()
    {
        using var temp = new TempDirectory();
        var gameRoot = Path.Combine(temp.Path, "Slay the Spire 2");
        Directory.CreateDirectory(gameRoot);
        var gameExe = Path.Combine(gameRoot, "game.exe");
        File.WriteAllText(gameExe, string.Empty);

        var result = new GameInstallLocator().TryCreateLaunchInfo(gameRoot);

        Assert.NotNull(result);
        Assert.Equal(gameExe, result.ExecutablePath);
        Assert.Equal(gameRoot, result.WorkingDirectory);
        Assert.Empty(result.Arguments);
    }

    [Fact]
    public void DiscoverGameRoots_UsesSteamRootThenLibraryFoldersInOrder()
    {
        using var temp = new TempDirectory();
        var steamRoot = Path.Combine(temp.Path, "Steam");
        var firstGameRoot = Path.Combine(steamRoot, "steamapps", "common", "Slay the Spire 2");
        var libraryRoot = Path.Combine(temp.Path, "LibraryTwo");
        var secondGameRoot = Path.Combine(libraryRoot, "steamapps", "common", "Slay the Spire 2");

        Directory.CreateDirectory(firstGameRoot);
        Directory.CreateDirectory(secondGameRoot);
        File.WriteAllText(Path.Combine(firstGameRoot, "game.exe"), string.Empty);
        File.WriteAllText(Path.Combine(secondGameRoot, "game.exe"), string.Empty);

        Directory.CreateDirectory(Path.Combine(steamRoot, "steamapps"));
        File.WriteAllText(
            Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf"),
            $$"""
            "libraryfolders"
            {
                "1"
                {
                    "path" "{{libraryRoot.Replace("\\", "\\\\")}}"
                }
            }
            """);

        var result = new GameInstallLocator().DiscoverGameRoots([steamRoot]);

        Assert.Equal([firstGameRoot, secondGameRoot], result);
    }
}
