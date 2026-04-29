using System.Windows;
using System.Windows.Controls;
using GPGems.AI.Pathfinding;
using GPGems.Visualization.Pathfinding;

namespace GPGems.DemoApp;

/// <summary>
/// 寻路算法对比演示窗口
/// 策略模式：支持切换多种寻路算法进行对比
/// </summary>
public partial class PathfindingDemoWindow : Window
{
    private GridMap? _map;
    private int _gridSize = 25;

    // 所有可用的寻路算法
    private readonly Dictionary<RadioButton, IPathfinder> _algorithms = new();

    public PathfindingDemoWindow()
    {
        try
        {
            InitializeComponent();
            Loaded += (s, e) => Initialize();
            
        }
        catch (Exception ex)
        {
            MessageBox.Show($"窗口初始化失败: {ex.Message}\n{ex.StackTrace}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>初始化演示窗口</summary>
    private void Initialize()
    {
        try
        {
            // 注册所有算法
            _algorithms.Add(AlgoAStar, new AStarPathfinder());
            _algorithms.Add(AlgoAStarOptimized, new AStarOptimizedPathfinder());
            _algorithms.Add(AlgoDijkstra, new DijkstraPathfinder());
            _algorithms.Add(AlgoBFS, new BFSPathfinder());
            _algorithms.Add(AlgoDFS, new DFSPathfinder());
            _algorithms.Add(AlgoBestFirst, new BestFirstPathfinder());
            _algorithms.Add(AlgoBidirectional, new BidirectionalAStarPathfinder());
            _algorithms.Add(AlgoParallelBidirectional, new ParallelBidirectionalAStarPathfinder());

            // 初始化地图
            InitializeMap();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"初始化失败: {ex.Message}\n{ex.StackTrace}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>初始化网格地图</summary>
    private void InitializeMap()
    {
        _map = new GridMap(_gridSize, _gridSize);
        PathCanvas.SetMap(_map);

        // // 设置默认起点和终点（居中各偏左/右）
        // var start = _map.GetNode(2, _gridSize / 2);
        // var goal = _map.GetNode(_gridSize - 3, _gridSize / 2);

        // if (start != null) start.VisualType = NodeType.Start;
        // if (goal != null) goal.VisualType = NodeType.Goal;

        // 默认使用 A* 算法
        SetCurrentAlgorithm(AlgoAStar);
        ModeStart.IsChecked = true;
    }

    /// <summary>切换当前使用的寻路算法</summary>
    private void SetCurrentAlgorithm(RadioButton button)
    {
        if (_algorithms.TryGetValue(button, out var algorithm))
        {
            PathCanvas.SetPathfinder(algorithm);
            if (AlgorithmNameText != null)
                AlgorithmNameText.Text = $"当前算法: {algorithm.Name}";
            if (AlgorithmDescText != null)
                AlgorithmDescText.Text = algorithm.Description;
            if (TitleText != null)
                TitleText.Text = $"{algorithm.Name} 寻路可视化";
            Title = $"{algorithm.Name} 寻路算法演示";
        }
    }

    /// <summary>算法切换事件</summary>
    private void OnAlgorithmChanged(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;

        if (sender is RadioButton button && button.IsChecked == true)
        {
            SetCurrentAlgorithm(button);
        }
    }

    /// <summary>编辑模式改变</summary>
    private void OnModeChanged(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded || PathCanvas == null) return;

        if (sender is RadioButton rb && rb.IsChecked == true)
        {
            if (rb == ModeObstacle)
                PathCanvas.CurrentMode = EditMode.Obstacle;
            else if (rb == ModeStart)
                PathCanvas.CurrentMode = EditMode.StartPoint;
            else if (rb == ModeGoal)
                PathCanvas.CurrentMode = EditMode.GoalPoint;
        }
    }

    /// <summary>执行寻路</summary>
    private void OnFindPath(object sender, RoutedEventArgs e)
    {
        StatusText.Text = "搜索中...";
                    
        var path = PathCanvas.FindPath();

        if (path != null && path.Count > 0)
        {
            StatusText.Text = "✅ 找到路径";
            StatusText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(78, 204, 163));
        }
        else
        {
            StatusText.Text = "❌ 无法到达";
            StatusText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(233, 69, 96));
        }

        // 更新统计
        var (open, closed, pathLen, algo, useTime) = PathCanvas.GetStats();
        OpenSetText.Text = $"开放集: {open}";
        ClosedSetText.Text = $"关闭集: {closed}";
        PathLengthText.Text = $"路径长度: {pathLen}";
        UseTimeText.Text = $"用时: {useTime} 毫秒";
    }

    /// <summary>清除所有</summary>
    private void OnClear(object sender, RoutedEventArgs e)
    {
        PathCanvas.ClearAll();
        StatusText.Text = "已清除";
        OpenSetText.Text = "开放集: 0";
        ClosedSetText.Text = "关闭集: 0";
        PathLengthText.Text = "路径长度: 0";
        UseTimeText.Text = "用时: 0 毫秒";
    }

    /// <summary>随机生成障碍物</summary>
    private void OnRandomObstacle(object sender, RoutedEventArgs e)
    {
        PathCanvas.GenerateRandomObstacles(0.2);
        StatusText.Text = "已生成随机障碍";
    }

    /// <summary>网格大小改变</summary>
    private void OnGridSizeChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        _gridSize = (int)e.NewValue;
        if (GridSizeText != null)
            GridSizeText.Text = $"{_gridSize}x{_gridSize}";

        if (IsLoaded && _map != null)
        {
            InitializeMap();
        }
    }
}
