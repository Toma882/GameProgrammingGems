using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using GPGems.AI.Steering;
using GPGems.Core.Math;

namespace GPGems.Visualization.Steering;

/// <summary>
/// 定向行为演示画布
/// 展示单一 Steering 行为的纯净效果
/// </summary>
public partial class SteeringCanvas : UserControl, IDisposable
{
    internal SteeringAgent? _agent;
    internal SteeringAgent? _targetAgent; // 移动目标（用于 Pursue/Evade）
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private int _frameCount;
    private double _lastFpsUpdate;
    private bool _isRunning;
    private Ellipse? _agentShape;
    private Ellipse? _targetShape;
    private Ellipse? _targetAgentShape;
    private readonly List<Line> _trailLines = [];
    private int _trailCounter;

    /// <summary>控制模拟速度</summary>
    public float TimeScale { get; set; } = 1f;

    /// <summary>当前行为类型</summary>
    public SteeringBehaviorType CurrentBehavior { get; set; } = SteeringBehaviorType.Seek;

    /// <summary>鼠标点击事件</summary>
    public event Action<Vector3>? OnMouseClick;

    public SteeringCanvas()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        DrawCanvas.MouseLeftButtonDown += OnMouseDown;
    }

    /// <summary>初始化智能体</summary>
    public void InitializeAgent()
    {
        _agent = new SteeringAgent(new Vector3(-100, 0, 0))
        {
            MaxSpeed = 3f,
            MaxForce = 0.15f,
            Mass = 1f
        };

        _targetAgent = new SteeringAgent(new Vector3(100, 50, 0))
        {
            MaxSpeed = 2f,
            MaxForce = 0.1f
        };
        _targetAgent.AddBehavior<WanderBehavior>();

        CreateShapes();
        UpdateBehaviorInfo();
    }

    /// <summary>切换行为类型</summary>
    public void SetBehavior(SteeringBehaviorType behaviorType)
    {
        CurrentBehavior = behaviorType;

        if (_agent == null) return;

        _agent.ClearBehaviors();
        _agent.Velocity = Vector3.Zero;

        switch (behaviorType)
        {
            case SteeringBehaviorType.Seek:
                _agent.AddBehavior<SeekBehavior>();
                break;
            case SteeringBehaviorType.Flee:
                _agent.AddBehavior<FleeBehavior>();
                break;
            case SteeringBehaviorType.Arrive:
                _agent.AddBehavior<ArriveBehavior>();
                break;
            case SteeringBehaviorType.Wander:
                _agent.AddBehavior<WanderBehavior>();
                break;
            case SteeringBehaviorType.Pursue:
                _agent.AddBehavior<PursueBehavior>();
                break;
            case SteeringBehaviorType.Evade:
                _agent.AddBehavior<EvadeBehavior>();
                break;
        }

        UpdateBehaviorInfo();
        ClearTrail();
    }

    /// <summary>设置目标位置</summary>
    public void SetTargetPosition(Vector3 position)
    {
        if (_agent != null)
            _agent.TargetPosition = position;

        if (_targetShape != null)
        {
            Canvas.SetLeft(_targetShape, position.X + 300 - 8);
            Canvas.SetTop(_targetShape, -position.Y + 200 - 8);
        }
    }

    /// <summary>重置智能体位置</summary>
    public void ResetAgent()
    {
        if (_agent != null)
        {
            _agent.Position = new Vector3(-100, 0, 0);
            _agent.Velocity = Vector3.Zero;
        }
        if (_targetAgent != null)
        {
            _targetAgent.Position = new Vector3(100, 50, 0);
            _targetAgent.Velocity = Vector3.Zero;
        }
        ClearTrail();
    }

    /// <summary>设置最大速度</summary>
    public void SetMaxSpeed(float speed)
    {
        if (_agent != null)
            _agent.MaxSpeed = speed;
    }

    /// <summary>设置最大转向力</summary>
    public void SetMaxForce(float force)
    {
        if (_agent != null)
            _agent.MaxForce = force;
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
        InitializeAgent();
        StartSimulation();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Dispose();
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        var point = e.GetPosition(DrawCanvas);

        // 屏幕坐标转世界坐标（中心原点）
        float worldX = (float)point.X - 300;
        float worldY = -((float)point.Y - 200);

        // 点击画布任意位置都可以设置/移动目标点
        OnMouseClick?.Invoke(new Vector3(worldX, worldY, 0));
    }

    /// <summary>创建显示形状</summary>
    private void CreateShapes()
    {
        DrawCanvas.Children.Clear();
        _trailLines.Clear();

        // 智能体（绿色）
        _agentShape = new Ellipse
        {
            Width = 16,
            Height = 16,
            Fill = new SolidColorBrush(Colors.LimeGreen)
        };
        DrawCanvas.Children.Add(_agentShape);

        // 目标点（黄色圆圈）
        _targetShape = new Ellipse
        {
            Width = 16,
            Height = 16,
            Fill = new SolidColorBrush(Colors.Transparent),
            Stroke = new SolidColorBrush(Colors.Yellow),
            StrokeThickness = 2
        };
        DrawCanvas.Children.Add(_targetShape);

        // 移动目标（蓝色，用于Pursue/Evade）
        _targetAgentShape = new Ellipse
        {
            Width = 14,
            Height = 14,
            Fill = new SolidColorBrush(Colors.DodgerBlue),
            Visibility = Visibility.Collapsed
        };
        DrawCanvas.Children.Add(_targetAgentShape);
    }

    /// <summary>清除轨迹</summary>
    private void ClearTrail()
    {
        foreach (var line in _trailLines)
            DrawCanvas.Children.Remove(line);
        _trailLines.Clear();
    }

    /// <summary>更新行为说明</summary>
    private void UpdateBehaviorInfo()
    {
        BehaviorInfoText.Text = CurrentBehavior switch
        {
            SteeringBehaviorType.Seek => "Seek：朝目标点直线移动",
            SteeringBehaviorType.Flee => "Flee：逃离目标点",
            SteeringBehaviorType.Arrive => "Arrive：接近时减速停止",
            SteeringBehaviorType.Wander => "Wander：自然随机漫游",
            SteeringBehaviorType.Pursue => "Pursue：预测目标移动轨迹追击",
            SteeringBehaviorType.Evade => "Evade：预测威胁轨迹躲避",
            _ => ""
        };
    }

    /// <summary>每帧更新</summary>
    private void OnRender(object? sender, EventArgs e)
    {
        if (_agent == null || !_isRunning) return;

        // 时间缩放
        int steps = System.Math.Max(1, (int)TimeScale);
        for (int i = 0; i < steps; i++)
        {
            // 对于需要移动目标的行为，更新目标智能体
            if (CurrentBehavior is SteeringBehaviorType.Pursue or SteeringBehaviorType.Evade)
            {
                _targetAgent?.Update(0.5f);
                _agent.TargetAgent = _targetAgent;

                // 显示移动目标
                if (_targetAgentShape != null && _targetAgent != null)
                {
                    _targetAgentShape.Visibility = Visibility.Visible;
                    Canvas.SetLeft(_targetAgentShape, _targetAgent.Position.X + 300 - 7);
                    Canvas.SetTop(_targetAgentShape, -_targetAgent.Position.Y + 200 - 7);
                }
            }
            else
            {
                if (_targetAgentShape != null)
                    _targetAgentShape.Visibility = Visibility.Collapsed;
            }

            _agent.Update(0.5f);
        }

        // 更新智能体位置
        if (_agentShape != null)
        {
            float x = _agent.Position.X + 300;
            float y = -_agent.Position.Y + 200;
            Canvas.SetLeft(_agentShape, x - 8);
            Canvas.SetTop(_agentShape, y - 8);

            // 绘制轨迹（每5帧画一次）
            _trailCounter++;
            if (_trailCounter >= 5)
            {
                var line = new Line
                {
                    X1 = x,
                    Y1 = y,
                    X2 = x,
                    Y2 = y,
                    Stroke = new SolidColorBrush(Colors.LimeGreen) { Opacity = 0.3 },
                    StrokeThickness = 3
                };
                _trailLines.Add(line);
                DrawCanvas.Children.Add(line);

                // 限制轨迹长度
                if (_trailLines.Count > 200)
                {
                    DrawCanvas.Children.Remove(_trailLines[0]);
                    _trailLines.RemoveAt(0);
                }

                _trailCounter = 0;
            }
        }

        // FPS 统计
        _frameCount++;
        if (_stopwatch.Elapsed.TotalSeconds - _lastFpsUpdate >= 1.0)
        {
            FpsText.Text = $"FPS: {_frameCount}";
            _frameCount = 0;
            _lastFpsUpdate = _stopwatch.Elapsed.TotalSeconds;
        }
    }

    public void Dispose()
    {
        PauseSimulation();
    }
}
