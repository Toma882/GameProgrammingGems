using GPGems.Core.Math;
using GPGems.Core.Geometry;

namespace GPGems.Core.Physics.Spatial;

/// <summary>
/// BVH (Bounding Volume Hierarchy) 节点
/// </summary>
/// <typeparam name="T">存储的物体类型</typeparam>
public class BVHNode<T>
    where T : class
{
    public Bounds Bounds { get; }
    public BVHNode<T>? Left { get; }
    public BVHNode<T>? Right { get; }
    public T? Object { get; }

    public bool IsLeaf => Object != null;

    public BVHNode(Bounds bounds, T obj)
    {
        Bounds = bounds;
        Object = obj;
    }

    public BVHNode(Bounds bounds, BVHNode<T> left, BVHNode<T> right)
    {
        Bounds = bounds;
        Left = left;
        Right = right;
    }
}

/// <summary>
/// BVH (Bounding Volume Hierarchy) 层次包围盒树
/// 用于动态物体的快速碰撞检测
/// </summary>
/// <typeparam name="T">存储的物体类型</typeparam>
public class BVHTree<T>
    where T : class
{
    private BVHNode<T>? _root;

    public BVHTree((T Obj, Bounds Bounds)[] objects)
    {
        if (objects.Length == 0)
            return;

        _root = BuildTree(objects, 0, objects.Length - 1, 0);
    }

    private BVHNode<T> BuildTree((T Obj, Bounds Bounds)[] objects, int start, int end, int depth)
    {
        if (start == end)
        {
            return new BVHNode<T>(objects[start].Bounds, objects[start].Obj);
        }

        int axis = depth % 3;
        var span = objects.AsSpan(start, end - start + 1);
        span.Sort((a, b) => GetAxisCenter(a.Bounds, axis).CompareTo(GetAxisCenter(b.Bounds, axis)));

        int mid = (start + end) / 2;
        var left = BuildTree(objects, start, mid, depth + 1);
        var right = BuildTree(objects, mid + 1, end, depth + 1);

        var combined = MergeBounds(left.Bounds, right.Bounds);
        return new BVHNode<T>(combined, left, right);
    }

    private static float GetAxisCenter(Bounds bounds, int axis)
    {
        return axis switch
        {
            0 => bounds.Center.X,
            1 => bounds.Center.Y,
            2 => bounds.Center.Z,
            _ => 0
        };
    }

    private static Bounds MergeBounds(Bounds a, Bounds b)
    {
        Vector3 min = Vector3.Min(a.Min, b.Min);
        Vector3 max = Vector3.Max(a.Max, b.Max);
        return Bounds.FromMinMax(min, max);
    }

    /// <summary>查询与边界盒相交的所有物体</summary>
    public List<T> Query(Bounds bounds)
    {
        var results = new List<T>();
        if (_root == null)
            return results;

        Query(_root, bounds, results);
        return results;
    }

    private void Query(BVHNode<T> node, Bounds bounds, List<T> results)
    {
        if (!node.Bounds.Intersects(bounds))
            return;

        if (node.IsLeaf)
        {
            results.Add(node.Object!);
            return;
        }

        if (node.Left != null)
            Query(node.Left, bounds, results);
        if (node.Right != null)
            Query(node.Right, bounds, results);
    }

    /// <summary>所有可能的碰撞对查询</summary>
    public List<(T A, T B)> FindAllCollisionPairs()
    {
        var results = new List<(T A, T B)>();
        if (_root == null || _root.IsLeaf)
            return results;

        FindAllCollisionPairs(_root.Left!, _root.Right!, results);
        return results;
    }

    private void FindAllCollisionPairs(BVHNode<T> a, BVHNode<T> b, List<(T A, T B)> results)
    {
        if (!a.Bounds.Intersects(b.Bounds))
            return;

        if (a.IsLeaf && b.IsLeaf)
        {
            results.Add((a.Object!, b.Object!));
            return;
        }

        if (a.IsLeaf)
        {
            FindAllCollisionPairs(a, b.Left!, results);
            FindAllCollisionPairs(a, b.Right!, results);
        }
        else if (b.IsLeaf)
        {
            FindAllCollisionPairs(a.Left!, b, results);
            FindAllCollisionPairs(a.Right!, b, results);
        }
        else
        {
            FindAllCollisionPairs(a.Left!, b.Left!, results);
            FindAllCollisionPairs(a.Left!, b.Right!, results);
            FindAllCollisionPairs(a.Right!, b.Left!, results);
            FindAllCollisionPairs(a.Right!, b.Right!, results);
        }
    }

    /// <summary>射线查询</summary>
    public List<(T Obj, float T)> RayCast(Ray ray)
    {
        var results = new List<(T Obj, float T)>();
        if (_root == null)
            return results;

        RayCast(_root, ray, results);
        results.Sort((a, b) => a.T.CompareTo(b.T));
        return results;
    }

    private void RayCast(BVHNode<T> node, Ray ray, List<(T Obj, float T)> results)
    {
        if (!Intersection3D.RayAABB(ray, node.Bounds, out _, out _))
            return;

        if (node.IsLeaf)
        {
            if (Intersection3D.RayAABB(ray, node.Bounds, out float t, out _))
            {
                results.Add((node.Object!, t));
            }
            return;
        }

        if (node.Left != null)
            RayCast(node.Left, ray, results);
        if (node.Right != null)
            RayCast(node.Right, ray, results);
    }
}
