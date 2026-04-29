using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using GPGems.Core.Math;
using GPGems.Graphics.LOD;
using System;

namespace GPGems.DemoApp.Graphics;

/// <summary>
/// 渐进网格 Progressive Mesh 演示窗口
/// </summary>
public partial class ProgressiveMeshDemoWindow : Window
{
    private ProgressiveMesh? _progressiveMesh;
    private readonly Random _random = new(42);
    private float _zoom = 1.0f;
    private float _rotationX = 0.5f;
    private float _rotationY = 0;
    private Point _lastMousePos;
    private bool _isAnimating;

    private const float CanvasWidth = 800;
    private const float CanvasHeight = 600;

    public ProgressiveMeshDemoWindow()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            InitializeSliders();
            GenerateMesh();
        };
    }

    private void InitializeSliders()
    {
        ResolutionSlider.ValueChanged += (_, _) =>
        {
            ResolutionValue.Text = ((int)ResolutionSlider.Value).ToString();
        };

        LODSlider.ValueChanged += (_, _) =>
        {
            var percent = (int)LODSlider.Value;
            LODSliderValue.Text = $"{percent}%";
            UpdateLODByPercent(percent);
        };

        TargetVertexSlider.ValueChanged += (_, _) =>
        {
            TargetVertexValue.Text = ((int)TargetVertexSlider.Value).ToString();
        };

        ShowOriginalEdgesCheck.Click += (_, _) => Redraw();
        ShowFoldedCheck.Click += (_, _) => Redraw();
        ShowNormalsCheck.Click += (_, _) => Redraw();
    }

    private void GenerateButton_Click(object sender, RoutedEventArgs e)
    {
        GenerateMesh();
    }

    private void AnimateLODButton_Click(object sender, RoutedEventArgs e)
    {
        _isAnimating = !_isAnimating;
        AnimateLODButton.Content = _isAnimating ? "⏹ 停止动画" : "▶ 自动播放LOD动画";

        if (_isAnimating)
        {
            Log("开始LOD动画，观察边折叠/分裂效果...");
            StartLODAnimation();
        }
        else
        {
            Log("停止LOD动画");
        }
    }

    private async void StartLODAnimation()
    {
        float t = 0;
        while (_isAnimating && IsVisible)
        {
            t += 0.01f;
            float percent = (MathF.Sin(t) + 1) * 50;
            LODSlider.Value = percent;
            await Task.Delay(30);
        }
    }

    private void GenerateMesh()
    {
        var sw = Stopwatch.StartNew();

        int resolution = (int)ResolutionSlider.Value;
        Log($"正在生成网格... 分辨率: {resolution}");

        Vector3[] vertices;
        int[] indices;

        switch (ModelComboBox.SelectedIndex)
        {
            case 0: // 不规则网格
                GenerateRandomMesh(resolution, out vertices, out indices);
                break;
            case 1: // 圆形网格
                GenerateCircleMesh(resolution, out vertices, out indices);
                break;
            case 2: // 高度场网格
                GenerateHeightfieldMesh(resolution, out vertices, out indices);
                break;
            case 3: // 星形网格
                GenerateStarMesh(resolution, out vertices, out indices);
                break;
            default:
                GenerateRandomMesh(resolution, out vertices, out indices);
                break;
        }

        Log($"原始网格: {vertices.Length} 顶点, {indices.Length / 3} 三角面");

        // 创建渐进网格
        _progressiveMesh = new ProgressiveMesh(vertices, indices);

        sw.Stop();
        PreprocessTimeText.Text = $"{sw.ElapsedMilliseconds} ms";

        TargetVertexSlider.Maximum = _progressiveMesh.OriginalVertexCount;
        TargetVertexSlider.Value = _progressiveMesh.OriginalVertexCount;

        Log($"渐进网格预处理完成: 最大简化层级 = {_progressiveMesh.MaxLevel}");

        UpdateStats();
        DrawErrorGraph();
        Redraw();
    }

    #region 网格生成器

    private void GenerateRandomMesh(int resolution, out Vector3[] vertices, out int[] indices)
    {
        int gridSize = (int)System.Math.Sqrt(resolution);
        var vertexList = new List<Vector3>();
        var indexList = new List<int>();

        // 生成随机点网格
        for (int y = 0; y < gridSize; y++)
        {
            for (int x = 0; x < gridSize; x++)
            {
                float fx = (x - gridSize / 2.0f) / gridSize * 400;
                float fy = (y - gridSize / 2.0f) / gridSize * 400;
                float fz = (float)(_random.NextDouble() - 0.5) * 100;
                vertexList.Add(new Vector3(fx, fz, fy));
            }
        }

        // 生成三角面
        for (int y = 0; y < gridSize - 1; y++)
        {
            for (int x = 0; x < gridSize - 1; x++)
            {
                int i = y * gridSize + x;
                indexList.Add(i);
                indexList.Add(i + gridSize);
                indexList.Add(i + 1);

                indexList.Add(i + 1);
                indexList.Add(i + gridSize);
                indexList.Add(i + gridSize + 1);
            }
        }

        vertices = vertexList.ToArray();
        indices = indexList.ToArray();
    }

    private void GenerateCircleMesh(int resolution, out Vector3[] vertices, out int[] indices)
    {
        var vertexList = new List<Vector3>();
        var indexList = new List<int>();

        int rings = 10;
        int segments = resolution / rings;

        // 中心点
        vertexList.Add(new Vector3(0, 0, 0));

        for (int ring = 1; ring <= rings; ring++)
        {
            float radius = ring * 200.0f / rings;
            float height = (float)System.Math.Sin(ring * System.Math.PI / rings) * 80;

            for (int seg = 0; seg < segments; seg++)
            {
                float angle = seg * 2.0f * (float)System.Math.PI / segments;
                float x = (float)System.Math.Cos(angle) * radius;
                float z = (float)System.Math.Sin(angle) * radius;
                vertexList.Add(new Vector3(x, height, z));
            }
        }

        // 生成三角面
        for (int ring = 0; ring < rings - 1; ring++)
        {
            for (int seg = 0; seg < segments; seg++)
            {
                int nextSeg = (seg + 1) % segments;
                int curr = ring * segments + seg + 1;
                int next = ring * segments + nextSeg + 1;
                int currNext = (ring + 1) * segments + seg + 1;
                int nextNext = (ring + 1) * segments + nextSeg + 1;

                if (ring == 0)
                {
                    indexList.Add(0);
                    indexList.Add(curr);
                    indexList.Add(next);
                }
                else
                {
                    indexList.Add(curr);
                    indexList.Add(currNext);
                    indexList.Add(next);

                    indexList.Add(next);
                    indexList.Add(currNext);
                    indexList.Add(nextNext);
                }
            }
        }

        vertices = vertexList.ToArray();
        indices = indexList.ToArray();
    }

    private void GenerateHeightfieldMesh(int resolution, out Vector3[] vertices, out int[] indices)
    {
        int gridSize = (int)System.Math.Sqrt(resolution);
        var vertexList = new List<Vector3>();
        var indexList = new List<int>();

        for (int y = 0; y < gridSize; y++)
        {
            for (int x = 0; x < gridSize; x++)
            {
                float fx = (x - gridSize / 2.0f) / gridSize * 400;
                float fy = (y - gridSize / 2.0f) / gridSize * 400;

                // 正弦波高度场
                float dist = (float)System.Math.Sqrt(fx * fx + fy * fy);
                float fz = (float)System.Math.Sin(dist * 0.05) * 50;

                vertexList.Add(new Vector3(fx, fz, fy));
            }
        }

        // 生成三角面
        for (int y = 0; y < gridSize - 1; y++)
        {
            for (int x = 0; x < gridSize - 1; x++)
            {
                int i = y * gridSize + x;
                indexList.Add(i);
                indexList.Add(i + gridSize);
                indexList.Add(i + 1);

                indexList.Add(i + 1);
                indexList.Add(i + gridSize);
                indexList.Add(i + gridSize + 1);
            }
        }

        vertices = vertexList.ToArray();
        indices = indexList.ToArray();
    }

    private void GenerateStarMesh(int resolution, out Vector3[] vertices, out int[] indices)
    {
        var vertexList = new List<Vector3>();
        var indexList = new List<int>();

        int points = 5;
        int layers = resolution / 20;

        vertexList.Add(new Vector3(0, 0, 0));

        for (int layer = 0; layer < layers; layer++)
        {
            float t = (float)layer / (layers - 1);
            float radius = 200 * (0.3f + 0.7f * (float)System.Math.Sin(t * System.Math.PI));
            float height = (t - 0.5f) * 200;

            for (int i = 0; i < points * 2; i++)
            {
                float angle = i * (float)System.Math.PI / points;
                float r = i % 2 == 0 ? radius : radius * 0.4f;
                float x = (float)System.Math.Cos(angle) * r;
                float z = (float)System.Math.Sin(angle) * r;
                vertexList.Add(new Vector3(x, height, z));
            }
        }

        // 生成三角面
        int verticesPerLayer = points * 2;
        for (int layer = 0; layer < layers - 1; layer++)
        {
            for (int i = 0; i < verticesPerLayer; i++)
            {
                int next = (i + 1) % verticesPerLayer;
                int curr = layer * verticesPerLayer + i + 1;
                int currNext = layer * verticesPerLayer + next + 1;
                int nextLayer = (layer + 1) * verticesPerLayer + i + 1;
                int nextLayerNext = (layer + 1) * verticesPerLayer + next + 1;

                indexList.Add(curr);
                indexList.Add(nextLayer);
                indexList.Add(currNext);

                indexList.Add(currNext);
                indexList.Add(nextLayer);
                indexList.Add(nextLayerNext);
            }
        }

        vertices = vertexList.ToArray();
        indices = indexList.ToArray();
    }

    #endregion

    private void UpdateLODByPercent(int percent)
    {
        if (_progressiveMesh == null) return;

        var sw = Stopwatch.StartNew();

        int targetLevel = (int)(_progressiveMesh.MaxLevel * percent / 100.0);
        _progressiveMesh.CurrentLevel = targetLevel;

        sw.Stop();
        LODSwitchTimeText.Text = $"{sw.ElapsedTicks / 10.0:F1} μs";

        UpdateStats();
        Redraw();
    }

    private void UpdateStats()
    {
        if (_progressiveMesh == null) return;

        int activeVerts = _progressiveMesh.ActiveVertexCount;
        int activeFaces = _progressiveMesh.ActiveFaceCount;
        int totalVerts = _progressiveMesh.OriginalVertexCount;
        int totalFaces = _progressiveMesh.OriginalFaceCount;

        VertexCountText.Text = $"顶点: {activeVerts} / {totalVerts}";
        FaceCountText.Text = $"三角面: {activeFaces} / {totalFaces}";
        CurrentLODText.Text = $"LOD层级: {_progressiveMesh.CurrentLevel} / {_progressiveMesh.MaxLevel}";

        float reduction = 100.0f * (1 - (float)activeVerts / totalVerts);
        VertexReductionText.Text = $"{reduction:F1}%";

        float error = _progressiveMesh.GetAccumulatedError(_progressiveMesh.CurrentLevel);
        ErrorText.Text = $"累计误差: {error:F2}";

        // 质量估算
        float quality = 100 - reduction * 0.5f;
        QualityBar.Value = quality;
        QualityText.Text = $"质量: {quality:F0}%";
    }

    private void DrawErrorGraph()
    {
        ErrorGraphCanvas.Children.Clear();

        if (_progressiveMesh == null) return;

        var errorCurve = _progressiveMesh.GetErrorCurve();
        if (errorCurve.Count == 0) return;

        float maxError = errorCurve.Max(e => e.Error);
        float maxFaces = errorCurve[0].Faces;
        float width = (float)ErrorGraphCanvas.ActualWidth;
        float height = (float)ErrorGraphCanvas.ActualHeight;

        if (width < 10) width = 200;

        var polyline = new Polyline
        {
            Stroke = new SolidColorBrush(Color.FromRgb(78, 204, 163)),
            StrokeThickness = 2
        };

        for (int i = 0; i < errorCurve.Count; i += System.Math.Max(1, errorCurve.Count / 50))
        {
            float x = width * (1.0f - (float)errorCurve[i].Faces / maxFaces);
            float y = height * errorCurve[i].Error / maxError;
            polyline.Points.Add(new Point(x, height - y));
        }

        ErrorGraphCanvas.Children.Add(polyline);
    }

    private void Redraw()
    {
        if (_progressiveMesh == null) return;

        DrawCanvas.Children.Clear();

        var activeVerts = _progressiveMesh.GetActiveVertices();
        var activeIndices = _progressiveMesh.GetActiveIndices();

        float centerX = CanvasWidth / 2;
        float centerY = CanvasHeight / 2;

        // 应用变换矩阵（简单的3D旋转投影）
        float cosX = MathF.Cos(_rotationX);
        float sinX = MathF.Sin(_rotationX);
        float cosY = MathF.Cos(_rotationY);
        float sinY = MathF.Sin(_rotationY);

        // 投影顶点
        var projected = new Point[activeVerts.Length];
        for (int i = 0; i < activeVerts.Length; i++)
        {
            var v = activeVerts[i];

            // 绕Y轴旋转
            float x = v.X * cosY - v.Z * sinY;
            float z = v.X * sinY + v.Z * cosY;

            // 绕X轴旋转
            float y = v.Y * cosX - z * sinX;
            z = v.Y * sinX + z * cosX;

            // 透视投影
            float scale = _zoom * 300 / (300 + z);
            projected[i] = new Point(centerX + x * scale, centerY + y * scale);
        }

        // 绘制三角面
        for (int i = 0; i < activeIndices.Length; i += 3)
        {
            int i0 = activeIndices[i];
            int i1 = activeIndices[i + 1];
            int i2 = activeIndices[i + 2];

            var p0 = projected[i0];
            var p1 = projected[i1];
            var p2 = projected[i2];

            // 计算法向量朝向
            float cross = (float)((p1.X - p0.X) * (p2.Y - p0.Y) - (p1.Y - p0.Y) * (p2.X - p0.X));
            if (cross < 0) continue; // 背面剔除

            var polygon = new Polygon
            {
                Fill = new SolidColorBrush(Color.FromArgb(180, 100, 150, 255)),
                Stroke = new SolidColorBrush(Color.FromArgb(100, 200, 220, 255)),
                StrokeThickness = 0.5
            };
            polygon.Points.Add(p0);
            polygon.Points.Add(p1);
            polygon.Points.Add(p2);
            DrawCanvas.Children.Add(polygon);
        }

        // 绘制顶点
        if (ShowOriginalEdgesCheck.IsChecked == true)
        {
            for (int i = 0; i < activeVerts.Length; i++)
            {
                var ellipse = new Ellipse
                {
                    Width = 3,
                    Height = 3,
                    Fill = Brushes.White
                };
                Canvas.SetLeft(ellipse, projected[i].X - 1.5);
                Canvas.SetTop(ellipse, projected[i].Y - 1.5);
                DrawCanvas.Children.Add(ellipse);
            }
        }
    }

    #region 鼠标交互

    private void DrawCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            var pos = e.GetPosition(DrawCanvas);
            _rotationY += (float)(pos.X - _lastMousePos.X) * 0.01f;
            _rotationX += (float)(pos.Y - _lastMousePos.Y) * 0.01f;
            _lastMousePos = pos;
            Redraw();
        }
        _lastMousePos = e.GetPosition(DrawCanvas);
    }

    private void DrawCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        _zoom *= e.Delta > 0 ? 1.1f : 0.9f;
        _zoom = System.Math.Clamp(_zoom, 0.3f, 3.0f);
        Redraw();
    }

    #endregion

    #region 预设方案

    private void PresetMaxDetail_Click(object sender, RoutedEventArgs e)
    {
        LODSlider.Value = 0;
        Log("已应用：最大细节 - 保留所有顶点");
    }

    private void PresetBalanced_Click(object sender, RoutedEventArgs e)
    {
        LODSlider.Value = 50;
        Log("已应用：平衡简化 - 约50%顶点");
    }

    private void PresetMaxSimplify_Click(object sender, RoutedEventArgs e)
    {
        LODSlider.Value = 95;
        Log("已应用：最大简化 - 仅保留关键顶点");
    }

    #endregion

    private void Log(string message)
    {
        LogText.Text += $"[{DateTime.Now:HH:mm:ss.fff}] {message}\n";
        var scroll = (ScrollViewer)LogText.Parent;
        scroll.ScrollToEnd();
    }
}
