namespace GPGems.AI.Pathfinding;

/// <summary>
/// Best-First 贪心搜索算法
/// 只看启发式距离，始终向最接近目标的方向前进
/// 速度快但不保证找到最短路径
/// 策略模式：实现 IPathfinder 接口
/// </summary>
public class BestFirstPathfinder : IPathfinder
{
    public string Name => "Best-First";
    public string Description => "贪心最佳优先搜索，只看启发式距离，速度快但可能不最优";
    public int OpenSetCount { get; private set; }
    public int ClosedSetCount { get; private set; }

    public List<GridNode> FindPath(GridMap map, GridNode start, GridNode goal)
    {
        OpenSetCount = 0;
        ClosedSetCount = 0;

        // 优先队列：按到目标的启发式距离排序（贪心）
        var openSet = new SortedSet<(double h, int x, int y)>();
        var openSetNodes = new HashSet<GridNode>();
        var closedSet = new HashSet<GridNode>();

        // 初始化父节点
        foreach (var node in map.Nodes)
            node.Parent = null;

        openSet.Add((GridMap.GetDistance(start, goal), start.X, start.Y));
        openSetNodes.Add(start);

        while (openSet.Count > 0)
        {
            // 取出离目标最近的节点（贪心选择）
            var min = openSet.Min;
            openSet.Remove(min);

            GridNode current = map.Nodes[min.x, min.y];
            openSetNodes.Remove(current);

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
                if (closedSet.Contains(neighbor) || openSetNodes.Contains(neighbor))
                    continue;

                neighbor.Parent = current;

                // Best-First: 只用启发式距离作为排序键
                double h = GridMap.GetDistance(neighbor, goal);
                openSet.Add((h, neighbor.X, neighbor.Y));
                openSetNodes.Add(neighbor);

                if (neighbor != goal && neighbor != start)
                    neighbor.VisualType = NodeType.OpenSet;
            }
        }

        OpenSetCount = openSet.Count;
        ClosedSetCount = closedSet.Count;
        return [];
    }

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
