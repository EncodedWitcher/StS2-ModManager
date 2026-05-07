using StS2ModManager.Core;

namespace StS2ModManager.Tests;

public sealed class ModScannerTests
{
    [Fact]
    public void Scan_ReadsOnlyImmediateModDirectories()
    {
        using var temp = new TempDirectory();
        var paths = ModPaths.FromGameRoot(temp.Path);
        Directory.CreateDirectory(Path.Combine(paths.EnabledDirectory, "EnabledMod"));
        Directory.CreateDirectory(Path.Combine(paths.DisabledDirectory, "DisabledMod"));
        File.WriteAllText(Path.Combine(paths.EnabledDirectory, "loose.dll"), string.Empty);

        var result = new ModScanner().Scan(paths);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, mod => mod.Name == "EnabledMod" && mod.IsEnabled);
        Assert.Contains(result, mod => mod.Name == "DisabledMod" && !mod.IsEnabled);
        Assert.DoesNotContain(result, mod => mod.Name == "loose.dll");
    }

    [Fact]
    public void Scan_MarksConflict_WhenSameNameExistsInBothDirectories()
    {
        using var temp = new TempDirectory();
        var paths = ModPaths.FromGameRoot(temp.Path);
        Directory.CreateDirectory(Path.Combine(paths.EnabledDirectory, "SameMod"));
        Directory.CreateDirectory(Path.Combine(paths.DisabledDirectory, "SameMod"));

        var result = new ModScanner().Scan(paths);

        var conflict = Assert.Single(result);
        Assert.True(conflict.IsEnabled);
        Assert.True(conflict.HasConflict);
    }

    [Fact]
    public void Scan_ReadsMetadataJsonFromModDirectory()
    {
        using var temp = new TempDirectory();
        var paths = ModPaths.FromGameRoot(temp.Path);
        var modDirectory = Path.Combine(paths.EnabledDirectory, "necrobinderSkin");
        Directory.CreateDirectory(modDirectory);
        File.WriteAllText(Path.Combine(modDirectory, "necrobinderSkin.json"), """
        {
          "id": "necrobinderSkin",
          "pck_name": "necrobinderSkin",
          "name": "necrobinderSkin",
          "author": "FeliNyx",
          "description": "Skin",
          "version": "0.0.1"
        }
        """);

        var result = new ModScanner().Scan(paths);

        var mod = Assert.Single(result);
        Assert.Equal("necrobinderSkin", mod.Metadata.Name);
        Assert.Equal("FeliNyx", mod.Metadata.Author);
        Assert.Equal("Skin", mod.Metadata.Description);
        Assert.Equal("0.0.1", mod.Metadata.Version);
    }

    [Fact]
    public void Scan_IgnoresBrokenMetadataJson()
    {
        using var temp = new TempDirectory();
        var paths = ModPaths.FromGameRoot(temp.Path);
        var modDirectory = Path.Combine(paths.EnabledDirectory, "BrokenMod");
        Directory.CreateDirectory(modDirectory);
        File.WriteAllText(Path.Combine(modDirectory, "BrokenMod.json"), "{ broken json");

        var result = new ModScanner().Scan(paths);

        var mod = Assert.Single(result);
        Assert.False(mod.Metadata.HasAnyValue);
    }
}
