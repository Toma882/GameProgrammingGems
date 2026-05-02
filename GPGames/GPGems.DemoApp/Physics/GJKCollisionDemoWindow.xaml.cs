using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Numerics;
using GPGems.Core.Math;
using GPGems.Core.Geometry;
using GPGems.Core.Physics.Collision;
using Vec2 = System.Numerics.Vector2;
using Polygon = System.Windows.Shapes.Polygon;

namespace GPGems.DemoApp.Physics;

/// <summary>
/// GJK 碰撞检测演示窗口
/// </summary>
public partial class GJKCollisionDemoWindow : Window
{
    private abstract class Shape2D
    {
        public Vec2 Center;
        public float Rotation;
        public float Scale;

        public abstract Vec2 Support(Vec2 direction);
        public abstract void Draw(Canvas canvas, Brush stroke, Brush fill, double thickness = 2);
    }

    private class BoxShape2D : Shape2D
    {
        public override Vec2 Support(Vec2 direction)
        {
            var localDir = Rotate(direction, -Rotation);
            var localSupport = new Vec2(
                MathF.Sign(localDir.X) * Scale * 0.5f,
                MathF.Sign(localDir.Y) * Scale * 0.5f
            );
            return Center + Rotate(localSupport, Rotation);
        }

        public override void Draw(Canvas canvas, Brush stroke, Brush fill, double thickness = 2)
        {
            var half = Scale * 0.5f;
            var corners = new[]
            {
                Rotate(new Vec2(-half, -half), Rotation) + Center,
                Rotate(new Vec2(half, -half), Rotation) + Center,
                Rotate(new Vec2(half, half), Rotation) + Center,
                Rotate(new Vec2(-half, half), Rotation) + Center
            };

            var polygon = new Polygon
            {
                Stroke = stroke,
                Fill = fill,
                StrokeThickness = thickness,
                StrokeDashArray = fill == null ? new DoubleCollection { 5, 5 } : null
            };

            foreach (var c in corners)
                polygon.Points.Add(new Point(c.X, c.Y));

            canvas.Children.Add(polygon);
        }

        private static Vec2 Rotate(Vec2 v, float angle)
        {
            float c = MathF.Cos(angle), s = MathF.Sin(angle);
            return new Vec2(v.X * c - v.Y * s, v.X * s + v.Y * c);
        }
    }

    private class CircleShape2D : Shape2D
    {
        public override Vec2 Support(Vec2 direction)
        {
            return Center + direction.Normalize() * Scale * 0.5f;
        }

        public override void Draw(Canvas canvas, Brush stroke, Brush fill, double thickness = 2)
        {
            var ellipse = new Ellipse
            {
                Width = Scale,
                Height = Scale,
                Stroke = stroke,
                Fill = fill,
                StrokeThickness = thickness
            };

            Canvas.SetLeft(ellipse, Center.X - Scale * 0.5);
            Canvas.SetTop(ellipse, Center.Y - Scale * 0.5);
            canvas.Children.Add(ellipse);
        }
    }

    private class TriangleShape2D : Shape2D
    {
        public override Vec2 Support(Vec2 direction)
        {
            var rot = Rotation;
            var points = new[]
            {
                Rotate(new Vec2(0, -Scale * 0.5f), rot),
                Rotate(new Vec2(Scale * 0.5f, Scale * 0.4f), rot),
                Rotate(new Vec2(-Scale * 0.5f, Scale * 0.4f), rot)
            };

            float maxDot = float.MinValue;
            Vec2 best = Center + points[0];

            foreach (var p in points)
            {
                float dot = Vec2.Dot(Center + p, direction);
                if (dot > maxDot)
                {
                    maxDot = dot;
                    best = Center + p;
                }
            }
            return best;
        }

        public override void Draw(Canvas canvas, Brush stroke, Brush fill, double thickness = 2)
        {
            var rot = Rotation;
            var points = new[]
            {
                Rotate(new Vec2(0, -Scale * 0.5f), rot) + Center,
                Rotate(new Vec2(Scale * 0.5f, Scale * 0.4f), rot) + Center,
                Rotate(new Vec2(-Scale * 0.5f, Scale * 0.4f), rot) + Center
            };

            var polygon = new Polygon
            {
                Stroke = stroke,
                Fill = fill,
                StrokeThickness = thickness
            };

            foreach (var p in points)
                polygon.Points.Add(new Point(p.X, p.Y));

            canvas.Children.Add(polygon);
        }

