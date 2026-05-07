using System.Windows;

namespace StS2ModManager;

public partial class ModDetailsWindow : Window
{
    public ModDetailsWindow(ModItemViewModel mod)
    {
        InitializeComponent();

        NameText.Text = DisplayValue(mod.Metadata.Name);
        AuthorText.Text = DisplayValue(mod.Metadata.Author);
        DescriptionText.Text = DisplayValue(mod.Metadata.Description);
        VersionText.Text = DisplayValue(mod.Metadata.Version);
        FolderNameBox.Text = mod.FolderName;
        NoteBox.Text = mod.Note;
    }

    public string FolderNameValue => FolderNameBox.Text.Trim();

    public string NoteValue => NoteBox.Text.Trim();

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private static string DisplayValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "-" : value;
    }
}
