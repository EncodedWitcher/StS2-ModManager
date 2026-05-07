using System.Windows;

namespace StS2ModManager;

public partial class ProfileNameDialog : Window
{
    public ProfileNameDialog(string? initialName)
    {
        InitializeComponent();
        ProfileNameBox.Text = string.IsNullOrWhiteSpace(initialName) ? "组合1" : initialName;
        ProfileNameBox.SelectAll();
        ProfileNameBox.Focus();
    }

    public string ProfileNameValue => ProfileNameBox.Text.Trim();

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ProfileNameValue))
        {
            MessageBox.Show(this, "配置组合名称不能为空。", "StS2 Mod Manager", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
