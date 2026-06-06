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
    private bool _suppressProfileSwitch;
    private string? _activeProfileName;

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
        // 程序内部静默改选时跳过，避免触发提醒/应用。
        if (_isBusy || _suppressProfileSwitch)
        {
            return;
        }

        var newName = ProfileComboBox.SelectedItem as string;
        if (string.IsNullOrEmpty(newName) || !_config.Profiles.TryGetValue(newName, out var newSet))
        {
            _activeProfileName = newName;
            return;
        }

        // req 7：从一个“有未保存改动”的组合切换出去时，提醒是否先保存到原组合。
        if (!string.IsNullOrEmpty(_activeProfileName)
            && !string.Equals(_activeProfileName, newName, StringComparison.OrdinalIgnoreCase)
            && _config.Profiles.TryGetValue(_activeProfileName, out var oldSet)
            && !CurrentEnabledMatches(oldSet))
        {
            var save = ConfirmDialog.Show(
                this,
                "未保存的改动",
                $"组合「{_activeProfileName}」有未保存的改动。\n要先把当前已启用的 MOD 保存到该组合吗？",
                "保存",
                "不保存");
            if (save)
            {
                _config.Profiles[_activeProfileName] = _viewModel.EnabledFolderNames.ToArray();
                _configStore.Save(_config);
            }
        }

        // 选中具名组合即应用（当前状态已一致时跳过多余的同步与刷新）。
        if (!CurrentEnabledMatches(newSet))
        {
            ApplyEnabledSet(newSet, "应用配置组合失败");
        }

        _activeProfileName = newName;
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        // “清空”按钮常驻：把当前所有已启用的 MOD 移回未启用。
        if (_isBusy || _viewModel.EnabledModCount == 0)
        {
            return;
        }

        var confirmed = ConfirmDialog.Show(
            this,
            "清空",
            "确定把当前所有已启用的 MOD 移回未启用吗？",
            "清空",
            "取消");
        if (!confirmed)
        {
            return;
        }

        ApplyEnabledSet(Array.Empty<string>(), "清空已启用 MOD 失败");
        SetSelectedProfileSilently(null);
        _activeProfileName = null;
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
        var dialog = new SaveProfileDialog(_viewModel.ProfileNames, _activeProfileName)
        {
            Owner = this
        };

        // 叉 / 取消 视为否。
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var name = dialog.SelectedProfileName;
        _config.Profiles[name] = _viewModel.EnabledFolderNames.ToArray();
        _configStore.Save(_config);
        _viewModel.ReloadProfiles(_config);
        SetSelectedProfileSilently(name);
        _activeProfileName = name;
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
        if (string.Equals(_activeProfileName, profileName, StringComparison.OrdinalIgnoreCase))
        {
            _activeProfileName = null;
        }

        _viewModel.ReloadProfiles(_config);
    }

    private void SetSelectedProfileSilently(string? name)
    {
        _suppressProfileSwitch = true;
        try
        {
            _viewModel.SelectedProfileName = name;
        }
        finally
        {
            _suppressProfileSwitch = false;
        }
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
