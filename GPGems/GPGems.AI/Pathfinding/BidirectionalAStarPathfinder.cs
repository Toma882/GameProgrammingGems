namespace GPGems.AI.Pathfinding;

/// <summary>
/// 双向 A* 寻路算法
/// 同时从起点和终点两个方向搜索，在中间相遇
/// 长距离寻路速度比单向 A* 快 50%~70%
/// 策略模式：实现 IPathfinder 接口
/// </summary>
public class BidirectionalAStarPathfinder : IPathfinder
{
    public string Name => "双向 A*";
    public string Description => "同时从起点和终点双向搜索，长距离寻路速度显著提升";
    public int OpenSetCount { get; private set; }
    public int ClosedSetCount { get; private set; }

    public List<GridNode> FindPath(GridMap map, GridNode start, GridNode goal)
    {
        OpenSetCount = 0;
        ClosedSetCount = 0;

        // 重置所有节点状态
        foreach (var node in map.Nodes)
        {
            node.GCost = double.PositiveInfinity;
            node.HCost = 0;
            node.Parent = null;
        }

        // ========== 前向搜索 (起点 -> 终点) ==========
        var forwardOpen = new PriorityQueue<GridNode, double>();
        var forwardOpenLookup = new HashSet<GridNode>();
        var forwardClosed = new HashSet<GridNode>();
        var forwardG = new Dictionary<GridNode, double>();  // 前向 G 值
        var forwardParent = new Dictionary<GridNode, GridNode?>();  // 前向父节点

        start.GCost = 0;
        forwardG[start] = 0;
        forwardParent[start] = null;
        forwardOpen.Enqueue(start, OctileDistance(start, goal));
        forwardOpenLookup.Add(start);

        // ========== 反向搜索 (终点 -> 起点) ==========
        var backwardOpen = new PriorityQueue<GridNode, double>();
        var backwardOpenLookup = new HashSet<GridNode>();
        var backwardClosed = new HashSet<GridNode>();
        var backwardG = new Dictionary<GridNode, double>();  // 反向 G 值
        var backwardParent = new Dictionary<GridNode, GridNode?>();  // 反向父节点

        backwardG[goal] = 0;
        backwardParent[goal] = null;
        backwardOpen.Enqueue(goal, OctileDistance(goal, start));
        backwardOpenLookup.Add(goal);

        // 相遇点
        GridNode? meetingNode = null;
        double bestPathCost = double.PositiveInfinity;

        while (forwardOpen.Count > 0 && backwardOpen.Count > 0)
        {
            // ========== 交替展开：每次展开代价较小的一边 ==========
            bool expandForward = forwardOpen.Count <= backwardOpen.Count;

            if (expandForward)
            {
                // 展开前向搜索
                var current = forwardOpen.Dequeue();
                forwardOpenLookup.Remove(current);
                forwardClosed.Add(current);

                // 可视化：标记已访问
                if (current != start && current != goal)
                    current.VisualType = NodeType.ClosedSet;

                // 检查相遇：此节点在反向搜索的关闭集中
                if (backwardClosed.Contains(current))
                {
                    double pathCost = forwardG[current] + backwardG[current];
                    if (pathCost < bestPathCost)
                    {
                        bestPathCost = pathCost;
                        meetingNode = current;
                    }
                }

                // 可以提前终止：当前最小 F*2 >= 已发现的最好路径
                // 这里为简化实现，继续搜索直到明确相遇

                // 展开邻居
                foreach (var neighbor in map.GetNeighbors(current))
                {
                    if (forwardClosed.Contains(neighbor))
                        continue;

                    double moveCost = OctileDistance(current, neighbor);
                    double tentativeG = forwardG[current] + moveCost;

                    if (!forwardG.ContainsKey(neighbor) || tentativeG < forwardG[neighbor])
                    {
                        forwardG[neighbor] = tentativeG;
                        forwardParent[neighbor] = current;

                        double f = tentativeG + OctileDistance(neighbor, goal);
                        if (!forwardOpenLookup.Contains(neighbor))
                        {
                            forwardOpen.Enqueue(neighbor, f);
                            forwardOpenLookup.Add(neighbor);
                            if (neighbor != start && neighbor != goal)
                                neighbor.VisualType = NodeType.OpenSet;
                        }
                    }
                }
            }
            else
            {
                // 展开反向搜索
                var current = backwardOpen.Dequeue();
                backwardOpenLookup.Remove(current);
                backwardClosed.Add(current);

                // 可视化：用不同色调区分反向搜索
                // 这里复用 ClosedSet 颜色，但在实际中可以用紫色等区分

                // 检查相遇：此节点在前向搜索的关闭集中
                if (forwardClosed.Contains(current))
                {
                    double pathCost = forwardG[current] + backwardG[current];
                    if (pathCost < bestPathCost)
                    {
                        bestPathCost = pathCost;
                        meetingNode = current;
                    }
                }

                // 展开邻居
                foreach (var neighbor in map.GetNeighbors(current))
                {
                    if (backwardClosed.Contains(neighbor))
                        continue;

                    double moveCost = OctileDistance(current, neighbor);
                    double tentativeG = backwardG[current] + moveCost;

                    if (!backwardG.ContainsKey(neighbor) || tentativeG < backwardG[neighbor])
                    {
                        backwardG[neighbor] = tentativeG;
                        backwardParent[neighbor] = current;

                        double f = tentativeG + OctileDistance(neighbor, start);
                        if (!backwardOpenLookup.Contains(neighbor))
                        {
                            backwardOpen.Enqueue(neighbor, f);
                            backwardOpenLookup.Add(neighbor);
                            // 反向搜索节点也标记为开放集
                            if (neighbor != start && neighbor != goal)
                                neighbor.VisualType = NodeType.OpenSet;
                        }
                    }
                }
            }

            // 找到了相遇点，再检查是否可能有更优路径
            // 简单策略：找到相遇点就停止（实际可优化为继续找更优）
            if (meetingNode != null)
                break;
        }

        OpenSetCount = forwardOpen.Count + backwardOpen.Count;
        ClosedSetCount = forwardClosed.Count + backwardClosed.Count;

        // 拼接路径
        if (meetingNode != null)
        {
            return JoinPaths(start, goal, meetingNode, forwardParent, backwardParent);
        }

        return [];
    }

    /// <summary>
    /// 拼接前向和反向路径
    /// 前向: start -> ... -> meeting
    /// 反向: meeting <- ... <- goal
    /// 结果: start -> ... -> meeting -> ... -> goal
    /// </summary>
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
        path.Reverse();  // 现在是: start -> ... -> meeting

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
