using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using GPGems.Core.Graphics;
using GPGems.Core.Math;
using GPGems.Graphics.SpatialPartitioning;
using Polygon = GPGems.Core.Geometry.Polygon;
using Vertex = GPGems.Core.Geometry.Vertex;

namespace GPGems.DemoApp.Graphics;

/// <summary>
/// BSP 树空间分割演示窗口
/// 四视图布局：俯视图 + 画家算法 + 射线检测 + 树结构
/// </summary>
public partial class BSPTreeDemoWindow : Window
{
    private BSPTree? _bspTree;
    private BSPCompiler? _compiler;
    private List<Polygon> _polygons = [];
    private readonly Random _random = new(42);
    private const float SpaceSize = 400;

    /// <summary>深度对应的颜色（从浅到深）</summary>
    private static readonly Color[] DepthColors =
    {
        Color.FromRgb(255, 200, 200),  // 0
        Color.FromRgb(255, 170, 170),  // 1
        Color.FromRgb(255, 140, 140),  // 2
        Color.FromRgb(255, 110, 110),  // 3
        Color.FromRgb(255, 80, 80),    // 4
        Color.FromRgb(255, 50, 50),    // 5
        Color.FromRgb(230, 30, 30),    // 6
        Color.FromRgb(200, 20, 20),    // 7
        Color.FromRgb(170, 10, 10),    // 8
    };

    /// <summary>当前鼠标位置</summary>
    private Vector2 _mousePos = new(SpaceSize / 2, SpaceSize / 2);

    /// <summary>射线起点</summary>
    private Vector2 _rayOrigin = new(SpaceSize / 2, SpaceSize / 2);

