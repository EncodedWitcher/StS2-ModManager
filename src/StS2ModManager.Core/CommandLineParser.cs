namespace StS2ModManager.Core;

public static class CommandLineParser
{
    public static GameLaunchInfo? Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var executableIndex = Array.FindIndex(args, LooksLikeExecutablePath);
        if (executableIndex < 0)
        {
            return null;
        }

        var executablePath = Unquote(args[executableIndex]);
        var workingDirectory = Path.GetDirectoryName(executablePath);
        if (string.IsNullOrWhiteSpace(workingDirectory))
        {
            return null;
        }

        return new GameLaunchInfo(
            executablePath,
            args.Skip(executableIndex + 1).Select(Unquote).ToArray(),
            workingDirectory);
    }

    private static bool LooksLikeExecutablePath(string value)
    {
        var unquoted = Unquote(value);
        return unquoted.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);
    }

    private static string Unquote(string value)
    {
        return value.Trim().Trim('"');
    }
}
