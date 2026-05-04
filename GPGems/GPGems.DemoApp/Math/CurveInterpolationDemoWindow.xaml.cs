using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Numerics;
using GPGems.Core.Math;
using Vec2 = System.Numerics.Vector2;
using Vec3 = System.Numerics.Vector3;

namespace GPGems.DemoApp.Math;

/// <summary>
/// 曲线插值对比演示窗口
/// </summary>
public partial class CurveInterpolationDemoWindow : Window
{
    private class ControlPoint
    {
        public Vec2 Position;
        public Ellipse? Visual;
        public TranslateTransform? Transform;
    }

    private readonly List<ControlPoint> _controlPoints = new();
    private ControlPoint? _draggingPoint;
    private bool _isPlaying;
    private TCBSpline? _tcbSpline;

    public CurveInterpolationDemoWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => InitializeDemo();
    }

    private void InitializeDemo()
    {
        Log("曲线插值演示已启动");
        Log("Bezier/Hermite/Catmull-Rom/TCB 样条对比");
        ResetControlPoints();
    }

    private void ResetControlPoints()
    {
        _controlPoints.Clear();
        DrawCanvas.Children.Clear();

        double width = DrawCanvas.ActualWidth;
        double height = DrawCanvas.ActualHeight;

        if (width < 100) width = 800;
        if (height < 100) height = 500;

        var defaultPoints = new[]
        {
            new Vec2((float)(width * 0.1), (float)(height * 0.5)),
            new Vec2((float)(width * 0.3), (float)(height * 0.3)),
            new Vec2((float)(width * 0.7), (float)(height * 0.7)),
            new Vec2((float)(width * 0.9), (float)(height * 0.5))
        };

        foreach (var p in defaultPoints)
        {
            AddControlPoint(p);
        }

        RedrawCurve();
        UpdateStats();
    }

    private void AddControlPoint(Vec2 position)
    {
        var ellipse = new Ellipse
        {
            Width = 20,
            Height = 20,
            Fill = new SolidColorBrush(Color.FromRgb(233, 69, 96)),
            Stroke = Brushes.White,
            StrokeThickness = 2
        };

        var transform = new TranslateTransform(position.X - 10, position.Y - 10);
        ellipse.RenderTransform = transform;

        Canvas.SetZIndex(ellipse, 100);
        DrawCanvas.Children.Add(ellipse);

        _controlPoints.Add(new ControlPoint
        {
            Position = position,
            Visual = ellipse,
            Transform = transform
        });
    }

    private void RedrawCurve()
    {
        for (int i = DrawCanvas.Children.Count - 1; i >= 0; i--)
        {
            if (DrawCanvas.Children[i] is not Ellipse || (Ellipse)DrawCanvas.Children[i] != MovingParticle)
            {
                if (DrawCanvas.Children[i] is Line || DrawCanvas.Children[i] is Path)
                    DrawCanvas.Children.RemoveAt(i);
            }
        }

        if (_controlPoints.Count < 2) return;

        if (ShowControlPointsCheck.IsChecked == true)
        {
            var controlLine = new Polyline
            {
                Stroke = new SolidColorBrush(Color.FromArgb(100, 100, 100, 100)),
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 5, 5 }
            };

            foreach (var cp in _controlPoints)
            {
                controlLine.Points.Add(new Point(cp.Position.X, cp.Position.Y));
            }

            DrawCanvas.Children.Add(controlLine);
        }

        var curvePoints = GenerateCurvePoints();

        var curve = new Polyline
        {
            Stroke = new SolidColorBrush(Color.FromRgb(78, 204, 163)),
            StrokeThickness = 3
        };

        foreach (var p in curvePoints)
        {
            curve.Points.Add(new Point(p.X, p.Y));
        }

        DrawCanvas.Children.Add(curve);

        if (ShowTangentsCheck.IsChecked == true && curvePoints.Count > 1)
        {
            DrawTangents(curvePoints);
        }
    }

    private List<Vec2> GenerateCurvePoints()
    {
        var result = new List<Vec2>();
        int steps = 100;

        if (TypeBezier.IsChecked == true)
        {
            if (_controlPoints.Count == 4)
            {
                for (int i = 0; i <= steps; i++)
                {
                    float t = (float)i / steps;
                    var p = BezierCurve.Cubic(
                        _controlPoints[0].Position.ToVec3(),
                        _controlPoints[1].Position.ToVec3(),
                        _controlPoints[2].Position.ToVec3(),
                        _controlPoints[3].Position.ToVec3(),
                        t
                    );
                    result.Add(new Vec2(p.X, p.Y));
                }
            }
            else
            {
                for (int i = 0; i <= steps; i++)
                {
                    float t = (float)i / steps;
                    result.Add(BezierPoint(t));
                }
            }
        }
        else if (TypeHermite.IsChecked == true && _controlPoints.Count >= 2)
        {
            for (int i = 0; i < _controlPoints.Count - 1; i += 2)
            {
                if (i + 2 >= _controlPoints.Count) break;

                var p0 = _controlPoints[i].Position;
                var p1 = _controlPoints[i + 1].Position;
                var p2 = _controlPoints[i + 2].Position;
                var p3 = i + 3 < _controlPoints.Count ? _controlPoints[i + 3].Position : p2 + (p2 - p1);

                var t0 = (p1 - p0).Normalized() * 50;
                var t1 = (p3 - p2).Normalized() * 50;

                for (int j = 0; j <= steps / (_controlPoints.Count / 2); j++)
                {
                    float t = (float)j / (steps / (_controlPoints.Count / 2));
                    var p = Hermite.Interpolate(p1.ToVec3(), t0.ToVec3(), p2.ToVec3(), t1.ToVec3(), t);
                    result.Add(new Vec2(p.X, p.Y));
                }
            }
        }
        else if (TypeCatmullRom.IsChecked == true && _controlPoints.Count >= 4)
        {
            for (int i = 0; i < _controlPoints.Count - 3; i++)
            {
                for (int j = 0; j <= 20; j++)
                {
                    float t = (float)j / 20;
                    var p = CatmullRomSpline.Interpolate(
                        _controlPoints[i].Position.ToVec3(),
                        _controlPoints[i + 1].Position.ToVec3(),
                        _controlPoints[i + 2].Position.ToVec3(),
                        _controlPoints[i + 3].Position.ToVec3(),
                        t
                    );
                    result.Add(new Vec2(p.X, p.Y));
                }
            }
        }
        else if (TypeTCB.IsChecked == true && _controlPoints.Count >= 4)
        {
            var tcbPoints = _controlPoints.Select(cp => cp.Position.ToVec3()).ToArray();
            _tcbSpline = new TCBSpline(tcbPoints)
            {
                Tension = (float)TensionSlider.Value,
                Continuity = (float)ContinuitySlider.Value,
                Bias = (float)BiasSlider.Value
            };

            for (int i = 0; i < tcbPoints.Length - 1; i++)
            {
                for (int j = 0; j <= 20; j++)
                {
                    float t = (float)j / 20;
                    var p = _tcbSpline.GetPoint(i, t);
                    result.Add(new Vec2(p.X, p.Y));
                }
            }
        }

        return result;
    }

    private Vec2 BezierPoint(float t)
    {
        if (_controlPoints.Count == 0) return new Vec2(0, 0);
        if (_controlPoints.Count == 1) return _controlPoints[0].Position;

        var points = _controlPoints.Select(cp => cp.Position).ToList();
        while (points.Count > 1)
        {
            var next = new List<Vec2>();
            for (int i = 0; i < points.Count - 1; i++)
            {
                next.Add(points[i] * (1 - t) + points[i + 1] * t);
            }
            points = next;
        }
        return points[0];
    }

    private void DrawTangents(List<Vec2> curvePoints)
    {
        for (int i = 0; i < curvePoints.Count; i += 10)
        {
            int prev = System.Math.Max(0, i - 1);
            int next = System.Math.Min(curvePoints.Count - 1, i + 1);

            var tangent = (curvePoints[next] - curvePoints[prev]).Normalized() * 30;
            var start = curvePoints[i];
            var end = start + tangent;

            var line = new Line
            {
                X1 = start.X, Y1 = start.Y,
                X2 = end.X, Y2 = end.Y,
                Stroke = new SolidColorBrush(Color.FromRgb(243, 156, 18)),
                StrokeThickness = 2
            };
            DrawCanvas.Children.Add(line);

            var marker = new Ellipse
            {
                Width = 6, Height = 6,
                Fill = new SolidColorBrush(Color.FromRgb(243, 156, 18))
            };
            Canvas.SetLeft(marker, start.X - 3);
            Canvas.SetTop(marker, start.Y - 3);
            DrawCanvas.Children.Add(marker);
        }
    }

    private void UpdateStats()
    {
        PointCountText.Text = $"控制点: {_controlPoints.Count}";
        StatsPoints.Text = $"控制点数量: {_controlPoints.Count}";

        var curvePoints = GenerateCurvePoints();
        float length = 0;
        for (int i = 0; i < curvePoints.Count - 1; i++)
        {
            length += (curvePoints[i + 1] - curvePoints[i]).Length();
        }

        CurveLengthText.Text = $"曲线长度: {length:F0}";
        StatsLength.Text = $"曲线长度: {length:F0} px";
    }

    private void Log(string message)
    {
        LogText.Text = $"[{DateTime.Now:HH:mm:ss}] {message}\n" + LogText.Text;
        if (LogText.Text.Length > 5000)
            LogText.Text = LogText.Text.Substring(0, 5000);
    }

    #region 事件处理

    private void DrawCanvas_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.RightButton == MouseButtonState.Pressed)
        {
            var pos = e.GetPosition(DrawCanvas);
            var toRemove = _controlPoints.FirstOrDefault(
                cp => (cp.Position - new Vec2((float)pos.X, (float)pos.Y)).Length() < 20);

            if (toRemove != null && _controlPoints.Count > 2)
            {
                DrawCanvas.Children.Remove(toRemove.Visual);
                _controlPoints.Remove(toRemove);
                Log($"删除控制点，剩余 {_controlPoints.Count} 个");
                RedrawCurve();
                UpdateStats();
            }
            return;
        }

        var mousePos = e.GetPosition(DrawCanvas);
        var clickedPoint = _controlPoints.FirstOrDefault(
            cp => (cp.Position - new Vec2((float)mousePos.X, (float)mousePos.Y)).Length() < 20);

        if (clickedPoint != null)
        {
            _draggingPoint = clickedPoint;
            Mouse.Capture(DrawCanvas);
            return;
        }

        if (_controlPoints.Count < 10)
        {
            AddControlPoint(new Vec2((float)mousePos.X, (float)mousePos.Y));
            Log($"添加新控制点，共 {_controlPoints.Count} 个");
            RedrawCurve();
            UpdateStats();
        }
    }

    private void DrawCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (_draggingPoint == null) return;

        var pos = e.GetPosition(DrawCanvas);
        _draggingPoint.Position = new Vec2((float)pos.X, (float)pos.Y);
        _draggingPoint.Transform!.X = pos.X - 10;
        _draggingPoint.Transform!.Y = pos.Y - 10;

        RedrawCurve();
    }

    private void DrawCanvas_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_draggingPoint != null)
        {
            UpdateStats();
            Log($"控制点移动完成");
        }

        _draggingPoint = null;
        Mouse.Capture(null);
    }

    private void CurveTypeChanged(object sender, RoutedEventArgs e)
    {
        bool isTCB = TypeTCB.IsChecked == true;
        TensionSlider.IsEnabled = isTCB;
        ContinuitySlider.IsEnabled = isTCB;
        BiasSlider.IsEnabled = isTCB;

        if (TypeBezier.IsChecked == true)
        {
            AlgorithmTitle.Text = "三次 Bezier 曲线";
            AlgorithmDesc.Text = "通过4个控制点定义平滑曲线。前两个和后两个控制点决定曲线的起止切线方向。广泛应用于游戏动画、字体渲染、矢量图形。";
        }
        else if (TypeHermite.IsChecked == true)
        {
            AlgorithmTitle.Text = "Hermite 埃尔米特插值";
            AlgorithmDesc.Text = "通过位置和切线向量精确控制曲线走向。适合角色运动轨迹，能保证起点和终点的速度方向与大小完全可控。";
        }
        else if (TypeCatmullRom.IsChecked == true)
        {
            AlgorithmTitle.Text = "Catmull-Rom 样条";
            AlgorithmDesc.Text = "经过所有控制点的平滑曲线，自动计算切线。是相机路径、导航轨迹的首选算法，C1 连续且实现简单。";
        }
        else if (TypeTCB.IsChecked == true)
        {
            AlgorithmTitle.Text = "TCB 样条（Kochanek-Bartels）";
            AlgorithmDesc.Text = "三个参数控制曲线形态：Tension（张力）控制松紧，Continuity（连续）控制拐角尖锐度，Bias（偏移）控制偏向。动画师最爱！";
        }

        RedrawCurve();
        UpdateStats();
    }

    private void TCBParameterChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        TensionValue.Text = TensionSlider.Value.ToString("F2");
        ContinuityValue.Text = ContinuitySlider.Value.ToString("F2");
        BiasValue.Text = BiasSlider.Value.ToString("F2");

        if (TypeTCB.IsChecked == true)
        {
            RedrawCurve();
            UpdateStats();
        }
    }

    private void DisplayOptionChanged(object sender, RoutedEventArgs e)
    {
        RedrawCurve();
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        ResetControlPoints();
        Log("控制点已重置");
    }

    private async void PlayAnimButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isPlaying) return;
        _isPlaying = true;
        PlayAnimButton.Content = "⏹ 停止";

        MovingParticle.Visibility = Visibility.Visible;
        var curvePoints = GenerateCurvePoints();

        Log($"播放曲线动画，共 {curvePoints.Count} 帧");

        for (int i = 0; i < curvePoints.Count; i++)
        {
            var p = curvePoints[i];
            Canvas.SetLeft(MovingParticle, p.X - 8);
            Canvas.SetTop(MovingParticle, p.Y - 8);

            await Task.Delay(16);

            if (!_isPlaying) break;
        }

        MovingParticle.Visibility = Visibility.Collapsed;
        _isPlaying = false;
        PlayAnimButton.Content = "▶ 播放动画";
    }

    #endregion
}

internal static class VectorExtensions
{
    public static Vec3 ToVec3(this Vec2 v) => new(v.X, v.Y, 0);
}
