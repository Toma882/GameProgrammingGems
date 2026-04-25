using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using GPGems.AI.Decision.Integration;

namespace GPGems.Visualization.DecisionSystem;

/// <summary>
/// 融合决策系统可视化控件
/// 展示 FSM + 行为树 + 模糊逻辑 + 效用系统 + GOAP 的协同工作
/// </summary>
public partial class DecisionSystemCanvas : UserControl, IDisposable
{
    private SmartNpc? _npc;
    private readonly DispatcherTimer _updateTimer;
    private bool _isRunning;
    private int _frameCount;
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

    // FSM 状态映射
    private readonly Dictionary<string, Ellipse> _stateElements;

    // 是否正在从 UI 更新（避免循环更新）
    private bool _isUpdatingFromUI;

    public DecisionSystemCanvas()
    {
        InitializeComponent();

        _updateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _updateTimer.Tick += OnUpdateTick;

        // 初始化 FSM 状态元素映射
        _stateElements = new Dictionary<string, Ellipse>
        {
            ["Morning"] = StateMorning,
            ["Working"] = StateWorking,
            ["Evening"] = StateEvening,
            ["Sleeping"] = StateSleeping,
            ["Weekend"] = StateWeekend
        };

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;

        InitializeChart();
        ResetNpc();
    }

    /// <summary>
    /// 初始化图表
    /// </summary>
    private void InitializeChart()
    {
        StateChart.Plot.Title("状态值变化趋势", 14);
        StateChart.Plot.YLabel("数值");
        StateChart.Plot.XLabel("帧");
        StateChart.Plot.Axes.SetLimitsY(0, 100);
        StateChart.Refresh();
    }

    /// <summary>
    /// 重置 NPC
    /// </summary>
    private void ResetNpc()
    {
        _npc = new SmartNpc("小明");
        _frameCount = 0;

        UpdateUI();
        StateChart.Refresh();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Dispose();
    }

    private void OnUpdateTick(object? sender, EventArgs e)
    {
        if (_npc == null) return;

        _npc.Update(1f);
        _frameCount++;

        UpdateUI();
        StateChart.Refresh();
    }

    /// <summary>
    /// 更新所有 UI 元素
    /// </summary>
    private void UpdateUI()
    {
        if (_npc == null) return;

        FrameText.Text = $"Frame: {_frameCount}";

        // 更新状态值显示（不触发滑块事件）
        _isUpdatingFromUI = true;
        try
        {
            var hour = _npc.Blackboard.GetOrDefault("hour_of_day", 0f);
            var energy = _npc.Blackboard.GetOrDefault("energy", 0f);
            var hunger = _npc.Blackboard.GetOrDefault("hunger", 0f);
            var stress = _npc.Blackboard.GetOrDefault("stress", 0f);
            var workIntensity = _npc.Blackboard.GetOrDefault("work_intensity", 0f);
            var bossNearby = _npc.Blackboard.GetOrDefault("boss_nearby", false);

            HourSlider.Value = hour;
            HourValue.Text = $"{hour:F1}";

            EnergySlider.Value = energy;
            EnergyValue.Text = $"{energy:F0}";

            HungerSlider.Value = hunger;
            HungerValue.Text = $"{hunger:F0}";

            StressSlider.Value = stress;
            StressValue.Text = $"{stress:F0}";

            WorkIntensitySlider.Value = workIntensity;
            WorkIntensityValue.Text = $"{workIntensity:F2}";

            BossNearbyCheck.IsChecked = bossNearby;
        }
        finally
        {
            _isUpdatingFromUI = false;
        }

        // 更新 FSM 状态高亮
        UpdateFsmStateHighlight();

        // 更新行为树名称
        CurrentBtName.Text = _npc.CurrentBehaviorTree?.Name ?? "None";

        // 更新行为树节点激活
        UpdateBtNodesPanel();

        // 更新效用系统得分
        UpdateUtilityPanel();

        // 更新 GOAP 计划
        UpdateGoapPanel();
    }

    /// <summary>
    /// 更新 FSM 状态高亮
    /// </summary>
    private void UpdateFsmStateHighlight()
    {
        if (_npc == null) return;

        var currentState = _npc.CurrentState;

        foreach (var (stateName, element) in _stateElements)
        {
            if (stateName == currentState.ToString())
            {
                element.Opacity = 1.0;
                element.StrokeThickness = 5;

                // 动画效果
                var animation = new DoubleAnimation
                {
                    From = 5,
                    To = 8,
                    Duration = TimeSpan.FromSeconds(0.5),
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever
                };
                element.BeginAnimation(Shape.StrokeThicknessProperty, animation);
            }
            else
            {
                element.Opacity = 0.3;
                element.StrokeThickness = 2;
                element.BeginAnimation(Shape.StrokeThicknessProperty, null);
            }
        }
    }