        private static Vec2 Rotate(Vec2 v, float angle)
        {
            float c = MathF.Cos(angle), s = MathF.Sin(angle);
            return new Vec2(v.X * c - v.Y * s, v.X * s + v.Y * c);
        }
    }

    private class HexagonShape2D : Shape2D
    {
        public override Vec2 Support(Vec2 direction)
        {
            var rot = Rotation;
            float maxDot = float.MinValue;
            Vec2 best = Center;

            for (int i = 0; i < 6; i++)
            {
                float angle = i * MathF.PI / 3;
                var p = Rotate(new Vec2(MathF.Cos(angle), MathF.Sin(angle)) * Scale * 0.5f, rot);

                float dot = Vec2.Dot(Center + p, direction);
                if (dot > maxDot)
                {
                    maxDot = dot;
                    best = Center + p;
                }
            }
            return best;
        }

        public override void Draw(Canvas canvas, Brush stroke, Brush fill, double thickness = 2)
        {
            var polygon = new Polygon
            {
                Stroke = stroke,
                Fill = fill,
                StrokeThickness = thickness
            };

            for (int i = 0; i < 6; i++)
            {
                float angle = i * MathF.PI / 3;
                var p = Rotate(new Vec2(MathF.Cos(angle), MathF.Sin(angle)) * Scale * 0.5f, Rotation) + Center;
                polygon.Points.Add(new Point(p.X, p.Y));
            }

            canvas.Children.Add(polygon);
        }

        private static Vec2 Rotate(Vec2 v, float angle)
        {
            float c = MathF.Cos(angle), s = MathF.Sin(angle);
            return new Vec2(v.X * c - v.Y * s, v.X * s + v.Y * c);
        }
    }

    private Shape2D _shapeA = null!;
    private Shape2D _shapeB = null!;
    private int _draggingShape;
    private bool _isRotating;
    private long _gjkTime, _epaTime;

