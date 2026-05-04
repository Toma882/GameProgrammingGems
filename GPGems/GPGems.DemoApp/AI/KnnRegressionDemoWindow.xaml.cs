using System;
using Math = System.Math;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using GPGems.Core.Algorithms;

namespace GPGems.DemoApp.AI;

/// <summary>
/// k-NN 回归模型可视化演示
/// 用于学习：加权平均、距离度量、k值对拟合曲线的影响
/// </summary>
public partial class KnnRegressionDemoWindow : Window
{
    private KNearestNeighborsRegressor<float[]>? _regressor;
    private readonly List<(float X, float Y)> _dataPoints = [];
    private readonly Random _random = new(42);
    private const double ChartPadding = 50;

    // 数据范围
    private float _dataMinX = 0;
    private float _dataMaxX = 10;
    private float _dataMinY = 0;
    private float _dataMaxY = 10;

    public KnnRegressionDemoWindow()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            LoadLinearData();
            KValueSlider.ValueChanged += (_, _) =>
            {
                KValueText.Text = KValueSlider.Value.ToString();
                Redraw();
            };
        };
    }

    #region 数据集加载

    private void LoadLinear_Click(object sender, RoutedEventArgs e) => LoadLinearData();
    private void LoadSin_Click(object sender, RoutedEventArgs e) => LoadSinData();
    private void LoadQuadratic_Click(object sender, RoutedEventArgs e) => LoadQuadraticData();
    private void LoadGameDamage_Click(object sender, RoutedEventArgs e) => LoadGameDamageData();

    private void LoadLinearData()
    {
        _dataPoints.Clear();
        for (float x = 0; x <= 10; x += 0.5f)
        {
            float y = 2 * x + (float)(_random.NextDouble() * 3 - 1.5); // y = 2x + 噪声
            _dataPoints.Add((x, y));
        }
        _dataMinX = 0; _dataMaxX = 10;
        _dataMinY = 0; _dataMaxY = 25;
        RebuildModel();
    }

    private void LoadSinData()
    {
        _dataPoints.Clear();
        for (float x = 0; x <= 10; x += 0.3f)
        {
            float y = (float)System.Math.Sin(x * 0.8) * 5 + 5 + (float)(_random.NextDouble() * 1 - 0.5);
            _dataPoints.Add((x, y));
        }
        _dataMinX = 0; _dataMaxX = 10;
        _dataMinY = 0; _dataMaxY = 10;
        RebuildModel();
    }

    private void LoadQuadraticData()
    {
        _dataPoints.Clear();
        for (float x = 0; x <= 10; x += 0.4f)
        {
            float y = x * x * 0.1f + (float)(_random.NextDouble() * 4 - 2);
            _dataPoints.Add((x, y));
        }
        _dataMinX = 0; _dataMaxX = 10;
        _dataMinY = 0; _dataMaxY = 12;
        RebuildModel();
    }

    private void LoadGameDamageData()
    {
        _dataPoints.Clear();
        // 模拟：攻击强度 vs 实际伤害（带护甲减免的非线性关系）
        for (float attack = 10; attack <= 200; attack += 8)
        {
            // 伤害 = 攻击 * (1 - e^(-攻击/50))
            float damage = attack * (1 - (float)System.Math.Exp(-attack / 50)) + (float)(_random.NextDouble() * 10 - 5);
            _dataPoints.Add((attack, damage));
        }
        _dataMinX = 0; _dataMaxX = 210;
        _dataMinY = 0; _dataMaxY = 200;
        RebuildModel();
    }

    private void AddRandom_Click(object sender, RoutedEventArgs e)
    {
        float x = _dataMinX + (float)(_random.NextDouble() * (_dataMaxX - _dataMinX));
        float y = _dataMinY + (float)(_random.NextDouble() * (_dataMaxY - _dataMinY));
        _dataPoints.Add((x, y));
        RebuildModel();
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        _dataPoints.Clear();
        RebuildModel();
    }

    #endregion

    #region 模型构建与预测

    private void RebuildModel()
    {
        int k = (int)KValueSlider.Value;
        _regressor = new KNearestNeighborsRegressor<float[]>(k, (a, b) => MathF.Abs(a[0] - b[0]));

        foreach (var (x, y) in _dataPoints)
        {
            _regressor.AddTrainingData(new[] { x }, y);
        }

        SampleCountText.Text = $"样本数: {_dataPoints.Count}";
        Redraw();
    }

    private float Predict(float x)
    {
        if (_regressor == null || _dataPoints.Count == 0) return 0;

        var sw = Stopwatch.StartNew();
        float result = DistanceWeight.IsChecked == true
            ? _regressor.RegressWeighted(new[] { x })
            : _regressor.Regress(new[] { x });
        sw.Stop();

        PredictionTimeText.Text = $"预测耗时: {sw.Elapsed.TotalMicroseconds:F1} μs";
        return result;
    }

    #endregion

    #region 坐标映射

    private (double X, double Y) ToCanvas(float x, float y)
    {
        double chartWidth = ChartCanvas.ActualWidth - ChartPadding * 2;
        double chartHeight = ChartCanvas.ActualHeight - ChartPadding * 2;

        double canvasX = ChartPadding + (x - _dataMinX) / (_dataMaxX - _dataMinX) * chartWidth;
        double canvasY = ChartCanvas.ActualHeight - ChartPadding - (y - _dataMinY) / (_dataMaxY - _dataMinY) * chartHeight;

        return (canvasX, canvasY);
    }

    private float ToDataX(double canvasX)
    {
        double chartWidth = ChartCanvas.ActualWidth - ChartPadding * 2;
        return (float)((canvasX - ChartPadding) / chartWidth * (_dataMaxX - _dataMinX) + _dataMinX);
    }

    #endregion

    #region 绘制

    private void Redraw(object? sender = null, MouseEventArgs? e = null)
    {
        if (_regressor == null || _dataPoints.Count == 0) return;

        ChartCanvas.Children.Clear();
        DrawAxes();
        DrawDataPoints();

        if (ShowRegressionLineCheck.IsChecked == true)
            DrawRegressionCurve();

        // 计算并显示均方误差
        UpdateMse();
    }

    private void DrawAxes()
    {
        // X轴
        var xAxis = new Line
        {
            X1 = ChartPadding,
            Y1 = ChartCanvas.ActualHeight - ChartPadding,
            X2 = ChartCanvas.ActualWidth - ChartPadding,
            Y2 = ChartCanvas.ActualHeight - ChartPadding,
            Stroke = new SolidColorBrush(Color.FromRgb(100, 100, 150)),
            StrokeThickness = 1
        };
        ChartCanvas.Children.Add(xAxis);

        // Y轴
        var yAxis = new Line
        {
            X1 = ChartPadding,
            Y1 = ChartPadding,
            X2 = ChartPadding,
            Y2 = ChartCanvas.ActualHeight - ChartPadding,
            Stroke = new SolidColorBrush(Color.FromRgb(100, 100, 150)),
            StrokeThickness = 1
        };
        ChartCanvas.Children.Add(yAxis);
    }

    private void DrawDataPoints()
    {
        foreach (var (x, y) in _dataPoints)
        {
            var (cx, cy) = ToCanvas(x, y);
            var ellipse = new Ellipse
            {
                Width = 8,
                Height = 8,
                Fill = new SolidColorBrush(Color.FromRgb(52, 152, 219))
            };
            Canvas.SetLeft(ellipse, cx - 4);
            Canvas.SetTop(ellipse, cy - 4);
            ChartCanvas.Children.Add(ellipse);
        }
    }

    private void DrawRegressionCurve()
    {
        if (_regressor == null) return;

        var pathGeometry = new PathGeometry();
        var figure = new PathFigure();
        bool isFirst = true;

        for (float x = _dataMinX; x <= _dataMaxX; x += 0.1f)
        {
            float y = Predict(x);
            var (cx, cy) = ToCanvas(x, y);

            if (isFirst)
            {
                figure.StartPoint = new Point(cx, cy);
                isFirst = false;
            }
            else
            {
                figure.Segments.Add(new LineSegment(new Point(cx, cy), true));
            }
        }

        pathGeometry.Figures.Add(figure);
        var path = new System.Windows.Shapes.Path
        {
            Data = pathGeometry,
            Stroke = new SolidColorBrush(Color.FromRgb(46, 204, 113)),
            StrokeThickness = 2,
            Opacity = 0.9
        };
        ChartCanvas.Children.Add(path);
    }

    private void UpdateMse()
    {
        if (_regressor == null || _dataPoints.Count == 0)
        {
            MseText.Text = "均方误差: --";
            return;
        }

        float mse = 0;
        foreach (var (x, y) in _dataPoints)
        {
            float pred = _regressor.RegressWeighted(new[] { x });
            mse += (y - pred) * (y - pred);
        }
        mse /= _dataPoints.Count;

        MseText.Text = $"均方误差: {mse:F3}";
    }

    #endregion

    #region 鼠标交互

    private void ChartCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (_regressor == null || _dataPoints.Count == 0) return;

        var pos = e.GetPosition(ChartCanvas);
        float queryX = ToDataX(pos.X);

        // 限制在数据范围内
        queryX = System.Math.Clamp(queryX, _dataMinX, _dataMaxX);
        float prediction = Predict(queryX);

        QueryXText.Text = $"{queryX:F2}";
        PredictionYText.Text = $"{prediction:F2}";

        // 获取k个最近邻居
        var neighbors = _regressor.FindNearest(new[] { queryX });

        // 更新邻居列表
        NeighborsListBox.Items.Clear();
        for (int i = 0; i < neighbors.Count; i++)
        {
            var n = neighbors[i];
            NeighborsListBox.Items.Add(
                $"#{i + 1} X={n.Point[0]:F2} → Y={n.Label:F2} 距离={n.Distance:F2}");
        }

        Redraw();
        DrawQueryPoint(queryX, prediction, pos);

        // 高亮最近的k个邻居
        if (ShowNeighborsCheck.IsChecked == true)
        {
            HighlightNeighbors(neighbors, queryX, prediction);
        }
    }

    private void DrawQueryPoint(float x, float y, Point mousePos)
    {
        var (cx, cy) = ToCanvas(x, y);

        // 查询点（红色大圆点）
        var queryPoint = new Ellipse
        {
            Width = 16,
            Height = 16,
            Fill = new SolidColorBrush(Color.FromRgb(231, 76, 60))
        };
        Canvas.SetLeft(queryPoint, cx - 8);
        Canvas.SetTop(queryPoint, cy - 8);
        Panel.SetZIndex(queryPoint, 100);
        ChartCanvas.Children.Add(queryPoint);

        // 预测值的水平参考线
        var hLine = new Line
        {
            X1 = ChartPadding,
            Y1 = cy,
            X2 = cx,
            Y2 = cy,
            Stroke = new SolidColorBrush(Color.FromRgb(231, 76, 60)),
            StrokeThickness = 1,
            Opacity = 0.5,
            StrokeDashArray = new DoubleCollection { 4, 4 }
        };
        ChartCanvas.Children.Add(hLine);
    }

    private void HighlightNeighbors(List<(float[] Point, float Label, float Distance)> neighbors, float queryX, float queryY)
    {
        var (qcx, qcy) = ToCanvas(queryX, queryY);

        foreach (var n in neighbors)
        {
            var (cx, cy) = ToCanvas(n.Point[0], n.Label);

            // 橙色高亮圆圈
            var highlight = new Ellipse
            {
                Width = 14,
                Height = 14,
                Stroke = new SolidColorBrush(Color.FromRgb(241, 196, 15)),
                StrokeThickness = 2,
                Fill = Brushes.Transparent
            };
            Canvas.SetLeft(highlight, cx - 7);
            Canvas.SetTop(highlight, cy - 7);
            ChartCanvas.Children.Add(highlight);

            // 距离连线
            if (ShowDistanceLinesCheck.IsChecked == true)
            {
                var line = new Line
                {
                    X1 = cx,
                    Y1 = cy,
                    X2 = qcx,
                    Y2 = qcy,
                    Stroke = new SolidColorBrush(Color.FromRgb(241, 196, 15)),
                    StrokeThickness = 1,
                    Opacity = 0.4
                };
                ChartCanvas.Children.Add(line);
            }
        }
    }

    private void ChartCanvas_MouseLeave(object sender, MouseEventArgs e)
    {
        Redraw();
        QueryXText.Text = "--";
        PredictionYText.Text = "--";
        NeighborsListBox.Items.Clear();
    }

    #endregion
}
