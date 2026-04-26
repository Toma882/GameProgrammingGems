using System;
using Math = System.Math;
using System.Windows;
using System.Windows.Input;
using GPGems.AI.Boids;
using GPGems.Core.Graphics;
using GPGems.Core.Math;

namespace GPGems.DemoApp;

/// <summary>
/// Boids 群体行为演示窗口
/// Reynolds 三大规则 + 扩展行为演示
/// </summary>
public partial class BoidsDemoWindow : Window
{
    private Flock _flock = null!;
    private Flock? _predatorFlock; // 捕食者群体
    private bool _isPaused;
    private bool _isLeaderMode;
    private bool _isPredatorPreyMode;

    public BoidsDemoWindow()
    {
        InitializeComponent();
        Loaded += (s, e) =>
        {
            InitializeFlock();
            BoidsCanvas.StartSimulation();
            BoidsCanvas.OnMouseClick += OnCanvasMouseClick;
        };
    }

    /// <summary>初始化群体</summary>
    private void InitializeFlock()
    {
        _flock = new Flock
        {
            Color = RgbColor.FromRgb(233, 69, 96)
        };

        // 先应用设置，再生成 Boids
        UpdateSettingsFromUI();

        // 生成 Boids
        _flock.SpawnBoids((int)BoidCountSlider.Value);

        // 绑定到可视化控件
        BoidsCanvas.SetFlock(_flock);
    }

    /// <summary>从 UI 滑块更新行为参数</summary>
    private void UpdateSettingsFromUI()
    {
        var settings = new BoidSettings
        {
            PerceptionRange = (float)PerceptionSlider.Value,
            SeparationDist = (float)SeparationSlider.Value,
            DesiredSpeed = (float)DesiredSpeedSlider.Value,
            MaxSpeed = (float)MaxSpeedSlider.Value,
            MaxAcceleration = 5f,
            MaxVisibleFriends = 30,
            SeparationWeight = (float)SepWeightSlider.Value,
            AlignmentWeight = (float)AliWeightSlider.Value,
            CohesionWeight = (float)CohWeightSlider.Value,
            SeekTargetWeight = (float)SeekWeightSlider.Value,
            EvadeWeight = (float)EvadeWeightSlider.Value,
            FollowLeaderWeight = (float)LeaderWeightSlider.Value,
            WanderWeight = (float)WanderWeightSlider.Value,
            CruiseGain = 0.5f,
            VerticalDamping = 0.98f,
            EvadePanicRadius = 20f,
            ArriveSlowingRadius = 15f,
        };

        _flock.Settings = settings;
        if (_predatorFlock != null)
        {
            _predatorFlock.Settings = settings with
            {
                DesiredSpeed = (float)DesiredSpeedSlider.Value * 0.8f,
                MaxSpeed = (float)MaxSpeedSlider.Value * 0.8f,
                SeekTargetWeight = 2f, // 捕食者更积极追逐
                EvadeWeight = 0f
            };
        }

        BoidsCanvas.TimeScale = (float)TimeScaleSlider.Value;

        // 更新显示文本
        PerceptionText.Text = $"{PerceptionSlider.Value:F0}";
        SeparationText.Text = $"{SeparationSlider.Value:F0}";
        DesiredSpeedText.Text = $"{DesiredSpeedSlider.Value:F0}";
        MaxSpeedText.Text = $"{MaxSpeedSlider.Value:F0}";
        SepWeightText.Text = $"{SepWeightSlider.Value:F1}";
        AliWeightText.Text = $"{AliWeightSlider.Value:F1}";
        CohWeightText.Text = $"{CohWeightSlider.Value:F1}";
        SeekWeightText.Text = $"{SeekWeightSlider.Value:F1}";
        EvadeWeightText.Text = $"{EvadeWeightSlider.Value:F1}";
        LeaderWeightText.Text = $"{LeaderWeightSlider.Value:F1}";
        WanderWeightText.Text = $"{WanderWeightSlider.Value:F1}";
        TimeScaleText.Text = $"{TimeScaleSlider.Value:F1}";
    }

