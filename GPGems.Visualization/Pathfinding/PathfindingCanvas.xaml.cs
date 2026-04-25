using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using GPGems.AI.Pathfinding;

namespace GPGems.Visualization.Pathfinding;

/// <summary>
/// 寻路算法可视化控件
/// 支持鼠标编辑障碍物、设置起点终点
/// 策略模式：支持注入不同的 IPathfinder 实现
/// </summary>
public partial class PathfindingCanvas : UserControl
{
    private GridMap? _map;
    private IPathfinder? _pathfinder;

    private readonly List<Rectangle> _cellRects = [];
    private GridNode? _startNode;
    private GridNode? _goalNode;

    private float _useTime = 0f;

    /// <summary>编辑模式</summary>
    public EditMode CurrentMode { get; set; } = EditMode.Obstacle;

    /// <summary>当前使用的寻路算法</summary>
    public IPathfinder? CurrentPathfinder => _pathfinder;

    /// <summary>是否显示搜索过程动画</summary>
    public bool ShowSearchAnimation { get; set; } = true;

    /// <summary>搜索动画回调</summary>
    public Action? OnSearchStep { get; set; }

    public PathfindingCanvas()
    {
        InitializeComponent();
        MouseLeftButtonDown += OnMouseDown;
        MouseMove += OnMouseMove;
        SizeChanged += OnSizeChanged;
    }

    /// <summary>设置网格地图</summary>
    public void SetMap(GridMap map)
    {
        _map = map;
        // 默认使用 A* 算法
        _pathfinder = new AStarPathfinder();
        CreateGridCells();
        Render();
    }

    /// <summary>切换寻路算法</summary>
    public void SetPathfinder(IPathfinder pathfinder)
    {
        _pathfinder = pathfinder;
    }

    /// <summary>执行寻路并返回路径</summary>
    public List<GridNode>? FindPath()
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        _useTime = 0f;
        if (_map == null || _startNode == null || _goalNode == null || _pathfinder == null)
            return null;

        _map.ResetPathfinding();
        var path = _pathfinder.FindPath(_map, _startNode, _goalNode);
        _useTime = (float)stopwatch.Elapsed.TotalMilliseconds;
        stopwatch.Stop();
        
