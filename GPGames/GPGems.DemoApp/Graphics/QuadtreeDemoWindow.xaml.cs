using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Numerics;
using GPGems.Core.Math;
using GPGems.Graphics.SpatialPartitioning;

namespace GPGems.DemoApp.Graphics;

/// <summary>
/// 四叉树空间分割演示窗口
/// 标准5段式结构：说明 + 参数 + 可视化 + 性能 + 日志
/// </summary>
public partial class QuadtreeDemoWindow : Window
{
    private Quadtree<int>? _quadtree;
    private List<Vector3> _points = [];
    private readonly Random _random = new(42);
    private const float CanvasSize = 600;
    private const float QueryRangeSize = 80;

    /// <summary>深度对应的颜色（从浅到深）</summary>
    private static readonly Color[] DepthColors =
    {
        Color.FromRgb(255, 230, 230),  // 0
        Color.FromRgb(255, 200, 200),  // 1
        Color.FromRgb(255, 170, 170),  // 2
        Color.FromRgb(255, 140, 140),  // 3
        Color.FromRgb(255, 110, 110),  // 4
        Color.FromRgb(255, 80, 80),    // 5
        Color.FromRgb(255, 50, 50),    // 6
        Color.FromRgb(230, 30, 30),    // 7
        Color.FromRgb(200, 10, 10),    // 8
        Color.FromRgb(170, 0, 0),      // 9
        Color.FromRgb(140, 0, 0),      // 10
    };