    /// <summary>
    /// 更新行为树节点面板
    /// </summary>
    private void UpdateBtNodesPanel()
    {
        BtNodesPanel.Children.Clear();

        if (_npc?.CurrentBehaviorTree == null) return;

        // 显示主要集成节点
        var nodeColors = new[] { Colors.MediumPurple, Colors.Orange, Colors.RoyalBlue, Colors.MediumSeaGreen };
        var nodeNames = new[] { "模糊逻辑节点", "效用选择节点", "GOAP 规划节点", "GOAP 执行节点" };
        var nodeTypes = new[] { "Fuzzy", "Utility", "GoapPlan", "GoapExecute" };

        for (var i = 0; i < nodeNames.Length; i++)
        {
            var container = new Border
            {
                Background = new SolidColorBrush(nodeColors[i]),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(10),
                Margin = new Thickness(0, 0, 0, 5),
                Opacity = 0.8
            };

            var panel = new StackPanel();
            var nameText = new TextBlock
            {
                Text = nodeNames[i],
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold  
            };
            var typeText = new TextBlock
            {
                Text = $"Type: {nodeTypes[i]}",
                Foreground = Brushes.White,
                FontSize = 10,
                Opacity = 0.7
            };
            panel.Children.Add(nameText);
            panel.Children.Add(typeText);
            container.Child = panel;

            BtNodesPanel.Children.Add(container);
        }

        // 显示当前执行的行为
        if (_npc.UtilityReasoner.CurrentAction != null)
        {
            var actionContainer = new Border
            {
                Background = new SolidColorBrush(Colors.MediumSeaGreen),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(10),
                Margin = new Thickness(0, 5, 0, 0)
            };

            var actionText = new TextBlock
            {
                Text = $"▶ 当前执行: {_npc.UtilityReasoner.CurrentAction.Name}",
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold
            };
            actionContainer.Child = actionText;
            BtNodesPanel.Children.Add(actionContainer);
        }
    }

    /// <summary>
    /// 更新效用系统得分面板
    /// </summary>
    private void UpdateUtilityPanel()
    {
        UtilityPanel.Children.Clear();

        if (_npc?.UtilityReasoner == null) return;

        var colors = new[] { Colors.RoyalBlue, Colors.MediumSeaGreen, Colors.Orange, Colors.HotPink };
        var colorIndex = 0;

        foreach (var action in _npc.UtilityReasoner.Actions)
        {
            var score = action.LastScore;
            var isSelected = _npc.UtilityReasoner.CurrentAction == action;

            var container = new Border
            {
                Background = new SolidColorBrush(Colors.Transparent),
                BorderBrush = new SolidColorBrush(colors[colorIndex % colors.Length]),
                BorderThickness = new Thickness(isSelected ? 2 : 1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8),
                Margin = new Thickness(0, 0, 0, 5)
            };

            if (isSelected)
            {
                var c = colors[colorIndex % colors.Length];
                container.Background = new SolidColorBrush(Color.FromArgb(50, c.R, c.G, c.B));
            }

            var panel = new StackPanel();

            var namePanel = new DockPanel();
            var nameText = new TextBlock
            {
                Text = (isSelected ? "▶ " : "") + action.Name,
                Foreground = Brushes.White,
                FontWeight = isSelected ? FontWeights.Bold : FontWeights.Normal
            };
            var scoreText = new TextBlock
            {
                Text = $"{score:F2}",
                Foreground = Brushes.White,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right
            };
            namePanel.Children.Add(nameText);
            namePanel.Children.Add(scoreText);

            var progressBar = new ProgressBar
            {
                Value = score * 100,
                Maximum = 100,
                Height = 6,
                Foreground = new SolidColorBrush(colors[colorIndex % colors.Length]),
                Background = new SolidColorBrush(Color.FromArgb(255, 30, 30, 30)),
                BorderThickness = new Thickness(0),
                Margin = new Thickness(0, 5, 0, 0)
            };

            panel.Children.Add(namePanel);
            panel.Children.Add(progressBar);
            container.Child = panel;

            UtilityPanel.Children.Add(container);
            colorIndex++;
        }
    }

