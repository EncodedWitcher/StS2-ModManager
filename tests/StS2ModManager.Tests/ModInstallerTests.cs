using StS2ModManager.Core;

namespace StS2ModManager.Tests;

public sealed class ModInstallerTests
{
    [Fact]
    public void Install_CopiesModIntoDisabledDirectory()
    {
        using var temp = new TempDirectory();
        var paths = ModPaths.FromGameRoot(temp.Path);
        using var package = CreatePackage(temp.Path, "ModA", "data.txt", "content");

        var name = new ModInstaller().Install(paths, package, enabled: false);

        Assert.Equal("ModA", name);
        Assert.True(File.Exists(Path.Combine(paths.DisabledDirectory, "ModA", "data.txt")));
        Assert.False(Directory.Exists(Path.Combine(paths.EnabledDirectory, "ModA")));
    }

    [Fact]
    public void Install_CopiesModIntoEnabledDirectory()
    {
        using var temp = new TempDirectory();
        var paths = ModPaths.FromGameRoot(temp.Path);
        using var package = CreatePackage(temp.Path, "ModA", "data.txt", "content");

        new ModInstaller().Install(paths, package, enabled: true);

        Assert.True(File.Exists(Path.Combine(paths.EnabledDirectory, "ModA", "data.txt")));
    }

    [Fact]
    public void Install_Throws_WhenSameNameAlreadyExists()
    {
        using var temp = new TempDirectory();
        var paths = ModPaths.FromGameRoot(temp.Path);
        Directory.CreateDirectory(Path.Combine(paths.EnabledDirectory, "ModA"));
        using var package = CreatePackage(temp.Path, "ModA", "data.txt", "content");

        var ex = Assert.Throws<IOException>(() => new ModInstaller().Install(paths, package, enabled: false));

        Assert.Contains("已存在同名 MOD", ex.Message);
    }

    [Fact]
    public void Update_ReplacesContentAndKeepsLocation()
    {
        using var temp = new TempDirectory();
        var paths = ModPaths.FromGameRoot(temp.Path);
        var existing = Path.Combine(paths.DisabledDirectory, "ModA");
        Directory.CreateDirectory(existing);
        File.WriteAllText(Path.Combine(existing, "old.txt"), "old");
        using var package = CreatePackage(temp.Path, "ModA", "new.txt", "fresh");

        new ModInstaller().Update(existing, package);

        Assert.True(Directory.Exists(existing));
        Assert.False(File.Exists(Path.Combine(existing, "old.txt")));
        Assert.Equal("fresh", File.ReadAllText(Path.Combine(existing, "new.txt")));
    }

    [Fact]
    public void Uninstall_DeletesModFolder()
    {
        using var temp = new TempDirectory();
        var paths = ModPaths.FromGameRoot(temp.Path);
        var modPath = Path.Combine(paths.EnabledDirectory, "ModA");
        Directory.CreateDirectory(modPath);
        File.WriteAllText(Path.Combine(modPath, "data.txt"), "content");

        new ModInstaller().Uninstall(modPath);

        Assert.False(Directory.Exists(modPath));
    }

    private static ResolvedModPackage CreatePackage(string root, string name, string fileName, string content)
    {
        var sourceDirectory = Path.Combine(root, "source-" + Guid.NewGuid().ToString("N"), name);
        Directory.CreateDirectory(sourceDirectory);
        File.WriteAllText(Path.Combine(sourceDirectory, name + ".json"), $"{{\"id\": \"{name}\"}}");
        File.WriteAllText(Path.Combine(sourceDirectory, fileName), content);
        return new ModImportResolver().Resolve(sourceDirectory);
    }
}