    public QuadtreeDemoWindow()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            InitializeSliders();
            RegeneratePoints();
        };
    }

    private void InitializeSliders()
    {
        MaxDepthSlider.ValueChanged += (_, _) =>
        {
            MaxDepthValue.Text = MaxDepthSlider.Value.ToString();
            RebuildQuadtree();
        };

        MaxElementsSlider.ValueChanged += (_, _) =>
        {
            MaxElementsValue.Text = MaxElementsSlider.Value.ToString();
            RebuildQuadtree();
        };

        PointCountSlider.ValueChanged += (_, _) =>
        {
            PointCountValue.Text = PointCountSlider.Value.ToString();
            RegeneratePoints();
        };

        ShowDepthColorsCheck.Click += (_, _) => Redraw();
        ShowBoundsCheck.Click += (_, _) => Redraw();
    }

    private void RegenerateButton_Click(object sender, RoutedEventArgs e)
    {
        RegeneratePoints();
    }

    private void StepBuildButton_Click(object sender, RoutedEventArgs e)
    {
        // TODO: 分步构建动画
        Log("分步构建动画功能开发中...");
    }

    private void PresetBalanced_Click(object sender, RoutedEventArgs e)
    {
        MaxDepthSlider.Value = 6;
        MaxElementsSlider.Value = 4;
        Log("已应用：均衡模式");
    }

    private void PresetHighPerformance_Click(object sender, RoutedEventArgs e)
    {
        MaxDepthSlider.Value = 4;
        MaxElementsSlider.Value = 16;
        Log("已应用：高性能模式（少节点，查询略慢）");
    }

    private void PresetHighQuality_Click(object sender, RoutedEventArgs e)
    {
        MaxDepthSlider.Value = 9;
        MaxElementsSlider.Value = 2;
        Log("已应用：高质量模式（细分割，查询更快）");
    }

    private void RegeneratePoints()
    {
        int count = (int)PointCountSlider.Value;
        _points.Clear();

        // 生成带聚类的点云（更贴近真实场景）
        int clusterCount = count / 50;
        for (int c = 0; c < clusterCount; c++)
        {
            float cx = (float)(_random.NextDouble() * CanvasSize);
            float cy = (float)(_random.NextDouble() * CanvasSize);
            int clusterSize = _random.Next(20, 80);

            for (int i = 0; i < clusterSize && _points.Count < count; i++)
            {
                double angle = _random.NextDouble() * System.Math.PI * 2;
                double radius = _random.NextDouble() * 80;
                float x = cx + (float)(System.Math.Cos(angle) * radius);
                float y = cy + (float)(System.Math.Sin(angle) * radius);
                x = System.Math.Clamp(x, 10, CanvasSize - 10);
                y = System.Math.Clamp(y, 10, CanvasSize - 10);
                _points.Add(new Vector3(x, y, 0));
            }
        }

        // 补充剩余的随机点
        while (_points.Count < count)
        {
            float x = (float)(_random.NextDouble() * CanvasSize);
            float y = (float)(_random.NextDouble() * CanvasSize);
            _points.Add(new Vector3(x, y, 0));
        }

        RebuildQuadtree();
        Log($"生成 {count} 个点，{clusterCount} 个聚类");
    }

    private void RebuildQuadtree()
    {
        int maxDepth = (int)MaxDepthSlider.Value;
        int maxElements = (int)MaxElementsSlider.Value;

        var bounds = new Bounds(
            new Vector3(CanvasSize / 2, CanvasSize / 2, 0),
            new Vector3(CanvasSize / 2, CanvasSize / 2, 100)
        );

        var sw = Stopwatch.StartNew();
        _quadtree = new Quadtree<int>(bounds, maxDepth, maxElements);

        for (int i = 0; i < _points.Count; i++)
        {
            _quadtree.Insert(_points[i], i);
        }

        sw.Stop();
        BuildTimeText.Text = $"{sw.Elapsed.TotalMilliseconds:F2} ms";

        // 更新统计
        NodeCountText.Text = $"节点数: {_quadtree.TotalNodes}";
        PointCountText.Text = $"点数: {_quadtree.TotalElements}";
        DepthText.Text = $"最大深度: {maxDepth}";

        Redraw();
    }

    private void Redraw()
    {
        DrawCanvas.Children.Clear();

        if (_quadtree == null) return;

        // 绘制所有节点边界
        if (ShowBoundsCheck.IsChecked == true)
        {
            var leaves = _quadtree.GetAllLeafNodes();
            foreach (var leaf in leaves)
            {
                var rect = new Rectangle
                {
                    Width = leaf.Bounds.Width,
                    Height = leaf.Bounds.Height,
                    StrokeThickness = 1,
                };

                if (ShowDepthColorsCheck.IsChecked == true)
                {
                    int colorIndex = System.Math.Min(leaf.Depth, DepthColors.Length - 1);
                    rect.Fill = new SolidColorBrush(DepthColors[colorIndex]);
                    rect.Stroke = new SolidColorBrush(Color.FromRgb(100, 100, 100));
                }
                else
                {
                    rect.Fill = Brushes.Transparent;
                    rect.Stroke = new SolidColorBrush(Color.FromRgb(80, 80, 150));
                }

                Canvas.SetLeft(rect, leaf.Bounds.Min.X);
                Canvas.SetTop(rect, leaf.Bounds.Min.Y);
                DrawCanvas.Children.Add(rect);
            }
        }

        // 绘制点
        foreach (var point in _points)
        {
            var ellipse = new Ellipse
            {
                Width = 4,
                Height = 4,
                Fill = Brushes.LightGreen
            };
            Canvas.SetLeft(ellipse, point.X - 2);
            Canvas.SetTop(ellipse, point.Y - 2);
            DrawCanvas.Children.Add(ellipse);
        }
    }

    private void DrawCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (_quadtree == null) return;

        var pos = e.GetPosition(DrawCanvas);
        float x = (float)pos.X;
        float y = (float)pos.Y;

        // 范围查询
        var queryBounds = new Bounds(
            new Vector3(x, y, 0),
            new Vector3(QueryRangeSize / 2, QueryRangeSize / 2, 100)
        );

        var sw = Stopwatch.StartNew();
        var results = _quadtree.QueryRange(queryBounds);
        sw.Stop();

        QueryTimeText.Text = $"{sw.Elapsed.TotalMilliseconds * 1000:F1} μs";
        QueryResultText.Text = results.Count.ToString();

        // 绘制查询范围
        Redraw();
        var queryRect = new Rectangle
        {
            Width = QueryRangeSize,
            Height = QueryRangeSize,
            Stroke = Brushes.Yellow,
            StrokeThickness = 2,
            Fill = new SolidColorBrush(Color.FromArgb(50, 255, 255, 0))
        };
        Canvas.SetLeft(queryRect, x - QueryRangeSize / 2);
        Canvas.SetTop(queryRect, y - QueryRangeSize / 2);
        DrawCanvas.Children.Add(queryRect);

        // 高亮结果点
        foreach (var index in results)
        {
            var p = _points[index];
            var ellipse = new Ellipse
            {
                Width = 8,
                Height = 8,
                Fill = Brushes.Yellow,
                Opacity = 0.8
            };
            Canvas.SetLeft(ellipse, p.X - 4);
            Canvas.SetTop(ellipse, p.Y - 4);
            DrawCanvas.Children.Add(ellipse);
        }
    }

    private void DrawCanvas_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_quadtree == null) return;

        var pos = e.GetPosition(DrawCanvas);
        var searchPos = new Vector3((float)pos.X, (float)pos.Y, 0);

        var sw = Stopwatch.StartNew();
        var (nearest, dist, found) = _quadtree.FindNearest(searchPos, 500);
        sw.Stop();

        if (found)
        {
            var p = _points[nearest];
            // 绘制连接线
            var line = new Line
            {
                X1 = pos.X,
                Y1 = pos.Y,
                X2 = p.X,
                Y2 = p.Y,
                Stroke = Brushes.Cyan,
                StrokeThickness = 2
            };
            DrawCanvas.Children.Add(line);

            // 高亮最近点
            var ellipse = new Ellipse
            {
                Width = 12,
                Height = 12,
                Fill = Brushes.Cyan,
                Opacity = 0.8
            };
            Canvas.SetLeft(ellipse, p.X - 6);
            Canvas.SetTop(ellipse, p.Y - 6);
            DrawCanvas.Children.Add(ellipse);

            Log($"最近邻搜索: 距离 {dist:F2}px, 耗时 {sw.Elapsed.TotalMilliseconds * 1000:F1} μs");
        }
    }

    private void Log(string message)
    {
        string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        LogText.Text += $"[{timestamp}] {message}\n";

        // 限制日志行数
        if (LogText.Text.Count(c => c == '\n') > 50)
        {
            int firstNewline = LogText.Text.IndexOf('\n');
            LogText.Text = LogText.Text.Substring(firstNewline + 1);
        }

        // 自动滚动到底部（通过 Dispatcher 确保布局更新后执行）
        Dispatcher.BeginInvoke(() =>
        {
            var scrollViewer = VisualTreeHelper.GetParent(LogText) as ScrollViewer;
            scrollViewer?.ScrollToEnd();
        });
    }
}
