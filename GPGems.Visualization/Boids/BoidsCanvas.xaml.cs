using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Shapes;
using GPGems.AI.Boids;
using GPGems.Core.Math;
using Vector3 = GPGems.Core.Math.Vector3;

namespace GPGems.Visualization.Boids;

/// <summary>
/// Boids 群体行为实时可视化控件
/// 使用简单的透视投影将 3D Boid 渲染到 2D Canvas
/// </summary>
public partial class BoidsCanvas : UserControl, IDisposable
{
    private Flock? _flock;
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private int _frameCount;
    private double _lastFpsUpdate;
    private bool _isRunning;
    private readonly List<Ellipse> _boidShapes = [];

    /// <summary>控制模拟速度的倍率</summary>
    public float TimeScale { get; set; } = 0.5f;

    public BoidsCanvas()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    /// <summary>设置要显示的群体</summary>
    public void SetFlock(Flock flock)
    {
        _flock = flock;
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

    /// <summary>每帧更新：更新物理 + 渲染</summary>
    private void OnRender(object? sender, EventArgs e)
    {
        if (_flock == null || !_isRunning) return;

        // 更新物理
        _flock.Update();

        // 渲染 Boids
        RenderBoids();

        // 更新 FPS
        _frameCount++;
        if (_stopwatch.Elapsed.TotalSeconds - _lastFpsUpdate >= 1.0)
        {
            FpsText.Text = $"FPS: {_frameCount}";
            BoidCountText.Text = $"Boids: {_flock.Boids.Count}";

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

        if (_flock == null) return;

        var color = _flock.Color;
        var wpfColor = System.Windows.Media.Color.FromRgb(color.R, color.G, color.B);
        for (int i = 0; i < _flock.Boids.Count; i++)
        {
            var ellipse = new Ellipse
            {
                Width = 8,
                Height = 8,
                Fill = new SolidColorBrush(wpfColor),
                Opacity = 0.9
            };

            _boidShapes.Add(ellipse);
            DrawCanvas.Children.Add(ellipse);
        }
    }

    /// <summary>将所有 Boid 渲染到画布</summary>
    private void RenderBoids()
    {
        if (_flock == null || _flock.Boids.Count == 0) return;

        var center = _flock.WorldBounds;
        float scaleX = (float)ActualWidth / (center.MaxX - center.MinX) * 0.8f;
        float scaleY = (float)ActualHeight / (center.MaxY - center.MinY) * 0.8f;
        float scale = Math.Min(scaleX, scaleY);

        float offsetX = (float)ActualWidth / 2;
        float offsetY = (float)ActualHeight / 2;

        // 简单的透视相机参数
        float cameraDistance = 200f;

        for (int i = 0; i < _flock.Boids.Count && i < _boidShapes.Count; i++)
        {
            var boid = _flock.Boids[i];
            var pos = boid.Position;

            // 透视投影：越远越小
            float perspective = cameraDistance / (cameraDistance + pos.Z);

            // 3D 转 2D 坐标
            float x = pos.X * scale * perspective + offsetX;
            float y = -pos.Y * scale * perspective + offsetY; // Y 轴翻转

            // 设置位置
            Canvas.SetLeft(_boidShapes[i], x - 4);
            Canvas.SetTop(_boidShapes[i], y - 4);

            // 距离越远，透明度越高
            _boidShapes[i].Opacity = Math.Clamp(perspective * 0.5 + 0.3, 0.3, 1.0);

            // 根据速度缩放大小
            float size = 6 + boid.Speed * 0.1f;
            _boidShapes[i].Width = size;
            _boidShapes[i].Height = size;
        }
    }

    public void Dispose()
    {
        PauseSimulation();
    }
}
