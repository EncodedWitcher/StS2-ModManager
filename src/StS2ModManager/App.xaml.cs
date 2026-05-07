using System.IO;
using System.Windows;
using StS2ModManager.Core;

namespace StS2ModManager;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            var configStore = AppConfigStore.CreateDefault();
            var executablePath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(executablePath)
                || !InstallDirectoryGuard.IsInstallDirectoryClean(executablePath, configStore.ConfigPath))
            {
                MessageBox.Show(
                    "请将 StS2 Mod Manager 解压到单独的空文件夹中运行。\n\n该文件夹只允许包含 StS2ModManager.exe、使用说明.txt 和 config/config.json。",
                    "StS2 Mod Manager",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                Shutdown(1);
                return;
            }

            var config = configStore.Load();
            var parsedLaunchInfo = CommandLineParser.Parse(e.Args);
            var launchInfo = parsedLaunchInfo ?? config.ToLaunchInfoIfValid();

            if (launchInfo is null)
            {
                MessageBox.Show(
                    "未从 Steam 启动参数中找到游戏 exe，且没有可用的上次启动记录。\n\n请通过 Steam 启动项运行管理器一次。",
                    "StS2 Mod Manager",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Shutdown(1);
                return;
            }

            if (!File.Exists(launchInfo.ExecutablePath))
            {
                MessageBox.Show(
                    "游戏 exe 不存在。请通过 Steam 启动项重新运行管理器以刷新启动记录。",
                    "StS2 Mod Manager",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Shutdown(1);
                return;
            }

            if (parsedLaunchInfo is not null)
            {
                config.ApplyLaunchInfo(parsedLaunchInfo);
                configStore.Save(config);
            }

            var paths = ModPaths.FromGameRoot(launchInfo.WorkingDirectory);
            var mods = new ModScanner().Scan(paths);
            var viewModel = new MainWindowViewModel(launchInfo, paths, mods, config);
            var window = new MainWindow(viewModel, config, configStore);
            MainWindow = window;
            window.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.Message,
                "StS2 Mod Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }
}
