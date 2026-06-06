using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Data;
using StS2ModManager.Core;

namespace StS2ModManager;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    /// <summary>
    /// 配置组合下拉框里的“空白”占位项：代表“未匹配任何组合”，选中它可清空当前启用配置。
    /// </summary>
    public const string BlankProfileOption = "（空白）";

    private readonly AppConfig _config;
    private readonly CollectionViewSource _enabledSource;
    private readonly CollectionViewSource _disabledSource;
    private string? _selectedProfileName;
    private ModItemViewModel? _selectedMod;

    public MainWindowViewModel(GameLaunchInfo launchInfo, ModPaths modPaths, IReadOnlyList<ModEntry> mods, AppConfig config)
    {
        _config = config;
        LaunchInfo = launchInfo;
        ModPaths = modPaths;
        Mods = new ObservableCollection<ModItemViewModel>();
        ProfileNames = new ObservableCollection<string>();

        _enabledSource = CreateDoorView(enabled: true);
        _disabledSource = CreateDoorView(enabled: false);

        ReloadMods(mods, config);
        ReloadProfiles(config);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public GameLaunchInfo LaunchInfo { get; }

    public ModPaths ModPaths { get; }

    public ObservableCollection<ModItemViewModel> Mods { get; }

    public ICollectionView EnabledMods => _enabledSource.View;

    public ICollectionView DisabledMods => _disabledSource.View;

    public ObservableCollection<string> ProfileNames { get; }

    public ModItemViewModel? SelectedMod
    {
        get => _selectedMod;
        set
        {
            if (ReferenceEquals(_selectedMod, value))
            {
                return;
            }

            _selectedMod = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSelectedMod));
        }
    }

    public bool HasSelectedMod => _selectedMod is not null;

    public string? SelectedProfileName
    {
        get => _selectedProfileName;
        set
        {
            if (_selectedProfileName == value)
            {
                return;
            }

            _selectedProfileName = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSelectedProfile));
            OnPropertyChanged(nameof(IsBlankProfileSelected));
            OnPropertyChanged(nameof(ApplyButtonVisibility));
        }
    }

    public string ModCountText => $"{EnabledModCount}/{Mods.Count} MODS";

    public int EnabledModCount => Mods.Count(mod => mod.IsEnabled);

    public int DisabledModCount => Mods.Count(mod => !mod.IsEnabled);

    public string EnabledHeaderText => $"已启用 · {EnabledModCount}";

    public string DisabledHeaderText => $"未启用 · {DisabledModCount}";

    /// <summary>当前选中的是“空白”占位项（或未选中），而非某个具名配置组合。</summary>
    public bool IsBlankProfileSelected =>
        string.IsNullOrWhiteSpace(SelectedProfileName)
        || string.Equals(SelectedProfileName, BlankProfileOption, StringComparison.Ordinal);

    /// <summary>只有具名配置组合被选中时才算“有选中组合”（用于启用“删除组合”等操作）。</summary>
    public bool HasSelectedProfile => !IsBlankProfileSelected;

    /// <summary>“应用”按钮只在空白/未保存状态下出现：具名组合是无感即时切换，无需确认。</summary>
    public Visibility ApplyButtonVisibility => IsBlankProfileSelected
        ? Visibility.Visible
        : Visibility.Collapsed;

    public string StatusText
    {
        get
        {
            var conflicts = Mods.Count(mod => mod.HasConflict);
            return conflicts > 0
                ? $"检测到 {conflicts} 个同名冲突。请手动清理后再保存。"
                : string.Empty;
        }
    }

    public Visibility StatusVisibility => string.IsNullOrWhiteSpace(StatusText)
        ? Visibility.Collapsed
        : Visibility.Visible;

    public IReadOnlyList<string> EnabledFolderNames => Mods
        .Where(mod => mod.IsEnabled)
        .Select(mod => mod.FolderName)
        .ToArray();

    public void ReloadMods(IReadOnlyList<ModEntry> mods, AppConfig config)
    {
        SelectedMod = null;
        Mods.Clear();
        foreach (var mod in mods)
        {
            Mods.Add(new ModItemViewModel(mod, config.GetModNote(mod.Name)));
        }

        NotifyModsMutated();
    }

    public void ReloadProfiles(AppConfig config)
    {
        ProfileNames.Clear();
        ProfileNames.Add(BlankProfileOption);
        foreach (var profileName in config.Profiles.Keys.OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
        {
            ProfileNames.Add(profileName);
        }

        RefreshProfileSelection(config);
    }

    /// <summary>
    /// 在 MOD 的启用状态、备注或集合内容发生变化后调用：重新分桶两个门、刷新计数与配置组合匹配。
    /// </summary>
    public void NotifyModsMutated()
    {
        EnabledMods.Refresh();
        DisabledMods.Refresh();
        RefreshProfileSelection(_config);
        OnPropertyChanged(nameof(ModCountText));
        OnPropertyChanged(nameof(EnabledModCount));
        OnPropertyChanged(nameof(DisabledModCount));
        OnPropertyChanged(nameof(EnabledHeaderText));
        OnPropertyChanged(nameof(DisabledHeaderText));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(StatusVisibility));
    }

    public IReadOnlyList<ModSelection> GetSelections()
    {
        return Mods
            .Select(mod => new ModSelection(mod.FolderName, mod.IsEnabled))
            .ToArray();
    }

    private CollectionViewSource CreateDoorView(bool enabled)
    {
        var source = new CollectionViewSource { Source = Mods };
        source.Filter += (_, args) => args.Accepted = args.Item is ModItemViewModel mod && mod.IsEnabled == enabled;
        source.SortDescriptions.Add(new SortDescription(nameof(ModItemViewModel.FolderName), ListSortDirection.Ascending));
        return source;
    }

    private void RefreshProfileSelection(AppConfig config)
    {
        var preferred = IsBlankProfileSelected ? null : SelectedProfileName;
        SelectedProfileName = config.FindMatchingProfileName(EnabledFolderNames, preferred) ?? BlankProfileOption;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
