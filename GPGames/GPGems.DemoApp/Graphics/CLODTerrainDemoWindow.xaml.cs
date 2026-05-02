using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Numerics;
using GPGems.Core.Math;
using GPGems.Graphics.LOD;
using GPGems.Graphics.Terrain;

namespace GPGems.DemoApp.Graphics;

/// <summary>
/// 连续LOD地形演示窗口
/// </summary>
public partial class CLODTerrainDemoWindow : Window
{
    private CLODTerrain? _clodTerrain;
    private Vector3 _viewPosition = new(0, 100, 0);
    private bool _isAnimating;
    private readonly Random _random = new(42);

    private const float CanvasWidth = 800;
    private const float CanvasHeight = 600;

    /// <summary>深度对应的颜色</summary>
    private static readonly Color[] DepthColors =
    {
        Color.FromRgb(200, 50, 50),      // 深度 0 - 最粗
        Color.FromRgb(255, 100, 50),     // 深度 1
        Color.FromRgb(255, 200, 50),     // 深度 2
        Color.FromRgb(255, 255, 50),     // 深度 3
        Color.FromRgb(150, 255, 100),    // 深度 4
        Color.FromRgb(50, 255, 150),     // 深度 5
        Color.FromRgb(50, 200, 255),     // 深度 6
        Color.FromRgb(100, 150, 255),    // 深度 7
        Color.FromRgb(150, 100, 255),    // 深度 8
        Color.FromRgb(200, 50, 255),     // 深度 9 - 最细
    };

