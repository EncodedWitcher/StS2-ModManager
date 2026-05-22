using System.Text.Json.Serialization;

namespace StS2ModManager.Core;

public sealed class AppConfig
{
    public string? LastExecutablePath { get; set; }

    public string[] LastArguments { get; set; } = [];

    public string? LastWorkingDirectory { get; set; }

    public WindowBounds? WindowBounds { get; set; }

    public Dictionary<string, string> ModNotes { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, string[]> Profiles { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonIgnore]
    public bool HasValidLaunchInfo =>
        !string.IsNullOrWhiteSpace(LastExecutablePath)
        && !string.IsNullOrWhiteSpace(LastWorkingDirectory)
        && File.Exists(LastExecutablePath)
        && Directory.Exists(LastWorkingDirectory);

    public static AppConfig FromLaunchInfo(GameLaunchInfo launchInfo)
    {
        ArgumentNullException.ThrowIfNull(launchInfo);

        var config = new AppConfig();
        config.ApplyLaunchInfo(launchInfo);
        return config;
    }

    public void ApplyLaunchInfo(GameLaunchInfo launchInfo)
    {
        ArgumentNullException.ThrowIfNull(launchInfo);

        LastExecutablePath = launchInfo.ExecutablePath;
        LastArguments = launchInfo.Arguments.ToArray();
        LastWorkingDirectory = launchInfo.WorkingDirectory;
    }

    public GameLaunchInfo? ToLaunchInfoIfValid()
    {
        return HasValidLaunchInfo
            ? new GameLaunchInfo(LastExecutablePath!, LastArguments, LastWorkingDirectory!)
            : null;
    }

    public string GetModNote(string folderName)
    {
        return TryGetValue(ModNotes, folderName, out var note) ? note : string.Empty;
    }

    public string? FindMatchingProfileName(IEnumerable<string> enabledFolderNames, string? preferredProfileName = null)
    {
        ArgumentNullException.ThrowIfNull(enabledFolderNames);

        var enabledNames = new HashSet<string>(
            enabledFolderNames.Where(name => !string.IsNullOrWhiteSpace(name)),
            StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(preferredProfileName))
        {
            var preferredKey = FindKey(Profiles, preferredProfileName);
            if (preferredKey is not null && ProfileMatches(enabledNames, Profiles[preferredKey]))
            {
                return preferredKey;
            }
        }

        foreach (var profileName in Profiles.Keys.OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
        {
            if (ProfileMatches(enabledNames, Profiles[profileName]))
            {
                return profileName;
            }
        }

        return null;
    }

    public void SetModNote(string folderName, string note)
    {
        var key = FindKey(ModNotes, folderName) ?? folderName;
        var normalizedNote = note.Trim();
        if (string.IsNullOrWhiteSpace(normalizedNote))
        {
            ModNotes.Remove(key);
            return;
        }

        ModNotes[key] = normalizedNote;
    }

    public void RenameModKey(string oldFolderName, string newFolderName)
    {
        if (string.Equals(oldFolderName, newFolderName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var noteKey = FindKey(ModNotes, oldFolderName);
        if (noteKey is not null)
        {
            var note = ModNotes[noteKey];
            ModNotes.Remove(noteKey);
            ModNotes[newFolderName] = note;
        }

        foreach (var profileName in Profiles.Keys.ToArray())
        {
            Profiles[profileName] = Profiles[profileName]
                .Select(name => string.Equals(name, oldFolderName, StringComparison.OrdinalIgnoreCase) ? newFolderName : name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
    }

    public void Normalize()
    {
        ModNotes = new Dictionary<string, string>(ModNotes ?? [], StringComparer.OrdinalIgnoreCase);
        Profiles = new Dictionary<string, string[]>(Profiles ?? [], StringComparer.OrdinalIgnoreCase);
        LastArguments ??= [];

        foreach (var profileName in Profiles.Keys.ToArray())
        {
            Profiles[profileName] = Profiles[profileName]
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
    }

    private static bool TryGetValue(Dictionary<string, string> values, string key, out string value)
    {
        var existingKey = FindKey(values, key);
        if (existingKey is null)
        {
            value = string.Empty;
            return false;
        }

        value = values[existingKey];
        return true;
    }

    private static bool ProfileMatches(HashSet<string> enabledNames, IEnumerable<string> profileNames)
    {
        var profileSet = new HashSet<string>(
            profileNames.Where(name => !string.IsNullOrWhiteSpace(name)),
            StringComparer.OrdinalIgnoreCase);

        return enabledNames.SetEquals(profileSet);
    }

    private static string? FindKey<TValue>(Dictionary<string, TValue> values, string key)
    {
        return values.Keys.FirstOrDefault(existingKey => string.Equals(existingKey, key, StringComparison.OrdinalIgnoreCase));
    }
}