    public GJKCollisionDemoWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => InitializeDemo();
    }

    private void InitializeDemo()
    {
        ResetPosition();
        Log("GJK 碰撞检测演示已启动");
        Log("拖拽形状观察碰撞检测效果");
    }

    private void ResetPosition()
    {
        double width = DrawCanvas.ActualWidth > 100 ? DrawCanvas.ActualWidth : 800;
        double height = DrawCanvas.ActualHeight > 100 ? DrawCanvas.ActualHeight : 500;

        _shapeA = CreateShape(ShapeAType, (float)(width * 0.3), (float)(height * 0.5));
        _shapeB = CreateShape(ShapeBType, (float)(width * 0.7), (float)(height * 0.5));

        _shapeA.Scale = (float)ScaleASlider.Value;
        _shapeB.Scale = (float)ScaleBSlider.Value;

        Redraw();
    }

    private Shape2D CreateShape(ComboBox combo, float x, float y)
    {
        var type = (combo.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "矩形";

        Shape2D shape = type switch
        {
            "圆形" => new CircleShape2D(),
            "三角形" => new TriangleShape2D(),
            "六边形" => new HexagonShape2D(),
            _ => new BoxShape2D()
        };

        shape.Center = new Vec2(x, y);
        return shape;
    }

    private void Redraw()
    {
        DrawCanvas.Children.Clear();

        var watch = System.Diagnostics.Stopwatch.StartNew();
        bool collided = GJK2D.DetectCollision(_shapeA.Support, _shapeB.Support, out var simplex);
        watch.Stop();
        _gjkTime = watch.ElapsedTicks;

        float penetration = 0;
        Vec2 normal = new Vec2(0, 0);

        if (collided && ShowEPA.IsChecked == true)
        {
            watch.Restart();
            (penetration, normal) = EPA2D.ComputePenetration(_shapeA.Support, _shapeB.Support, simplex);
            watch.Stop();
            _epaTime = watch.ElapsedTicks;
        }

        var colorA = collided ? Brushes.LimeGreen : Brushes.ForestGreen;
        var colorB = collided ? Brushes.OrangeRed : Brushes.Firebrick;

        _shapeA.Draw(DrawCanvas, Brushes.White, colorA);
        _shapeB.Draw(DrawCanvas, Brushes.White, colorB);

        if (collided && ShowEPA.IsChecked == true && penetration > 0)
        {
            var midPoint = (_shapeA.Center + _shapeB.Center) * 0.5f;
            var arrowEnd = midPoint + normal * penetration * 2;

            var line = new Line
            {
                X1 = midPoint.X, Y1 = midPoint.Y,
                X2 = arrowEnd.X, Y2 = arrowEnd.Y,
                Stroke = Brushes.Yellow,
                StrokeThickness = 3
            };
            DrawCanvas.Children.Add(line);

            var head = new Ellipse { Width = 10, Height = 10, Fill = Brushes.Yellow };
            Canvas.SetLeft(head, arrowEnd.X - 5);
            Canvas.SetTop(head, arrowEnd.Y - 5);
            DrawCanvas.Children.Add(head);

            EPA_Depth.Text = $"穿透深度: {penetration:F2}";
            EPA_Normal.Text = $"法向量: ({normal.X:F2}, {normal.Y:F2})";
        }
        else
        {
            EPA_Depth.Text = "穿透深度: -";
            EPA_Normal.Text = "法向量: -";
        }

        CollisionStatus.Text = collided ? "状态: 碰撞!" : "状态: 无碰撞";
        CollisionStatus.Foreground = collided ? Brushes.LimeGreen : Brushes.White;
        IterationText.Text = $"迭代: {simplex.Iteration} 次";

        long freq = System.Diagnostics.Stopwatch.Frequency;
        Performance_GJK.Text = $"GJK: {(_gjkTime * 1000000.0 / freq):F0} μs";
        Performance_EPA.Text = $"EPA: {(_epaTime * 1000000.0 / freq):F0} μs";
    }

    private void Log(string message)
    {
        LogText.Text = $"[{DateTime.Now:HH:mm:ss}] {message}\n" + LogText.Text;
        if (LogText.Text.Length > 5000)
            LogText.Text = LogText.Text.Substring(0, 5000);
    }

    #region 2D GJK Implementation

    private struct Simplex2D
    {
        public Vec2 A, B, C;
        public int Count;
        public int Iteration;

        public void PushFront(Vec2 point)
        {
            C = B;
            B = A;
            A = point;
            Count = System.Math.Min(Count + 1, 3);
        }
    }

    private class GJK2D
    {
        public static bool DetectCollision(Func<Vec2, Vec2> supportA, Func<Vec2, Vec2> supportB, out Simplex2D simplex)
        {
            simplex = new Simplex2D();
            Vec2 direction = new Vec2(1, 0);

            simplex.PushFront(Support(supportA, supportB, direction));
            direction = -simplex.A;

            for (int i = 0; i < 50; i++)
            {
                simplex.Iteration = i + 1;
                simplex.PushFront(Support(supportA, supportB, direction));

                if (Vec2.Dot(simplex.A, direction) < 0)
                    return false;

                if (ContainsOrigin(ref simplex, ref direction))
                    return true;
            }

            return false;
        }

        private static Vec2 Support(Func<Vec2, Vec2> a, Func<Vec2, Vec2> b, Vec2 direction)
        {
            return a(direction) - b(-direction);
        }

        private static bool ContainsOrigin(ref Simplex2D simplex, ref Vec2 direction)
        {
            if (simplex.Count == 2)
                return LineContainsOrigin(ref simplex, ref direction);
            return TriangleContainsOrigin(ref simplex, ref direction);
        }

        private static bool LineContainsOrigin(ref Simplex2D s, ref Vec2 direction)
        {
            Vec2 ab = s.B - s.A;
            Vec2 ao = -s.A;

            if (Vec2.Dot(ab, ao) > 0)
            {
                direction = new Vec2(ab.Y, -ab.X).Normalize();
                if (Vec2.Dot(direction, ao) < 0)
                    direction = -direction;
            }
            else
            {
                s.Count = 1;
                direction = ao;
            }

            return false;
        }

        private static bool TriangleContainsOrigin(ref Simplex2D s, ref Vec2 direction)
        {
            Vec2 ab = s.B - s.A;
            Vec2 ac = s.C - s.A;
            Vec2 ao = -s.A;

            var abPerp = TripleProduct(ac, ab, ab);
            var acPerp = TripleProduct(ab, ac, ac);

            if (Vec2.Dot(abPerp, ao) > 0)
            {
                direction = abPerp;
                s.Count = 2;
                s.C = default;
            }
            else if (Vec2.Dot(acPerp, ao) > 0)
            {
                direction = acPerp;
                s.Count = 2;
                s.B = s.C;
                s.C = default;
            }
            else
            {
                return true;
            }

            return false;
        }

        private static Vec2 TripleProduct(Vec2 a, Vec2 b, Vec2 c)
        {
            float ac = a.X * c.X + a.Y * c.Y;
            float bc = b.X * c.X + b.Y * c.Y;
            return new Vec2(b.X * ac - a.X * bc, b.Y * ac - a.Y * bc).Normalize();
        }
    }

    private class EPA2D
    {
        public static (float depth, Vec2 normal) ComputePenetration(Func<Vec2, Vec2> supportA, Func<Vec2, Vec2> supportB, Simplex2D simplex)
        {
            var vertices = new List<Vec2>();
            if (simplex.Count >= 1) vertices.Add(simplex.A);
            if (simplex.Count >= 2) vertices.Add(simplex.B);
            if (simplex.Count >= 3) vertices.Add(simplex.C);

            for (int iteration = 0; iteration < 30; iteration++)
            {
                int closestIndex = FindClosestEdge(vertices);
                var (normal, distance) = GetEdgeNormal(vertices, closestIndex);

                var support = Support(supportA, supportB, normal);
                float supportDist = Vec2.Dot(support, normal);

                if (MathF.Abs(supportDist - distance) < 0.001f)
                    return (distance, normal);

                vertices.Insert(closestIndex + 1, support);
            }

            int finalClosest = FindClosestEdge(vertices);
            var (finalNormal, finalDist) = GetEdgeNormal(vertices, finalClosest);
            return (finalDist, finalNormal);
        }

        private static Vec2 Support(Func<Vec2, Vec2> a, Func<Vec2, Vec2> b, Vec2 direction)
        {
            return a(direction) - b(-direction);
        }

        private static int FindClosestEdge(List<Vec2> vertices)
        {
            float minDist = float.MaxValue;
            int closest = 0;

            for (int i = 0; i < vertices.Count; i++)
            {
                int j = (i + 1) % vertices.Count;
                var edge = vertices[j] - vertices[i];
                var normal = new Vec2(edge.Y, -edge.X).Normalize();
                float dist = Vec2.Dot(normal, vertices[i]);

                if (dist < minDist)
                {
                    minDist = dist;
                    closest = i;
                }
            }

            return closest;
        }

        private static (Vec2 normal, float distance) GetEdgeNormal(List<Vec2> vertices, int index)
        {
            int j = (index + 1) % vertices.Count;
            var edge = vertices[j] - vertices[index];
            var normal = new Vec2(edge.Y, -edge.X).Normalize();
            float dist = Vec2.Dot(normal, vertices[index]);
            return (normal, dist);
        }
    }

    #endregion

    #region 事件处理

    private void DrawCanvas_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        var pos = e.GetPosition(DrawCanvas);
        var mouse = new Vec2((float)pos.X, (float)pos.Y);

        if ((mouse - _shapeA.Center).Length() < _shapeA.Scale * 0.6f)
            _draggingShape = 1;
        else if ((mouse - _shapeB.Center).Length() < _shapeB.Scale * 0.6f)
            _draggingShape = 2;
        else
            _draggingShape = 0;
    }

    private void DrawCanvas_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_draggingShape == 0) return;

        var pos = e.GetPosition(DrawCanvas);

        if (_draggingShape == 1)
            _shapeA.Center = new Vec2((float)pos.X, (float)pos.Y);
        else
            _shapeB.Center = new Vec2((float)pos.X, (float)pos.Y);

        Redraw();
    }

    private void DrawCanvas_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _draggingShape = 0;
    }

    private void ShapeTypeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        ResetPosition();
    }

    private void ParameterChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        ScaleAValue.Text = $"大小 A: {ScaleASlider.Value:F0}";
        ScaleBValue.Text = $"大小 B: {ScaleBSlider.Value:F0}";

        if (_shapeA != null) _shapeA.Scale = (float)ScaleASlider.Value;
        if (_shapeB != null) _shapeB.Scale = (float)ScaleBSlider.Value;

        if (IsLoaded) Redraw();
    }

    private void DisplayOptionChanged(object sender, RoutedEventArgs e)
    {
        if (IsLoaded) Redraw();
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        ResetPosition();
        Log("位置已重置");
    }

    private void StepButton_Click(object sender, RoutedEventArgs e)
    {
        Log("单步演示: GJK 迭代检测碰撞");
    }

    private async void AutoRotateButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isRotating)
        {
            _isRotating = false;
            AutoRotateButton.Content = "🔄 自动旋转";
            return;
        }

        _isRotating = true;
        AutoRotateButton.Content = "⏹ 停止旋转";
        Log("开始自动旋转演示");

        float angle = 0;
        while (_isRotating)
        {
            angle += 0.03f;
            _shapeA.Rotation = angle;
            _shapeB.Rotation = -angle * 0.7f;
            Redraw();
            await Task.Delay(16);
        }
    }

    #endregion
}