    public CLODTerrainDemoWindow()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            InitializeSliders();
            GenerateTerrain();
        };
    }

    private void InitializeSliders()
    {
        TerrainSizeSlider.ValueChanged += (_, _) =>
        {
            // 调整到最近的 2^n + 1
            int value = (int)TerrainSizeSlider.Value;
            int power = 1;
            while (power < value) power *= 2;
            int adjusted = power + 1;
            TerrainSizeValue.Text = adjusted.ToString();
        };

        MaxDepthSlider.ValueChanged += (_, _) =>
        {
            MaxDepthValue.Text = ((int)MaxDepthSlider.Value).ToString();
        };

        ErrorThresholdSlider.ValueChanged += (_, _) =>
        {
            ErrorThresholdValue.Text = $"{ErrorThresholdSlider.Value:F1}";
            if (_clodTerrain != null)
            {
                _clodTerrain.ErrorThreshold = (float)ErrorThresholdSlider.Value;
                UpdateTerrain();
            }
        };

        ShowDepthColorsCheck.Click += (_, _) => Redraw();
        ShowEdgesCheck.Click += (_, _) => Redraw();
        ShowSplitPointsCheck.Click += (_, _) => Redraw();
    }

    private void RegenerateButton_Click(object sender, RoutedEventArgs e)
    {
        GenerateTerrain();
    }

    private void AnimateViewButton_Click(object sender, RoutedEventArgs e)
    {
        _isAnimating = !_isAnimating;
        AnimateViewButton.Content = _isAnimating ? "⏹ 停止漫游" : "▶ 自动漫游";

        if (_isAnimating)
        {
            Log("开始自动漫游，观察自适应剖分效果...");
            StartAnimation();
        }
        else
        {
            Log("停止自动漫游");
        }
    }

    private async void StartAnimation()
    {
        float angle = 0;
        while (_isAnimating && IsVisible)
        {
            angle += 0.015f;
            float radius = 150;
            int size = _clodTerrain?.Width ?? 65;
            float center = (size - 1) * 0.5f;

            _viewPosition = new Vector3(
                center + MathF.Cos(angle) * radius,
                50,
                center + MathF.Sin(angle) * radius
            );

            UpdateTerrain();
            await Task.Delay(16);
        }
    }

    private void GenerateTerrain()
    {
        var sw = Stopwatch.StartNew();

        // 确保是 2^n + 1
        int rawSize = (int)TerrainSizeSlider.Value;
        int power = 1;
        while (power < rawSize - 1) power *= 2;
        int size = power + 1;

        int maxDepth = (int)MaxDepthSlider.Value;
        float errorThreshold = (float)ErrorThresholdSlider.Value;

        Log($"正在生成地形... 大小: {size}x{size}, 最大深度: {maxDepth}");

        // 生成高度场
        var heightfield = new Heightfield(size, size);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float fx = (x - size / 2.0f) / size * 10;
                float fy = (y - size / 2.0f) / size * 10;
                float dist = MathF.Sqrt(fx * fx + fy * fy);
                float height = MathF.Sin(dist) * 30 + MathF.Sin(fx * 2) * 10 + MathF.Cos(fy * 3) * 10;
                heightfield[x, y] = height + 50;
            }
        }

        // 创建连续LOD地形
        _clodTerrain = new CLODTerrain(heightfield, 2.0f, maxDepth)
        {
            ErrorThreshold = errorThreshold
        };

        sw.Stop();
        InitTimeText.Text = $"{sw.ElapsedMilliseconds} ms";

        Log($"地形生成完成: {size}x{size}");

        UpdateTerrain();
    }

    private void UpdateTerrain()
    {
        if (_clodTerrain == null) return;

        var sw = Stopwatch.StartNew();

        // 更新自适应剖分
        _clodTerrain.Update(_viewPosition);

        sw.Stop();
        UpdateTimeText.Text = $"{sw.ElapsedTicks / 10.0:F1} μs";

        // 更新统计
        var stats = _clodTerrain.GetStats();
        TriangleCountText.Text = $"三角面: {stats.TotalTriangles:N0}";
        TriangleStatText.Text = $"{stats.TotalTriangles:N0}";
        DepthRangeText.Text = $"深度: 0-{stats.MaxDepthReached}";
        AvgDepthText.Text = $"平均深度: {stats.AverageLevel:F1}";
        MaxReachedDepthText.Text = $"最大深度: {stats.MaxDepthReached}";

        ViewPosText.Text = $"观察点: ({_viewPosition.X:F0}, {_viewPosition.Z:F0})";

        // 更新深度分布
        var distribution = _clodTerrain.GetLevelDistribution();
        var items = distribution.Select(kvp => new
        {
            Label = $"D{kvp.Key}",
            Percent = 100.0 * kvp.Value / stats.TotalTriangles,
            Count = kvp.Value,
            Color = new SolidColorBrush(DepthColors[System.Math.Min(kvp.Key, DepthColors.Length - 1)])
        }).ToList();
        DepthDistributionList.ItemsSource = items;

        Redraw();
    }

    private void Redraw()
    {
        if (_clodTerrain == null) return;

        DrawCanvas.Children.Clear();

        var triangles = _clodTerrain.GetLeafTriangles();
        float scale = System.Math.Min(CanvasWidth, CanvasHeight) / _clodTerrain.Width;

        // 绘制每个三角形
        foreach (var tri in triangles)
        {
            // 获取三角形顶点（2D投影 - 从上方俯视）
            var p0 = new Point(tri.Vertices[0].X * scale, tri.Vertices[0].Z * scale);
            var p1 = new Point(tri.Vertices[1].X * scale, tri.Vertices[1].Z * scale);
            var p2 = new Point(tri.Vertices[2].X * scale, tri.Vertices[2].Z * scale);

            var polygon = new Polygon();

            // 按深度着色
            if (ShowDepthColorsCheck.IsChecked == true)
            {
                var color = DepthColors[System.Math.Min(tri.Level, DepthColors.Length - 1)];
                polygon.Fill = new SolidColorBrush(Color.FromArgb(180, color.R, color.G, color.B));
            }
            else
            {
                // 按高度着色
                float avgHeight = (tri.Vertices[0].Y + tri.Vertices[1].Y + tri.Vertices[2].Y) / 3;
                float heightNorm = (avgHeight + 50) / 150;
                byte r = (byte)(50 + heightNorm * 150);
                byte g = (byte)(100 + heightNorm * 100);
                byte b = (byte)(150 - heightNorm * 100);
                polygon.Fill = new SolidColorBrush(Color.FromArgb(180, r, g, b));
            }

            if (ShowEdgesCheck.IsChecked == true)
            {
                polygon.Stroke = new SolidColorBrush(Color.FromArgb(100, 200, 220, 255));
                polygon.StrokeThickness = 0.5;
            }

            polygon.Points.Add(p0);
            polygon.Points.Add(p1);
            polygon.Points.Add(p2);
            DrawCanvas.Children.Add(polygon);

            // 显示剖分点（斜边中点）
            if (ShowSplitPointsCheck.IsChecked == true && tri.Level > 0)
            {
                var splitPoint = tri.GetSplitPoint();
                var sp = new Ellipse
                {
                    Width = 4,
                    Height = 4,
                    Fill = Brushes.Yellow
                };
                Canvas.SetLeft(sp, splitPoint.X * scale - 2);
                Canvas.SetTop(sp, splitPoint.Y * scale - 2);
                DrawCanvas.Children.Add(sp);
            }
        }

        // 绘制观察点
        float viewX = (float)(_viewPosition.X * scale);
        float viewZ = (float)(_viewPosition.Z * scale);

        var viewMarker = new Ellipse
        {
            Width = 14,
            Height = 14,
            Fill = Brushes.Red,
            Stroke = Brushes.White,
            StrokeThickness = 2
        };
        Canvas.SetLeft(viewMarker, viewX - 7);
        Canvas.SetTop(viewMarker, viewZ - 7);
        DrawCanvas.Children.Add(viewMarker);

        // 绘制观察范围指示 - 影响区域
        var influenceCircle = new Ellipse
        {
            Width = 200,
            Height = 200,
            Stroke = new SolidColorBrush(Color.FromArgb(100, 255, 0, 0)),
            StrokeThickness = 1,
            StrokeDashArray = new DoubleCollection { 5, 3 }
        };
        Canvas.SetLeft(influenceCircle, viewX - 100);
        Canvas.SetTop(influenceCircle, viewZ - 100);
        DrawCanvas.Children.Add(influenceCircle);
    }

    #region 鼠标交互

    private void DrawCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (_clodTerrain == null) return;

        var pos = e.GetPosition(DrawCanvas);
        float scale = System.Math.Min(CanvasWidth, CanvasHeight) / _clodTerrain.Width;

        _viewPosition = new Vector3(
            (float)(pos.X / scale),
            50,
            (float)(pos.Y / scale)
        );

        UpdateTerrain();
    }

    private void DrawCanvas_MouseDown(object sender, MouseButtonEventArgs e)
    {
        var pos = e.GetPosition(DrawCanvas);
        Log($"鼠标点击位置: ({pos.X:F0}, {pos.Y:F0})");
    }

    private void DrawCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        float delta = e.Delta > 0 ? -0.5f : 0.5f;
        float newValue = (float)System.Math.Clamp(ErrorThresholdSlider.Value + delta, 0.1, 10.0);
        ErrorThresholdSlider.Value = newValue;
    }

    #endregion

    #region 预设方案

    private void PresetFast_Click(object sender, RoutedEventArgs e)
    {
        ErrorThresholdSlider.Value = 5;
        MaxDepthSlider.Value = 4;
        Log("已应用：极速模式 - 最小细节，最高性能");
        UpdateTerrain();
    }

    private void PresetNormal_Click(object sender, RoutedEventArgs e)
    {
        ErrorThresholdSlider.Value = 1;
        MaxDepthSlider.Value = 6;
        Log("已应用：标准模式 - 质量与性能平衡");
        UpdateTerrain();
    }

    private void PresetBest_Click(object sender, RoutedEventArgs e)
    {
        ErrorThresholdSlider.Value = 0.2;
        MaxDepthSlider.Value = 10;
        Log("已应用：电影级模式 - 最大细节，适合截图");
        UpdateTerrain();
    }

    #endregion

    private void Log(string message)
    {
        LogText.Text += $"[{DateTime.Now:HH:mm:ss.fff}] {message}\n";
        var scroll = (ScrollViewer)LogText.Parent;
        scroll.ScrollToEnd();
    }
}
