using Microsoft.Win32;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using StS2ModManager.Core;

namespace StS2ModManager;

public partial class StartupConfigWindow : Window
{
    private readonly GameInstallLocator _locator;

    public StartupConfigWindow(GameInstallLocator locator, string? initialGameRoot)
    {
        _locator = locator ?? throw new ArgumentNullException(nameof(locator));

        InitializeComponent();
        GamePathBox.Text = string.IsNullOrWhiteSpace(initialGameRoot) ? string.Empty : initialGameRoot;
        StatusText.Text = "请选择游戏目录，目录内需要包含游戏 exe。";
    }

    public GameLaunchInfo? SelectedLaunchInfo { get; private set; }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "选择 Slay the Spire 2 游戏目录"
        };

        if (Directory.Exists(GamePathBox.Text))
        {
            dialog.InitialDirectory = GamePathBox.Text;
        }

        if (dialog.ShowDialog(this) == true)
        {
            GamePathBox.Text = dialog.FolderName;
            StatusText.Text = "已选择目录，点击确认继续。";
        }
    }

    private void AutoScan_Click(object sender, RoutedEventArgs e)
    {
        var candidates = _locator.DiscoverGameRoots();
        CandidateList.ItemsSource = candidates;

        if (candidates.Count == 0)
        {
            CandidateList.SelectedItem = null;
            StatusText.Text = "未找到可用的 Steam 游戏目录，请手动选择。";
            return;
        }

        CandidateList.SelectedIndex = 0;
        GamePathBox.Text = candidates[0];
        StatusText.Text = $"找到 {candidates.Count} 个候选目录，已自动选中第一个。";
    }

    private void CandidateList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CandidateList.SelectedItem is not string candidatePath)
        {
            return;
        }

        GamePathBox.Text = candidatePath;
        StatusText.Text = "已从扫描结果中选择目录。";
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        var launchInfo = _locator.TryCreateLaunchInfo(GamePathBox.Text);
        if (launchInfo is null)
        {
            MessageBox.Show(
                this,
                "所选目录中未找到可用的游戏 exe，请重新选择 Slay the Spire 2 游戏目录。",
                "StS2 Mod Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        SelectedLaunchInfo = launchInfo;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
