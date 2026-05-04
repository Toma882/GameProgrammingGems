namespace GPGems.AI.Pathfinding;

/// <summary>
/// DFS 深度优先搜索算法
/// 一条路走到黑，不保证最短路径，仅作教学对比用
/// 策略模式：实现 IPathfinder 接口
/// </summary>
public class DFSPathfinder : IPathfinder
{
    public string Name => "DFS";
    public string Description => "深度优先搜索，一条路走到黑，路径可能很长，仅教学演示用";
    public int OpenSetCount { get; private set; }
    public int ClosedSetCount { get; private set; }

    public List<GridNode> FindPath(GridMap map, GridNode start, GridNode goal)
    {
        OpenSetCount = 0;
        ClosedSetCount = 0;

        // 栈：后进先出
        var stack = new Stack<GridNode>();
        var visited = new HashSet<GridNode>();

        // 初始化父节点
        foreach (var node in map.Nodes)
            node.Parent = null;

        stack.Push(start);
        visited.Add(start);

        while (stack.Count > 0)
        {
            GridNode current = stack.Pop();

            // 到达目标
            if (current == goal)
            {
                OpenSetCount = stack.Count;
                ClosedSetCount = visited.Count - stack.Count - 1;
                return ReconstructPath(start, goal);
            }

            if (current != start)
                current.VisualType = NodeType.ClosedSet;

            // 展开邻居（8方向，逆序入栈保持遍历顺序一致）
            var neighbors = map.GetNeighbors(current);
            for (int i = neighbors.Count - 1; i >= 0; i--)
            {
                var neighbor = neighbors[i];
                if (visited.Contains(neighbor))
                    continue;

                visited.Add(neighbor);
                neighbor.Parent = current;
                stack.Push(neighbor);

                if (neighbor != goal && neighbor != start)
                    neighbor.VisualType = NodeType.OpenSet;
            }
        }

        OpenSetCount = stack.Count;
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
