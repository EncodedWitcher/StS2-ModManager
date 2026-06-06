using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;
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
    private readonly ModImportResolver _resolver = new();
    private readonly ModInstaller _installer = new();
    private readonly GameProcessLauncher _launcher = new();
    private bool _isBusy;
    private bool _syncingSelection;

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
        if (!SyncAndReload("保存 MOD 状态失败"))
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

    // ===== MOD 启用 / 禁用（双开门移动）=====

    private void ToggleMod_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ModItemViewModel mod })
        {
            MoveMod(mod, !mod.IsEnabled);
        }
    }

    private void ModItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ModItemViewModel mod })
        {
            MoveMod(mod, !mod.IsEnabled);
        }
    }

    private void MoveMod(ModItemViewModel mod, bool enable)
    {
        if (_isBusy || mod.IsEnabled == enable)
        {
            return;
        }

        mod.IsEnabled = enable;
        SyncAndReload("保存 MOD 状态失败");
    }

    // ===== 选中态：两个门共享一个“当前选中 MOD” =====

    private void ModList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingSelection || sender is not ListView list)
        {
            return;
        }

        if (list.SelectedItem is ModItemViewModel selected)
        {
            _syncingSelection = true;
            var other = ReferenceEquals(list, EnabledList) ? DisabledList : EnabledList;
            other.SelectedItem = null;
            _syncingSelection = false;
            _viewModel.SelectedMod = selected;
        }
        else if (EnabledList.SelectedItem is null && DisabledList.SelectedItem is null)
        {
            _viewModel.SelectedMod = null;
        }
    }

    private void SelectModByName(string folderName)
    {
        var item = _viewModel.Mods.FirstOrDefault(mod =>
            string.Equals(mod.FolderName, folderName, StringComparison.OrdinalIgnoreCase));
        if (item is null)
        {
            return;
        }

        var list = item.IsEnabled ? EnabledList : DisabledList;
        list.SelectedItem = item;
        list.ScrollIntoView(item);
    }

    // ===== MOD 详情 =====

    private void ModInfo_Click(object sender, RoutedEventArgs e)
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

    // ===== 刷新 / 自动检测 =====

    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        ReloadMods();
    }

    private void Window_Activated(object sender, EventArgs e)
    {
        if (_isBusy || !IsLoaded)
        {
            return;
        }

        try
        {
            var scanned = _scanner.Scan(_viewModel.ModPaths);
            if (!ModSetMatches(scanned))
            {
                _viewModel.ReloadMods(scanned, _config);
            }
        }
        catch
        {
            // 后台自动刷新失败时静默忽略，避免打断用户操作。
        }
    }

    private bool ModSetMatches(IReadOnlyList<ModEntry> scanned)
    {
        var current = _viewModel.Mods.Select(mod => (mod.FolderName, mod.IsEnabled)).ToHashSet();
        var incoming = scanned.Select(mod => (mod.Name, mod.IsEnabled)).ToHashSet();
        return current.SetEquals(incoming);
    }

    // ===== 添加 mod =====

    private void AddMod_Click(object sender, RoutedEventArgs e)
    {
        OpenMenu(sender);
    }

    private void AddFromZip_Click(object sender, RoutedEventArgs e)
    {
        AddMod(PickZip());
    }

    private void AddFromFolder_Click(object sender, RoutedEventArgs e)
    {
        AddMod(PickFolder());
    }

    private void AddMod(string? source)
    {
        if (_isBusy || string.IsNullOrWhiteSpace(source))
        {
            return;
        }

        try
        {
            _isBusy = true;
            using var package = _resolver.Resolve(source);
            var confirmed = ConfirmDialog.Show(
                this,
                "添加 mod",
                $"检测到 MOD：{package.SuggestedName}\n{DescribePackage(package)}\n\n将添加到 mods_disabled（默认未启用）。确认添加？",
                "添加",
                "取消");
            if (!confirmed)
            {
                return;
            }

            var installedName = _installer.Install(_viewModel.ModPaths, package, enabled: false);
            ReloadMods();
            SelectModByName(installedName);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "添加 mod 失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _isBusy = false;
        }
    }

    // ===== 更新 mod =====

    private void UpdateMod_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedMod is null)
        {
            MessageBox.Show(this, "请先在列表中选择要更新的 MOD。", "更新 mod", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        OpenMenu(sender);
    }

    private void UpdateFromZip_Click(object sender, RoutedEventArgs e)
    {
        UpdateMod(PickZip());
    }

    private void UpdateFromFolder_Click(object sender, RoutedEventArgs e)
    {
        UpdateMod(PickFolder());
    }

    private void UpdateMod(string? source)
    {
        var target = _viewModel.SelectedMod;
        if (_isBusy || target is null || string.IsNullOrWhiteSpace(source))
        {
            return;
        }

        try
        {
            _isBusy = true;
            using var package = _resolver.Resolve(source);
            var confirmed = ConfirmDialog.Show(
                this,
                "更新 mod",
                $"将用所选内容替换 MOD：{target.FolderName}\n新内容检测为：{package.SuggestedName}\n{DescribePackage(package)}\n\n更新会保留原有的文件夹名与启用状态，确认更新？",
                "更新",
                "取消");
            if (!confirmed)
            {
                return;
            }

            _installer.Update(target.SourcePath, package);
            var folderName = target.FolderName;
            ReloadMods();
            SelectModByName(folderName);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "更新 mod 失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _isBusy = false;
        }
    }

    // ===== 卸载 mod =====

    private void UninstallMod_Click(object sender, RoutedEventArgs e)
    {
        var target = _viewModel.SelectedMod;
        if (target is null)
        {
            MessageBox.Show(this, "请先在列表中选择要卸载的 MOD。", "卸载 mod", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (_isBusy)
        {
            return;
        }

        var confirmed = ConfirmDialog.Show(
            this,
            "卸载 mod",
            $"确认卸载（删除）MOD：{target.FolderName}？\n该操作会从磁盘删除整个 MOD 文件夹，无法恢复。",
            "卸载",
            "取消");
        if (!confirmed)
        {
            return;
        }

        try
        {
            _isBusy = true;
            _installer.Uninstall(target.SourcePath);
            _config.RemoveMod(target.FolderName);
            _configStore.Save(_config);
            _viewModel.ReloadProfiles(_config);
            ReloadMods();
        }
        catch (Exception ex)
        {
            ReloadMods();
            MessageBox.Show(this, ex.Message, "卸载 mod 失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _isBusy = false;
        }
    }

    // ===== 配置组合 =====

    private void ProfileComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // 程序内部刷新选中项时 _isBusy 为真，跳过，避免回环。
        if (_isBusy)
        {
            return;
        }

        var profileName = ProfileComboBox.SelectedItem as string;

        // 空白项不自动应用：清空操作交给“应用”按钮显式确认。
        if (string.IsNullOrEmpty(profileName)
            || string.Equals(profileName, MainWindowViewModel.BlankProfileOption, StringComparison.Ordinal)
            || !_config.Profiles.TryGetValue(profileName, out var enabledFolderNames))
        {
            return;
        }

        // 当前启用状态已经与该组合一致（多为程序刷新或手动拨动导致的选中）：无需重复应用。
        if (CurrentEnabledMatches(enabledFolderNames))
        {
            return;
        }

        // 选中具名组合即无感应用。
        ApplyEnabledSet(enabledFolderNames, "应用配置组合失败");
    }

    private void ApplyProfile_Click(object sender, RoutedEventArgs e)
    {
        // “应用”按钮只在空白/未保存状态下可见，作用是清空当前所有已启用 MOD。
        if (!_viewModel.IsBlankProfileSelected || _viewModel.EnabledModCount == 0)
        {
            return;
        }

        var confirmed = ConfirmDialog.Show(
            this,
            "应用空白组合",
            "应用“空白”组合会把当前所有已启用的 MOD 移回未启用。确认清空？",
            "清空",
            "取消");
        if (!confirmed)
        {
            return;
        }

        ApplyEnabledSet(Array.Empty<string>(), "清空已启用 MOD 失败");
    }

    private void ApplyEnabledSet(IReadOnlyList<string> enabledFolderNames, string errorTitle)
    {
        if (_isBusy)
        {
            return;
        }

        try
        {
            _isBusy = true;
            var mods = _scanner.Scan(_viewModel.ModPaths);
            var selections = _profileApplier.BuildExactSelections(mods, enabledFolderNames);
            _synchronizer.Sync(_viewModel.ModPaths, selections);
            ReloadMods();
        }
        catch (Exception ex)
        {
            ReloadMods();
            MessageBox.Show(this, ex.Message, errorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _isBusy = false;
        }
    }

    private bool CurrentEnabledMatches(IEnumerable<string> profileEnabledNames)
    {
        var current = new HashSet<string>(_viewModel.EnabledFolderNames, StringComparer.OrdinalIgnoreCase);
        var target = new HashSet<string>(
            profileEnabledNames.Where(name => !string.IsNullOrWhiteSpace(name)),
            StringComparer.OrdinalIgnoreCase);
        return current.SetEquals(target);
    }

    private void SaveProfile_Click(object sender, RoutedEventArgs e)
    {
        var initialName = _viewModel.HasSelectedProfile ? _viewModel.SelectedProfileName : null;
        var dialog = new ProfileNameDialog(initialName)
        {
            Owner = this
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        if (string.Equals(dialog.ProfileNameValue, MainWindowViewModel.BlankProfileOption, StringComparison.Ordinal))
        {
            MessageBox.Show(this, $"“{MainWindowViewModel.BlankProfileOption}”是保留名称，请换一个组合名。", "保存配置组合", MessageBoxButton.OK, MessageBoxImage.Warning);
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

        var confirmed = ConfirmDialog.Show(this, "删除组合", $"删除配置组合“{profileName}”？", "删除", "取消");
        if (!confirmed)
        {
            return;
        }

        _config.Profiles.Remove(profileName);
        _configStore.Save(_config);
        _viewModel.ReloadProfiles(_config);
    }

    // ===== 打开文件夹 =====

    private void OpenEnabledMods_Click(object sender, RoutedEventArgs e)
    {
        OpenDirectory(_viewModel.ModPaths.EnabledDirectory);
    }

    private void OpenDisabledMods_Click(object sender, RoutedEventArgs e)
    {
        OpenDirectory(_viewModel.ModPaths.DisabledDirectory);
    }

    // ===== 公共辅助 =====

    private bool SyncAndReload(string errorTitle)
    {
        if (_isBusy)
        {
            return false;
        }

        try
        {
            _isBusy = true;
            _synchronizer.Sync(_viewModel.ModPaths, _viewModel.GetSelections());
            ReloadMods();
            return true;
        }
        catch (Exception ex)
        {
            ReloadMods();
            MessageBox.Show(this, ex.Message, errorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
        finally
        {
            _isBusy = false;
        }
    }

    private void ReloadMods()
    {
        _viewModel.ReloadMods(_scanner.Scan(_viewModel.ModPaths), _config);
    }

    private static void OpenMenu(object sender)
    {
        if (sender is Button { ContextMenu: { } menu } button)
        {
            menu.PlacementTarget = button;
            menu.IsOpen = true;
        }
    }

    private string? PickZip()
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择 MOD 压缩包",
            Filter = "压缩包 (*.zip)|*.zip"
        };

        return dialog.ShowDialog(this) == true ? dialog.FileName : null;
    }

    private string? PickFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "选择 MOD 文件夹"
        };

        return dialog.ShowDialog(this) == true ? dialog.FolderName : null;
    }

    private static string DescribePackage(ResolvedModPackage package)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(package.Metadata.Author))
        {
            parts.Add($"作者：{package.Metadata.Author}");
        }

        if (!string.IsNullOrWhiteSpace(package.Metadata.Version))
        {
            parts.Add($"版本：{package.Metadata.Version}");
        }

        return parts.Count > 0 ? string.Join("　", parts) : "（未在 manifest 中读取到 name/author 信息）";
    }

    private void OpenDirectory(string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);
            Process.Start(new ProcessStartInfo
            {
                FileName = directory,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "打开文件夹失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
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
