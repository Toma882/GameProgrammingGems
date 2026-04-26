using GPGems.Core.Math;

namespace GPGems.Graphics.SpatialPartitioning;

/// <summary>
/// KD 树（k-维树）
/// 基于 Game Programming Gems 系列的经典实现
/// 特点：按轴交替二分 + 中位数分割 + k-近邻搜索
/// </summary>
/// <typeparam name="T">存储的元素类型</typeparam>
public class KDTree<T>
    where T : notnull
{
    /// <summary>
    /// KD 树节点
    /// </summary>
    public class Node
    {
        /// <summary>分割轴（0=X, 1=Y, 2=Z）</summary>
        public int Axis { get; set; }

        /// <summary>分割值</summary>
        public float SplitValue { get; set; }

        /// <summary>点位置（叶子节点）</summary>
        public Vector3? Position { get; set; }

        /// <summary>存储的值（叶子节点）</summary>
        public T? Value { get; set; }

        /// <summary>左子节点（小于等于分割值）</summary>
        public Node? Left { get; set; }

        /// <summary>右子节点（大于分割值）</summary>
        public Node? Right { get; set; }

        /// <summary>节点深度（根为0）</summary>
        public int Depth { get; set; }

        /// <summary>是否是叶子节点</summary>
        public bool IsLeaf => Left == null && Right == null;

        /// <summary>该节点管辖的边界（用于可视化）</summary>
        public Bounds Bounds { get; set; }
    }

    /// <summary>
    /// k-近邻搜索结果
    /// </summary>
    public struct NearestResult
    {
        public T Value;
        public Vector3 Position;
        public float DistanceSquared;

        public float Distance => MathF.Sqrt(DistanceSquared);
    }

    private Node? _root;
    private int _dimensions = 3;

    /// <summary>根节点</summary>
    public Node? Root => _root;

    /// <summary>构建 KD 树</summary>
    public void Build(List<(Vector3 Position, T Value)> points)
    {
        if (points.Count == 0)
        {
            _root = null;
            return;
        }

        // 计算点云的边界框
        Vector3 min = points[0].Position;
        Vector3 max = points[0].Position;

        foreach (var p in points)
        {
            min = new Vector3(
                MathF.Min(min.X, p.Position.X),
                MathF.Min(min.Y, p.Position.Y),
                MathF.Min(min.Z, p.Position.Z));
            max = new Vector3(
                MathF.Max(max.X, p.Position.X),
                MathF.Max(max.Y, p.Position.Y),
                MathF.Max(max.Z, p.Position.Z));
        }

        var bounds = Bounds.FromMinMax(min, max);
        _root = BuildRecursive(points.ToList(), 0, bounds);
    }

    /// <summary>递归构建</summary>
    private Node BuildRecursive(List<(Vector3 Position, T Value)> points, int depth, Bounds bounds)
    {
        int axis = depth % _dimensions;

        // 只有一个点，创建叶子节点
        if (points.Count == 1)
        {
            return new Node
            {
                Axis = axis,
                SplitValue = GetAxisValue(points[0].Position, axis),
                Position = points[0].Position,
                Value = points[0].Value,
                Depth = depth,
                Bounds = bounds
            };
        }

        // 按当前轴排序，取中位数分割
        points.Sort((a, b) => GetAxisValue(a.Position, axis).CompareTo(GetAxisValue(b.Position, axis)));

        int medianIndex = points.Count / 2;
        float splitValue = GetAxisValue(points[medianIndex].Position, axis);

        var leftPoints = points.Take(medianIndex).ToList();
        var rightPoints = points.Skip(medianIndex + 1).ToList();

        var node = new Node
        {
            Axis = axis,
            SplitValue = splitValue,
            Depth = depth,
            Bounds = bounds
        };

        // 计算子节点边界
        var (leftBounds, rightBounds) = SplitBounds(bounds, axis, splitValue);

        // 递归构建子树
        if (leftPoints.Count > 0)
        {
            node.Left = BuildRecursive(leftPoints, depth + 1, leftBounds);
        }

        if (rightPoints.Count > 0)
        {
            node.Right = BuildRecursive(rightPoints, depth + 1, rightBounds);
        }

        // 如果没有子节点，作为叶子（存储中位数点）
        if (node.Left == null && node.Right == null)
        {
            node.Position = points[medianIndex].Position;
            node.Value = points[medianIndex].Value;
        }

        return node;
    }

    /// <summary>获取指定轴的值</summary>
    private static float GetAxisValue(Vector3 p, int axis)
    {
        return axis switch
        {
            0 => p.X,
            1 => p.Y,
            2 => p.Z,
            _ => 0
        };
    }

    /// <summary>分割边界</summary>
    private static (Bounds left, Bounds right) SplitBounds(Bounds bounds, int axis, float splitValue)
    {
        var center = bounds.Center;
        var extents = bounds.Extents;

        Vector3 leftCenter = axis switch
        {
            0 => new Vector3((bounds.Min.X + splitValue) / 2, center.Y, center.Z),
            1 => new Vector3(center.X, (bounds.Min.Y + splitValue) / 2, center.Z),
            2 => new Vector3(center.X, center.Y, (bounds.Min.Z + splitValue) / 2),
            _ => center
        };

        Vector3 rightCenter = axis switch
        {
            0 => new Vector3((splitValue + bounds.Max.X) / 2, center.Y, center.Z),
            1 => new Vector3(center.X, (splitValue + bounds.Max.Y) / 2, center.Z),
            2 => new Vector3(center.X, center.Y, (splitValue + bounds.Max.Z) / 2),
            _ => center
        };

        Vector3 leftExtents = axis switch
        {
            0 => new Vector3(splitValue - bounds.Min.X, extents.Y, extents.Z),
            1 => new Vector3(extents.X, splitValue - bounds.Min.Y, extents.Z),
            2 => new Vector3(extents.X, extents.Y, splitValue - bounds.Min.Z),
            _ => extents
        };

        Vector3 rightExtents = axis switch
        {
            0 => new Vector3(bounds.Max.X - splitValue, extents.Y, extents.Z),
            1 => new Vector3(extents.X, bounds.Max.Y - splitValue, extents.Z),
            2 => new Vector3(extents.X, extents.Y, bounds.Max.Z - splitValue),
            _ => extents
        };

        return (new Bounds(leftCenter, leftExtents), new Bounds(rightCenter, rightExtents));
    }

    /// <summary>k-近邻搜索</summary>
    public List<NearestResult> FindKNearest(Vector3 queryPoint, int k)
    {
        if (_root == null) return new List<NearestResult>();

        var results = new List<NearestResult>(k);
        FindKNearestRecursive(_root, queryPoint, k, results);
        return results.OrderBy(r => r.DistanceSquared).ToList();
    }

    private void FindKNearestRecursive(Node node, Vector3 queryPoint, int k, List<NearestResult> results)
    {
        if (node.IsLeaf && node.Position.HasValue)
        {
            float distSq = (queryPoint - node.Position.Value).LengthSquared();
            AddToResults(results, new NearestResult
            {
                Value = node.Value!,
                Position = node.Position.Value,
                DistanceSquared = distSq
            }, k);
            return;
        }

        // 决定先搜索哪个子树
        int axis = node.Axis;
        float queryValue = GetAxisValue(queryPoint, axis);
        Node? firstSearch = queryValue <= node.SplitValue ? node.Left : node.Right;
        Node? secondSearch = queryValue <= node.SplitValue ? node.Right : node.Left;

        // 先搜索近的一侧
        if (firstSearch != null)
        {
            FindKNearestRecursive(firstSearch, queryPoint, k, results);
        }

        // 检查是否需要回溯搜索另一侧
        // 如果到分割面的距离 < 当前第k近的距离，就需要搜索另一侧
        float distToPlane = Math.Abs(queryValue - node.SplitValue);
        bool needSearchOtherSide = results.Count < k ||
                                   distToPlane * distToPlane < results[results.Count - 1].DistanceSquared;

        if (needSearchOtherSide && secondSearch != null)
        {
            FindKNearestRecursive(secondSearch, queryPoint, k, results);
        }

        // 内部节点也可能存储点（当分割后某侧没有点时）
        if (node.Position.HasValue)
        {
            float distSq = (queryPoint - node.Position.Value).LengthSquared();
            AddToResults(results, new NearestResult
            {
                Value = node.Value!,
                Position = node.Position.Value,
                DistanceSquared = distSq
            }, k);
        }
    }

    /// <summary>保持结果列表有序，最多k个</summary>
    private static void AddToResults(List<NearestResult> results, NearestResult item, int k)
    {
        if (results.Count < k)
        {
            results.Add(item);
            results.Sort((a, b) => a.DistanceSquared.CompareTo(b.DistanceSquared));
        }
        else if (item.DistanceSquared < results[results.Count - 1].DistanceSquared)
        {
            results.RemoveAt(results.Count - 1);
            results.Add(item);
            results.Sort((a, b) => a.DistanceSquared.CompareTo(b.DistanceSquared));
        }
    }

    /// <summary>范围查询</summary>
    public List<T> QueryRange(Bounds range)
    {
        var results = new List<T>();
        QueryRangeRecursive(_root, range, results);
        return results;
    }

    private void QueryRangeRecursive(Node? node, Bounds range, List<T> results)
    {
        if (node == null) return;

        // 检查节点边界是否与查询范围相交，剪枝
        if (!node.Bounds.Intersects(range)) return;

        // 叶子节点：检查点是否在范围内
        if (node.IsLeaf && node.Position.HasValue)
        {
            if (range.Contains(node.Position.Value))
            {
                results.Add(node.Value!);
            }
            return;
        }

        // 递归搜索子节点
        QueryRangeRecursive(node.Left, range, results);
        QueryRangeRecursive(node.Right, range, results);

        // 内部节点存储的点
        if (node.Position.HasValue && range.Contains(node.Position.Value))
        {
            results.Add(node.Value!);
        }
    }

    /// <summary>获取所有节点（用于可视化）</summary>
    public List<Node> GetAllNodes()
    {
        var nodes = new List<Node>();
        GetAllNodesRecursive(_root, nodes);
        return nodes;
    }

    private void GetAllNodesRecursive(Node? node, List<Node> nodes)
    {
        if (node == null) return;
        nodes.Add(node);
        GetAllNodesRecursive(node.Left, nodes);
        GetAllNodesRecursive(node.Right, nodes);
    }

    /// <summary>获取搜索路径（用于演示回溯过程）</summary>
    public List<Node> GetSearchPath(Vector3 queryPoint)
    {
        var path = new List<Node>();
        GetSearchPathRecursive(_root, queryPoint, path);
        return path;
    }

    private void GetSearchPathRecursive(Node? node, Vector3 queryPoint, List<Node> path)
    {
        if (node == null) return;
        path.Add(node);

        if (node.IsLeaf) return;

        int axis = node.Axis;
        float queryValue = GetAxisValue(queryPoint, axis);

        if (queryValue <= node.SplitValue)
        {
            GetSearchPathRecursive(node.Left, queryPoint, path);
        }
        else
        {
            GetSearchPathRecursive(node.Right, queryPoint, path);
        }
    }
}
