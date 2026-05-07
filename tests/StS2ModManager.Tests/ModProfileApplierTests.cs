using StS2ModManager.Core;

namespace StS2ModManager.Tests;

public sealed class ModProfileApplierTests
{
    [Fact]
    public void BuildExactSelections_EnablesOnlyProfileMembers()
    {
        var mods = new[]
        {
            new ModEntry("ModA", true, false, "a", ModMetadata.Empty),
            new ModEntry("ModB", false, false, "b", ModMetadata.Empty),
            new ModEntry("ModC", true, false, "c", ModMetadata.Empty)
        };

        var selections = new ModProfileApplier().BuildExactSelections(mods, ["ModB"]).ToArray();

        Assert.Contains(selections, selection => selection.Name == "ModA" && !selection.IsEnabled);
        Assert.Contains(selections, selection => selection.Name == "ModB" && selection.IsEnabled);
        Assert.Contains(selections, selection => selection.Name == "ModC" && !selection.IsEnabled);
    }
}
