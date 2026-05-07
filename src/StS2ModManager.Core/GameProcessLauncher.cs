using System.Diagnostics;

namespace StS2ModManager.Core;

public sealed class GameProcessLauncher
{
    public Process Start(GameLaunchInfo launchInfo)
    {
        ArgumentNullException.ThrowIfNull(launchInfo);

        var startInfo = new ProcessStartInfo
        {
            FileName = launchInfo.ExecutablePath,
            WorkingDirectory = launchInfo.WorkingDirectory,
            UseShellExecute = false
        };

        foreach (var argument in launchInfo.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return Process.Start(startInfo)
            ?? throw new InvalidOperationException("游戏进程启动失败。");
    }
}
