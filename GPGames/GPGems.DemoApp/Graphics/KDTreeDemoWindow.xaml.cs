using System;
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
/// KD 树 k-近邻搜索演示窗口
/// 特点：轴交替分割 + 中位数分割 + 回溯剪枝可视化
/// </summary>
public partial class KDTreeDemoWindow : Window
{
    private KDTree<int>? _kdTree;
    private List<Vector3> _points = [];
    private readonly Random _random = new(42);
    private const float SpaceSize = 400;

    /// <summary>各轴颜色</summary>
    private static readonly Color[] AxisColors =
    {
        Color.FromRgb(231, 76, 60),    // X = 红
        Color.FromRgb(46, 204, 113),   // Y = 绿
        Color.FromRgb(52, 152, 219),   // Z = 蓝
    };

    /// <summary>当前查询点位置</summary>
    private Vector3 _queryPos = new(SpaceSize / 2, SpaceSize / 2, SpaceSize / 2);

    public KDTreeDemoWindow()
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
        KValueSlider.ValueChanged += (_, _) =>
        {
            KValueText.Text = KValueSlider.Value.ToString();
            PerformKNearestSearch();
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

    private void RegeneratePoints()
    {
        int count = (int)PointCountSlider.Value;
        _points.Clear();

        // 生成带聚类的 3D 点云
        int clusterCount = count / 30;
        for (int c = 0; c < clusterCount; c++)
        {
            float cx = 30 + (float)(_random.NextDouble() * (SpaceSize - 60));
            float cy = 30 + (float)(_random.NextDouble() * (SpaceSize - 60));
            float cz = 30 + (float)(_random.NextDouble() * (SpaceSize - 60));
            int clusterSize = _random.Next(10, 40);

            for (int i = 0; i < clusterSize && _points.Count < count; i++)
            {
                double theta = _random.NextDouble() * System.Math.PI * 2;
                double phi = _random.NextDouble() * System.Math.PI;
                double radius = _random.NextDouble() * 35;

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

        BuildKDTree();
        Log($"生成 {count} 个 3D 点, {clusterCount} 个聚类");
    }

    private void BuildKDTree()
    {
        var pointsWithIndex = _points.Select((p, i) => (p, i)).ToList();

        var sw = Stopwatch.StartNew();
        _kdTree = new KDTree<int>();
        _kdTree.Build(pointsWithIndex);
        sw.Stop();

        BuildTimeText.Text = $"{sw.Elapsed.TotalMilliseconds:F2} ms";

        var allNodes = _kdTree.GetAllNodes();
        int maxDepth = allNodes.Count > 0 ? allNodes.Max(n => n.Depth) : 0;

        NodeCountText.Text = $"节点数: {allNodes.Count}";
        PointCountText.Text = $"点数: {_points.Count}";
        DepthText.Text = $"最大深度: {maxDepth}";

        Redraw();
        PerformKNearestSearch();
    }

    private List<(Vector3 Position, int i)> _pointsWithIndex = [];

    private void Redraw(object? sender = null, RoutedEventArgs? e = null)
    {
        if (_kdTree == null) return;

        ClearAllViews();

        if (ShowSplitsCheck.IsChecked == true)
        {
            DrawSplitLines();
        }

        if (ShowPointsCheck.IsChecked == true)
        {
            DrawAllPoints();
        }

        // 高亮搜索路径和 k-近邻
        if (ShowSearchPathCheck.IsChecked == true)
        {
            HighlightSearchPathAndResults();
        }
    }

    private void ClearAllViews()
    {
        ViewXY.Children.Clear();
        ViewXZ.Children.Clear();
        ViewYZ.Children.Clear();
        View3D.Children.Clear();
    }

    private void DrawSplitLines()
    {
        if (_kdTree?.Root == null) return;

        var allNodes = _kdTree.GetAllNodes();

        foreach (var node in allNodes)
        {
            if (node.IsLeaf) continue;

            Color color = ShowAxisColorsCheck.IsChecked == true
                ? AxisColors[node.Axis]
                : Color.FromRgb(100, 100, 150);

            var brush = new SolidColorBrush(color) { Opacity = 0.7 };

            // XY 视图
            DrawSplitLine(ViewXY, node, 0, 1);

            // XZ 视图
            DrawSplitLine(ViewXZ, node, 0, 2);

            // YZ 视图
            DrawSplitLine(ViewYZ, node, 1, 2);

            // 3D 视图（简化）
            DrawSplitLine3D(View3D, node);
        }
    }

    private void DrawSplitLine(Canvas canvas, KDTree<int>.Node node, int horizAxis, int vertAxis)
    {
        double scale = System.Math.Min(canvas.ActualWidth, canvas.ActualHeight) / SpaceSize;
        if (scale <= 0) scale = 0.8;

        var bounds = node.Bounds;
        double canvasWidth = canvas.ActualWidth;
        double canvasHeight = canvas.ActualHeight;

        // 只画分割线
        if (node.Axis == horizAxis)
        {
            // 竖线（X轴分割 / YZ视图）
            float x = node.SplitValue;
            float minVert = vertAxis == 1 ? bounds.Min.Y : bounds.Min.Z;
            float maxVert = vertAxis == 1 ? bounds.Max.Y : bounds.Max.Z;

            double lineX = x * scale;
            double lineY1 = minVert * scale;
            double lineY2 = maxVert * scale;

            // 只画在Canvas范围内的线
            if (lineX >= 0 && lineX <= canvasWidth)
            {
                lineY1 = System.Math.Clamp(lineY1, 0, canvasHeight);
                lineY2 = System.Math.Clamp(lineY2, 0, canvasHeight);

                var line = new Line
                {
                    X1 = lineX,
                    Y1 = lineY1,
                    X2 = lineX,
                    Y2 = lineY2,
                    Stroke = new SolidColorBrush(AxisColors[node.Axis]),
                    StrokeThickness = 1.5,
                    Opacity = 0.6
                };
                canvas.Children.Add(line);
            }
        }
        else if (node.Axis == vertAxis)
        {
            // 横线
            float y = node.SplitValue;
            float minHoriz = horizAxis == 0 ? bounds.Min.X : bounds.Min.Y;
            float maxHoriz = horizAxis == 0 ? bounds.Max.X : bounds.Max.Y;

            double lineY = y * scale;
            double lineX1 = minHoriz * scale;
            double lineX2 = maxHoriz * scale;

            // 只画在Canvas范围内的线
            if (lineY >= 0 && lineY <= canvasHeight)
            {
                lineX1 = System.Math.Clamp(lineX1, 0, canvasWidth);
                lineX2 = System.Math.Clamp(lineX2, 0, canvasWidth);

                var line = new Line
                {
                    X1 = lineX1,
                    Y1 = lineY,
                    X2 = lineX2,
                    Y2 = lineY,
                    Stroke = new SolidColorBrush(AxisColors[node.Axis]),
                    StrokeThickness = 1.5,
                    Opacity = 0.6
                };
                canvas.Children.Add(line);
            }
        }

        // 绘制节点边界框（同样裁剪）
        DrawBoundsRect(canvas, bounds, 0.05);
    }

    private void DrawSplitLine3D(Canvas canvas, KDTree<int>.Node node)
    {
        var bounds = node.Bounds;
        var brush = new SolidColorBrush(AxisColors[node.Axis]) { Opacity = 0.3 };

        // 简化的 3D 分割平面框
        DrawBoundsRect3D(canvas, bounds, 0.08);
    }

    private void DrawBoundsRect(Canvas canvas, Bounds bounds, double opacity)
    {
        double scale = System.Math.Min(canvas.ActualWidth, canvas.ActualHeight) / SpaceSize;
        if (scale <= 0) scale = 0.8;

        double canvasWidth = canvas.ActualWidth;
        double canvasHeight = canvas.ActualHeight;

        double left = bounds.Min.X * scale;
        double top = bounds.Min.Y * scale;
        double width = bounds.Width * scale;
        double height = bounds.Height * scale;

        // 快速AABB检测，完全在外面就跳过绘制
        if (left + width < 0 || top + height < 0 || left > canvasWidth || top > canvasHeight)
            return;

        var rect = new Rectangle
        {
            Width = width,
            Height = height,
            Stroke = new SolidColorBrush(Color.FromRgb(80, 80, 120)),
            StrokeThickness = 0.5,
            Opacity = opacity
        };

        Canvas.SetLeft(rect, left);
        Canvas.SetTop(rect, top);
        canvas.Children.Add(rect);
    }

    private void DrawBoundsRect3D(Canvas canvas, Bounds bounds, double opacity)
    {
        var (x, y) = Project3D(bounds.Center.X, bounds.Center.Y, bounds.Center.Z);
        float size = (bounds.Width + bounds.Height + bounds.Depth) / 3 * 0.8f;

        var rect = new Rectangle
        {
            Width = size,
            Height = size,
            Stroke = new SolidColorBrush(Color.FromRgb(80, 80, 120)),
            StrokeThickness = 0.5,
            Opacity = opacity
        };

        Canvas.SetLeft(rect, x - size / 2);
        Canvas.SetTop(rect, y - size / 2);
        canvas.Children.Add(rect);
    }

    private void DrawAllPoints()
    {
        foreach (var p in _points)
        {
            DrawPoint(ViewXY, p.X, p.Y, Brushes.LightGreen);
            DrawPoint(ViewXZ, p.X, p.Z, Brushes.LightGreen);
            DrawPoint(ViewYZ, p.Y, p.Z, Brushes.LightGreen);

            var (x3d, y3d) = Project3D(p.X, p.Y, p.Z);
            DrawPoint(View3D, x3d, y3d, Brushes.LightGreen);
        }
    }

    private void DrawPoint(Canvas canvas, float x, float y, Brush color, double size = 2.5)
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

    private void HighlightSearchPathAndResults()
    {
        if (_kdTree == null) return;

        // 获取搜索路径
        var path = _kdTree.GetSearchPath(_queryPos);

        // 高亮路径上的节点
        var pathBrush = new SolidColorBrush(Color.FromArgb(100, 241, 196, 15));

        foreach (var node in path)
        {
            // 高亮搜索路径的边界框
            HighlightNodeBounds(ViewXY, node.Bounds, pathBrush);
            HighlightNodeBounds(ViewXZ, node.Bounds, pathBrush);
            HighlightNodeBounds(ViewYZ, node.Bounds, pathBrush);
            HighlightNodeBounds3D(View3D, node.Bounds, pathBrush);
        }

        // 绘制查询点（黄色大点
        DrawPoint(ViewXY, _queryPos.X, _queryPos.Y, Brushes.Yellow, 8);
        DrawPoint(ViewXZ, _queryPos.X, _queryPos.Z, Brushes.Yellow, 8);
        DrawPoint(ViewYZ, _queryPos.Y, _queryPos.Z, Brushes.Yellow, 8);

        var (qx, qy) = Project3D(_queryPos.X, _queryPos.Y, _queryPos.Z);
        DrawPoint(View3D, qx, qy, Brushes.Yellow, 8);
    }

    private void HighlightNodeBounds(Canvas canvas, Bounds bounds, Brush brush)
    {
        double scale = System.Math.Min(canvas.ActualWidth, canvas.ActualHeight) / SpaceSize;
        if (scale <= 0) scale = 0.8;

        var rect = new Rectangle
        {
            Width = bounds.Width * scale,
            Height = bounds.Height * scale,
            Stroke = brush,
            StrokeThickness = 2,
            Fill = new SolidColorBrush(Color.FromArgb(30, 241, 196, 15))
        };

        Canvas.SetLeft(rect, bounds.Min.X * scale);
        Canvas.SetTop(rect, bounds.Min.Y * scale);
        canvas.Children.Add(rect);
    }

    private void HighlightNodeBounds3D(Canvas canvas, Bounds bounds, Brush brush)
    {
        var (x, y) = Project3D(bounds.Center.X, bounds.Center.Y, bounds.Center.Z);
        float size = (bounds.Width + bounds.Height + bounds.Depth) / 3 * 0.8f;

        var rect = new Rectangle
        {
            Width = size,
            Height = size,
            Stroke = brush,
            StrokeThickness = 1.5,
            Opacity = 0.5
        };

        Canvas.SetLeft(rect, x - size / 2);
        Canvas.SetTop(rect, y - size / 2);
        canvas.Children.Add(rect);
    }

    #region 鼠标移动查询

    private void ViewXY_MouseMove(object sender, MouseEventArgs e)
    {
        var pos = e.GetPosition(ViewXY);
        double scale = System.Math.Min(ViewXY.ActualWidth, ViewXY.ActualHeight) / SpaceSize;
        if (scale <= 0) scale = 0.8;

        float x = (float)(pos.X / scale);
        float y = (float)(pos.Y / scale);

        _queryPos = new Vector3(x, y, _queryPos.Z);
        PerformKNearestSearch();
    }

    private void ViewXZ_MouseMove(object sender, MouseEventArgs e)
    {
        var pos = e.GetPosition(ViewXZ);
        double scale = System.Math.Min(ViewXZ.ActualWidth, ViewXZ.ActualHeight) / SpaceSize;
        if (scale <= 0) scale = 0.8;

        float x = (float)(pos.X / scale);
        float z = (float)(pos.Y / scale);

        _queryPos = new Vector3(x, _queryPos.Y, z);
        PerformKNearestSearch();
    }

    private void ViewYZ_MouseMove(object sender, MouseEventArgs e)
    {
        var pos = e.GetPosition(ViewYZ);
        double scale = System.Math.Min(ViewYZ.ActualWidth, ViewYZ.ActualHeight) / SpaceSize;
        if (scale <= 0) scale = 0.8;

        float y = (float)(pos.X / scale);
        float z = (float)(pos.Y / scale);

        _queryPos = new Vector3(_queryPos.X, y, z);
        PerformKNearestSearch();
    }

    private void View3D_MouseMove(object sender, MouseEventArgs e)
    {
        PerformKNearestSearch();
    }

    private void PerformKNearestSearch()
    {
        if (_kdTree == null) return;

        int k = (int)KValueSlider.Value;

        // KD 树搜索
        var sw = Stopwatch.StartNew();
        var results = _kdTree.FindKNearest(_queryPos, k);
        sw.Stop();
        SearchTimeText.Text = $"{sw.Elapsed.TotalMilliseconds * 1000:F1} μs";

        // 暴力搜索对比
        sw.Restart();
        var bruteResults = _points
            .Select((p, i) => new { Point = p, Index = i, DistSq = (_queryPos - p).LengthSquared() })
            .OrderBy(x => x.DistSq)
            .Take(k)
            .ToList();
        sw.Stop();
        BruteForceTimeText.Text = $"{sw.Elapsed.TotalMilliseconds * 1000:F1} μs";

        // 计算加速比
        double kdTime = results.Count > 0 ? sw.Elapsed.TotalMilliseconds * 1000 : 1;
        double bruteTime = bruteResults.Count > 0 ? sw.Elapsed.TotalMilliseconds * 1000 : 1;
        if (kdTime > 0)
        {
            SpeedupText.Text = $"{bruteTime / kdTime:F1}x";
        }

        // 更新结果列表
        ResultsListBox.Items.Clear();
        for (int i = 0; i < results.Count; i++)
        {
            var r = results[i];
            ResultsListBox.Items.Add($"#{i + 1} 距离: {r.Distance:F2}");
        }

        // 高亮 k-近邻点
        var neighborBrushes = new[]
        {
            Brushes.Red, Brushes.Orange, Brushes.Gold, Brushes.LightGreen, Brushes.Cyan,
            Brushes.Blue, Brushes.Purple, Brushes.Pink
        };

        for (int i = 0; i < results.Count; i++)
        {
            var p = results[i].Position;
            var brush = neighborBrushes[i % neighborBrushes.Length];

            DrawPoint(ViewXY, p.X, p.Y, brush, 6);
            DrawPoint(ViewXZ, p.X, p.Z, brush, 6);
            DrawPoint(ViewYZ, p.Y, p.Z, brush, 6);

            var (x3d, y3d) = Project3D(p.X, p.Y, p.Z);
            DrawPoint(View3D, x3d, y3d, brush, 6);
        }
    }

    private Brush[] _neighborBrushes = Array.Empty<Brush>();

    #endregion

    /// <summary>3D 到 2D 斜二测投影</summary>
    private (float x, float y) Project3D(float x, float y, float z)
    {
        float scale = 0.8f;

        float px = x * scale + z * scale * 0.5f;
        float py = y * scale + z * scale * 0.3f;

        return (px + 10, py + 10);
    }

    private void Log(string message)
    {
        string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        LogText.Text += $"[{timestamp}] {message}\n";

        if (LogText.Text.Count(c => c == '\n') > 20)
        {
            int firstNewline = LogText.Text.IndexOf('\n');
            LogText.Text = LogText.Text.Substring(firstNewline + 1);
        }
    }
}
