using StS2ModManager.Core;

namespace StS2ModManager.Tests;

public sealed class ModFolderRenamerTests
{
    [Fact]
    public void Rename_RenamesModFolder()
    {
        using var temp = new TempDirectory();
        var paths = ModPaths.FromGameRoot(temp.Path);
        var sourcePath = Path.Combine(paths.EnabledDirectory, "OldMod");
        Directory.CreateDirectory(sourcePath);

        var result = new ModFolderRenamer().Rename(sourcePath, "OldMod", "NewMod");

        Assert.Equal("NewMod", result);
        Assert.False(Directory.Exists(sourcePath));
        Assert.True(Directory.Exists(Path.Combine(paths.EnabledDirectory, "NewMod")));
    }

    [Fact]
    public void Rename_Throws_WhenTargetFolderExists()
    {
        using var temp = new TempDirectory();
        var paths = ModPaths.FromGameRoot(temp.Path);
        var sourcePath = Path.Combine(paths.DisabledDirectory, "OldMod");
        Directory.CreateDirectory(sourcePath);
        Directory.CreateDirectory(Path.Combine(paths.DisabledDirectory, "NewMod"));

        var ex = Assert.Throws<IOException>(() =>
            new ModFolderRenamer().Rename(sourcePath, "OldMod", "NewMod"));

        Assert.Contains("同名 MOD", ex.Message);
    }
}
