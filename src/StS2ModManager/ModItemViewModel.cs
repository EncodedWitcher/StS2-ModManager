using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using StS2ModManager.Core;

namespace StS2ModManager;

public sealed class ModItemViewModel : INotifyPropertyChanged
{
    private bool _isEnabled;

    public ModItemViewModel(ModEntry entry, string note)
    {
        FolderName = entry.Name;
        SourcePath = entry.SourcePath;
        Metadata = entry.Metadata;
        Note = note;
        HasConflict = entry.HasConflict;
        _isEnabled = entry.IsEnabled;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string FolderName { get; }

    public string SourcePath { get; }

    public ModMetadata Metadata { get; }

    public string Note { get; }

    public bool HasConflict { get; }

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (_isEnabled == value)
            {
                return;
            }

            _isEnabled = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(StateText));
        }
    }

    public string DisplayName => string.IsNullOrWhiteSpace(Metadata.Version)
        ? FolderName
        : $"{FolderName} [{Metadata.Version}]";

    public Visibility NoteVisibility => string.IsNullOrWhiteSpace(Note)
        ? Visibility.Collapsed
        : Visibility.Visible;

    public string StateText
    {
        get
        {
            if (HasConflict)
            {
                return "冲突";
            }

            return IsEnabled ? "启用" : "禁用";
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
