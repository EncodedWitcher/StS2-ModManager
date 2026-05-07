using System.Text.Json;

namespace StS2ModManager.Core;

public sealed class AppConfigStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public AppConfigStore(string configPath, string? legacyConfigPath = null)
    {
        ConfigPath = configPath;
        LegacyConfigPath = legacyConfigPath;
    }

    public string ConfigPath { get; }

    public string? LegacyConfigPath { get; }

    public static AppConfigStore CreateDefault()
    {
        return new AppConfigStore(GetDefaultConfigPath(), GetLegacyConfigPath());
    }

    public static AppConfigStore CreateForExecutable(string executablePath)
    {
        return new AppConfigStore(GetDefaultConfigPath(executablePath), GetLegacyConfigPath());
    }

    public static string GetDefaultConfigPath(string? executablePath = null)
    {
        var processPath = executablePath ?? Environment.ProcessPath;
        var executableDirectory = !string.IsNullOrWhiteSpace(processPath)
            ? Path.GetDirectoryName(processPath)
            : AppContext.BaseDirectory;

        if (string.IsNullOrWhiteSpace(executableDirectory))
        {
            executableDirectory = AppContext.BaseDirectory;
        }

        return Path.Combine(executableDirectory, "config", "config.json");
    }

    public static string GetLegacyConfigPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "StS2-ModManager", "config.json");
    }

    public AppConfig Load()
    {
        if (File.Exists(ConfigPath))
        {
            return ReadConfig(ConfigPath);
        }

        var config = LegacyConfigPath is not null && File.Exists(LegacyConfigPath)
            ? ReadConfig(LegacyConfigPath)
            : new AppConfig();

        Save(config);
        return config;
    }

    public void Save(AppConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        config.Normalize();
        var directory = Path.GetDirectoryName(ConfigPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(config, JsonOptions);
        File.WriteAllText(ConfigPath, json);
    }

    private static AppConfig ReadConfig(string configPath)
    {
        try
        {
            var json = File.ReadAllText(configPath);
            var config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions) ?? new AppConfig();
            config.Normalize();
            return config;
        }
        catch (JsonException)
        {
            return new AppConfig();
        }
        catch (IOException)
        {
            return new AppConfig();
        }
    }
}
