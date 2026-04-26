using GPGems.Core.Math;
using GPGems.Core.Geometry;

namespace GPGems.Core.Physics.Spatial;

/// <summary>
/// 八叉树节点
/// </summary>
/// <typeparam name="T">存储的物体类型</typeparam>
public class OctreeNode<T>
    where T : class
{
    private const int MaxObjectsPerNode = 8;
    private const int MaxDepth = 6;

    public Bounds Bounds { get; }
    public int Depth { get; }
    public OctreeNode<T>? Parent { get; }
    public OctreeNode<T>[]? Children { get; private set; }

    private readonly List<(T Obj, Bounds Bounds)> _objects = new();
    public IReadOnlyList<(T Obj, Bounds Bounds)> Objects => _objects;

    public bool IsLeaf => Children == null;

    public OctreeNode(Bounds bounds, int depth = 0, OctreeNode<T>? parent = null)
    {
        Bounds = bounds;
        Depth = depth;
        Parent = parent;
    }

    /// <summary>插入物体</summary>
    public void Insert(T obj, Bounds objBounds)
    {
        if (Depth >= MaxDepth)
        {
            _objects.Add((obj, objBounds));
            return;
        }

        if (!IsLeaf)
        {
            InsertIntoChildren(obj, objBounds);
            return;
        }

        _objects.Add((obj, objBounds));

        if (_objects.Count > MaxObjectsPerNode)
        {
            Split();
        }
    }

    private void InsertIntoChildren(T obj, Bounds objBounds)
    {
        if (Children == null)
            return;

        foreach (var child in Children)
        {
            if (child.Bounds.Contains(objBounds))
            {
                child.Insert(obj, objBounds);
                return;
            }
        }

        _objects.Add((obj, objBounds));
    }

    private void Split()
    {
        Vector3 center = Bounds.Center;
        Vector3 half = Bounds.Extents * 0.5f;

        Children = new OctreeNode<T>[8];

        for (int i = 0; i < 8; i++)
        {
            var offset = new Vector3(
                (i & 1) == 0 ? -half.X : half.X,
                (i & 2) == 0 ? -half.Y : half.Y,
                (i & 4) == 0 ? -half.Z : half.Z
            );

            var childBounds = Bounds.FromMinMax(
                center + offset - half * 0.5f,
                center + offset + half * 0.5f
            );

            Children[i] = new OctreeNode<T>(childBounds, Depth + 1, this);
        }

        var toReinsert = _objects.ToList();
        _objects.Clear();

        foreach (var (obj, bounds) in toReinsert)
        {
            InsertIntoChildren(obj, bounds);
        }
    }

    /// <summary>查询与边界盒相交的所有物体</summary>
    public void Query(Bounds queryBounds, List<T> results)
    {
        if (!Bounds.Intersects(queryBounds))
            return;

        foreach (var (obj, bounds) in _objects)
        {
            if (bounds.Intersects(queryBounds) && !results.Contains(obj))
                results.Add(obj);
        }

        if (Children != null)
        {
            foreach (var child in Children)
                child.Query(queryBounds, results);
        }
    }

    /// <summary>射线查询</summary>
    public void RayCast(Ray ray, List<(T Obj, float T)> results)
    {
        if (!Intersection3D.RayAABB(ray, Bounds, out float tMin, out _))
            return;

        foreach (var (obj, bounds) in _objects)
        {
            if (Intersection3D.RayAABB(ray, bounds, out float objT, out _))
            {
                results.Add((obj, objT));
            }
        }

        if (Children != null)
        {
            foreach (var child in Children)
                child.RayCast(ray, results);
        }
    }

    /// <summary>移除物体</summary>
    public bool Remove(T obj)
    {
        for (int i = 0; i < _objects.Count; i++)
        {
            if (_objects[i].Obj == obj)
            {
                _objects.RemoveAt(i);
                return true;
            }
        }

        if (Children != null)
        {
            foreach (var child in Children)
            {
                if (child.Remove(obj))
                    return true;
            }
        }

        return false;
    }

    /// <summary>清空节点</summary>
    public void Clear()
    {
        _objects.Clear();
        if (Children != null)
        {
            foreach (var child in Children)
                child.Clear();
            Children = null;
        }
    }
}

/// <summary>
/// 八叉树空间划分
/// 用于 3D 场景的快速空间查询
/// </summary>
/// <typeparam name="T">存储的物体类型</typeparam>
public class Octree<T>
    where T : class
{
    private readonly OctreeNode<T> _root;

    public Octree(Bounds bounds)
    {
        _root = new OctreeNode<T>(bounds);
    }

    public void Insert(T obj, Bounds bounds) => _root.Insert(obj, bounds);
    public bool Remove(T obj) => _root.Remove(obj);

    public List<T> Query(Bounds bounds)
    {
        var results = new List<T>();
        _root.Query(bounds, results);
        return results;
    }

    public List<(T Obj, float T)> RayCast(Ray ray)
    {
        var results = new List<(T Obj, float T)>();
        _root.RayCast(ray, results);
        results.Sort((a, b) => a.T.CompareTo(b.T));
        return results;
    }

    public void Clear() => _root.Clear();
}
