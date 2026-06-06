using System.Windows;
using System.Windows.Controls;

namespace StS2ModManager;

/// <summary>
/// 保存配置组合对话框：可从列表中选择要覆盖的组合，或点击“新组合”输入名称创建新组合。
/// 点右上角的叉或“取消”视为否。
/// </summary>
public partial class SaveProfileDialog : Window
{
    private bool _creatingNew;

    public SaveProfileDialog(IEnumerable<string> existingProfileNames, string? preselect)
    {
        InitializeComponent();

        foreach (var name in existingProfileNames)
        {
            ProfileList.Items.Add(name);
        }

        if (ProfileList.Items.Count == 0)
        {
            EmptyHint.Visibility = Visibility.Visible;
            EnterNewMode();
        }
        else if (!string.IsNullOrEmpty(preselect) && ProfileList.Items.Contains(preselect))
        {
            ProfileList.SelectedItem = preselect;
        }
    }

    public string SelectedProfileName { get; private set; } = string.Empty;

    private void NewProfile_Click(object sender, RoutedEventArgs e)
    {
        EnterNewMode();
    }

    private void ProfileList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // 用户点了一个已有组合：切回“覆盖”模式。
        if (ProfileList.SelectedItem is not null)
        {
            _creatingNew = false;
            NewNamePanel.Visibility = Visibility.Collapsed;
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        string name;
        if (_creatingNew)
        {
            name = NewNameBox.Text.Trim();
            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show(this, "请输入新组合的名称。", "保存为配置组合", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }
        else if (ProfileList.SelectedItem is string selected)
        {
            name = selected;
        }
        else
        {
            MessageBox.Show(this, "请选择要覆盖的组合，或点击“新组合”。", "保存为配置组合", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        SelectedProfileName = name;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void EnterNewMode()
    {
        _creatingNew = true;
        ProfileList.SelectedItem = null;
        NewNamePanel.Visibility = Visibility.Visible;
        NewNameBox.Focus();
    }
}
