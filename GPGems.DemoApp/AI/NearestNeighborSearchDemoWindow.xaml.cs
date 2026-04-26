using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using GPGems.Core.Algorithms;

namespace GPGems.DemoApp.AI;

/// <summary>
/// 最近邻搜索多算法对比演示窗口
/// 参考 PathfindingDemoWindow 的设计风格
/// </summary>
public partial class NearestNeighborSearchDemoWindow : Window
{
    private readonly Random _random = new(42);
    private List<(float[] Point, int Index)> _dataPoints = [];
    private float[]? _queryPoint;
    private List<(float[] Point, int Value, float Distance)>? _searchResults;

    // 各算法实例
    private BruteForceSearch<int>? _bruteForce;
    private KDTreeIndex<int>? _kdTree;
    private BallTreeIndex<int>? _ballTree;
    private LocalitySensitiveHashing<int>? _lsh;

    private readonly Dictionary<string, string> _algorithmDescriptions = new()
    {
        { "暴力搜索", "O(N) 线性扫描，精确但慢，适合小数据集" },
        { "KD树", "O(logN) 空间分治，低维高效，高维退化" },
        { "Ball树", "O(logN) 超球分治，比KD树更适合高维数据" },
        { "LSH 局部敏感哈希", "近似搜索，O(1) 哈希查找，超大规模数据集首选" },
        { "全部对比", "同时运行所有算法，对比性能和精度" },
    };

