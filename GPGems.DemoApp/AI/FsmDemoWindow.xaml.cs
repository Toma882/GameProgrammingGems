using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using GPGems.AI.FSM;
using WpfCanvas = System.Windows.Controls.Canvas;

namespace GPGems.DemoApp;

/// <summary>
/// FSM 有限状态机演示窗口
/// NPC AI 巡逻-追击-攻击 状态机的交互式演示
/// </summary>
public partial class FsmDemoWindow : Window
{
    private NpcEntity _npc = null!;
    private NpcEntity _target = null!;
    private FiniteStateMachine _fsm = null!;
    private bool _isRunning;
    private double _timeScale = 1.0;

    // 场景可视化元素
    private Ellipse? _npcShape;
    private Ellipse? _targetShape;
    private Ellipse? _perceptionRangeShape;
    private Ellipse? _attackRangeShape;

    public FsmDemoWindow()
    {
        InitializeComponent();
        Loaded += (s, e) =>
        {
            InitializeSimulation();
            StartSimulation();
        };
    }

    /// <summary>初始化模拟</summary>
    private void InitializeSimulation()
    {
        // 清除旧的场景元素
        SceneCanvas.Children.Clear();

        // 创建 NPC 和目标（使用相对于画布中心的坐标，范围限制在画布内）
        double cx = SceneCanvas.ActualWidth / 2;
        double cy = SceneCanvas.ActualHeight / 2;
        _npc = new NpcEntity(cx - 100, cy);
        _target = new NpcEntity(cx + 100, cy);

        // 创建 FSM
        _fsm = NpcFsmFactory.CreatePatrolFSM(_npc, _target);

        // 绑定到可视化控件
        FsmCanvas.SetFSM(_fsm);

        // 创建场景元素
        CreateSceneElements();
    }

    /// <summary>创建2D场景的可视化元素</summary>
    private void CreateSceneElements()
    {
        // 感知范围圈
        _perceptionRangeShape = new Ellipse
        {
            Width = _npc.DetectionRange * 2,
            Height = _npc.DetectionRange * 2,
            Stroke = new SolidColorBrush(Color.FromRgb(78, 204, 163)),
            StrokeThickness = 1,
            StrokeDashArray = new DoubleCollection(new[] { 4.0, 4.0 }),
            Opacity = 0.5
        };
        SceneCanvas.Children.Add(_perceptionRangeShape);

        // 攻击范围圈
        _attackRangeShape = new Ellipse
        {
            Width = _npc.AttackRange * 2,
            Height = _npc.AttackRange * 2,
            Stroke = new SolidColorBrush(Color.FromRgb(233, 69, 96)),
            StrokeThickness = 1,
            StrokeDashArray = new DoubleCollection(new[] { 4.0, 4.0 }),
            Opacity = 0.5
        };
        SceneCanvas.Children.Add(_attackRangeShape);

        // NPC 形状（蓝色）
        _npcShape = new Ellipse
        {
            Width = 20,
            Height = 20,
            Fill = new SolidColorBrush(Color.FromRgb(78, 204, 163))
        };
        SceneCanvas.Children.Add(_npcShape);

        // 目标形状（红色）
        _targetShape = new Ellipse
        {
            Width = 20,
            Height = 20,
            Fill = new SolidColorBrush(Color.FromRgb(233, 69, 96))
        };
        SceneCanvas.Children.Add(_targetShape);
    }

    /// <summary>开始模拟</summary>
    public void StartSimulation()
    {
        if (_isRunning) return;
        _isRunning = true;
        CompositionTarget.Rendering += OnSimulationTick;
    }

    /// <summary>暂停模拟</summary>
    public void PauseSimulation()
    {
        _isRunning = false;
        CompositionTarget.Rendering -= OnSimulationTick;
    }

    /// <summary>模拟更新循环</summary>
    private void OnSimulationTick(object? sender, EventArgs e)
    {
        // 应用时间缩放
        int ticks = Math.Max(1, (int)_timeScale);
        for (int i = 0; i < ticks; i++)
        {
            // 更新 FSM
            _fsm.Update();

            // NPC 移动范围限制（确保不超出画布）
            _npc.X = Math.Clamp(_npc.X, 20, SceneCanvas.ActualWidth - 20);
            _npc.Y = Math.Clamp(_npc.Y, 20, SceneCanvas.ActualHeight - 20);

            // 目标随机移动（逃跑）
            if (_target.IsAlive)
            {
                double dx = (Random.Shared.NextDouble() - 0.5) * _target.Speed;
                double dy = (Random.Shared.NextDouble() - 0.5) * _target.Speed;
                _target.X = Math.Clamp(_target.X + dx, 20, SceneCanvas.ActualWidth - 20);
                _target.Y = Math.Clamp(_target.Y + dy, 20, SceneCanvas.ActualHeight - 20);
            }
        }

        // 更新 UI
        UpdateUI();
        RenderScene();
    }

    /// <summary>更新状态显示</summary>
    private void UpdateUI()
    {
        CurrentStateText.Text = $"当前状态: {_fsm.CurrentState.Name}";
        NpcHealthText.Text = $"NPC 生命值: {_npc.Health:F0}";
        TargetHealthText.Text = $"目标生命值: {_target.Health:F0}";
        DistanceText.Text = $"距离: {_npc.DistanceTo(_target):F1}";
    }

    /// <summary>渲染2D场景</summary>
    private void RenderScene()
    {
        if (_npcShape == null || _targetShape == null) return;

        // 更新 NPC 位置（直接使用实际坐标）
        WpfCanvas.SetLeft(_npcShape, _npc.X - 10);
        WpfCanvas.SetTop(_npcShape, _npc.Y - 10);

        // 更新感知范围
        WpfCanvas.SetLeft(_perceptionRangeShape, _npc.X - _npc.DetectionRange);
        WpfCanvas.SetTop(_perceptionRangeShape, _npc.Y - _npc.DetectionRange);

        // 更新攻击范围
        WpfCanvas.SetLeft(_attackRangeShape, _npc.X - _npc.AttackRange);
        WpfCanvas.SetTop(_attackRangeShape, _npc.Y - _npc.AttackRange);

        // 更新目标位置
        WpfCanvas.SetLeft(_targetShape, _target.X - 10);
        WpfCanvas.SetTop(_targetShape, _target.Y - 10);

        // 如果目标死亡，改变颜色
        if (!_target.IsAlive)
        {
            _targetShape.Fill = new SolidColorBrush(Color.FromRgb(128, 128, 128));
        }
    }

    /// <summary>重置模拟</summary>
    private void OnReset(object sender, RoutedEventArgs e)
    {
        PauseSimulation();
        InitializeSimulation();
        StartSimulation();

        _isRunning = false;
        PauseBtn.Content = "⏸️ 暂停";
        StartSimulation();
    }

    /// <summary>暂停/继续</summary>
    private void OnPause(object sender, RoutedEventArgs e)
    {
        if (_isRunning)
        {
            PauseSimulation();
            PauseBtn.Content = "▶️ 继续";
        }
        else
        {
            StartSimulation();
            PauseBtn.Content = "⏸️ 暂停";
        }
    }

    /// <summary>速度变化</summary>
    private void OnSpeedChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        _timeScale = e.NewValue;
        if (SpeedText != null)
            SpeedText.Text = $"{_timeScale:F1}x";
    }

    protected override void OnClosed(EventArgs e)
    {
        PauseSimulation();
        base.OnClosed(e);
    }
}
