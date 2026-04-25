namespace GPGems.AI.Pathfinding;

/// <summary>
/// 网格节点类型
/// </summary>
public enum NodeType
{
    Walkable,
    Obstacle,
    Start,
    Goal,
    Path,
    OpenSet,
    ClosedSet
}

/// <summary>
/// 网格节点
/// </summary>
public class GridNode
{
    public int X { get; }
    public int Y { get; }
    public bool IsWalkable { get; set; } = true;

    // A* 算法使用的属性
    public double GCost { get; set; }   // 从起点到当前节点的实际代价
    public double HCost { get; set; }   // 启发式估计：当前节点到终点的估计代价
    public double FCost => GCost + HCost;
    public GridNode? Parent { get; set; }

    // 可视化状态
    public NodeType VisualType { get; set; } = NodeType.Walkable;

    public GridNode(int x, int y)
    {
        X = x;
        Y = y;
    }

    public override string ToString() => $"({X},{Y})";
}

/// <summary>
/// 2D 网格地图
/// </summary>
public class GridMap
{
    public int Width { get; }
    public int Height { get; }
    public GridNode[,] Nodes { get; }

    public GridMap(int width, int height)
    {
        Width = width;
        Height = height;
        Nodes = new GridNode[width, height];

        for (int x = 0; x < width; x++)
        for (int y = 0; y < height; y++)
            Nodes[x, y] = new GridNode(x, y);
    }

    public GridNode? GetNode(int x, int y)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height)
            return null;
        return Nodes[x, y];
    }

    /// <summary>获取相邻的可行走节点（8方向）</summary>
    public List<GridNode> GetNeighbors(GridNode node)
    {
        var neighbors = new List<GridNode>();

        // 8个方向的偏移
        int[] dx = [-1, 0, 1, -1, 1, -1, 0, 1];
        int[] dy = [-1, -1, -1, 0, 0, 1, 1, 1];

        for (int i = 0; i < 8; i++)
        {
            int nx = node.X + dx[i];
            int ny = node.Y + dy[i];

            if (nx >= 0 && nx < Width && ny >= 0 && ny < Height && Nodes[nx, ny].IsWalkable)
            {
                // 对角线移动时，不允许穿过墙角
                if (i == 0 || i == 3 || i == 5 || i == 7) // 对角线索引
                {
                    // 检查两个相邻的轴向方向是否都可行走
                }
                neighbors.Add(Nodes[nx, ny]);
            }
        }

        return neighbors;
    }

    /// <summary>计算两点之间的移动代价</summary>
    public static double GetDistance(GridNode a, GridNode b)
    {
        int dx = Math.Abs(a.X - b.X);
        int dy = Math.Abs(a.Y - b.Y);

        // 对角线距离（Chebyshev距离用于8方向移动）
        int dMax = Math.Max(dx, dy);
        int dMin = Math.Min(dx, dy);
        return dMin * 1.414 + (dMax - dMin); // 对角线代价 + 直线代价
    }

    /// <summary>重置所有节点的寻路状态</summary>
    public void ResetPathfinding()
    {
        for (int x = 0; x < Width; x++)
        for (int y = 0; y < Height; y++)
        {
            Nodes[x, y].GCost = 0;
            Nodes[x, y].HCost = 0;
            Nodes[x, y].Parent = null;
            if (Nodes[x, y].VisualType != NodeType.Obstacle &&
                Nodes[x, y].VisualType != NodeType.Start &&
                Nodes[x, y].VisualType != NodeType.Goal)
            {
                Nodes[x, y].VisualType = NodeType.Walkable;
            }
        }
    }

    public void ClearAll()
    {
        for (int x = 0; x < Width; x++)
        for (int y = 0; y < Height; y++)
        {
            Nodes[x, y].IsWalkable = true;
            Nodes[x, y].VisualType = NodeType.Walkable;
            Nodes[x, y].GCost = 0;
            Nodes[x, y].HCost = 0;
            Nodes[x, y].Parent = null;
        }
    }
}
