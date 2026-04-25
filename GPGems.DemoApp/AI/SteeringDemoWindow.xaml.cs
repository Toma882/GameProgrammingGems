using System.Windows;
using System.Windows.Controls;
using GPGems.AI.Steering;
using GPGems.Core.Math;

namespace GPGems.DemoApp;

/// <summary>
/// Steering 定向行为演示窗口
/// 展示单一 Steering 行为的纯净效果
/// </summary>
public partial class SteeringDemoWindow : Window
{
    public SteeringDemoWindow()
    {
        try
        {
            InitializeComponent();
            Loaded += (s, e) =>
            {
                SteeringCanvas.OnMouseClick += OnCanvasMouseClick;
                OnBehaviorChanged(null, null);
            };
        }
        catch (Exception ex)
        {
            MessageBox.Show($"SteeringDemoWindow 初始化失败: {ex.Message}\n\n{ex.StackTrace}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>行为类型切换</summary>
    private void OnBehaviorChanged(object sender, RoutedEventArgs e)
    {
        // XAML 加载时 Checked 事件可能在 SteeringCanvas 初始化前触发
        if (SteeringCanvas == null) return;

        var radio = sender as RadioButton;
        if (radio == null)
        {
            // 默认选中第一个
            SteeringCanvas.SetBehavior(SteeringBehaviorType.Seek);
            return;
        }

        var behavior = radio.Content?.ToString()?.Split(' ')[0] switch
        {
            "Seek" => SteeringBehaviorType.Seek,
            "Flee" => SteeringBehaviorType.Flee,
            "Arrive" => SteeringBehaviorType.Arrive,
            "Wander" => SteeringBehaviorType.Wander,
            "Pursue" => SteeringBehaviorType.Pursue,
            "Evade" => SteeringBehaviorType.Evade,
            _ => SteeringBehaviorType.Seek
        };

        SteeringCanvas.SetBehavior(behavior);
    }

    /// <summary>画布鼠标点击：设置目标点</summary>
    private void OnCanvasMouseClick(Vector3 worldPosition)
    {
        Console.WriteLine($"OnCanvasMouseClick: {worldPosition}");
        SteeringCanvas.SetTargetPosition(worldPosition);
    }

    /// <summary>重置智能体位置</summary>
    private void OnReset(object sender, RoutedEventArgs e)
    {
        SteeringCanvas.ResetAgent();
    }

    /// <summary>清除轨迹</summary>
    private void OnClearTrail(object sender, RoutedEventArgs e)
    {
        // 重置会自动清除轨迹
        SteeringCanvas.ResetAgent();
    }

    /// <summary>最大速度变化</summary>
    private void OnSpeedChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (SteeringCanvas == null || SpeedText == null) return;
        SteeringCanvas.SetMaxSpeed((float)e.NewValue);
        SpeedText.Text = $"{e.NewValue:F1}";
    }

    /// <summary>转向力变化</summary>
    private void OnForceChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (SteeringCanvas == null || ForceText == null) return;
        SteeringCanvas.SetMaxForce((float)e.NewValue);
        ForceText.Text = $"{e.NewValue:F2}";
    }
}
