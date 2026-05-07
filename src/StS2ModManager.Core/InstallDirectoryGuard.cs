namespace StS2ModManager.Core;

public static class InstallDirectoryGuard
{
    private static readonly string[] AllowedRootFileNames =
    [
        "StS2ModManager.exe",
        "使用说明.txt"
    ];

    public static bool IsInstallDirectoryClean(string executablePath, string configPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(configPath);

        var executableDirectory = Path.GetDirectoryName(Path.GetFullPath(executablePath));
        if (string.IsNullOrWhiteSpace(executableDirectory) || !Directory.Exists(executableDirectory))
        {
            return false;
        }

        var allowedExecutable = Path.GetFullPath(executablePath);
        var allowedConfig = Path.GetFullPath(configPath);
        var allowedConfigDirectory = Path.GetDirectoryName(allowedConfig);

        foreach (var file in Directory.EnumerateFiles(executableDirectory, "*", SearchOption.AllDirectories))
        {
            var fullPath = Path.GetFullPath(file);
            if (string.Equals(fullPath, allowedExecutable, StringComparison.OrdinalIgnoreCase)
                || string.Equals(fullPath, allowedConfig, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (IsAllowedRootFile(executableDirectory, fullPath))
            {
                continue;
            }

            return false;
        }

        foreach (var directory in Directory.EnumerateDirectories(executableDirectory, "*", SearchOption.AllDirectories))
        {
            var fullPath = Path.GetFullPath(directory);
            if (allowedConfigDirectory is not null
                && string.Equals(fullPath, allowedConfigDirectory, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return false;
        }

        return true;
    }

    private static bool IsAllowedRootFile(string executableDirectory, string filePath)
    {
        var parentDirectory = Path.GetDirectoryName(filePath);
        if (!string.Equals(parentDirectory, executableDirectory, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var fileName = Path.GetFileName(filePath);
        return AllowedRootFileNames.Any(allowed => string.Equals(allowed, fileName, StringComparison.OrdinalIgnoreCase));
    }
}
