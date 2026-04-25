using System.Threading.Tasks;

namespace GPGems.AI.Pathfinding;

/// <summary>
/// 并行双向 A* 寻路算法
/// 使用多线程同时从起点和终点两个方向搜索，真正发挥双向搜索优势
/// 在长距离、大地图上性能优势显著
/// </summary>
public class ParallelBidirectionalAStarPathfinder : IPathfinder
{
    public string Name => "并行双向 A*";
    public string Description => "多线程并行双向搜索，长距离寻路性能最优";
    public int OpenSetCount { get; private set; }
    public int ClosedSetCount { get; private set; }

    // 线程安全的相遇检测机制
    private readonly object _lock = new();
    private GridNode? _meetingNode;
    private double _bestPathCost = double.PositiveInfinity;

    public List<GridNode> FindPath(GridMap map, GridNode start, GridNode goal)
    {
        OpenSetCount = 0;
        ClosedSetCount = 0;
        _meetingNode = null;
        _bestPathCost = double.PositiveInfinity;

        // 重置所有节点状态
        foreach (var node in map.Nodes)
        {
            node.GCost = double.PositiveInfinity;
            node.HCost = 0;
            node.Parent = null;
        }

        // ========== 前向搜索状态 (起点 -> 终点) ==========
        var forwardState = new SearchState
        {
            Open = new PriorityQueue<GridNode, double>(),
            OpenLookup = new HashSet<GridNode>(),
            Closed = new HashSet<GridNode>(),
            G = new Dictionary<GridNode, double>(),
            Parent = new Dictionary<GridNode, GridNode?>(),
            StartNode = start,
            GoalNode = goal,
            IsForward = true
        };

        start.GCost = 0;
        forwardState.G[start] = 0;
        forwardState.Parent[start] = null;
        forwardState.Open.Enqueue(start, OctileDistance(start, goal));
        forwardState.OpenLookup.Add(start);

        // ========== 反向搜索状态 (终点 -> 起点) ==========
        var backwardState = new SearchState
        {
            Open = new PriorityQueue<GridNode, double>(),
            OpenLookup = new HashSet<GridNode>(),
            Closed = new HashSet<GridNode>(),
            G = new Dictionary<GridNode, double>(),
            Parent = new Dictionary<GridNode, GridNode?>(),
            StartNode = goal,
            GoalNode = start,
            IsForward = false
        };

        backwardState.G[goal] = 0;
        backwardState.Parent[goal] = null;
        backwardState.Open.Enqueue(goal, OctileDistance(goal, start));
        backwardState.OpenLookup.Add(goal);

        // ========== 启动两个并行任务 ==========
        var forwardTask = Task.Run(() => SearchLoop(map, forwardState, backwardState));
        var backwardTask = Task.Run(() => SearchLoop(map, backwardState, forwardState));

        // 等待任意一个任务完成（找到相遇点或搜索结束）
        Task.WaitAny(forwardTask, backwardTask);

        // 等待另一个任务安全退出
        try
        {
            Task.WaitAll(forwardTask, backwardTask);
        }
        catch
        {
            // 忽略任务取消后的异常
        }

        OpenSetCount = forwardState.Open.Count + backwardState.Open.Count;
        ClosedSetCount = forwardState.Closed.Count + backwardState.Closed.Count;

        // 拼接路径
        if (_meetingNode != null)
        {
            return JoinPaths(start, goal, _meetingNode, forwardState.Parent, backwardState.Parent);
        }

        return [];
    }

    /// <summary>
    /// 单个方向的搜索循环（可并行执行）
    /// </summary>
    private void SearchLoop(GridMap map, SearchState myState, SearchState oppositeState)
    {
        while (myState.Open.Count > 0)
        {
            // 检查是否已找到相遇点（线程安全读取）
            lock (_lock)
            {
                if (_meetingNode != null)
                    break;
            }

            var current = myState.Open.Dequeue();
            myState.OpenLookup.Remove(current);
            myState.Closed.Add(current);

            // 可视化标记
            if (current != myState.StartNode && current != myState.GoalNode)
                current.VisualType = NodeType.ClosedSet;

            // ========== 线程安全的相遇检测 ==========
            lock (_lock)
            {
                if (oppositeState.Closed.Contains(current))
                {
                    double pathCost = myState.G[current] + oppositeState.G[current];
                    if (pathCost < _bestPathCost)
                    {
                        _bestPathCost = pathCost;
                        _meetingNode = current;
                        break;  // 找到更优路径，退出本方向搜索
                    }
                }
            }

            // 展开邻居
            foreach (var neighbor in map.GetNeighbors(current))
            {
                lock (_lock)
                {
                    if (myState.Closed.Contains(neighbor))
                        continue;
                }

                double moveCost = OctileDistance(current, neighbor);
                double tentativeG = myState.G[current] + moveCost;

                lock (_lock)
                {
                    if (!myState.G.ContainsKey(neighbor) || tentativeG < myState.G[neighbor])
                    {
                        myState.G[neighbor] = tentativeG;
                        myState.Parent[neighbor] = current;

                        double f = tentativeG + OctileDistance(neighbor, myState.GoalNode);
                        if (!myState.OpenLookup.Contains(neighbor))
                        {
                            myState.Open.Enqueue(neighbor, f);
                            myState.OpenLookup.Add(neighbor);
                            if (neighbor != myState.StartNode && neighbor != myState.GoalNode)
                                neighbor.VisualType = NodeType.OpenSet;
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// 搜索状态封装（每个方向一个独立实例）
    /// </summary>
    private class SearchState
    {
        public PriorityQueue<GridNode, double> Open = null!;
        public HashSet<GridNode> OpenLookup = null!;
        public HashSet<GridNode> Closed = null!;
        public Dictionary<GridNode, double> G = null!;
        public Dictionary<GridNode, GridNode?> Parent = null!;
        public GridNode StartNode = null!;
        public GridNode GoalNode = null!;
        public bool IsForward;
    }

    /// <summary>拼接前向和反向路径</summary>
    private static List<GridNode> JoinPaths(
        GridNode start,
        GridNode goal,
        GridNode meeting,
        Dictionary<GridNode, GridNode?> forwardParent,
        Dictionary<GridNode, GridNode?> backwardParent)
    {
        var path = new List<GridNode>();

        // 从相遇点追溯到起点（前向路径的逆序）
        var current = meeting;
        while (current != null)
        {
            path.Add(current);
            if (current != start && current != goal && current != meeting)
                current.VisualType = NodeType.Path;
            forwardParent.TryGetValue(current, out current);
        }
        path.Reverse();

        // 从相遇点的下一个追溯到终点（反向路径）
        current = backwardParent.TryGetValue(meeting, out var next) ? next : null;
        while (current != null)
        {
            path.Add(current);
            if (current != start && current != goal)
                current.VisualType = NodeType.Path;
            backwardParent.TryGetValue(current, out current);
        }

        return path;
    }

    /// <summary>Octile 距离 - 8方向网格启发式</summary>
    private static double OctileDistance(GridNode a, GridNode b)
    {
        int dx = Math.Abs(a.X - b.X);
        int dy = Math.Abs(a.Y - b.Y);
        int dMax = Math.Max(dx, dy);
        int dMin = Math.Min(dx, dy);
        return dMin * 1.41421356 + (dMax - dMin);
    }
}
