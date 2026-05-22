using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using StS2ModManager.Core;

namespace StS2ModManager;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly AppConfig _config;
    private string? _selectedProfileName;

    public MainWindowViewModel(GameLaunchInfo launchInfo, ModPaths modPaths, IReadOnlyList<ModEntry> mods, AppConfig config)
    {
        _config = config;
        LaunchInfo = launchInfo;
        ModPaths = modPaths;
        Mods = new ObservableCollection<ModItemViewModel>();
        ProfileNames = new ObservableCollection<string>();
        ReloadMods(mods, config);
        ReloadProfiles(config);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public GameLaunchInfo LaunchInfo { get; }

    public ModPaths ModPaths { get; }

    public ObservableCollection<ModItemViewModel> Mods { get; }

    public ObservableCollection<string> ProfileNames { get; }

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
        }
    }

    public string ModCountText => $"{EnabledModCount}/{Mods.Count} MODS";

    public int EnabledModCount => Mods.Count(mod => mod.IsEnabled);

    public bool HasSelectedProfile => !string.IsNullOrWhiteSpace(SelectedProfileName);

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
        Mods.Clear();
        foreach (var mod in mods)
        {
            var item = new ModItemViewModel(mod, config.GetModNote(mod.Name));
            item.PropertyChanged += ModItem_PropertyChanged;
            Mods.Add(item);
        }

        RefreshProfileSelection(config);
        OnPropertyChanged(nameof(ModCountText));
        OnPropertyChanged(nameof(EnabledModCount));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(StatusVisibility));
    }

    public void ReloadModsPreservingOrder(IReadOnlyList<ModEntry> mods, AppConfig config)
    {
        var previousIndexes = Mods
            .Select((mod, index) => new { mod.FolderName, Index = index })
            .ToDictionary(mod => mod.FolderName, mod => mod.Index, StringComparer.OrdinalIgnoreCase);

        var orderedMods = mods
            .Select((mod, scanIndex) => new { Mod = mod, ScanIndex = scanIndex })
            .OrderBy(item => previousIndexes.TryGetValue(item.Mod.Name, out var index) ? index : int.MaxValue)
            .ThenBy(item => item.ScanIndex)
            .Select(item => item.Mod)
            .ToArray();

        ReloadMods(orderedMods, config);
    }

    public void ReloadProfiles(AppConfig config)
    {
        ProfileNames.Clear();
        foreach (var profileName in config.Profiles.Keys.OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
        {
            ProfileNames.Add(profileName);
        }

        RefreshProfileSelection(config);
    }

    public IReadOnlyList<ModSelection> GetSelections()
    {
        return Mods
            .Select(mod => new ModSelection(mod.FolderName, mod.IsEnabled))
            .ToArray();
    }

    private void ModItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!string.Equals(e.PropertyName, nameof(ModItemViewModel.IsEnabled), StringComparison.Ordinal))
        {
            return;
        }

        OnPropertyChanged(nameof(EnabledModCount));
        OnPropertyChanged(nameof(ModCountText));
        RefreshProfileSelection(_config);
    }

    private void RefreshProfileSelection(AppConfig config)
    {
        SelectedProfileName = config.FindMatchingProfileName(EnabledFolderNames, SelectedProfileName);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
