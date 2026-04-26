using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Shapes;
using GPGems.AI.Boids;
using GPGems.Core.Math;

namespace GPGems.Visualization.Boids;

/// <summary>
/// Boids 群体行为实时可视化控件
/// 使用简单的透视投影将 3D Boid 渲染到 2D Canvas
/// </summary>
public partial class BoidsCanvas : UserControl, IDisposable
{
    private Flock? _flock;
    private Flock? _secondFlock; // 第二个群体（捕食者）
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private int _frameCount;
    private double _lastFpsUpdate;
    private bool _isRunning;
    private readonly List<Ellipse> _boidShapes = [];
    private readonly List<Ellipse> _secondBoidShapes = [];
    private Ellipse? _targetMarker;

    /// <summary>控制模拟速度的倍率</summary>
    public float TimeScale { get; set; } = 0.5f;

    /// <summary>鼠标点击事件（返回世界坐标位置）</summary>
    public event Action<Vector3>? OnMouseClick;

    /// <summary>每帧更新前的回调</summary>
    public event Action? BeforeUpdate;

    public BoidsCanvas()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        DrawCanvas.MouseLeftButtonDown += OnMouseDown;
    }

    /// <summary>设置要显示的群体（单群体）</summary>
    public void SetFlock(Flock flock)
    {
        _flock = flock;
        _secondFlock = null;
        CreateBoidShapes();
    }

    /// <summary>设置要显示的群体（双群体）</summary>
    public void SetFlock(Flock flock, Flock? secondFlock)
    {
        _flock = flock;
        _secondFlock = secondFlock;
        CreateBoidShapes();
    }

    /// <summary>开始模拟</summary>
    public void StartSimulation()
    {
        if (_isRunning) return;
        _isRunning = true;
        CompositionTarget.Rendering += OnRender;
    }

    /// <summary>暂停模拟</summary>
    public void PauseSimulation()
    {
        _isRunning = false;
        CompositionTarget.Rendering -= OnRender;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        StartSimulation();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Dispose();
    }

    /// <summary>鼠标点击：转换为世界坐标并触发事件</summary>
    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_flock == null) return;

        var point = e.GetPosition(DrawCanvas);

        // 屏幕坐标转世界坐标
        var bounds = _flock.WorldBounds;
        float worldWidth = bounds.MaxX - bounds.MinX;
        float worldHeight = bounds.MaxY - bounds.MinY;

        float scaleX = (float)ActualWidth / worldWidth * 0.8f;
        float scaleY = (float)ActualHeight / worldHeight * 0.8f;
        float scale = System.Math.Min(scaleX, scaleY);

        float offsetX = (float)ActualWidth / 2;
        float offsetY = (float)ActualHeight / 2;

        // 反向转换
        float worldX = ((float)point.X - offsetX) / scale;
        float worldY = -((float)point.Y - offsetY) / scale;

        OnMouseClick?.Invoke(new Vector3(worldX, worldY, 0));
    }

    /// <summary>每帧更新：更新物理 + 渲染</summary>
    private void OnRender(object? sender, EventArgs e)
    {
        if (_flock == null || !_isRunning) return;

        // 每帧更新前的回调
        BeforeUpdate?.Invoke();

        // 时间缩放：每帧跳过多帧
        int steps = System.Math.Max(1, (int)(TimeScale * 2));
        for (int i = 0; i < steps; i++)
        {
            _flock.Update();
            _secondFlock?.Update();
        }

        // 渲染 Boids
        RenderBoids();

        // 更新 FPS
        _frameCount++;
        if (_stopwatch.Elapsed.TotalSeconds - _lastFpsUpdate >= 1.0)
        {
            int totalBoids = _flock.Boids.Count + (_secondFlock?.Boids.Count ?? 0);
            FpsText.Text = $"FPS: {_frameCount}";
            BoidCountText.Text = $"Boids: {totalBoids}";

            var avgVel = _flock.GetAverageVelocity();
            SpeedText.Text = $"速度: {avgVel.Length():F1}";

            _frameCount = 0;
            _lastFpsUpdate = _stopwatch.Elapsed.TotalSeconds;
        }
    }

    /// <summary>为每个 Boid 创建显示形状</summary>
    private void CreateBoidShapes()
    {
        DrawCanvas.Children.Clear();
        _boidShapes.Clear();
        _secondBoidShapes.Clear();
        _targetMarker = null;

        if (_flock == null) return;

        // 目标点标记
        _targetMarker = new Ellipse
        {
            Width = 20,
            Height = 20,
            Fill = new SolidColorBrush(Colors.Transparent),
            Stroke = new SolidColorBrush(Colors.Yellow),
            StrokeThickness = 2,
            Visibility = Visibility.Collapsed
        };
        DrawCanvas.Children.Add(_targetMarker);

        // 第一个群体形状
        var color1 = _flock.Color;
        var wpfColor1 = System.Windows.Media.Color.FromRgb(color1.R, color1.G, color1.B);
        for (int i = 0; i < _flock.Boids.Count; i++)
        {
            var ellipse = new Ellipse
            {
                Width = 8,
                Height = 8,
                Fill = new SolidColorBrush(wpfColor1),
                Opacity = 0.9
            };

            // 领导者用三角形或更大的标记
            if (_flock.Boids[i].IsLeader)
            {
                ellipse.Width = 14;
                ellipse.Height = 14;
                ellipse.Stroke = new SolidColorBrush(Colors.Gold);
                ellipse.StrokeThickness = 2;
            }

            _boidShapes.Add(ellipse);
            DrawCanvas.Children.Add(ellipse);
        }

        // 第二个群体形状
        if (_secondFlock != null)
        {
            var color2 = _secondFlock.Color;
            var wpfColor2 = System.Windows.Media.Color.FromRgb(color2.R, color2.G, color2.B);
            for (int i = 0; i < _secondFlock.Boids.Count; i++)
            {
                var ellipse = new Ellipse
                {
                    Width = 12, // 捕食者更大
                    Height = 12,
                    Fill = new SolidColorBrush(wpfColor2),
                    Opacity = 0.9
                };

                _secondBoidShapes.Add(ellipse);
                DrawCanvas.Children.Add(ellipse);
            }
        }
    }

    /// <summary>将所有 Boid 渲染到画布</summary>
    private void RenderBoids()
    {
        if (_flock == null || _flock.Boids.Count == 0) return;

        var bounds = _flock.WorldBounds;
        float worldWidth = bounds.MaxX - bounds.MinX;
        float worldHeight = bounds.MaxY - bounds.MinY;

        float offsetX = (float)ActualWidth / 2;
        float offsetY = (float)ActualHeight / 2;

        // 基础缩放：将世界坐标映射到画布（保留80%边距）
        float scaleX = (float)ActualWidth / worldWidth * 0.8f;
        float scaleY = (float)ActualHeight / worldHeight * 0.8f;
        float scale = System.Math.Min(scaleX, scaleY);

        // 更新目标点标记
        if (_targetMarker != null && _flock.GroupTarget.HasValue)
        {
            float tx = _flock.GroupTarget.Value.X * scale + offsetX;
            float ty = -_flock.GroupTarget.Value.Y * scale + offsetY;
            Canvas.SetLeft(_targetMarker, tx - 10);
            Canvas.SetTop(_targetMarker, ty - 10);
            _targetMarker.Visibility = Visibility.Visible;
        }
        else if (_targetMarker != null)
        {
            _targetMarker.Visibility = Visibility.Collapsed;
        }

        // 渲染第一个群体
        RenderFlock(_flock, _boidShapes, scale, offsetX, offsetY);

        // 渲染第二个群体
        if (_secondFlock != null)
        {
            RenderFlock(_secondFlock, _secondBoidShapes, scale, offsetX, offsetY);
        }
    }

    /// <summary>渲染单个群体</summary>
    private void RenderFlock(Flock flock, List<Ellipse> shapes, float scale, float offsetX, float offsetY)
    {
        for (int i = 0; i < flock.Boids.Count && i < shapes.Count; i++)
        {
            var boid = flock.Boids[i];
            var pos = boid.Position;

            // 纯 2D 显示
            float x = pos.X * scale + offsetX;
            float y = -pos.Y * scale + offsetY;

            // 设置位置
            Canvas.SetLeft(shapes[i], x - shapes[i].Width / 2);
            Canvas.SetTop(shapes[i], y - shapes[i].Height / 2);

            // 速度越快，越透明（减少密集时的混乱）
            shapes[i].Opacity = System.Math.Max(0.5, 0.95 - boid.Speed * 0.02);
        }
    }

    public void Dispose()
    {
        PauseSimulation();
    }
}
