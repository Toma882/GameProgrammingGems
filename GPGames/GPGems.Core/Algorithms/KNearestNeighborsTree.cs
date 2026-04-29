using GPGems.Core.Math;

namespace GPGems.Core.Algorithms;

/// <summary>
/// 基于KD树优化的k-近邻搜索
/// 适合高维数据、大规模数据集
///
/// 复杂度：构建O(n log²n)，查询O(log n + k)
/// 对比暴力搜索O(n)：在n>1000时优势明显
/// </summary>
/// <typeparam name="T">存储的值类型</typeparam>
public class KNearestNeighborsTree<T>
    where T : notnull
{
    private KDTree<T>? _tree;
    private readonly int _dimensions;

    public KNearestNeighborsTree(int dimensions = 3)
    {
        _dimensions = dimensions;
    }

    /// <summary>构建KD树索引</summary>
    public void Build(List<(Vector3 Position, T Value)> points)
    {
        _tree = new KDTree<T>();
        _tree.Build(points);
    }

    /// <summary>k-近邻查询</summary>
    public List<(Vector3 Position, T Value, float Distance)> FindKNearest(Vector3 query, int k)
    {
        if (_tree == null) return [];

        var results = _tree.FindKNearest(query, k);
        return results
            .Select(r => (r.Position, r.Value, r.Distance))
            .ToList();
    }

    /// <summary>半径范围内的近邻查询</summary>
    public List<(Vector3 Position, T Value, float Distance)> FindRadius(Vector3 query, float radius)
    {
        if (_tree == null) return [];

        // 先用k-NN估计，再过滤半径内的
        var all = _tree.FindKNearest(query, 1000); // 足够大的k
        float radiusSq = radius * radius;

        return all
            .Where(r => r.DistanceSquared <= radiusSq)
            .Select(r => (r.Position, r.Value, r.Distance))
            .ToList();
    }

    /// <summary>内部KD树实现（最小化版）</summary>
    private class KDTree<TInner>
        where TInner : notnull
    {
        public class Node
        {
            public int Axis;
            public float SplitValue;
            public Vector3? Position;
            public TInner? Value;
            public Node? Left;
            public Node? Right;
        }

        private Node? _root;

        public void Build(List<(Vector3 Position, TInner Value)> points)
        {
            _root = BuildRecursive(points, 0);
        }

        private Node BuildRecursive(List<(Vector3 Position, TInner Value)> points, int depth)
        {
            int axis = depth % 3;

            if (points.Count == 1)
            {
                return new Node
                {
                    Axis = axis,
                    Position = points[0].Position,
                    Value = points[0].Value
                };
            }

            points.Sort((a, b) => GetAxisValue(a.Position, axis)
                .CompareTo(GetAxisValue(b.Position, axis)));

            int median = points.Count / 2;
            float splitValue = GetAxisValue(points[median].Position, axis);

            var leftPoints = points.Take(median).ToList();
            var rightPoints = points.Skip(median + 1).ToList();

            var node = new Node
            {
                Axis = axis,
                SplitValue = splitValue
            };

            if (leftPoints.Count > 0)
                node.Left = BuildRecursive(leftPoints, depth + 1);

            if (rightPoints.Count > 0)
                node.Right = BuildRecursive(rightPoints, depth + 1);

            if (node.Left == null && node.Right == null)
            {
                node.Position = points[median].Position;
                node.Value = points[median].Value;
            }

            return node;
        }

        private static float GetAxisValue(Vector3 p, int axis)
        {
            return axis switch { 0 => p.X, 1 => p.Y, 2 => p.Z, _ => 0 };
        }

        public List<NearestResult> FindKNearest(Vector3 query, int k)
        {
            var results = new List<NearestResult>(k);
            FindKNearestRecursive(_root, query, k, results);
            return results.OrderBy(r => r.DistanceSquared).ToList();
        }

        private void FindKNearestRecursive(Node? node, Vector3 query, int k,
            List<NearestResult> results)
        {
            if (node == null) return;

            if (node.Position.HasValue)
            {
                float distSq = (query - node.Position.Value).LengthSquared();
                AddToResults(results, new NearestResult
                {
                    Value = node.Value!,
                    Position = node.Position.Value,
                    DistanceSquared = distSq
                }, k);
                return;
            }

            int axis = node.Axis;
            float queryValue = GetAxisValue(query, axis);
            bool goLeftFirst = queryValue <= node.SplitValue;

            Node? first = goLeftFirst ? node.Left : node.Right;
            Node? second = goLeftFirst ? node.Right : node.Left;

            if (first != null)
                FindKNearestRecursive(first, query, k, results);

            float distToPlane = MathF.Abs(queryValue - node.SplitValue);
            bool needSearchOtherSide = results.Count < k ||
                                       distToPlane * distToPlane < results[results.Count - 1].DistanceSquared;

            if (needSearchOtherSide && second != null)
                FindKNearestRecursive(second, query, k, results);
        }

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

        public struct NearestResult
        {
            public TInner Value;
            public Vector3 Position;
            public float DistanceSquared;
            public float Distance => MathF.Sqrt(DistanceSquared);
        }
    }
}
