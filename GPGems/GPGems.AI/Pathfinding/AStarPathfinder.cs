namespace GPGems.AI.Pathfinding;

/// <summary>
/// A* 寻路算法（优化版）
/// 使用 .NET 内置 PriorityQueue 提升性能，Octile 距离启发函数
/// 策略模式：实现 IPathfinder 接口
/// </summary>
public class AStarPathfinder : IPathfinder
{
    public string Name => "A*";
    public string Description => "经典启发式搜索，结合 Dijkstra 最优性和贪心搜索的速度";
    public int OpenSetCount { get; private set; }
    public int ClosedSetCount { get; private set; }

    /// <summary>
    /// 在指定地图上查找从起点到终点的最短路径
    /// </summary>
    /// <returns>路径节点列表。找不到路径返回空列表。</returns>
    public List<GridNode> FindPath(GridMap map, GridNode start, GridNode goal)
    {
        OpenSetCount = 0;
        ClosedSetCount = 0;

        // 重置搜索状态
        foreach (var node in map.Nodes)
        {
            node.GCost = double.PositiveInfinity;
            node.HCost = 0;
            node.Parent = null;
        }

        // PriorityQueue: (节点, F代价) - 比 SortedSet 性能更好
        var openSet = new PriorityQueue<GridNode, double>();
        var openSetLookup = new HashSet<GridNode>();
        var closedSet = new HashSet<GridNode>();

        // 初始化起点
        start.GCost = 0;
        start.HCost = OctileDistance(start, goal);
        openSet.Enqueue(start, start.FCost);
        openSetLookup.Add(start);

        while (openSet.Count > 0)
        {
            // 取出 F 值最小的节点
            GridNode current = openSet.Dequeue();
            openSetLookup.Remove(current);

            // 到达目标
            if (current == goal)
            {
                OpenSetCount = openSet.Count;
                ClosedSetCount = closedSet.Count;
                return ReconstructPath(start, goal);
            }

            closedSet.Add(current);
            if (current != start)
                current.VisualType = NodeType.ClosedSet;

            // 展开邻居
            foreach (var neighbor in map.GetNeighbors(current))
            {
                if (closedSet.Contains(neighbor))
                    continue;

                // 计算移动代价（对角线代价 1.414，直线 1）
                double moveCost = OctileDistance(current, neighbor);
                double tentativeG = current.GCost + moveCost;

                if (tentativeG < neighbor.GCost)
                {
                    // 找到更优路径，更新节点
                    neighbor.GCost = tentativeG;
                    neighbor.HCost = OctileDistance(neighbor, goal);
                    neighbor.Parent = current;

                    if (!openSetLookup.Contains(neighbor))
                    {
                        openSet.Enqueue(neighbor, neighbor.FCost);
                        openSetLookup.Add(neighbor);
                        if (neighbor != goal && neighbor != start)
                            neighbor.VisualType = NodeType.OpenSet;
                    }
                }
            }
        }

        OpenSetCount = openSet.Count;
        ClosedSetCount = closedSet.Count;
        return [];
    }

    /// <summary>
    /// Octile 距离 - 8方向网格中最精确的启发式
    /// 相当于 sqrt(dx*dx + dy*dy) 的近似版本，避免浮点运算
    /// </summary>
    private static double OctileDistance(GridNode a, GridNode b)
    {
        int dx = Math.Abs(a.X - b.X);
        int dy = Math.Abs(a.Y - b.Y);
        int dMax = Math.Max(dx, dy);
        int dMin = Math.Min(dx, dy);
        return dMin * 1.41421356 + (dMax - dMin); // sqrt(2) ≈ 1.41421356
    }

    /// <summary>从终点追溯到起点，构建路径列表</summary>
    private static List<GridNode> ReconstructPath(GridNode start, GridNode goal)
    {
        var path = new List<GridNode>();
        GridNode? current = goal;

        while (current != null && current != start)
        {
            path.Add(current);
            if (current != start && current != goal)
                current.VisualType = NodeType.Path;
            current = current.Parent;
        }

        path.Add(start);
        path.Reverse();
        return path;
    }
}
