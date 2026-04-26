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


    /// <summary>打开寻路算法对比演示</summary>
    private void OpenAStarDemo(object sender, RoutedEventArgs e)
    {
        new PathfindingDemoWindow().Show();
    }

    /// <summary>打开 Steering 定向行为演示</summary>
    private void OpenSteeringDemo(object sender, RoutedEventArgs e)
    {
        new SteeringDemoWindow().Show();
    }

    /// <summary>打开融合决策系统演示</summary>
    private void OpenDecisionSystemDemo(object sender, RoutedEventArgs e)
    {
        new DecisionSystemDemoWindow().Show();
    }

    /// <summary>打开四叉树空间分割演示</summary>
    private void OpenQuadtreeDemo(object sender, RoutedEventArgs e)
    {
        new Graphics.QuadtreeDemoWindow().Show();
    }

    /// <summary>打开八叉树 3D 空间分割演示</summary>
    private void OpenOctreeDemo(object sender, RoutedEventArgs e)
    {
        new Graphics.OctreeDemoWindow().Show();
    }

    /// <summary>打开 KD 树 k-近邻搜索演示</summary>
    private void OpenKDTreeDemo(object sender, RoutedEventArgs e)
    {
        new Graphics.KDTreeDemoWindow().Show();
    }

    /// <summary>打开 k-NN 回归模型演示</summary>
    private void OpenKnnRegressionDemo(object sender, RoutedEventArgs e)
    {
        new AI.KnnRegressionDemoWindow().Show();
    }

    /// <summary>打开最近邻算法性能对比演示</summary>
    private void OpenNearestNeighborBenchmark(object sender, RoutedEventArgs e)
    {
        new AI.NearestNeighborBenchmarkWindow().Show();
    }
}
