using System.Windows;

namespace GPGems.DemoApp;

/// <summary>
/// 算法演示启动器窗口
/// </summary>
public partial class LauncherWindow : Window
{
    public LauncherWindow()
    {
        InitializeComponent();
    }

    /// <summary>打开 Boids 群体行为演示</summary>
    private void OpenBoidsDemo(object sender, RoutedEventArgs e)
    {
        new BoidsDemoWindow().Show();
    }

    /// <summary>打开博弈树搜索演示</summary>
    private void OpenGameTreeDemo(object sender, RoutedEventArgs e)
    {
        new GameTreeWindow().Show();
    }
}
