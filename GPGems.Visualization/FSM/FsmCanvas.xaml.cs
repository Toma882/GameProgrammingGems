using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using GPGems.AI.FSM;

namespace GPGems.Visualization.FSM;

/// <summary>
/// FSM 状态图可视化控件
/// 将有限状态机的状态和转换关系绘制为图形
/// </summary>
public partial class FsmCanvas : UserControl
{
    private FiniteStateMachine? _fsm;

    // 形状缓存
    private readonly List<UIElement> _elements = [];
    private readonly Dictionary<FsmState, (double x, double y)> _statePositions = new();

    public FsmCanvas()
    {
        InitializeComponent();
    }

    /// <summary>设置要显示的有限状态机</summary>
    public void SetFSM(FiniteStateMachine fsm)
    {
        _fsm = fsm;
        _fsm.OnStateChanged += OnStateChanged;
        LayoutStates();
        Render();
    }

    /// <summary>重新布局渲染</summary>
    public void Refresh()
    {
        if (_fsm != null) Render();
    }

    /// <summary>状态切换时高亮新状态</summary>
    private void OnStateChanged(FsmState prev, FsmState next)
    {
        Render();
    }

    /// <summary>计算状态节点的位置（圆形布局）</summary>
    private void LayoutStates()
    {
        if (_fsm == null || _fsm.States.Count == 0) return;

        _statePositions.Clear();
        double cx = ActualWidth / 2;
        double cy = ActualHeight / 2;
        double radius = Math.Min(ActualWidth, ActualHeight) * 0.35;

        int count = _fsm.States.Count;
        for (int i = 0; i < count; i++)
        {
            double angle = 2 * Math.PI * i / count - Math.PI / 2;
            double x = cx + radius * Math.Cos(angle);
            double y = cy + radius * Math.Sin(angle);
            _statePositions[_fsm.States[i]] = (x, y);
        }
    }

    /// <summary>绘制状态图和转换关系</summary>
    private void Render()
    {
        if (_fsm == null) return;

        DrawCanvas.Children.Clear();
        _elements.Clear();

        // 先绘制转换箭头（在状态圆下方）
        foreach (var transition in _fsm.Transitions)
        {
            DrawTransition(transition);
        }

        // 再绘制状态圆
        foreach (var state in _fsm.States)
        {
            DrawState(state);
        }
    }

    /// <summary>绘制状态节点</summary>
    private void DrawState(FsmState state)
    {
        if (!_statePositions.TryGetValue(state, out var pos))
            return;

        double radius = 45;
        bool isCurrent = _fsm?.CurrentState == state;

        // 圆形节点
        var ellipse = new Ellipse
        {
            Width = radius * 2,
            Height = radius * 2,
            Fill = isCurrent
                ? new SolidColorBrush(Color.FromRgb(233, 69, 96))       // 红色高亮当前状态
                : new SolidColorBrush(Color.FromRgb(22, 33, 62)),
            Stroke = isCurrent
                ? new SolidColorBrush(Color.FromRgb(233, 69, 96))
                : new SolidColorBrush(Color.FromRgb(83, 52, 131)),
            StrokeThickness = isCurrent ? 3 : 2
        };
        Canvas.SetLeft(ellipse, pos.x - radius);
        Canvas.SetTop(ellipse, pos.y - radius);
        DrawCanvas.Children.Add(ellipse);

        // 状态名称
        var text = new TextBlock
        {
            Text = state.Name,
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Colors.White),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Center
        };
        text.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Canvas.SetLeft(text, pos.x - text.DesiredSize.Width / 2);
        Canvas.SetTop(text, pos.y - text.DesiredSize.Height / 2);
        DrawCanvas.Children.Add(text);
    }

    /// <summary>绘制转换箭头</summary>
    private void DrawTransition(FsmTransition transition)
    {
        if (!_statePositions.TryGetValue(transition.From, out var fromPos) ||
            !_statePositions.TryGetValue(transition.To, out var toPos))
            return;

        double dx = toPos.x - fromPos.x;
        double dy = toPos.y - fromPos.y;
        double dist = Math.Sqrt(dx * dx + dy * dy);
        if (dist < 1) return;

        // 单位向量
        double ux = dx / dist;
        double uy = dy / dist;

        // 从圆心外缘出发，到另一个圆心外缘结束
        double nodeRadius = 45;
        double startX = fromPos.x + ux * nodeRadius;
        double startY = fromPos.y + uy * nodeRadius;
        double endX = toPos.x - ux * (nodeRadius + 8);
        double endY = toPos.y - uy * (nodeRadius + 8);

        // 线条
        var line = new Line
        {
            X1 = startX,
            Y1 = startY,
            X2 = endX,
            Y2 = endY,
            Stroke = new SolidColorBrush(Color.FromRgb(78, 204, 163)),
            StrokeThickness = 1.5
        };
        DrawCanvas.Children.Add(line);

        // 箭头头部
        double arrowLen = 10;
        double arrowAngle = Math.PI / 6;
        double angle = Math.Atan2(dy, dx);

        var arrow = new Polygon
        {
            Points =
            [
                new Point(endX, endY),
                new Point(
                    endX - arrowLen * Math.Cos(angle - arrowAngle),
                    endY - arrowLen * Math.Sin(angle - arrowAngle)),
                new Point(
                    endX - arrowLen * Math.Cos(angle + arrowAngle),
                    endY - arrowLen * Math.Sin(angle + arrowAngle))
            ],
            Fill = new SolidColorBrush(Color.FromRgb(78, 204, 163))
        };
        DrawCanvas.Children.Add(arrow);
    }

    private void OnCanvasSizeChanged(object sender, SizeChangedEventArgs e)
    {
        LayoutStates();
        Render();
    }
}
