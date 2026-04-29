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

    /// <summary>打开最近邻搜索多算法对比演示</summary>
    private void OpenNearestNeighborSearchDemo(object sender, RoutedEventArgs e)
    {
        new AI.NearestNeighborSearchDemoWindow().Show();
    }

    /// <summary>打开地形生成算法三剑合一大对比</summary>
    private void OpenTerrainGenerationDemo(object sender, RoutedEventArgs e)
    {
        new Graphics.TerrainGenerationDemoWindow().Show();
    }

    /// <summary>打开 BSP 树空间分割演示</summary>
    private void OpenBSPTreeDemo(object sender, RoutedEventArgs e)
    {
        new Graphics.BSPTreeDemoWindow().Show();
    }

    /// <summary>打开多分辨率栅格 Chunked LOD 演示</summary>
    private void OpenMultiresGridDemo(object sender, RoutedEventArgs e)
    {
        new Graphics.MultiresGridDemoWindow().Show();
    }

    /// <summary>打开渐进网格 Progressive Mesh 演示</summary>
    private void OpenProgressiveMeshDemo(object sender, RoutedEventArgs e)
    {
        new Graphics.ProgressiveMeshDemoWindow().Show();
    }

    /// <summary>打开连续LOD地形演示</summary>
    private void OpenCLODTerrainDemo(object sender, RoutedEventArgs e)
    {
        new Graphics.CLODTerrainDemoWindow().Show();
    }

    /// <summary>打开曲线插值对比演示</summary>
    private void OpenCurveInterpolationDemo(object sender, RoutedEventArgs e)
    {
        new Math.CurveInterpolationDemoWindow().Show();
    }

    /// <summary>打开噪声生成器演示</summary>
    private void OpenNoiseDemo(object sender, RoutedEventArgs e)
    {
        new Math.NoiseGenerationDemoWindow().Show();
    }

    /// <summary>打开 GJK 碰撞检测演示</summary>
    private void OpenGJKDemo(object sender, RoutedEventArgs e)
    {
        new Physics.GJKCollisionDemoWindow().Show();
    }
}