    public BSPTreeDemoWindow()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            InitializeSliders();
            RegenerateScene();
        };
    }

    private void InitializeSliders()
    {
        MaxDepthSlider.ValueChanged += (_, _) =>
        {
            MaxDepthValue.Text = MaxDepthSlider.Value.ToString();
            RebuildBSP();
        };

        MaxPolygonsSlider.ValueChanged += (_, _) =>
        {
            MaxPolygonsValue.Text = MaxPolygonsSlider.Value.ToString();
            RebuildBSP();
        };

        PolygonCountSlider.ValueChanged += (_, _) =>
        {
            PolygonCountValue.Text = PolygonCountSlider.Value.ToString();
            RegenerateScene();
        };
    }

    private void RegenerateButton_Click(object sender, RoutedEventArgs e)
    {
        RegenerateScene();
    }

    private void PresetRoom_Click(object sender, RoutedEventArgs e)
    {
        GenerateRoomScene();
        RebuildBSP();
        Log("已加载：室内房间场景");
    }

    private void PresetCity_Click(object sender, RoutedEventArgs e)
    {
        GenerateCityScene();
        RebuildBSP();
        Log("已加载：城市建筑场景");
    }

    private void PresetRandom_Click(object sender, RoutedEventArgs e)
    {
        RegenerateScene();
        Log("已加载：随机凸多边形场景");
    }

    private void RayCastButton_Click(object sender, RoutedEventArgs e)
    {
        Log("射线检测模式：点击设置起点，移动设置方向");
    }

    private void PainterSortButton_Click(object sender, RoutedEventArgs e)
    {
        var viewpoint = new Vector3(_mousePos.X, _mousePos.Y, 200);
        var sw = Stopwatch.StartNew();
        var sorted = _bspTree?.TraverseBackToFront(viewpoint) ?? [];
        sw.Stop();

        PainterTimeText.Text = $"{sw.Elapsed.TotalMilliseconds * 1000:F1} μs";
        Log($"画家算法排序完成: {sorted.Count} 个多边形");
        Redraw();
    }

    private void RegenerateScene()
    {
        int count = (int)PolygonCountSlider.Value;
        _polygons.Clear();

        // 生成随机凸多边形场景
        for (int i = 0; i < count; i++)
        {
            var poly = GenerateRandomConvexPolygon();
            _polygons.Add(poly);
        }

        RebuildBSP();
        Log($"生成 {count} 个凸多边形");
    }

    private Polygon GenerateRandomConvexPolygon()
    {
        // 生成一个随机位置的矩形/凸四边形
        float cx = 50 + (float)(_random.NextDouble() * (SpaceSize - 100));
        float cy = 50 + (float)(_random.NextDouble() * (SpaceSize - 100));
        float width = 20 + (float)(_random.NextDouble() * 40);
        float height = 20 + (float)(_random.NextDouble() * 40);
        float rotation = (float)(_random.NextDouble() * System.Math.PI * 0.5);

        var vertices = new List<Vertex>();
        for (int i = 0; i < 4; i++)
        {
            float angle = rotation + i * MathF.PI / 2;
            float wx = (i < 1 || i > 2) ? width / 2 : -width / 2;
            float wy = (i < 2) ? height / 2 : -height / 2;

            float rx = wx * MathF.Cos(angle) - wy * MathF.Sin(angle);
            float ry = wx * MathF.Sin(angle) + wy * MathF.Cos(angle);

            vertices.Add(new Vertex(new Vector3(cx + rx, cy + ry, 0), Vector3.UnitZ));
        }

        return new Polygon(vertices);
    }

    private void GenerateRoomScene()
    {
        _polygons.Clear();

        // 外墙壁
        AddRect(20, 20, SpaceSize - 40, 5);        // 上
        AddRect(20, SpaceSize - 25, SpaceSize - 40, 5);  // 下
        AddRect(20, 20, 5, SpaceSize - 40);        // 左
        AddRect(SpaceSize - 25, 20, 5, SpaceSize - 40);  // 右

        // 内部分隔
        AddRect(100, 80, 80, 5);
        AddRect(250, 80, 80, 5);
        AddRect(100, 200, 5, 100);
        AddRect(200, 150, 80, 5);
        AddRect(280, 250, 5, 80);

        // 家具
        AddRect(60, 60, 25, 25);    // 桌子
        AddRect(300, 300, 30, 20);  // 柜子
        AddRect(150, 280, 20, 20);  // 椅子
        AddRect(320, 100, 25, 25);  // 沙发
    }

    private void GenerateCityScene()
    {
        _polygons.Clear();

        // 主干道
        AddRect(180, 0, 40, SpaceSize);  // 纵向主路
        AddRect(0, 180, SpaceSize, 40);  // 横向主路

        // 建筑
        for (int i = 0; i < 4; i++)
        {
            for (int j = 0; j < 4; j++)
            {
                float x = 20 + i * 100;
                float y = 20 + j * 100;
                if (x > 160 && x < 220) x += 50;
                if (y > 160 && y < 220) y += 50;

                float w = 30 + (float)(_random.NextDouble() * 40);
                float h = 30 + (float)(_random.NextDouble() * 40);
                AddRect(x, y, w, h);
            }
        }
    }

    private void AddRect(float x, float y, float width, float height)
    {
        var vertices = new List<Vertex>
        {
            new(new Vector3(x, y, 0), Vector3.UnitZ),
            new(new Vector3(x + width, y, 0), Vector3.UnitZ),
            new(new Vector3(x + width, y + height, 0), Vector3.UnitZ),
            new(new Vector3(x, y + height, 0), Vector3.UnitZ)
        };
        _polygons.Add(new Polygon(vertices));
    }

    private void RebuildBSP()
    {
        int maxDepth = (int)MaxDepthSlider.Value;
        int maxPolygonsPerLeaf = (int)MaxPolygonsSlider.Value;

        var options = new BSPCompilerOptions
        {
            MaxDepth = maxDepth,
            MaxPolygonsPerLeaf = maxPolygonsPerLeaf,
            EnableOptimization = true
        };

        _compiler = new BSPCompiler(options);

        var sw = Stopwatch.StartNew();
        _bspTree = _compiler.BuildOptimized(_polygons);
        sw.Stop();

        BuildTimeText.Text = $"{sw.Elapsed.TotalMilliseconds:F2} ms";

        // 更新统计
        var stats = _compiler.ComputeStats(_bspTree);
        NodeCountText.Text = $"节点数: {stats.TotalNodes}";
        LeafCountText.Text = $"叶子数: {stats.LeafNodes}";
        PolygonCountText.Text = $"多边形数: {stats.TotalPolygons}";
        DepthText.Text = $"最大深度: {stats.MaxDepth}";

        Redraw();
    }

    private void Redraw(object? sender = null, RoutedEventArgs? e = null)
    {
        if (_bspTree == null) return;

        ClearAllViews();

        // 俯视图
        DrawTopDownView();

        // 画家算法视图
        DrawPainterView();

        // 射线检测视图
        DrawRayCastView();

        // 树结构视图
        DrawTreeStructure();
    }

    private void ClearAllViews()
    {
        ViewTopDown.Children.Clear();
        ViewPainter.Children.Clear();
        ViewRayCast.Children.Clear();
        ViewTreeStructure.Children.Clear();
    }

    private void DrawTopDownView()
    {
        double scale = System.Math.Min(ViewTopDown.ActualWidth, ViewTopDown.ActualHeight) / SpaceSize;
        if (scale <= 0) scale = 1;

        // 绘制多边形
        if (ShowPolygonsCheck.IsChecked == true)
        {
            foreach (var poly in _polygons)
            {
                DrawPolygon(ViewTopDown, poly, Brushes.RoyalBlue, Brushes.LightBlue, scale);
            }
        }

        // 绘制分割平面
        if (ShowSplitPlanesCheck.IsChecked == true)
        {
            DrawSplitPlanesRecursive(ViewTopDown, _bspTree.Root, 0, scale);
        }

        // 绘制视点
        DrawPoint(ViewTopDown, _mousePos.X, _mousePos.Y, Brushes.Yellow, 8, scale);
    }

    private void DrawSplitPlanesRecursive(Canvas canvas, BSPNode? node, int depth, double scale)
    {
        if (node == null || !node.SplitPlane.HasValue) return;

        var plane = node.SplitPlane.Value;

        // 简化：在 2D 视图中绘制分割线
        // 平面法向量的 x,y 分量决定了线的方向
        Color color = ShowDepthColorsCheck.IsChecked == true
            ? DepthColors[System.Math.Min(depth, DepthColors.Length - 1)]
            : Color.FromRgb(255, 100, 100);

        var brush = new SolidColorBrush(color) { Opacity = 0.8 };

        // 绘制一条穿过空间中心的分割线
        var line = new Line
        {
            X1 = 0,
            Y1 = 0,
            X2 = SpaceSize * scale,
            Y2 = SpaceSize * scale,
            Stroke = brush,
            StrokeThickness = 2
        };

        // 简化显示：用不同的斜线表示不同的分割
        double center = SpaceSize / 2 * scale;
        if (System.Math.Abs(plane.Normal.X) > System.Math.Abs(plane.Normal.Y))
        {
            // 近似垂直分割
            line.X1 = center + plane.Normal.X * 100 * scale;
            line.Y1 = 0;
            line.X2 = center + plane.Normal.X * 100 * scale;
            line.Y2 = SpaceSize * scale;
        }
        else
        {
            // 近似水平分割
            line.X1 = 0;
            line.Y1 = center + plane.Normal.Y * 100 * scale;
            line.X2 = SpaceSize * scale;
            line.Y2 = center + plane.Normal.Y * 100 * scale;
        }

        canvas.Children.Add(line);

        // 递归绘制子节点
        DrawSplitPlanesRecursive(canvas, node.FrontChild, depth + 1, scale);
        DrawSplitPlanesRecursive(canvas, node.BackChild, depth + 1, scale);
    }

    private void DrawPainterView()
    {
        double scale = System.Math.Min(ViewPainter.ActualWidth, ViewPainter.ActualHeight) / SpaceSize;
        if (scale <= 0) scale = 1;

        var viewpoint = new Vector3(_mousePos.X, _mousePos.Y, 200);

        // 获取画家算法排序的多边形
        var sorted = _bspTree?.TraverseBackToFront(viewpoint) ?? [];

        // 从后到前绘制（用透明度区分远近）
        float maxDist = 0;
        foreach (var poly in sorted)
        {
            var center = ComputePolygonCenter(poly);
            float dist = (center - viewpoint).Length();
            maxDist = System.Math.Max(maxDist, dist);
        }

        for (int i = 0; i < sorted.Count; i++)
        {
            var poly = sorted[i];
            var center = ComputePolygonCenter(poly);
            float dist = (center - viewpoint).Length();
            float alpha = maxDist > 0 ? 1 - dist / maxDist * 0.5f : 0.5f;

            var color = Color.FromRgb(
                (byte)(50 + i * 200 / System.Math.Max(sorted.Count, 1)),
                (byte)(200 - i * 150 / System.Math.Max(sorted.Count, 1)),
                (byte)(200)
            );

            var fillBrush = new SolidColorBrush(color) { Opacity = alpha };
            var strokeBrush = new SolidColorBrush(color) { Opacity = 1 };

            DrawPolygon(ViewPainter, poly, strokeBrush, fillBrush, scale);
        }

        // 绘制视点
        DrawPoint(ViewPainter, _mousePos.X, _mousePos.Y, Brushes.Red, 10, scale);

        // 绘制视点到各多边形中心的连线
        foreach (var poly in sorted.Take(5))
        {
            var center = ComputePolygonCenter(poly);
            var line = new Line
            {
                X1 = _mousePos.X * scale,
                Y1 = _mousePos.Y * scale,
                X2 = center.X * scale,
                Y2 = center.Y * scale,
                Stroke = new SolidColorBrush(Color.FromArgb(100, 255, 255, 0)),
                StrokeThickness = 1
            };
            ViewPainter.Children.Add(line);
        }
    }

    private void DrawRayCastView()
    {
        double scale = System.Math.Min(ViewRayCast.ActualWidth, ViewRayCast.ActualHeight) / SpaceSize;
        if (scale <= 0) scale = 1;

        // 绘制所有多边形
        foreach (var poly in _polygons)
        {
            DrawPolygon(ViewRayCast, poly, Brushes.Gray, Brushes.DarkGray, scale);
        }

        // 计算射线方向
        var direction = new Vector3(_mousePos.X - _rayOrigin.X, _mousePos.Y - _rayOrigin.Y, 0);
        if (direction.Length() > 0.1f)
        {
            direction = direction.Normalize();

            // 绘制射线
            var rayLine = new Line
            {
                X1 = _rayOrigin.X * scale,
                Y1 = _rayOrigin.Y * scale,
                X2 = _rayOrigin.X * scale + direction.X * SpaceSize * scale,
                Y2 = _rayOrigin.Y * scale + direction.Y * SpaceSize * scale,
                Stroke = Brushes.Yellow,
                StrokeThickness = 2
            };
            ViewRayCast.Children.Add(rayLine);

            // 执行射线检测
            var rayOrigin3D = new Vector3(_rayOrigin.X, _rayOrigin.Y, 0);
            var sw = Stopwatch.StartNew();
            var hit = BSPTechniques.RayCast(_bspTree!, rayOrigin3D, direction, out var result, SpaceSize * 2);
            sw.Stop();

            RayCastTimeText.Text = $"{sw.Elapsed.TotalMilliseconds * 1000:F1} μs";
            RayHitCountText.Text = hit ? "1" : "0";

            // 绘制交点
            if (hit && result.Hit)
            {
                DrawPoint(ViewRayCast, result.Point.X, result.Point.Y, Brushes.Red, 8, scale);

                // 高亮被击中的多边形
                if (result.HitPolygon != null)
                {
                    DrawPolygon(ViewRayCast, result.HitPolygon, Brushes.Red, Brushes.Pink, scale);
                }
            }
        }

        // 绘制射线起点
        DrawPoint(ViewRayCast, _rayOrigin.X, _rayOrigin.Y, Brushes.Green, 10, scale);
    }

    private void DrawTreeStructure()
    {
        if (_bspTree?.Root == null) return;

        double width = ViewTreeStructure.ActualWidth;
        double height = ViewTreeStructure.ActualHeight;
        if (width <= 0 || height <= 0)
        {
            width = 500;
            height = 200;
        }

        // 递归绘制树结构
        DrawTreeNode(ViewTreeStructure, _bspTree.Root, width / 2, 30, width / 4, 40);
    }

    private void DrawTreeNode(Canvas canvas, BSPNode? node, double x, double y, double hSpacing, double vSpacing)
    {
        if (node == null) return;

        // 节点颜色
        Color color = node.IsLeaf
            ? Color.FromRgb(100, 200, 100)  // 叶子节点绿色
            : Color.FromRgb(200, 100, 100); // 内部节点红色

        // 绘制节点
        var ellipse = new Ellipse
        {
            Width = 20,
            Height = 20,
            Fill = new SolidColorBrush(color),
            Stroke = Brushes.White,
            StrokeThickness = 1
        };
        Canvas.SetLeft(ellipse, x - 10);
        Canvas.SetTop(ellipse, y - 10);
        canvas.Children.Add(ellipse);

        // 显示多边形数量
        if (node.Polygons.Count > 0)
        {
            var text = new TextBlock
            {
                Text = node.Polygons.Count.ToString(),
                Foreground = Brushes.White,
                FontSize = 8,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            Canvas.SetLeft(text, x - 6);
            Canvas.SetTop(text, y - 6);
            canvas.Children.Add(text);
        }

        // 绘制到子节点的连线
        if (node.FrontChild != null)
        {
            var line = new Line
            {
                X1 = x,
                Y1 = y + 10,
                X2 = x - hSpacing,
                Y2 = y + vSpacing - 10,
                Stroke = Brushes.LightGray,
                StrokeThickness = 1
            };
            canvas.Children.Add(line);
            DrawTreeNode(canvas, node.FrontChild, x - hSpacing, y + vSpacing, hSpacing / 2, vSpacing);
        }

        if (node.BackChild != null)
        {
            var line = new Line
            {
                X1 = x,
                Y1 = y + 10,
                X2 = x + hSpacing,
                Y2 = y + vSpacing - 10,
                Stroke = Brushes.LightGray,
                StrokeThickness = 1
            };
            canvas.Children.Add(line);
            DrawTreeNode(canvas, node.BackChild, x + hSpacing, y + vSpacing, hSpacing / 2, vSpacing);
        }
    }

    private void DrawPolygon(Canvas canvas, Polygon poly, Brush stroke, Brush fill, double scale)
    {
        var polygon = new System.Windows.Shapes.Polygon
        {
            Stroke = stroke,
            Fill = fill,
            StrokeThickness = 1
        };

        foreach (var v in poly.Vertices)
        {
            polygon.Points.Add(new Point(v.Position.X * scale, v.Position.Y * scale));
        }

        canvas.Children.Add(polygon);
    }

    private void DrawPoint(Canvas canvas, float x, float y, Brush color, double size = 3, double scale = 1)
    {
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

    private Vector3 ComputePolygonCenter(Polygon polygon)
    {
        Vector3 sum = Vector3.Zero;
        foreach (var v in polygon.Vertices)
        {
            sum += v.Position;
        }
        return sum / polygon.VertexCount;
    }

    #region 鼠标事件

    private void ViewTopDown_MouseMove(object sender, MouseEventArgs e)
    {
        var pos = e.GetPosition(ViewTopDown);
        double scale = System.Math.Min(ViewTopDown.ActualWidth, ViewTopDown.ActualHeight) / SpaceSize;
        if (scale <= 0) scale = 1;

        _mousePos = new Vector2((float)(pos.X / scale), (float)(pos.Y / scale));
        Redraw();
    }

    private void ViewTopDown_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var pos = e.GetPosition(ViewTopDown);
        double scale = System.Math.Min(ViewTopDown.ActualWidth, ViewTopDown.ActualHeight) / SpaceSize;
        if (scale <= 0) scale = 1;

        _rayOrigin = new Vector2((float)(pos.X / scale), (float)(pos.Y / scale));
        Redraw();
    }

    private void ViewPainter_MouseMove(object sender, MouseEventArgs e)
    {
        var pos = e.GetPosition(ViewPainter);
        double scale = System.Math.Min(ViewPainter.ActualWidth, ViewPainter.ActualHeight) / SpaceSize;
        if (scale <= 0) scale = 1;

        _mousePos = new Vector2((float)(pos.X / scale), (float)(pos.Y / scale));
        Redraw();
    }

    private void ViewRayCast_MouseMove(object sender, MouseEventArgs e)
    {
        var pos = e.GetPosition(ViewRayCast);
        double scale = System.Math.Min(ViewRayCast.ActualWidth, ViewRayCast.ActualHeight) / SpaceSize;
        if (scale <= 0) scale = 1;

        _mousePos = new Vector2((float)(pos.X / scale), (float)(pos.Y / scale));
        Redraw();
    }

    private void ViewRayCast_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var pos = e.GetPosition(ViewRayCast);
        double scale = System.Math.Min(ViewRayCast.ActualWidth, ViewRayCast.ActualHeight) / SpaceSize;
        if (scale <= 0) scale = 1;

        _rayOrigin = new Vector2((float)(pos.X / scale), (float)(pos.Y / scale));
        Redraw();
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
