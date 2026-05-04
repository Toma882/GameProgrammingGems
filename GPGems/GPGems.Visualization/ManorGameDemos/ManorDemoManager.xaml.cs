using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace GPGems.Visualization.ManorGameDemos;

/// <summary>
/// 庄园演示管理器
/// TODO: 基于新架构重新实现演示场景选择器
///
/// 新架构模块清单：
/// - ManorAlgorithmFacade      - 统一入口
/// - EmployeeManager           - 员工管理
/// - BuildingManager           - 建筑管理
/// - TaskScheduler             - 全局任务调度（GPGems.Core）
/// - VisitorFlowSystem         - 游客人流系统
/// - AnimalGroupSystem         - 动物群体系统
/// - EvacuationSystem          - 紧急疏散系统
/// </summary>
public partial class ManorDemoManager : UserControl
{
    private DispatcherTimer? _timer;
    private bool _isPlaying = false;
    private int _frame = 0;
    private DateTime _lastTime;

    private readonly List<Shape> _renderCache = new();
    private readonly ManorDemoScene _demoScene = new();

    public ManorDemoManager()
    {
        InitializeComponent();
        InitializeTimer();
        ResetCurrentScene();
    }

    private void InitializeTimer()
    {
        _timer = new DispatcherTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(16); // ~60fps
        _timer.Tick += (s, e) => OnFrame();
        _lastTime = DateTime.Now;
    }

    private void BtnPlayPause_Click(object sender, RoutedEventArgs e)
    {
        _isPlaying = !_isPlaying;
        BtnPlayPause.Content = _isPlaying ? "⏸ 暂停" : "▶ 播放";

        if (_isPlaying)
            _timer?.Start();
        else
            _timer?.Stop();
    }

    private void BtnReset_Click(object sender, RoutedEventArgs e)
    {
        ResetCurrentScene();
    }

    private void ResetCurrentScene()
    {
        _frame = 0;
        _demoScene.Reset((int)SliderCount.Value, (float)SliderSpeed.Value);
        Render();
    }

    private void OnFrame()
    {
        // 计算真实DeltaTime
        var now = DateTime.Now;
        float dt = (float)(now - _lastTime).TotalSeconds;
        _lastTime = now;

        // 应用速度倍率
        dt *= (float)SliderSpeed.Value;

        // 更新场景
        _demoScene.Update(dt);
        _frame++;

        // 更新统计显示
        StatsFrame.Text = $"帧数: {_frame}";
        StatsTime.Text = $"模拟时间: {_frame * 0.05f * SliderSpeed.Value:F1}s";
        StatsCollisions.Text = $"碰撞: {_demoScene.GetStat("collision")}";
        StatsThroughput.Text = $"吞吐量: {_demoScene.GetStat("throughput")}/秒";

        Render();
    }

    private void Render()
    {
        // 清除旧的渲染元素
        foreach (var shape in _renderCache)
            RenderCanvas.Children.Remove(shape);
        _renderCache.Clear();

        // 渲染场景
        _demoScene.RenderBackground(RenderCanvas, _renderCache);
        _demoScene.RenderAgents(RenderCanvas, _renderCache);
    }

    // TODO: 基于新架构实现以下交互事件
    private void IntFloor_Checked(object _, RoutedEventArgs __) { }
    private void IntegratedBuildingSelector_SelectionChanged(object _, SelectionChangedEventArgs __) { }
    private void BtnIntPlace_Click(object _, RoutedEventArgs __) { }
    private void BtnIntClear_Click(object _, RoutedEventArgs __) { }
}


