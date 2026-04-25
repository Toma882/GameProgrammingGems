namespace GPGems.AI.Pathfinding;

/// <summary>
/// BFS 广度优先搜索算法
/// 按层级逐层扩展，在单位权重网格中保证最短路径
/// 策略模式：实现 IPathfinder 接口
/// </summary>
public class BFSPathfinder : IPathfinder
{
    public string Name => "BFS";
    public string Description => "广度优先搜索，按层级扩展，单位网格中路径最短，但搜索范围大";
    public int OpenSetCount { get; private set; }
    public int ClosedSetCount { get; private set; }

    public List<GridNode> FindPath(GridMap map, GridNode start, GridNode goal)
    {
        OpenSetCount = 0;
        ClosedSetCount = 0;

        // 队列：先进先出
        var queue = new Queue<GridNode>();
        var visited = new HashSet<GridNode>();

        // 初始化父节点
        foreach (var node in map.Nodes)
            node.Parent = null;

        queue.Enqueue(start);
        visited.Add(start);

        while (queue.Count > 0)
        {
            GridNode current = queue.Dequeue();

            // 到达目标
            if (current == goal)
            {
                OpenSetCount = queue.Count;
                ClosedSetCount = visited.Count - queue.Count - 1;
                return ReconstructPath(start, goal);
            }

            if (current != start)
                current.VisualType = NodeType.ClosedSet;

            // 展开邻居（8方向）
            foreach (var neighbor in map.GetNeighbors(current))
            {
                if (visited.Contains(neighbor))
                    continue;

                visited.Add(neighbor);
                neighbor.Parent = current;
                queue.Enqueue(neighbor);

                if (neighbor != goal && neighbor != start)
                    neighbor.VisualType = NodeType.OpenSet;
            }
        }

        OpenSetCount = queue.Count;
        ClosedSetCount = visited.Count;
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
