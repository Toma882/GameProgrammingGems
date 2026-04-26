using SysMath = System.Math;

namespace GPGems.Core.Algorithms;

/// <summary>
/// Ball 树（球树）
/// 比KD树更适合高维特征空间的最近邻搜索
///
/// 核心思想：
/// - 每个节点用一个超球体包围其下所有点
/// - 沿方差最大的维度分割（K均值）
/// - 回溯时用球心距离快速剪枝
///
/// 适用场景：AI特征向量（≥10维）、推荐系统用户画像
/// </summary>
/// <typeparam name="T">存储的值类型</typeparam>
public class BallTree<T>
    where T : notnull
{
    /// <summary>
    /// Ball 树节点
    /// </summary>
    public class Node
    {
        /// <summary>球心（所有点的中心）</summary>
        public float[] Center { get; set; }

        /// <summary>半径（球心到最远点的距离）</summary>
        public float Radius { get; set; }

        /// <summary>本节点存储的点（叶子节点）</summary>
        public List<(float[] Point, T Value)>? Points { get; set; }

        /// <summary>左子球（包含距离较近的点）</summary>
        public Node? Left { get; set; }

        /// <summary>右子球（包含距离较远的点）</summary>
        public Node? Right { get; set; }

        /// <summary>是否是叶子节点</summary>
        public bool IsLeaf => Points != null;
    }

    private Node? _root;
    private readonly int _dimensions;
    private readonly int _leafCapacity;

    /// <summary>距离度量函数</summary>
    public Func<float[], float[], float> DistanceMetric { get; set; }

    public BallTree(int dimensions, int leafCapacity = 20)
    {
        _dimensions = dimensions;
        _leafCapacity = leafCapacity;
        DistanceMetric = DistanceMetrics.Euclidean;
    }

    /// <summary>构建 Ball 树</summary>
    public void Build(List<(float[] Point, T Value)> points)
    {
        _root = BuildRecursive(points, 0);
    }

    private Node BuildRecursive(List<(float[] Point, T Value)> points, int depth)
    {
        // 计算中心
        var center = ComputeCentroid(points);

        // 计算半径（到最远点的距离）
        float radius = 0;
        foreach (var p in points)
        {
            float d = DistanceMetric(center, p.Point);
            radius = MathF.Max(radius, d);
        }

        // 点足够少，创建叶子
        if (points.Count <= _leafCapacity)
        {
            return new Node
            {
                Center = center,
                Radius = radius,
                Points = points
            };
        }

        // 找方差最大的维度进行分割
        int splitAxis = FindMaxVarianceAxis(points);

        // 按该维度排序，取中位数分割
        points.Sort((a, b) => a.Point[splitAxis].CompareTo(b.Point[splitAxis]));
        int median = points.Count / 2;

        var leftPoints = points.Take(median).ToList();
        var rightPoints = points.Skip(median).ToList();

        return new Node
        {
            Center = center,
            Radius = radius,
            Left = BuildRecursive(leftPoints, depth + 1),
            Right = BuildRecursive(rightPoints, depth + 1)
        };
    }

    /// <summary>计算所有点的质心</summary>
    private float[] ComputeCentroid(List<(float[] Point, T Value)> points)
    {
        var centroid = new float[_dimensions];
        foreach (var p in points)
        {
            for (int i = 0; i < _dimensions; i++)
            {
                centroid[i] += p.Point[i];
            }
        }
        for (int i = 0; i < _dimensions; i++)
        {
            centroid[i] /= points.Count;
        }
        return centroid;
    }

    /// <summary>找方差最大的维度</summary>
    private int FindMaxVarianceAxis(List<(float[] Point, T Value)> points)
    {
        float[] mean = new float[_dimensions];
        float[] variance = new float[_dimensions];

        foreach (var p in points)
        {
            for (int i = 0; i < _dimensions; i++)
            {
                mean[i] += p.Point[i];
            }
        }
        for (int i = 0; i < _dimensions; i++)
        {
            mean[i] /= points.Count;
        }

        foreach (var p in points)
        {
            for (int i = 0; i < _dimensions; i++)
            {
                float diff = p.Point[i] - mean[i];
                variance[i] += diff * diff;
            }
        }

        int maxAxis = 0;
        float maxVar = variance[0];
        for (int i = 1; i < _dimensions; i++)
        {
            if (variance[i] > maxVar)
            {
                maxVar = variance[i];
                maxAxis = i;
            }
        }
        return maxAxis;
    }

    /// <summary>k-近邻搜索</summary>
    public List<(float[] Point, T Value, float Distance)> FindKNearest(float[] query, int k)
    {
        if (_root == null) return new();

        var results = new List<(float[], T, float)>(k);
        FindKNearestRecursive(_root, query, k, results);
        return results.OrderBy(r => r.Item3).ToList();
    }

    private void FindKNearestRecursive(Node node, float[] query, int k,
        List<(float[], T, float)> results)
    {
        // 到球心的距离
        float distToCenter = DistanceMetric(query, node.Center);

        // 剪枝：如果这个球里不可能有更近的点，直接跳过
        if (results.Count >= k)
        {
            float maxDistInResults = results[results.Count - 1].Item3;
            if (distToCenter - node.Radius >= maxDistInResults)
                return;
        }

        if (node.IsLeaf)
        {
            // 检查叶子中所有点
            foreach (var (point, value) in node.Points!)
            {
                float dist = DistanceMetric(query, point);
                AddToResults(results, (point, value, dist), k);
            }
            return;
        }

        // 优先搜索更近的子球
        float distLeft = node.Left != null ? DistanceMetric(query, node.Left.Center) : float.MaxValue;
        float distRight = node.Right != null ? DistanceMetric(query, node.Right.Center) : float.MaxValue;

        Node first = distLeft < distRight ? node.Left! : node.Right!;
        Node second = distLeft < distRight ? node.Right! : node.Left!;

        if (first != null)
            FindKNearestRecursive(first, query, k, results);

        // 检查另一个子球是否有搜索价值
        float currentMaxDist = results.Count >= k ? results[results.Count - 1].Item3 : float.MaxValue;
        float distToSecond = second != null ? DistanceMetric(query, second.Center) : float.MaxValue;

        if (second != null && (results.Count < k || distToSecond - second.Radius < currentMaxDist))
        {
            FindKNearestRecursive(second, query, k, results);
        }
    }

    private static void AddToResults(List<(float[], T, float)> results, (float[], T, float) item, int k)
    {
        if (results.Count < k)
        {
            results.Add(item);
            results.Sort((a, b) => a.Item3.CompareTo(b.Item3));
        }
        else if (item.Item3 < results[results.Count - 1].Item3)
        {
            results.RemoveAt(results.Count - 1);
            results.Add(item);
            results.Sort((a, b) => a.Item3.CompareTo(b.Item3));
        }
    }

    /// <summary>范围查询</summary>
    public List<(float[] Point, T Value, float Distance)> RangeQuery(float[] query, float radius)
    {
        if (_root == null) return new();

        var results = new List<(float[], T, float)>();
        RangeQueryRecursive(_root, query, radius * radius, results);
        return results;
    }

    private void RangeQueryRecursive(Node node, float[] query, float radiusSq,
        List<(float[], T, float)> results)
    {
        float distToCenterSq = DistanceMetric(query, node.Center);
        distToCenterSq *= distToCenterSq;

        // 剪枝：查询球与节点球不相交
        float minDist = MathF.Sqrt(distToCenterSq) - node.Radius;
        if (minDist * minDist > radiusSq)
            return;

        if (node.IsLeaf)
        {
            foreach (var (point, value) in node.Points!)
            {
                float dist = DistanceMetric(query, point);
                if (dist * dist <= radiusSq)
                {
                    results.Add((point, value, dist));
                }
            }
            return;
        }

        if (node.Left != null)
            RangeQueryRecursive(node.Left, query, radiusSq, results);

        if (node.Right != null)
            RangeQueryRecursive(node.Right, query, radiusSq, results);
    }

    /// <summary>获取树的统计信息</summary>
    public (int NodeCount, int LeafCount, int MaxDepth) GetStats()
    {
        if (_root == null) return (0, 0, 0);
        return GetStatsRecursive(_root, 0);
    }

    private (int NodeCount, int LeafCount, int MaxDepth) GetStatsRecursive(Node node, int depth)
    {
        if (node.IsLeaf)
        {
            return (1, 1, depth);
        }

        var left = node.Left != null ? GetStatsRecursive(node.Left, depth + 1) : (0, 0, 0);
        var right = node.Right != null ? GetStatsRecursive(node.Right, depth + 1) : (0, 0, 0);

        return (
            1 + left.Item1 + right.Item1,
            left.Item2 + right.Item2,
            SysMath.Max(left.Item3, right.Item3)
        );
    }
}