    /// <summary>
    /// 更新 GOAP 计划面板
    /// </summary>
    private void UpdateGoapPanel()
    {
        GoapPanel.Children.Clear();

        if (_npc?.GoapAgent == null) return;

        var plan = _npc.GoapAgent.CurrentPlan;

        if (plan.Count == 0)
        {
            var emptyText = new TextBlock
            {
                Text = "暂无计划（等待目标触发）",
                Foreground = new SolidColorBrush(Color.FromArgb(255, 150, 150, 150)),
                FontStyle = FontStyles.Italic,
                Margin = new Thickness(10)
            };
            GoapPanel.Children.Add(emptyText);
            return;
        }

        for (var i = 0; i < plan.Count; i++)
        {
            var step = plan[i];
            var isExecuting = i == 0;

            var container = new Border
            {
                Background = isExecuting
                    ? new SolidColorBrush(Color.FromArgb(255, 76, 175, 80))
                    : new SolidColorBrush(Color.FromArgb(255, 33, 150, 243)),
                Opacity = isExecuting ? 1.0 : 0.5,
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(10),
                Margin = new Thickness(0, 0, 0, 5)
            };

            var panel = new StackPanel();
            var stepText = new TextBlock
            {
                Text = $"{(isExecuting ? "▶ " : "")}步骤 {i + 1}: {step.Name}",
                Foreground = Brushes.White,
                FontWeight = isExecuting ? FontWeights.Bold : FontWeights.Normal
            };
            panel.Children.Add(stepText);
            container.Child = panel;

            GoapPanel.Children.Add(container);
        }

        // 显示目标
        if (_npc.GoapAgent.CurrentGoal != null)
        {
            var goalContainer = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(255, 156, 39, 176)),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(10),
                Margin = new Thickness(0, 10, 0, 0)
            };

            var goalText = new TextBlock
            {
                Text = $"🎯 目标: {_npc.GoapAgent.CurrentGoal.Name}",
                Foreground = Brushes.White
            };
            goalContainer.Child = goalText;
            GoapPanel.Children.Add(goalContainer);
        }
    }

    /// <summary>
    /// 更新图表
    /// </summary>
    private void UpdateChart()
    {
        StateChart.Refresh();
    }

    #region 事件处理

    private void BtnStart_Click(object sender, RoutedEventArgs e)
    {
        if (!_isRunning)
        {
            _isRunning = true;
            _updateTimer.Start();
        }
    }

    private void BtnPause_Click(object sender, RoutedEventArgs e)
    {
        if (_isRunning)
        {
            _isRunning = false;
            _updateTimer.Stop();
        }
    }

    private void BtnReset_Click(object sender, RoutedEventArgs e)
    {
        _isRunning = false;
        _updateTimer.Stop();
        ResetNpc();
    }

    private void BtnStep_Click(object sender, RoutedEventArgs e)
    {
        _npc?.Update(1f);
        _frameCount++;
        UpdateUI();
        UpdateChart();
    }

    private void HourSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isUpdatingFromUI || _npc == null) return;
        _npc.Blackboard.Set("hour_of_day", (float)e.NewValue);
        HourValue.Text = $"{e.NewValue:F1}";
    }

    private void EnergySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isUpdatingFromUI || _npc == null) return;
        _npc.Blackboard.Set("energy", (float)e.NewValue);
        EnergyValue.Text = $"{e.NewValue:F0}";
    }

    private void HungerSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isUpdatingFromUI || _npc == null) return;
        _npc.Blackboard.Set("hunger", (float)e.NewValue);
        HungerValue.Text = $"{e.NewValue:F0}";
    }

    private void StressSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isUpdatingFromUI || _npc == null) return;
        _npc.Blackboard.Set("stress", (float)e.NewValue);
        StressValue.Text = $"{e.NewValue:F0}";
    }

    private void BossNearbyCheck_Checked(object sender, RoutedEventArgs e)
    {
        if (_npc == null) return;
        _npc.Blackboard.Set("boss_nearby", BossNearbyCheck.IsChecked == true);
    }

    private void SceneLowEnergy_Click(object sender, RoutedEventArgs e)
    {
        if (_npc == null) return;
        _npc.Blackboard.Set("energy", 15f);
        _npc.Blackboard.Set("hour_of_day", 14f);
        UpdateUI();
    }

    private void SceneHungry_Click(object sender, RoutedEventArgs e)
    {
        if (_npc == null) return;
        _npc.Blackboard.Set("hunger", 85f);
        _npc.Blackboard.Set("hour_of_day", 12f);
        UpdateUI();
    }

    private void SceneHighStress_Click(object sender, RoutedEventArgs e)
    {
        if (_npc == null) return;
        _npc.Blackboard.Set("stress", 90f);
        _npc.Blackboard.Set("boss_nearby", true);
        UpdateUI();
    }

    private void SceneOffWork_Click(object sender, RoutedEventArgs e)
    {
        if (_npc == null) return;
        _npc.Blackboard.Set("hour_of_day", 18.5f);
        UpdateUI();
    }

    private void SceneWeekend_Click(object sender, RoutedEventArgs e)
    {
        if (_npc == null) return;
        _npc.Blackboard.Set("is_weekend", true);
        _npc.Blackboard.Set("hour_of_day", 9f);
        UpdateUI();
    }

    private void SceneReport_Click(object sender, RoutedEventArgs e)
    {
        if (_npc == null) return;
        // 设置报告任务相关的黑板值
        _npc.Blackboard.Set("data_collected", false);
        _npc.Blackboard.Set("analysis_done", false);
        _npc.Blackboard.Set("report_written", false);
        _npc.Blackboard.Set("report_submitted", false);
        UpdateUI();
    }

    #endregion

    public void Dispose()
    {
        _updateTimer.Stop();
    }
}
