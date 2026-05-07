using System.Windows;
using StS2ModManager.Core;

namespace StS2ModManager;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;
    private readonly AppConfig _config;
    private readonly AppConfigStore _configStore;
    private readonly ModScanner _scanner = new();
    private readonly ModSynchronizer _synchronizer = new();
    private readonly ModFolderRenamer _renamer = new();
    private readonly ModProfileApplier _profileApplier = new();
    private readonly GameProcessLauncher _launcher = new();
    private bool _isSyncing;

    public MainWindow(MainWindowViewModel viewModel, AppConfig config, AppConfigStore configStore)
    {
        _viewModel = viewModel;
        _config = config;
        _configStore = configStore;
        InitializeComponent();
        DataContext = viewModel;
        RestoreWindowBounds();
    }

    private async void SaveAndLaunch_Click(object sender, RoutedEventArgs e)
    {
        if (!TrySyncMods())
        {
            return;
        }

        try
        {
            var process = _launcher.Start(_viewModel.LaunchInfo);
            Hide();

            var exitCode = await Task.Run(() =>
            {
                process.WaitForExit();
                return process.ExitCode;
            });

            process.Dispose();
            Application.Current.Shutdown(exitCode);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "启动游戏失败", MessageBoxButton.OK, MessageBoxImage.Error);
            Show();
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ModToggle_Click(object sender, RoutedEventArgs e)
    {
        TrySyncMods();
    }

    private void ModDetails_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: ModItemViewModel mod })
        {
            return;
        }

        var dialog = new ModDetailsWindow(mod)
        {
            Owner = this
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            var newFolderName = _renamer.Rename(mod.SourcePath, mod.FolderName, dialog.FolderNameValue);
            _config.RenameModKey(mod.FolderName, newFolderName);
            _config.SetModNote(newFolderName, dialog.NoteValue);
            _configStore.Save(_config);
            ReloadMods();
        }
        catch (Exception ex)
        {
            ReloadMods();
            MessageBox.Show(this, ex.Message, "保存 MOD 信息失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ApplyProfile_Click(object sender, RoutedEventArgs e)
    {
        var profileName = _viewModel.SelectedProfileName;
        if (string.IsNullOrWhiteSpace(profileName) || !_config.Profiles.TryGetValue(profileName, out var enabledFolderNames))
        {
            return;
        }

        try
        {
            var mods = _scanner.Scan(_viewModel.ModPaths);
            var selections = _profileApplier.BuildExactSelections(mods, enabledFolderNames);
            _synchronizer.Sync(_viewModel.ModPaths, selections);
            ReloadMods();
        }
        catch (Exception ex)
        {
            ReloadMods();
            MessageBox.Show(this, ex.Message, "应用配置组合失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SaveProfile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ProfileNameDialog(_viewModel.SelectedProfileName)
        {
            Owner = this
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        _config.Profiles[dialog.ProfileNameValue] = _viewModel.EnabledFolderNames.ToArray();
        _configStore.Save(_config);
        _viewModel.ReloadProfiles(_config);
        _viewModel.SelectedProfileName = dialog.ProfileNameValue;
    }

    private void DeleteProfile_Click(object sender, RoutedEventArgs e)
    {
        var profileName = _viewModel.SelectedProfileName;
        if (string.IsNullOrWhiteSpace(profileName))
        {
            return;
        }

        var result = MessageBox.Show(this, $"删除配置组合“{profileName}”？", "StS2 Mod Manager", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        _config.Profiles.Remove(profileName);
        _configStore.Save(_config);
        _viewModel.ReloadProfiles(_config);
    }

    private bool TrySyncMods()
    {
        if (_isSyncing)
        {
            return true;
        }

        try
        {
            _isSyncing = true;
            _synchronizer.Sync(_viewModel.ModPaths, _viewModel.GetSelections());
            ReloadMods();
            return true;
        }
        catch (Exception ex)
        {
            ReloadMods();
            MessageBox.Show(this, ex.Message, "保存 MOD 状态失败", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
        finally
        {
            _isSyncing = false;
        }
    }

    private void ReloadMods()
    {
        _viewModel.ReloadMods(_scanner.Scan(_viewModel.ModPaths), _config);
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        SaveWindowBounds();
        base.OnClosing(e);
    }

    private void RestoreWindowBounds()
    {
        var bounds = _config.WindowBounds;
        if (bounds is null || !IsVisibleOnCurrentDesktop(bounds))
        {
            return;
        }

        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = bounds.Left;
        Top = bounds.Top;
        Width = Math.Max(MinWidth, bounds.Width);
        Height = Math.Max(MinHeight, bounds.Height);
        WindowState = WindowState.Normal;
    }

    private void SaveWindowBounds()
    {
        if (WindowState != WindowState.Normal)
        {
            return;
        }

        _config.WindowBounds = new WindowBounds(Left, Top, Width, Height);
        _configStore.Save(_config);
    }

    private static bool IsVisibleOnCurrentDesktop(WindowBounds bounds)
    {
        var desktopLeft = SystemParameters.VirtualScreenLeft;
        var desktopTop = SystemParameters.VirtualScreenTop;
        var desktopRight = desktopLeft + SystemParameters.VirtualScreenWidth;
        var desktopBottom = desktopTop + SystemParameters.VirtualScreenHeight;

        var windowRight = bounds.Left + bounds.Width;
        var windowBottom = bounds.Top + bounds.Height;
        var overlapWidth = Math.Min(windowRight, desktopRight) - Math.Max(bounds.Left, desktopLeft);
        var overlapHeight = Math.Min(windowBottom, desktopBottom) - Math.Max(bounds.Top, desktopTop);

        return overlapWidth > 80 && overlapHeight > 80;
    }
}
