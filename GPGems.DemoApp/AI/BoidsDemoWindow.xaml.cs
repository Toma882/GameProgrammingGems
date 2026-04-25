using System.Windows;
using GPGems.AI.Boids;
using GPGems.Core.Graphics;

namespace GPGems.DemoApp;   

/// <summary>
/// Boids 群体行为演示窗口
/// Reynolds 三大规则的交互式演示
/// </summary>
public partial class BoidsDemoWindow : Window
{
    private Flock _flock = null!;
    private bool _isPaused;

    public BoidsDemoWindow()
    {
        InitializeComponent();
        Loaded += (s, e) =>
        {
            InitializeFlock();
            BoidsCanvas.StartSimulation();
        };
    }

    /// <summary>初始化群体</summary>
    private void InitializeFlock()
    {
        _flock = new Flock
        {
            Color = RgbColor.FromRgb(233, 69, 96)
        };

        // 先应用设置，再生成 Boids（重要：生成需要 Settings.DesiredSpeed）
        UpdateSettingsFromUI();

        // 生成 Boids
        _flock.SpawnBoids((int)BoidCountSlider.Value);

        // 绑定到可视化控件
        BoidsCanvas.SetFlock(_flock);
    }

    /// <summary>从 UI 滑块更新行为参数</summary>
    private void UpdateSettingsFromUI()
    {
        _flock.Settings = new BoidSettings
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
            CruiseGain = 0.5f,
            VerticalDamping = 0.98f,
        };

        BoidsCanvas.TimeScale = (float)TimeScaleSlider.Value;

        // 更新显示文本
        PerceptionText.Text = $"{PerceptionSlider.Value:F0}";
        SeparationText.Text = $"{SeparationSlider.Value:F0}";
        DesiredSpeedText.Text = $"{DesiredSpeedSlider.Value:F0}";
        MaxSpeedText.Text = $"{MaxSpeedSlider.Value:F0}";
        SepWeightText.Text = $"{SepWeightSlider.Value:F1}";
        AliWeightText.Text = $"{AliWeightSlider.Value:F1}";
        CohWeightText.Text = $"{CohWeightSlider.Value:F1}";
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

    /// <summary>群体数量变化（需要重建）</summary>
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
            BoidsCanvas.SetFlock(_flock);

            if (wasRunning) BoidsCanvas.StartSimulation();
        }
    }

    /// <summary>重置整个群体</summary>
    private void OnReset(object sender, RoutedEventArgs e)
    {
        _flock.Reset();
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
