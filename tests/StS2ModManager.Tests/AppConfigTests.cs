using StS2ModManager.Core;

namespace StS2ModManager.Tests;

public sealed class AppConfigTests
{
    [Fact]
    public void RemoveMod_RemovesNoteAndProfileReferences()
    {
        var config = new AppConfig();
        config.SetModNote("ModA", "note");
        config.Profiles["combo"] = ["ModA", "ModB"];

        config.RemoveMod("moda");

        Assert.Empty(config.GetModNote("ModA"));
        Assert.Equal(["ModB"], config.Profiles["combo"]);
    }

    [Fact]
    public void RemoveMod_LeavesUnrelatedDataUntouched()
    {
        var config = new AppConfig();
        config.Profiles["combo"] = ["ModB"];

        config.RemoveMod("ModA");

        Assert.Equal(["ModB"], config.Profiles["combo"]);
    }
}
