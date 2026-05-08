namespace StS2ModManager.Core;

public sealed class GameLaunchInfoResolver
{
    public GameLaunchInfo? Resolve(GameLaunchInfo? parsedLaunchInfo, AppConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        if (IsUsable(parsedLaunchInfo))
        {
            return parsedLaunchInfo;
        }

        var cachedLaunchInfo = config.ToLaunchInfoIfValid();
        return IsUsable(cachedLaunchInfo) ? cachedLaunchInfo : null;
    }

    private static bool IsUsable(GameLaunchInfo? launchInfo)
    {
        return launchInfo is not null
            && File.Exists(launchInfo.ExecutablePath)
            && Directory.Exists(launchInfo.WorkingDirectory);
    }
}
