using System.Text.RegularExpressions;
using Microsoft.Win32;
using System.Runtime.Versioning;

namespace StS2ModManager.Core;

public sealed partial class GameInstallLocator
{
    private static readonly string[] PreferredExecutableNames =
    [
        "game.exe",
        "Slay the Spire 2.exe",
        "SlayTheSpire2.exe"
    ];

    public IReadOnlyList<string> DiscoverGameRoots()
    {
        return DiscoverGameRoots(GetSteamRoots());
    }

    public IReadOnlyList<string> DiscoverGameRoots(IEnumerable<string> steamRoots)
    {
        ArgumentNullException.ThrowIfNull(steamRoots);

        var results = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var steamRoot in steamRoots)
        {
            var normalizedSteamRoot = NormalizePath(steamRoot);
            if (normalizedSteamRoot is null)
            {
                continue;
            }

            AddCandidate(results, seen, Path.Combine(normalizedSteamRoot, "steamapps", "common", "Slay the Spire 2"));

            foreach (var libraryRoot in ReadLibraryRoots(normalizedSteamRoot))
            {
                AddCandidate(results, seen, Path.Combine(libraryRoot, "steamapps", "common", "Slay the Spire 2"));
            }
        }

        return results;
    }

    public GameLaunchInfo? TryCreateLaunchInfo(string? gameRoot)
    {
        var normalizedRoot = NormalizePath(gameRoot);
        if (normalizedRoot is null || !Directory.Exists(normalizedRoot))
        {
            return null;
        }

        foreach (var executableName in PreferredExecutableNames)
        {
            var executablePath = Path.Combine(normalizedRoot, executableName);
            if (File.Exists(executablePath))
            {
                return new GameLaunchInfo(executablePath, [], normalizedRoot);
            }
        }

        try
        {
            var executablePaths = Directory.GetFiles(normalizedRoot, "*.exe", SearchOption.TopDirectoryOnly);
            if (executablePaths.Length == 1)
            {
                return new GameLaunchInfo(executablePaths[0], [], normalizedRoot);
            }
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }

        return null;
    }

    private void AddCandidate(ICollection<string> results, ISet<string> seen, string gameRoot)
    {
        var normalizedRoot = NormalizePath(gameRoot);
        if (normalizedRoot is null || !seen.Add(normalizedRoot))
        {
            return;
        }

        if (TryCreateLaunchInfo(normalizedRoot) is not null)
        {
            results.Add(normalizedRoot);
        }
    }

    private static IEnumerable<string> GetSteamRoots()
    {
        if (OperatingSystem.IsWindows())
        {
            foreach (var registryRoot in ReadSteamRootsFromRegistry())
            {
                yield return registryRoot;
            }
        }

        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        if (!string.IsNullOrWhiteSpace(programFilesX86))
        {
            yield return Path.Combine(programFilesX86, "Steam");
        }

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        if (!string.IsNullOrWhiteSpace(programFiles))
        {
            yield return Path.Combine(programFiles, "Steam");
        }
    }

    [SupportedOSPlatform("windows")]
    private static IEnumerable<string> ReadSteamRootsFromRegistry()
    {
        foreach (var keyPath in new[]
        {
            @"HKEY_CURRENT_USER\Software\Valve\Steam",
            @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam",
            @"HKEY_LOCAL_MACHINE\SOFTWARE\Valve\Steam"
        })
        {
            var value = Registry.GetValue(keyPath, "SteamPath", null) as string
                ?? Registry.GetValue(keyPath, "InstallPath", null) as string;

            if (!string.IsNullOrWhiteSpace(value))
            {
                yield return value;
            }
        }
    }

    private static IEnumerable<string> ReadLibraryRoots(string steamRoot)
    {
        var libraryFoldersPath = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
        if (!File.Exists(libraryFoldersPath))
        {
            yield break;
        }

        string content;
        try
        {
            content = File.ReadAllText(libraryFoldersPath);
        }
        catch (IOException)
        {
            yield break;
        }
        catch (UnauthorizedAccessException)
        {
            yield break;
        }

        foreach (Match match in LibraryPathRegex().Matches(content))
        {
            var libraryRoot = NormalizePath(match.Groups["path"].Value.Replace(@"\\", @"\"));
            if (!string.IsNullOrWhiteSpace(libraryRoot))
            {
                yield return libraryRoot;
            }
        }
    }

    private static string? NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            return Path.GetFullPath(path.Trim().Trim('"'));
        }
        catch (Exception) when (path is not null)
        {
            return null;
        }
    }

    [GeneratedRegex("\"path\"\\s*\"(?<path>[^\"]+)\"", RegexOptions.IgnoreCase)]
    private static partial Regex LibraryPathRegex();
}
