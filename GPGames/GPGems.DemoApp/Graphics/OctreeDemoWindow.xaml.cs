using System;
using Math = System.Math;
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
/// 八叉树 3D 空间分割演示窗口
/// 四视图布局：XY / XZ / YZ / 3D 斜投影
/// </summary>
public partial class OctreeDemoWindow : Window
{
    private Octree<int>? _octree;
    private List<Vector3> _points = [];
    private readonly Random _random = new(42);
    private const float SpaceSize = 400;
    private const float QueryRangeSize = 60;

    /// <summary>深度对应的颜色（从浅到深）</summary>
    private static readonly Color[] DepthColors =
    {
        Color.FromRgb(220, 200, 255),  // 0
        Color.FromRgb(200, 170, 255),  // 1
        Color.FromRgb(180, 140, 255),  // 2
        Color.FromRgb(160, 110, 255),  // 3
        Color.FromRgb(140, 80, 255),   // 4
        Color.FromRgb(120, 50, 255),   // 5
        Color.FromRgb(100, 20, 255),   // 6
        Color.FromRgb(80, 0, 230),     // 7
        Color.FromRgb(60, 0, 200),     // 8
    };

    /// <summary>当前鼠标在 3D 空间中的位置</summary>
    private Vector3 _mousePos = new(SpaceSize / 2, SpaceSize / 2, SpaceSize / 2);

