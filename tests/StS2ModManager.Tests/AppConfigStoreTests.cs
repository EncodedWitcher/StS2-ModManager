using StS2ModManager.Core;

namespace StS2ModManager.Tests;

public sealed class AppConfigStoreTests
{
    [Fact]
    public void GetDefaultConfigPath_UsesExecutableSiblingConfigDirectory()
    {
        var executablePath = Path.Combine("D:", "Games", "StS2ModManager", "StS2ModManager.exe");

        var result = AppConfigStore.GetDefaultConfigPath(executablePath);

        Assert.EndsWith(Path.Combine("StS2ModManager", "config", "config.json"), result);
    }

    [Fact]
    public void SaveAndLoad_RoundTripsLastLaunchInfo()
    {
        using var temp = new TempDirectory();
        var gameExe = Path.Combine(temp.Path, "game.exe");
        File.WriteAllText(gameExe, string.Empty);

        var store = new AppConfigStore(Path.Combine(temp.Path, "config", "config.json"));
        var launchInfo = new GameLaunchInfo(gameExe, ["--one"], temp.Path);

        store.Save(AppConfig.FromLaunchInfo(launchInfo));
        var loaded = store.Load();

        Assert.True(loaded.HasValidLaunchInfo);
        Assert.Equal(gameExe, loaded.LastExecutablePath);
        Assert.Equal(["--one"], loaded.LastArguments);
        Assert.Equal(temp.Path, loaded.LastWorkingDirectory);
    }

    [Fact]
    public void SaveAndLoad_RoundTripsWindowNotesAndProfiles()
    {
        using var temp = new TempDirectory();
        var store = new AppConfigStore(Path.Combine(temp.Path, "config", "config.json"));
        var config = new AppConfig
        {
            WindowBounds = new WindowBounds(10, 20, 900, 640),
            ModNotes = new Dictionary<string, string>
            {
                ["ModA"] = "note"
            },
            Profiles = new Dictionary<string, string[]>
            {
                ["组合1"] = ["ModA", "ModB"]
            }
        };

        store.Save(config);
        var loaded = store.Load();

        Assert.Equal(new WindowBounds(10, 20, 900, 640), loaded.WindowBounds);
        Assert.Equal("note", loaded.GetModNote("moda"));
        Assert.Equal(["ModA", "ModB"], loaded.Profiles["组合1"]);
    }

    [Fact]
    public void Load_MigratesLegacyConfig_WhenLocalConfigDoesNotExist()
    {
        using var temp = new TempDirectory();
        var legacyPath = Path.Combine(temp.Path, "legacy", "config.json");
        var localPath = Path.Combine(temp.Path, "local", "config", "config.json");
        var gameExe = Path.Combine(temp.Path, "game.exe");
        File.WriteAllText(gameExe, string.Empty);

        new AppConfigStore(legacyPath).Save(AppConfig.FromLaunchInfo(new GameLaunchInfo(gameExe, ["--legacy"], temp.Path)));

        var loaded = new AppConfigStore(localPath, legacyPath).Load();

        Assert.True(File.Exists(localPath));
        Assert.Equal(gameExe, loaded.LastExecutablePath);
        Assert.Equal(["--legacy"], loaded.LastArguments);
    }

    [Fact]
    public void RenameModKey_MigratesNotesAndProfileReferences()
    {
        var config = new AppConfig
        {
            ModNotes = new Dictionary<string, string>
            {
                ["OldMod"] = "note"
            },
            Profiles = new Dictionary<string, string[]>
            {
                ["组合1"] = ["OldMod", "OtherMod"]
            }
        };

        config.RenameModKey("OldMod", "NewMod");

        Assert.Equal("note", config.GetModNote("NewMod"));
        Assert.Equal(["NewMod", "OtherMod"], config.Profiles["组合1"]);
        Assert.Equal(string.Empty, config.GetModNote("OldMod"));
    }
}
