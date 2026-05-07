namespace StS2ModManager.Core;

public sealed class ModProfileApplier
{
    public IReadOnlyList<ModSelection> BuildExactSelections(IEnumerable<ModEntry> mods, IEnumerable<string> enabledFolderNames)
    {
        ArgumentNullException.ThrowIfNull(mods);
        ArgumentNullException.ThrowIfNull(enabledFolderNames);

        var enabledNames = new HashSet<string>(enabledFolderNames, StringComparer.OrdinalIgnoreCase);
        return mods
            .Select(mod => new ModSelection(mod.Name, enabledNames.Contains(mod.Name)))
            .ToArray();
    }
}