        Render();
        return path.Count > 0 ? path : null;
    }

    /// <summary>清除所有障碍物和路径</summary>
    public void ClearAll()
    {
        _map?.ClearAll();
        _startNode = null;
        _goalNode = null;
        Render();
    }

    /// <summary>随机生成障碍物</summary>
    public void GenerateRandomObstacles(double density = 0.2)
    {
        if (_map == null) return;

        foreach (var node in _map.Nodes)
        {
            if (Random.Shared.NextDouble() < density)
            {
                node.IsWalkable = false;
                node.VisualType = NodeType.Obstacle;
            }
        }
        Render();
    }

    /// <summary>获取统计信息</summary>
    public (int openCount, int closedCount, int pathLength, string algorithmName, float useTime) GetStats()
    {
        if (_map == null) return (0, 0, 0, _pathfinder?.Name ?? "", 0f);

        int open = 0, closed = 0, path = 0;
        foreach (var node in _map.Nodes)
        {
            if (node.VisualType == NodeType.OpenSet) open++;
            else if (node.VisualType == NodeType.ClosedSet) closed++;
            else if (node.VisualType == NodeType.Path) path++;
        }

        // 优先使用算法自身提供的精确统计
        if (_pathfinder != null)
        {
            open = _pathfinder.OpenSetCount;
            closed = _pathfinder.ClosedSetCount;
        }

        return (open, closed, path, _pathfinder?.Name ?? "", _useTime);
    }

    /// <summary>创建网格单元格</summary>
    private void CreateGridCells()
    {
        if (_map == null) return;

        GridCanvas.Children.Clear();
        _cellRects.Clear();

        double cellSize = Math.Min(ActualWidth, ActualHeight) / Math.Max(_map.Width, _map.Height);
        cellSize = Math.Max(cellSize, 8);

        for (int y = 0; y < _map.Height; y++)
        {
            for (int x = 0; x < _map.Width; x++)
            {
                var rect = new Rectangle
                {
                    Width = cellSize - 1,
                    Height = cellSize - 1,
                    Stroke = new SolidColorBrush(Color.FromRgb(30, 40, 60)),
                    StrokeThickness = 0.5
                };

                Canvas.SetLeft(rect, x * cellSize);
                Canvas.SetTop(rect, y * cellSize);
                GridCanvas.Children.Add(rect);
                _cellRects.Add(rect);
            }
        }
    }

    /// <summary>渲染整个网格</summary>
    private void Render()
    {
        if (_map == null || _cellRects.Count == 0) return;

        int idx = 0;
        for (int y = 0; y < _map.Height; y++)
        {
            for (int x = 0; x < _map.Width; x++)
            {
                var node = _map.Nodes[x, y];
                var rect = _cellRects[idx++];

                rect.Fill = GetNodeBrush(node.VisualType);
            }
        }
    }

    /// <summary>根据节点类型获取颜色</summary>
    private static SolidColorBrush GetNodeBrush(NodeType type)
    {
        return type switch
        {
            NodeType.Walkable => new SolidColorBrush(Color.FromRgb(22, 33, 62)),
            NodeType.Obstacle => new SolidColorBrush(Color.FromRgb(15, 52, 96)),
            NodeType.Start => new SolidColorBrush(Color.FromRgb(78, 204, 163)),
            NodeType.Goal => new SolidColorBrush(Color.FromRgb(233, 69, 96)),
            NodeType.Path => new SolidColorBrush(Color.FromRgb(255, 193, 7)),
            NodeType.OpenSet => new SolidColorBrush(Color.FromRgb(67, 97, 238)),
            NodeType.ClosedSet => new SolidColorBrush(Color.FromRgb(83, 52, 131)),
            _ => new SolidColorBrush(Colors.Gray)
        };
    }

    /// <summary>鼠标点击处理</summary>
    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_map == null) return;

        var pos = e.GetPosition(GridCanvas);
        double cellSize = Math.Min(ActualWidth, ActualHeight) / Math.Max(_map.Width, _map.Height);

        int x = (int)(pos.X / cellSize);
        int y = (int)(pos.Y / cellSize);

        if (x < 0 || x >= _map.Width || y < 0 || y >= _map.Height) return;

        var node = _map.Nodes[x, y];

        switch (CurrentMode)
        {
            case EditMode.Obstacle:
                node.IsWalkable = !node.IsWalkable;
                node.VisualType = node.IsWalkable ? NodeType.Walkable : NodeType.Obstacle;
                break;

            case EditMode.StartPoint:
                if (_startNode != null && _startNode != node)
                {
                    _startNode.VisualType = _startNode.IsWalkable ? NodeType.Walkable : NodeType.Obstacle;
                }
                _startNode = node;
                node.IsWalkable = true;
                node.VisualType = NodeType.Start;
                break;

            case EditMode.GoalPoint:
                if (_goalNode != null && _goalNode != node)
                {
                    _goalNode.VisualType = _goalNode.IsWalkable ? NodeType.Walkable : NodeType.Obstacle;
                }
                _goalNode = node;
                node.IsWalkable = true;
                node.VisualType = NodeType.Goal;
                break;
        }

        Render();
    }

    /// <summary>鼠标拖动处理</summary>
    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (_map == null || e.LeftButton != MouseButtonState.Pressed) return;
        if (CurrentMode != EditMode.Obstacle) return;

        var pos = e.GetPosition(GridCanvas);
        double cellSize = Math.Min(ActualWidth, ActualHeight) / Math.Max(_map.Width, _map.Height);

        int x = (int)(pos.X / cellSize);
        int y = (int)(pos.Y / cellSize);

        if (x < 0 || x >= _map.Width || y < 0 || y >= _map.Height) return;

        var node = _map.Nodes[x, y];
        if (node.VisualType != NodeType.Start && node.VisualType != NodeType.Goal)
        {
            node.IsWalkable = false;
            node.VisualType = NodeType.Obstacle;
            Render();
        }
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_map != null)
        {
            CreateGridCells();
            Render();
        }
    }
}

/// <summary>编辑模式</summary>
public enum EditMode
{
    Obstacle,   // 绘制障碍物
    StartPoint, // 设置起点
    GoalPoint   // 设置终点
}