    /// <summary>参数变化时实时更新</summary>
    private void OnSettingChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_flock != null && IsLoaded)
            UpdateSettingsFromUI();
    }

    /// <summary>时间缩放变化</summary>
    private void OnTimeScaleChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (BoidsCanvas != null && IsLoaded)
            BoidsCanvas.TimeScale = (float)TimeScaleSlider.Value;
        if (TimeScaleText != null)
            TimeScaleText.Text = $"{TimeScaleSlider.Value:F1}";
    }

    /// <summary>群体大小变化（需要重建）</summary>
    private void OnBoidCountChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (BoidCountText != null)
            BoidCountText.Text = $"{BoidCountSlider.Value:F0}";

        if (_flock != null && IsLoaded)
        {
            bool wasRunning = !_isPaused;
            if (wasRunning) BoidsCanvas.PauseSimulation();

            // 重新生成
            _flock.Boids.Clear();
            _flock.SpawnBoids((int)BoidCountSlider.Value);

            // 如果有捕食者，也重新生成
            if (_predatorFlock != null)
            {
                _predatorFlock.Boids.Clear();
                _predatorFlock.SpawnBoids(System.Math.Max(5, (int)BoidCountSlider.Value / 5));
            }

            // 恢复模式
            if (_isLeaderMode) _flock.SetLeader();
            if (_isPredatorPreyMode) SetupPredatorPrey();

            BoidsCanvas.SetFlock(_flock, _predatorFlock);

            if (wasRunning) BoidsCanvas.StartSimulation();
        }
    }

    /// <summary>画布鼠标点击：设置目标吸引点</summary>
    private void OnCanvasMouseClick(Vector3 worldPosition)
    {
        if (_flock == null) return;

        // 点击的位置作为群体目标
        if (_flock.GroupTarget == null)
        {
            _flock.GroupTarget = worldPosition;
            SeekWeightSlider.Value = 1.5; // 自动调高吸引权重
        }
        else
        {
            // 再次点击清除目标
            _flock.GroupTarget = null;
        }
    }

    /// <summary>领导者模式切换</summary>
    private void OnLeaderModeChanged(object sender, RoutedEventArgs e)
    {
        if (_flock == null) return;

        _isLeaderMode = LeaderModeCheck.IsChecked == true;

        if (_isLeaderMode)
        {
            _flock.SetLeader();
            LeaderWeightSlider.Value = 1.5;
        }
        else
        {
            _flock.ClearLeader();
        }
    }

    /// <summary>捕食者-猎物模式切换</summary>
    private void OnPredatorPreyChanged(object sender, RoutedEventArgs e)
    {
        if (_flock == null) return;

        _isPredatorPreyMode = PredatorPreyCheck.IsChecked == true;

        if (_isPredatorPreyMode)
        {
            SetupPredatorPrey();
        }
        else
        {
            // 清除捕食者
            _predatorFlock = null;
            _flock.EnemyFlocks = null;
            BoidsCanvas.SetFlock(_flock);
        }
    }

    /// <summary>设置捕食者-猎物关系</summary>
    private void SetupPredatorPrey()
    {
        // 创建捕食者群体（蓝色）
        _predatorFlock = new Flock
        {
            Color = RgbColor.FromRgb(0, 122, 204),
            WorldBounds = _flock.WorldBounds
        };

        UpdateSettingsFromUI(); // 应用捕食者速度设置

        // 捕食者数量 = 猎物的 1/5
        int predatorCount = System.Math.Max(3, _flock.Boids.Count / 5);
        _predatorFlock.SpawnBoids(predatorCount);

        // 猎物的敌人是捕食者（猎物逃跑）
        _flock.EnemyFlocks = [_predatorFlock];

        // 捕食者追逐猎物的质心（通过目标吸引）
        _predatorFlock.EnemyFlocks = null;

        // 捕食者的目标：跟随猎物质心 - 在更新中动态设置
        BoidsCanvas.BeforeUpdate += () =>
        {
            if (_flock != null && _predatorFlock != null)
            {
                _predatorFlock.GroupTarget = _flock.GetCenter();
            }
        };

        EvadeWeightSlider.Value = 2.5; // 猎物更迫切逃跑

        BoidsCanvas.SetFlock(_flock, _predatorFlock);
    }

    /// <summary>重置整个群体</summary>
    private void OnReset(object sender, RoutedEventArgs e)
    {
        _isLeaderMode = false;
        _isPredatorPreyMode = false;
        LeaderModeCheck.IsChecked = false;
        PredatorPreyCheck.IsChecked = false;
        _predatorFlock = null;

        _flock.Reset();
        _flock.GroupTarget = null;
        _flock.EnemyFlocks = null;
        BoidsCanvas.SetFlock(_flock);

        if (_isPaused)
        {
            _isPaused = false;
            PauseBtn.Content = "⏸️ 暂停";
            BoidsCanvas.StartSimulation();
        }
    }

    /// <summary>暂停/继续</summary>
    private void OnPause(object sender, RoutedEventArgs e)
    {
        _isPaused = !_isPaused;

        if (_isPaused)
        {
            BoidsCanvas.PauseSimulation();
            PauseBtn.Content = "▶️ 继续";
        }
        else
        {
            BoidsCanvas.StartSimulation();
            PauseBtn.Content = "⏸️ 暂停";
        }
    }
}
