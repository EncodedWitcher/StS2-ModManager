namespace StS2ModManager.Core;

public sealed class ModScanner
{
    private readonly ModMetadataReader _metadataReader;

    public ModScanner(ModMetadataReader? metadataReader = null)
    {
        _metadataReader = metadataReader ?? new ModMetadataReader();
    }

    public IReadOnlyList<ModEntry> Scan(ModPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

        var enabled = ReadImmediateDirectories(paths.EnabledDirectory);
        var disabled = ReadImmediateDirectories(paths.DisabledDirectory);
        var entries = new List<ModEntry>();

        foreach (var enabledDirectory in enabled.OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase))
        {
            var hasConflict = disabled.Any(d => string.Equals(d.Name, enabledDirectory.Name, StringComparison.OrdinalIgnoreCase));
            entries.Add(CreateEntry(enabledDirectory, true, hasConflict));
        }

        foreach (var disabledDirectory in disabled.OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase))
        {
            var alreadyListed = enabled.Any(d => string.Equals(d.Name, disabledDirectory.Name, StringComparison.OrdinalIgnoreCase));
            if (alreadyListed)
            {
                continue;
            }

            entries.Add(CreateEntry(disabledDirectory, false, false));
        }

        return entries.ToArray();
    }

    private ModEntry CreateEntry(DirectoryInfo directory, bool isEnabled, bool hasConflict)
    {
        return new ModEntry(
            directory.Name,
            isEnabled,
            hasConflict,
            directory.FullName,
            _metadataReader.Read(directory));
    }

    private static DirectoryInfo[] ReadImmediateDirectories(string directory)
    {
        return Directory.Exists(directory)
            ? new DirectoryInfo(directory).GetDirectories()
            : [];
    }
}
