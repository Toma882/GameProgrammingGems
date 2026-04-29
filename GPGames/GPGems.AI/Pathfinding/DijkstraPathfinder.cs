namespace GPGems.AI.Pathfinding;

/// <summary>
/// Dijkstra 寻路算法
/// 无启发式的统一代价搜索，保证找到最短路径
/// 策略模式：实现 IPathfinder 接口
/// </summary>
public class DijkstraPathfinder : IPathfinder
{
    public string Name => "Dijkstra";
    public string Description => "经典最短路径算法，无启发式，保证最优但速度较慢";
    public int OpenSetCount { get; private set; }
    public int ClosedSetCount { get; private set; }

    public List<GridNode> FindPath(GridMap map, GridNode start, GridNode goal)
    {
        OpenSetCount = 0;
        ClosedSetCount = 0;

        // 优先队列（按代价从小到大）
        var openSet = new SortedSet<(double cost, int x, int y)>();
        var openSetNodes = new HashSet<GridNode>();
        var closedSet = new HashSet<GridNode>();

        // 初始化所有节点距离为无穷大
        foreach (var node in map.Nodes)
        {
            node.GCost = double.PositiveInfinity;
            node.Parent = null;
        }

        // 起点距离为 0
        start.GCost = 0;
        openSet.Add((0, start.X, start.Y));
        openSetNodes.Add(start);

        while (openSet.Count > 0)
        {
            // 取出当前代价最小的节点
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
                if (closedSet.Contains(neighbor))
                    continue;

                // Dijkstra 只有代价值，没有启发式
                double tentativeG = current.GCost + GridMap.GetDistance(current, neighbor);

                if (tentativeG < neighbor.GCost)
                {
                    // 找到更优路径
                    neighbor.GCost = tentativeG;
                    neighbor.Parent = current;

                    if (!openSetNodes.Contains(neighbor))
                    {
                        openSet.Add((tentativeG, neighbor.X, neighbor.Y));
                        openSetNodes.Add(neighbor);
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
