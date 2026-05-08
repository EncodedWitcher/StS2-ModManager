using System.Windows;
using StS2ModManager.Core;

namespace StS2ModManager;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

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
            var launchInfo = new GameLaunchInfoResolver().Resolve(parsedLaunchInfo, config);

            if (launchInfo is null)
            {
                var startupWindow = new StartupConfigWindow(new GameInstallLocator(), config.LastWorkingDirectory);
                if (startupWindow.ShowDialog() != true || startupWindow.SelectedLaunchInfo is null)
                {
                    Shutdown();
                    return;
                }

                launchInfo = startupWindow.SelectedLaunchInfo;
                config.ApplyLaunchInfo(launchInfo);
                configStore.Save(config);
            }
            else if (parsedLaunchInfo is not null)
            {
                config.ApplyLaunchInfo(parsedLaunchInfo);
                configStore.Save(config);
            }

            var paths = ModPaths.FromGameRoot(launchInfo.WorkingDirectory);
            var mods = new ModScanner().Scan(paths);
            var viewModel = new MainWindowViewModel(launchInfo, paths, mods, config);
            var window = new MainWindow(viewModel, config, configStore);
            MainWindow = window;
            ShutdownMode = ShutdownMode.OnMainWindowClose;
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