    public OctreeDemoWindow()
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
            RebuildOctree();
        };

        MaxElementsSlider.ValueChanged += (_, _) =>
        {
            MaxElementsValue.Text = MaxElementsSlider.Value.ToString();
            RebuildOctree();
        };

        PointCountSlider.ValueChanged += (_, _) =>
        {
            PointCountValue.Text = PointCountSlider.Value.ToString();
            RegeneratePoints();
        };
    }

    private void RegenerateButton_Click(object sender, RoutedEventArgs e)
    {
        RegeneratePoints();
    }

    private void PresetBalanced_Click(object sender, RoutedEventArgs e)
    {
        MaxDepthSlider.Value = 5;
        MaxElementsSlider.Value = 6;
        Log("已应用：均衡模式");
    }

    private void PresetHighPerformance_Click(object sender, RoutedEventArgs e)
    {
        MaxDepthSlider.Value = 3;
        MaxElementsSlider.Value = 16;
        Log("已应用：高性能模式");
    }

    private void PresetHighQuality_Click(object sender, RoutedEventArgs e)
    {
        MaxDepthSlider.Value = 7;
        MaxElementsSlider.Value = 2;
        Log("已应用：高质量模式");
    }

    private void RegeneratePoints()
    {
        int count = (int)PointCountSlider.Value;
        _points.Clear();

        // 生成 3D 聚类点云
        int clusterCount = count / 40;
        for (int c = 0; c < clusterCount; c++)
        {
            float cx = 30 + (float)(_random.NextDouble() * (SpaceSize - 60));
            float cy = 30 + (float)(_random.NextDouble() * (SpaceSize - 60));
            float cz = 30 + (float)(_random.NextDouble() * (SpaceSize - 60));
            int clusterSize = _random.Next(15, 50);

            for (int i = 0; i < clusterSize && _points.Count < count; i++)
            {
                // 球形分布
                double theta = _random.NextDouble() * System.Math.PI * 2;
                double phi = _random.NextDouble() * System.Math.PI;
                double radius = _random.NextDouble() * 40;

                float x = cx + (float)(radius * System.Math.Sin(phi) * System.Math.Cos(theta));
                float y = cy + (float)(radius * System.Math.Sin(phi) * System.Math.Sin(theta));
                float z = cz + (float)(radius * System.Math.Cos(phi));

                x = System.Math.Clamp(x, 10, SpaceSize - 10);
                y = System.Math.Clamp(y, 10, SpaceSize - 10);
                z = System.Math.Clamp(z, 10, SpaceSize - 10);

                _points.Add(new Vector3(x, y, z));
            }
        }

        // 补充剩余随机点
        while (_points.Count < count)
        {
            float x = 10 + (float)(_random.NextDouble() * (SpaceSize - 20));
            float y = 10 + (float)(_random.NextDouble() * (SpaceSize - 20));
            float z = 10 + (float)(_random.NextDouble() * (SpaceSize - 20));
            _points.Add(new Vector3(x, y, z));
        }

        RebuildOctree();
        Log($"生成 {count} 个 3D 点, {clusterCount} 个聚类");
    }

    private void RebuildOctree()
    {
        int maxDepth = (int)MaxDepthSlider.Value;
        int maxElements = (int)MaxElementsSlider.Value;

        var bounds = new Bounds(
            new Vector3(SpaceSize / 2, SpaceSize / 2, SpaceSize / 2),
            new Vector3(SpaceSize / 2, SpaceSize / 2, SpaceSize / 2)
        );

        var sw = Stopwatch.StartNew();
        _octree = new Octree<int>(bounds, maxDepth, maxElements);

        foreach (var p in _points)
        {
            // 每个点作为一个小的包围盒插入
            var pointBounds = new Bounds(p, new Vector3(0.1f, 0.1f, 0.1f));
            _octree.Insert(pointBounds, _points.IndexOf(p));
        }

        sw.Stop();
        BuildTimeText.Text = $"{sw.Elapsed.TotalMilliseconds:F2} ms";

        // 更新统计
        NodeCountText.Text = $"节点数: {_octree.TotalNodes}";
        PointCountText.Text = $"点数: {_octree.TotalElements}";
        LeafCountText.Text = $"叶子数: {_octree.GetAllLeafNodes().Count}";
        DepthText.Text = $"最大深度: {maxDepth}";

        Redraw();
    }

    private void Redraw(object? sender = null, RoutedEventArgs? e = null)
    {
        if (_octree == null) return;

        ClearAllViews();

        if (ShowBoundsCheck.IsChecked == true)
        {
            DrawOctreeBounds();
        }

        if (ShowPointsCheck.IsChecked == true)
        {
            DrawAllPoints();
        }
    }

    private void ClearAllViews()
    {
        ViewXY.Children.Clear();
        ViewXZ.Children.Clear();
        ViewYZ.Children.Clear();
        View3D.Children.Clear();
    }

    private void DrawOctreeBounds()
    {
        var allNodes = _octree!.GetAllNodes();

        foreach (var node in allNodes)
        {
            var bounds = node.Bounds;
            Color color = ShowDepthColorsCheck.IsChecked == true
                ? DepthColors[System.Math.Min(node.Depth, DepthColors.Length - 1)]
                : Color.FromRgb(100, 100, 150);

            var brush = new SolidColorBrush(color) { Opacity = 0.15 };
            var strokeBrush = new SolidColorBrush(color) { Opacity = 0.6 };

            // XY 视图 (x, y)
            DrawRect(ViewXY, bounds.Min.X, bounds.Min.Y, bounds.Width, bounds.Height, brush, strokeBrush);

            // XZ 视图 (x, z)
            DrawRect(ViewXZ, bounds.Min.X, bounds.Min.Z, bounds.Width, bounds.Depth, brush, strokeBrush);

            // YZ 视图 (y, z)
            DrawRect(ViewYZ, bounds.Min.Y, bounds.Min.Z, bounds.Height, bounds.Depth, brush, strokeBrush);

            // 3D 斜投影
            var (x3d, y3d) = Project3D(bounds.Center.X, bounds.Center.Y, bounds.Center.Z);
            var size3d = (bounds.Width + bounds.Height + bounds.Depth) / 3;
            DrawRect(View3D, x3d - size3d / 2, y3d - size3d / 2, size3d, size3d, brush, strokeBrush);
        }
    }

    private void DrawAllPoints()
    {
        foreach (var p in _points)
        {
            // XY 视图
            DrawPoint(ViewXY, p.X, p.Y, Brushes.LightGreen);

            // XZ 视图
            DrawPoint(ViewXZ, p.X, p.Z, Brushes.LightGreen);

            // YZ 视图
            DrawPoint(ViewYZ, p.Y, p.Z, Brushes.LightGreen);

            // 3D 视图
            var (x3d, y3d) = Project3D(p.X, p.Y, p.Z);
            DrawPoint(View3D, x3d, y3d, Brushes.LightGreen);
        }
    }

    private void DrawRect(Canvas canvas, float x, float y, float width, float height, Brush fill, Brush stroke)
    {
        // 缩放以适应画布
        double scale = System.Math.Min(canvas.ActualWidth, canvas.ActualHeight) / SpaceSize;
        if (scale <= 0) scale = 0.8;

        var rect = new Rectangle
        {
            Width = width * scale,
            Height = height * scale,
            Fill = fill,
            Stroke = stroke,
            StrokeThickness = 0.5
        };

        Canvas.SetLeft(rect, x * scale);
        Canvas.SetTop(rect, y * scale);
        canvas.Children.Add(rect);
    }

    private void DrawPoint(Canvas canvas, float x, float y, Brush color)
    {
        double scale = System.Math.Min(canvas.ActualWidth, canvas.ActualHeight) / SpaceSize;
        if (scale <= 0) scale = 0.8;

        var ellipse = new Ellipse
        {
            Width = 3,
            Height = 3,
            Fill = color
        };

        Canvas.SetLeft(ellipse, x * scale - 1.5);
        Canvas.SetTop(ellipse, y * scale - 1.5);
        canvas.Children.Add(ellipse);
    }

    /// <summary>3D 到 2D 斜投影</summary>
    private (float x, float y) Project3D(float x, float y, float z)
    {
        // 斜二测投影
        float scale = 0.8f;
        float angle = 45 * MathF.PI / 180;

        float px = x * scale + z * scale * MathF.Cos(angle);
        float py = y * scale + z * scale * MathF.Sin(angle);

        return (px + 20, py + 20);
    }

    #region 鼠标移动 - 各视图范围查询

    private void ViewXY_MouseMove(object sender, MouseEventArgs e)
    {
        var pos = e.GetPosition(ViewXY);
        double scale = System.Math.Min(ViewXY.ActualWidth, ViewXY.ActualHeight) / SpaceSize;
        if (scale <= 0) scale = 0.8;

        float x = (float)(pos.X / scale);
        float y = (float)(pos.Y / scale);

        _mousePos = new Vector3(x, y, _mousePos.Z);
        PerformRangeQuery();
    }

    private void ViewXZ_MouseMove(object sender, MouseEventArgs e)
    {
        var pos = e.GetPosition(ViewXZ);
        double scale = System.Math.Min(ViewXZ.ActualWidth, ViewXZ.ActualHeight) / SpaceSize;
        if (scale <= 0) scale = 0.8;

        float x = (float)(pos.X / scale);
        float z = (float)(pos.Y / scale);

        _mousePos = new Vector3(x, _mousePos.Y, z);
        PerformRangeQuery();
    }

    private void ViewYZ_MouseMove(object sender, MouseEventArgs e)
    {
        var pos = e.GetPosition(ViewYZ);
        double scale = System.Math.Min(ViewYZ.ActualWidth, ViewYZ.ActualHeight) / SpaceSize;
        if (scale <= 0) scale = 0.8;

        float y = (float)(pos.X / scale);
        float z = (float)(pos.Y / scale);

        _mousePos = new Vector3(_mousePos.X, y, z);
        PerformRangeQuery();
    }

    private void View3D_MouseMove(object sender, MouseEventArgs e)
    {
        // 3D 视图中只更新查询框显示
        PerformRangeQuery();
    }

    private void PerformRangeQuery()
    {
        if (_octree == null) return;

        var queryBounds = new Bounds(
            _mousePos,
            new Vector3(QueryRangeSize / 2, QueryRangeSize / 2, QueryRangeSize / 2)
        );

        var sw = Stopwatch.StartNew();
        var results = _octree.QueryRange(queryBounds);
        sw.Stop();

        QueryTimeText.Text = $"{sw.Elapsed.TotalMilliseconds * 1000:F1} μs";
        QueryResultText.Text = results.Count.ToString();

        // 重绘并高亮结果
        Redraw();
        HighlightQueryResults(results, queryBounds);
    }

    private void HighlightQueryResults(List<int> results, Bounds queryBounds)
    {
        var highlightBrush = Brushes.Yellow;
        var rangeBrush = new SolidColorBrush(Color.FromArgb(80, 255, 255, 0));
        var rangeStroke = Brushes.Yellow;

        // 在所有视图中绘制查询范围和高亮结果点
        foreach (var index in results)
        {
            var p = _points[index];

            DrawPoint(ViewXY, p.X, p.Y, highlightBrush, 6);
            DrawPoint(ViewXZ, p.X, p.Z, highlightBrush, 6);
            DrawPoint(ViewYZ, p.Y, p.Z, highlightBrush, 6);

            var (x3d, y3d) = Project3D(p.X, p.Y, p.Z);
            DrawPoint(View3D, x3d, y3d, highlightBrush, 6);
        }

        // 绘制查询范围框
        double scale = System.Math.Min(ViewXY.ActualWidth, ViewXY.ActualHeight) / SpaceSize;
        if (scale <= 0) scale = 0.8;

        // XY
        DrawRect(ViewXY, queryBounds.Min.X, queryBounds.Min.Y, queryBounds.Width, queryBounds.Height,
            rangeBrush, rangeStroke, scale);

        // XZ
        DrawRect(ViewXZ, queryBounds.Min.X, queryBounds.Min.Z, queryBounds.Width, queryBounds.Depth,
            rangeBrush, rangeStroke, scale);

        // YZ
        DrawRect(ViewYZ, queryBounds.Min.Y, queryBounds.Min.Z, queryBounds.Height, queryBounds.Depth,
            rangeBrush, rangeStroke, scale);
    }

    private void DrawPoint(Canvas canvas, float x, float y, Brush color, double size = 3)
    {
        double scale = System.Math.Min(canvas.ActualWidth, canvas.ActualHeight) / SpaceSize;
        if (scale <= 0) scale = 0.8;

        var ellipse = new Ellipse
        {
            Width = size,
            Height = size,
            Fill = color
        };

        Canvas.SetLeft(ellipse, x * scale - size / 2);
        Canvas.SetTop(ellipse, y * scale - size / 2);
        canvas.Children.Add(ellipse);
    }

    private void DrawRect(Canvas canvas, float x, float y, float width, float height, Brush fill, Brush stroke, double scale)
    {
        var rect = new Rectangle
        {
            Width = width * scale,
            Height = height * scale,
            Fill = fill,
            Stroke = stroke,
            StrokeThickness = 1.5
        };

        Canvas.SetLeft(rect, x * scale);
        Canvas.SetTop(rect, y * scale);
        canvas.Children.Add(rect);
    }

    #endregion

    private void Log(string message)
    {
        string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        LogText.Text += $"[{timestamp}] {message}\n";

        if (LogText.Text.Count(c => c == '\n') > 30)
        {
            int firstNewline = LogText.Text.IndexOf('\n');
            LogText.Text = LogText.Text.Substring(firstNewline + 1);
        }
    }
}
