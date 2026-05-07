namespace StS2ModManager.Core;

public sealed class ModSynchronizer
{
    private readonly ModScanner _scanner = new();

    public void Sync(ModPaths paths, IEnumerable<ModSelection> selections)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(selections);

        Directory.CreateDirectory(paths.EnabledDirectory);
        Directory.CreateDirectory(paths.DisabledDirectory);

        var currentEntries = _scanner.Scan(paths);
        var conflicts = currentEntries.Where(entry => entry.HasConflict).Select(entry => entry.Name).ToArray();
        if (conflicts.Length > 0)
        {
            throw new InvalidOperationException($"同名 MOD 同时存在于 mods 和 mods_disabled：{string.Join(", ", conflicts)}");
        }

        var currentByName = currentEntries.ToDictionary(entry => entry.Name, StringComparer.OrdinalIgnoreCase);
        foreach (var selection in selections)
        {
            if (!currentByName.TryGetValue(selection.Name, out var current))
            {
                continue;
            }

            if (selection.IsEnabled == current.IsEnabled)
            {
                continue;
            }

            var targetRoot = selection.IsEnabled ? paths.EnabledDirectory : paths.DisabledDirectory;
            var targetPath = Path.Combine(targetRoot, current.Name);
            if (Directory.Exists(targetPath))
            {
                throw new IOException($"同名 MOD 已存在于目标目录，无法覆盖：{current.Name}");
            }

            Directory.Move(current.SourcePath, targetPath);
        }
    }
}
