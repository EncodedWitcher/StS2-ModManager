using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using StS2ModManager.Core;

namespace StS2ModManager;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private string? _selectedProfileName;

    public MainWindowViewModel(GameLaunchInfo launchInfo, ModPaths modPaths, IReadOnlyList<ModEntry> mods, AppConfig config)
    {
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

    public string ModCountText => $"{Mods.Count} MODS";

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
            Mods.Add(new ModItemViewModel(mod, config.GetModNote(mod.Name)));
        }

        OnPropertyChanged(nameof(ModCountText));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(StatusVisibility));
    }

    public void ReloadProfiles(AppConfig config)
    {
        var previousSelection = SelectedProfileName;
        ProfileNames.Clear();
        foreach (var profileName in config.Profiles.Keys.OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
        {
            ProfileNames.Add(profileName);
        }

        SelectedProfileName = previousSelection is not null && ProfileNames.Contains(previousSelection)
            ? previousSelection
            : ProfileNames.FirstOrDefault();

        OnPropertyChanged(nameof(HasSelectedProfile));
    }

    public IReadOnlyList<ModSelection> GetSelections()
    {
        return Mods
            .Select(mod => new ModSelection(mod.FolderName, mod.IsEnabled))
            .ToArray();
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
