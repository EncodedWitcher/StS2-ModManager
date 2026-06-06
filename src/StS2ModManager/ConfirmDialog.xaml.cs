using System.Windows;

namespace StS2ModManager;

/// <summary>
/// 统一的确认弹窗：点击右上角的叉或按 Esc 都视为“否”（返回 false）。
/// </summary>
public partial class ConfirmDialog : Window
{
    public ConfirmDialog(string title, string message, string positiveText, string negativeText)
    {
        InitializeComponent();
        Title = title;
        TitleText.Text = title;
        MessageText.Text = message;
        PositiveButton.Content = positiveText;
        NegativeButton.Content = negativeText;
    }

    /// <summary>显示确认弹窗。返回 true 表示用户点了肯定按钮；叉/Esc/否定按钮都返回 false。</summary>
    public static bool Show(Window owner, string title, string message, string positiveText = "确定", string negativeText = "取消")
    {
        var dialog = new ConfirmDialog(title, message, positiveText, negativeText)
        {
            Owner = owner
        };

        return dialog.ShowDialog() == true;
    }

    private void Positive_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void Negative_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
