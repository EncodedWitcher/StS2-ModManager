namespace StS2ModManager.Core;

public sealed record ModPaths(string GameRoot, string EnabledDirectory, string DisabledDirectory)
{
    public static ModPaths FromGameRoot(string gameRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameRoot);

        return new ModPaths(
            gameRoot,
            Path.Combine(gameRoot, "mods"),
            Path.Combine(gameRoot, "mods_disabled"));
    }
}
