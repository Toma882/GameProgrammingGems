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
/// 多分辨率栅格 Chunked LOD 演示窗口
/// </summary>
public partial class MultiresGridDemoWindow : Window
{
    private MultiresGrid? _multiresGrid;
    private float[,]? _heightfield;
    private readonly Random _random = new(42);
    private Vector3 _viewPosition = new(0, 100, 0);
    private bool _isAnimating;

    private const float CanvasWidth = 800;
    private const float CanvasHeight = 600;

    /// <summary>LOD层级对应的颜色</summary>
    private static readonly Color[] LODColors =
    {
        Color.FromRgb(0, 255, 136),     // LOD 0 - 青绿
        Color.FromRgb(0, 200, 255),     // LOD 1 - 天蓝
        Color.FromRgb(255, 221, 0),     // LOD 2 - 黄色
        Color.FromRgb(255, 136, 0),     // LOD 3 - 橙色
        Color.FromRgb(255, 68, 68),     // LOD 4 - 红色
        Color.FromRgb(136, 68, 255),    // LOD 5 - 紫色
        Color.FromRgb(255, 105, 180),   // LOD 6 - 粉色
        Color.FromRgb(100, 100, 100),   // LOD 7 - 灰色
    };

    public MultiresGridDemoWindow()
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
            TerrainSizeValue.Text = ((int)TerrainSizeSlider.Value).ToString();
        };

        ChunkSizeSlider.ValueChanged += (_, _) =>
        {
            ChunkSizeValue.Text = ((int)ChunkSizeSlider.Value).ToString();
        };

        MaxLODSlider.ValueChanged += (_, _) =>
        {
            MaxLODValue.Text = ((int)MaxLODSlider.Value).ToString();
        };

        LODDistanceSlider.ValueChanged += (_, _) =>
        {
            LODDistanceValue.Text = ((int)LODDistanceSlider.Value).ToString();
            if (_multiresGrid != null) UpdateLOD();
        };

        ShowLODColorsCheck.Click += (_, _) => Redraw();
        ShowChunkBoundsCheck.Click += (_, _) => Redraw();
        ShowWireframeCheck.Click += (_, _) => Redraw();
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
            Log("开始自动漫游，观察LOD动态切换效果...");
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
            angle += 0.02f;
            float radius = 300;
            int size = _heightfield?.GetLength(0) ?? 129;
            float center = (size - 1) * 0.5f;

            _viewPosition = new Vector3(
                center + MathF.Cos(angle) * radius,
                50,
                center + MathF.Sin(angle) * radius
            );

            UpdateLOD();
            await Task.Delay(16);
        }
    }

    private void GenerateTerrain()
    {
        var sw = Stopwatch.StartNew();

        int size = (int)TerrainSizeSlider.Value;
        int chunkSize = (int)ChunkSizeSlider.Value;
        int maxLOD = (int)MaxLODSlider.Value;

        Log($"正在生成地形... 大小: {size}x{size}");

        // 使用中点位移算法生成地形（size 是 2^level + 1，需反算 level）
        int level = (int)System.Math.Round(System.Math.Log(size - 1, 2));
        _heightfield = MidpointDisplacement.Generate(level, 0.7f, 100);

        // 创建多分辨率栅格
        _multiresGrid = new MultiresGrid(_heightfield, chunkSize, 2.0f, maxLOD);

        sw.Stop();
        GenTimeText.Text = $"{sw.ElapsedMilliseconds} ms";

        Log($"地形生成完成: {size}x{size}, 分块: {_multiresGrid.ChunkCountX}x{_multiresGrid.ChunkCountY}");

        UpdateLOD();
    }

    private void UpdateLOD()
    {
        if (_multiresGrid == null) return;

        var sw = Stopwatch.StartNew();

        // 更新LOD
        _multiresGrid.UpdateLOD(_viewPosition, (float)LODDistanceSlider.Value);

        sw.Stop();
        LODUpdateTimeText.Text = $"{sw.ElapsedTicks / 10.0:F1} μs";

        // 更新统计
        var chunks = _multiresGrid.GetAllChunks();
        int triangleCount = _multiresGrid.GetTotalTriangleCount();
        int maxTriangles = chunks.Count * ((_multiresGrid.ChunkSize + 1) * (_multiresGrid.ChunkSize + 1) * 2);
        float reduction = 100.0f * (1 - (float)triangleCount / maxTriangles);

        ChunkCountText.Text = $"块数: {chunks.Count}";
        TriangleCountText.Text = $"三角面: {triangleCount:N0}";
        ReductionText.Text = $"{reduction:F1}%";

        var lodLevels = chunks.GroupBy(c => c.CurrentLOD).OrderBy(g => g.Key).ToList();
        int minLOD = lodLevels.Min(g => g.Key);
        int maxLOD = lodLevels.Max(g => g.Key);
        LODRangeText.Text = $"LOD层级: {minLOD}-{maxLOD}";
        StatMaxLODText.Text = $"最高LOD: {maxLOD}";
        StatMinLODText.Text = $"最低LOD: {minLOD}";

        // 更新LOD分布条
        var distribution = new List<object>();
        foreach (var group in lodLevels)
        {
            float percent = 100.0f * group.Count() / chunks.Count;
            var color = new SolidColorBrush(LODColors[System.Math.Min(group.Key, LODColors.Length - 1)]);
            distribution.Add(new { Percent = percent, Color = color });
        }
        LODDistributionList.ItemsSource = distribution;

        ViewPosText.Text = $"观察点: ({_viewPosition.X:F0}, {_viewPosition.Z:F0})";

        Redraw();
    }

    private void Redraw()
    {
        if (_multiresGrid == null) return;

        DrawCanvas.Children.Clear();

        var chunks = _multiresGrid.GetAllChunks();
        float scale = System.Math.Min(CanvasWidth, CanvasHeight) / _multiresGrid.TerrainWidth;

        // 绘制每个块
        foreach (var chunk in chunks)
        {
            float x = (float)(chunk.Bounds.Min.X * scale);
            float z = (float)(chunk.Bounds.Min.Z * scale);
            float w = (float)((chunk.Bounds.Max.X - chunk.Bounds.Min.X) * scale);
            float h = (float)((chunk.Bounds.Max.Z - chunk.Bounds.Min.Z) * scale);

            // 块填充颜色（按LOD）
            if (ShowLODColorsCheck.IsChecked == true)
            {
                var color = LODColors[System.Math.Min(chunk.CurrentLOD, LODColors.Length - 1)];
                var fill = new SolidColorBrush(Color.FromArgb(150, color.R, color.G, color.B));

                var rect = new Rectangle
                {
                    Width = w,
                    Height = h,
                    Fill = fill
                };
                Canvas.SetLeft(rect, x);
                Canvas.SetTop(rect, z);
                DrawCanvas.Children.Add(rect);
            }

            // 块边界
            if (ShowChunkBoundsCheck.IsChecked == true)
            {
                var border = new Rectangle
                {
                    Width = w,
                    Height = h,
                    Stroke = new SolidColorBrush(Color.FromArgb(100, 255, 255, 255)),
                    StrokeThickness = 1
                };
                Canvas.SetLeft(border, x);
                Canvas.SetTop(border, z);
                DrawCanvas.Children.Add(border);
            }

            // LOD标签
            var label = new TextBlock
            {
                Text = $"L{chunk.CurrentLOD}",
                Foreground = Brushes.White,
                FontSize = 10,
                FontWeight = FontWeights.Bold
            };
            Canvas.SetLeft(label, x + w / 2 - 12);
            Canvas.SetTop(label, z + h / 2 - 8);
            DrawCanvas.Children.Add(label);
        }

        // 绘制观察点
        float viewX = (float)(_viewPosition.X * scale);
        float viewZ = (float)(_viewPosition.Z * scale);

        var viewMarker = new Ellipse
        {
            Width = 12,
            Height = 12,
            Fill = Brushes.Red,
            Stroke = Brushes.White,
            StrokeThickness = 2
        };
        Canvas.SetLeft(viewMarker, viewX - 6);
        Canvas.SetTop(viewMarker, viewZ - 6);
        DrawCanvas.Children.Add(viewMarker);

        // 绘制视线方向
        var line = new Line
        {
            X1 = viewX,
            Y1 = viewZ,
            X2 = viewX + 50,
            Y2 = viewZ - 30,
            Stroke = Brushes.Red,
            StrokeThickness = 2,
            Opacity = 0.7
        };
        DrawCanvas.Children.Add(line);
    }

    private void DrawCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (_multiresGrid == null) return;

        var pos = e.GetPosition(DrawCanvas);
        float scale = System.Math.Min(CanvasWidth, CanvasHeight) / _multiresGrid.TerrainWidth;

        _viewPosition = new Vector3(
            (float)(pos.X / scale),
            50,
            (float)(pos.Y / scale)
        );

        UpdateLOD();
    }

    private void DrawCanvas_MouseDown(object sender, MouseButtonEventArgs e)
    {
        var pos = e.GetPosition(DrawCanvas);
        Log($"鼠标点击位置: ({pos.X:F0}, {pos.Y:F0})");
    }

    #region 预设方案

    private void PresetPerformance_Click(object sender, RoutedEventArgs e)
    {
        TerrainSizeSlider.Value = 65;
        ChunkSizeSlider.Value = 32;
        MaxLODSlider.Value = 8;
        LODDistanceSlider.Value = 100;
        Log("已应用：高性能模式 - 块大、层级多、切换快");
        GenerateTerrain();
    }

    private void PresetBalanced_Click(object sender, RoutedEventArgs e)
    {
        TerrainSizeSlider.Value = 129;
        ChunkSizeSlider.Value = 16;
        MaxLODSlider.Value = 5;
        LODDistanceSlider.Value = 200;
        Log("已应用：均衡模式 - 性能与质量平衡");
        GenerateTerrain();
    }

    private void PresetQuality_Click(object sender, RoutedEventArgs e)
    {
        TerrainSizeSlider.Value = 257;
        ChunkSizeSlider.Value = 8;
        MaxLODSlider.Value = 4;
        LODDistanceSlider.Value = 400;
        Log("已应用：高质量模式 - 块小、细节高");
        GenerateTerrain();
    }

    #endregion

    private void Log(string message)
    {
        LogText.Text += $"[{DateTime.Now:HH:mm:ss.fff}] {message}\n";
        var scroll = (ScrollViewer)LogText.Parent;
        scroll.ScrollToEnd();
    }
}
