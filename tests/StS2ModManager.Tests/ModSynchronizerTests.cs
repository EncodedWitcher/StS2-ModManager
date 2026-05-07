using StS2ModManager.Core;

namespace StS2ModManager.Tests;

public sealed class ModSynchronizerTests
{
    [Fact]
    public void Sync_MovesDisabledModToEnabledDirectory()
    {
        using var temp = new TempDirectory();
        var paths = ModPaths.FromGameRoot(temp.Path);
        Directory.CreateDirectory(Path.Combine(paths.DisabledDirectory, "RegentCardsAnimeReworkv0.4.2"));

        new ModSynchronizer().Sync(paths, [
            new ModSelection("RegentCardsAnimeReworkv0.4.2", true)
        ]);

        Assert.True(Directory.Exists(Path.Combine(paths.EnabledDirectory, "RegentCardsAnimeReworkv0.4.2")));
        Assert.False(Directory.Exists(Path.Combine(paths.DisabledDirectory, "RegentCardsAnimeReworkv0.4.2")));
    }

    [Fact]
    public void Sync_MovesEnabledModToDisabledDirectory()
    {
        using var temp = new TempDirectory();
        var paths = ModPaths.FromGameRoot(temp.Path);
        Directory.CreateDirectory(Path.Combine(paths.EnabledDirectory, "ModA"));

        new ModSynchronizer().Sync(paths, [
            new ModSelection("ModA", false)
        ]);

        Assert.False(Directory.Exists(Path.Combine(paths.EnabledDirectory, "ModA")));
        Assert.True(Directory.Exists(Path.Combine(paths.DisabledDirectory, "ModA")));
    }

    [Fact]
    public void Sync_Throws_WhenConflictingDirectoriesExist()
    {
        using var temp = new TempDirectory();
        var paths = ModPaths.FromGameRoot(temp.Path);
        Directory.CreateDirectory(Path.Combine(paths.EnabledDirectory, "ModA"));
        Directory.CreateDirectory(Path.Combine(paths.DisabledDirectory, "ModA"));

        var ex = Assert.Throws<InvalidOperationException>(() =>
            new ModSynchronizer().Sync(paths, [new ModSelection("ModA", false)]));

        Assert.Contains("同名 MOD", ex.Message);
    }
}