    public NearestNeighborSearchDemoWindow()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            AlgoBruteForce.IsChecked = true;
            RegenerateData();
        };
    }

    #region 事件处理

    private void OnAlgorithmChanged(object sender, RoutedEventArgs e)
    {
        var algoName = ((RadioButton)sender).Content.ToString();
        if (_algorithmDescriptions.TryGetValue(algoName, out var desc))
        {
            AlgorithmDescText.Text = desc;
            AlgorithmNameText.Text = $"当前算法: {algoName}";
        }

        if (_dataPoints.Count > 0 && _queryPoint != null)
        {
            PerformSearch();
        }
    }

    private void OnSettingsChanged(object sender, RoutedEventArgs e)
    {
        PointCountText.Text = ((int)PointCountSlider.Value).ToString();
        DimensionText.Text = ((int)DimensionSlider.Value).ToString();
        KValueText.Text = ((int)KValueSlider.Value).ToString();
        RadiusText.Text = ((int)RadiusSlider.Value).ToString();
    }

    private void OnRegenerate(object sender, RoutedEventArgs e)
    {
        RegenerateData();
    }

    private void OnClearSearch(object sender, RoutedEventArgs e)
    {
        _queryPoint = null;
        _searchResults = null;
        DrawVisualization();
        ClearStats();
    }

    private void OnCanvasClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        var pos = e.GetPosition(SearchCanvas);
        int dimensions = (int)DimensionSlider.Value;

        // 将点击位置转换为查询点（前两维是画布坐标，后续维度随机）
        _queryPoint = new float[dimensions];
        _queryPoint[0] = (float)pos.X;
        _queryPoint[1] = (float)pos.Y;
        for (int i = 2; i < dimensions; i++)
        {
            _queryPoint[i] = (float)(_random.NextDouble() * 400);
        }

        PerformSearch();
    }

    #endregion

    #region 核心逻辑

    private void RegenerateData()
    {
        int pointCount = (int)PointCountSlider.Value;
        int dimensions = (int)DimensionSlider.Value;

        _dataPoints.Clear();

        // 生成随机数据点（前两维在画布范围内）
        for (int i = 0; i < pointCount; i++)
        {
            var point = new float[dimensions];
            point[0] = (float)(_random.NextDouble() * SearchCanvas.ActualWidth);
            point[1] = (float)(_random.NextDouble() * SearchCanvas.ActualHeight);
            for (int d = 2; d < dimensions; d++)
            {
                point[d] = (float)(_random.NextDouble() * 400);
            }
            _dataPoints.Add((point, i));
        }

        // 构建所有索引
        var sw = Stopwatch.StartNew();

        _bruteForce = new BruteForceSearch<int>(dimensions);
        _bruteForce.Build(_dataPoints);

        _kdTree = new KDTreeIndex<int>(dimensions);
        _kdTree.Build(_dataPoints);

        _ballTree = new BallTreeIndex<int>(dimensions);
        _ballTree.Build(_dataPoints);

        _lsh = new LocalitySensitiveHashing<int>(dimensions, hashTables: 4);
        _lsh.Build(_dataPoints);

        sw.Stop();
        BuildTimeText.Text = $"全部构建时间: {sw.Elapsed.TotalMilliseconds:F2} ms";

        _queryPoint = null;
        _searchResults = null;
        DrawVisualization();
    }

    private void PerformSearch()
    {
        if (_queryPoint == null) return;

        int k = (int)KValueSlider.Value;
        var sw = Stopwatch.StartNew();

        // 暴力搜索作为基准
        var baselineResults = _bruteForce?.FindKNearest(_queryPoint, k) ?? [];

        if (AlgoAll.IsChecked == true)
        {
            // 全部对比模式
            PerformAllAlgorithmsComparison(k, baselineResults);
        }
        else
        {
            // 单算法模式
            INearestNeighborIndex<int>? algorithm = null;

            if (AlgoBruteForce.IsChecked == true) algorithm = _bruteForce;
            else if (AlgoKDTree.IsChecked == true) algorithm = _kdTree;
            else if (AlgoBallTree.IsChecked == true) algorithm = _ballTree;
            else if (AlgoLSH.IsChecked == true) algorithm = _lsh;

            if (algorithm == null) return;

            sw.Restart();
            _searchResults = algorithm.FindKNearest(_queryPoint, k);
            sw.Stop();

            // 计算精度（与暴力搜索结果的重合率）
            float accuracy = CalculateAccuracy(baselineResults, _searchResults);

            SearchTimeText.Text = $"搜索时间: {sw.Elapsed.TotalMicroseconds:F2} μs";
            FoundCountText.Text = $"找到邻居: {_searchResults.Count}";
            AvgDistanceText.Text = $"平均距离: {_searchResults.Average(r => r.Distance):F2}";
            AccuracyText.Text = $"相对精度: {accuracy:P0}";
            AccuracyText.Foreground = accuracy >= 0.9 ? Brushes.LimeGreen : Brushes.Orange;
        }

        DrawVisualization();
    }

    private void PerformAllAlgorithmsComparison(int k,
        List<(float[] Point, int Value, float Distance)> baseline)
    {
        var algorithms = new (string Name, INearestNeighborIndex<int>? Instance)[]
        {
            ("暴力搜索", _bruteForce),
            ("KD树", _kdTree),
            ("Ball树", _ballTree),
            ("LSH", _lsh),
        };

        var results = new List<string>();

        foreach (var (name, algo) in algorithms)
        {
            if (algo == null) continue;

            var sw = Stopwatch.StartNew();
            var found = algo.FindKNearest(_queryPoint, k);
            sw.Stop();

            float accuracy = CalculateAccuracy(baseline, found);
            results.Add($"{name}: {sw.Elapsed.TotalMicroseconds:F1}μs, 精度 {accuracy:P0}");
        }

        // 显示对比结果
        SearchTimeText.Text = string.Join("\n", results);
        FoundCountText.Text = $"找到邻居: {baseline.Count}";
        AvgDistanceText.Text = $"平均距离: {baseline.Average(r => r.Distance):F2}";
        AccuracyText.Text = "多算法对比模式";
        AccuracyText.Foreground = Brushes.White;

        _searchResults = baseline;
    }

    private float CalculateAccuracy(List<(float[] Point, int Value, float Distance)> baseline,
        List<(float[] Point, int Value, float Distance)> test)
    {
        if (baseline.Count == 0) return 0;

        var baselineIndices = baseline.Select(r => r.Value).ToHashSet();
        int matches = test.Count(r => baselineIndices.Contains(r.Value));

        return (float)matches / baseline.Count;
    }

    #endregion

    #region 可视化绘制

    private void DrawVisualization()
    {
        SearchCanvas.Children.Clear();

        // 绘制所有数据点
        foreach (var (point, _) in _dataPoints)
        {
            DrawPoint(point[0], point[1], Brushes.MediumSeaGreen, 3);
        }

        // 绘制搜索结果（k个最近邻）
        if (_searchResults != null && _searchResults.Count > 0)
        {
            foreach (var (point, _, dist) in _searchResults)
            {
                DrawPoint(point[0], point[1], Brushes.Gold, 6);

                // 绘制到查询点的连线
                if (_queryPoint != null)
                {
                    var line = new Line
                    {
                        X1 = point[0],
                        Y1 = point[1],
                        X2 = _queryPoint[0],
                        Y2 = _queryPoint[1],
                        Stroke = new SolidColorBrush(Color.FromArgb(100, 255, 193, 7)),
                        StrokeThickness = 1
                    };
                    SearchCanvas.Children.Add(line);
                }
            }
        }

        // 绘制查询点和搜索半径
        if (_queryPoint != null)
        {
            float radius = (int)RadiusSlider.Value;

            // 搜索半径圆圈
            var radiusCircle = new Ellipse
            {
                Width = radius * 2,
                Height = radius * 2,
                Stroke = new SolidColorBrush(Color.FromArgb(150, 67, 97, 238)),
                StrokeThickness = 2,
                StrokeDashArray = new DoubleCollection { 4, 4 }
            };
            Canvas.SetLeft(radiusCircle, _queryPoint[0] - radius);
            Canvas.SetTop(radiusCircle, _queryPoint[1] - radius);
            SearchCanvas.Children.Add(radiusCircle);

            // 查询点（红色大圆点）
            DrawPoint(_queryPoint[0], _queryPoint[1], Brushes.Crimson, 10);
        }
    }

    private void DrawPoint(float x, float y, Brush color, double size)
    {
        var ellipse = new Ellipse
        {
            Width = size,
            Height = size,
            Fill = color
        };
        Canvas.SetLeft(ellipse, x - size / 2);
        Canvas.SetTop(ellipse, y - size / 2);
        SearchCanvas.Children.Add(ellipse);
    }

    private void ClearStats()
    {
        SearchTimeText.Text = "搜索时间: --";
        FoundCountText.Text = "找到邻居: 0";
        AvgDistanceText.Text = "平均距离: --";
        AccuracyText.Text = "相对精度: --";
    }

    #endregion
}
