using GPGems.Core.Math;
using GPGems.Core.Geometry;

namespace GPGems.Core.Physics.Spatial;

/// <summary>
/// KD-Tree 节点
/// </summary>
/// <typeparam name="T">存储的物体类型</typeparam>
public class KDTreeNode<T>
    where T : class
{
    public int Axis { get; }
    public float SplitValue { get; }
    public KDTreeNode<T>? Left { get; }
    public KDTreeNode<T>? Right { get; }
    public (T Obj, Vector3 Point)[]? Points { get; }

    public bool IsLeaf => Points != null;

    public KDTreeNode(int axis, float splitValue, KDTreeNode<T>? left, KDTreeNode<T>? right)
    {
        Axis = axis;
        SplitValue = splitValue;
        Left = left;
        Right = right;
    }

    public KDTreeNode((T Obj, Vector3 Point)[] points)
    {
        Points = points;
        Axis = 0;
    }
}

/// <summary>
/// KD-Tree 空间划分
/// 用于点数据的快速最近邻查询
/// </summary>
/// <typeparam name="T">存储的物体类型</typeparam>
public class KDTree<T>
    where T : class
{
    private const int MaxPointsPerLeaf = 8;
    private readonly KDTreeNode<T>? _root;

    public KDTree((T Obj, Vector3 Point)[] points)
    {
        if (points.Length == 0)
            return;

        _root = BuildTree(points, 0);
    }

    private KDTreeNode<T> BuildTree((T Obj, Vector3 Point)[] points, int depth)
    {
        int axis = depth % 3;

        if (points.Length <= MaxPointsPerLeaf)
        {
            return new KDTreeNode<T>(points);
        }

        Array.Sort(points, (a, b) => GetAxisValue(a.Point, axis).CompareTo(GetAxisValue(b.Point, axis)));

        int median = points.Length / 2;
        float splitValue = GetAxisValue(points[median].Point, axis);

        var leftPoints = points.Take(median).ToArray();
        var rightPoints = points.Skip(median).ToArray();

        return new KDTreeNode<T>(
            axis,
            splitValue,
            BuildTree(leftPoints, depth + 1),
            BuildTree(rightPoints, depth + 1)
        );
    }

    private static float GetAxisValue(Vector3 point, int axis)
    {
        return axis switch
        {
            0 => point.X,
            1 => point.Y,
            2 => point.Z,
            _ => 0
        };
    }

    /// <summary>最近邻查询</summary>
    public T? NearestNeighbor(Vector3 queryPoint)
    {
        if (_root == null)
            return null;

        (T? best, float bestDist) = NearestNeighbor(_root, queryPoint, (default, float.MaxValue));
        return best;
    }

    private (T? Best, float BestDist) NearestNeighbor(KDTreeNode<T> node, Vector3 queryPoint, (T? Best, float BestDist) currentBest)
    {
        if (node.IsLeaf)
        {
            foreach (var (obj, point) in node.Points!)
            {
                float dist = Vector3.DistanceSquared(point, queryPoint);
                if (dist < currentBest.BestDist)
                {
                    currentBest = (obj, dist);
                }
            }
            return currentBest;
        }

        KDTreeNode<T>? nearNode;
        KDTreeNode<T>? farNode;

        float queryAxis = GetAxisValue(queryPoint, node.Axis);
        if (queryAxis < node.SplitValue)
        {
            nearNode = node.Left;
            farNode = node.Right;
        }
        else
        {
            nearNode = node.Right;
            farNode = node.Left;
        }

        if (nearNode != null)
        {
            currentBest = NearestNeighbor(nearNode, queryPoint, currentBest);
        }

        float planeDist = queryAxis - node.SplitValue;
        if (planeDist * planeDist < currentBest.BestDist && farNode != null)
        {
            currentBest = NearestNeighbor(farNode, queryPoint, currentBest);
        }

        return currentBest;
    }

    /// <summary>半径查询</summary>
    public List<T> RadiusQuery(Vector3 center, float radius)
    {
        var results = new List<T>();
        if (_root == null)
            return results;

        RadiusQuery(_root, center, radius * radius, results);
        return results;
    }

    private void RadiusQuery(KDTreeNode<T> node, Vector3 center, float radiusSq, List<T> results)
    {
        if (node.IsLeaf)
        {
            foreach (var (obj, point) in node.Points!)
            {
                if (Vector3.DistanceSquared(point, center) <= radiusSq)
                    results.Add(obj);
            }
            return;
        }

        float queryAxis = GetAxisValue(center, node.Axis);
        float planeDist = queryAxis - node.SplitValue;

        if (planeDist * planeDist <= radiusSq)
        {
            if (node.Left != null)
                RadiusQuery(node.Left, center, radiusSq, results);
            if (node.Right != null)
                RadiusQuery(node.Right, center, radiusSq, results);
        }
        else if (queryAxis < node.SplitValue && node.Left != null)
        {
            RadiusQuery(node.Left, center, radiusSq, results);
        }
        else if (node.Right != null)
        {
            RadiusQuery(node.Right, center, radiusSq, results);
        }
    }
}
