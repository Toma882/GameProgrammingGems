using System.Numerics;
using GPGems.Core.Math;

namespace GPGems.Graphics.SpatialPartitioning;

/// <summary>
/// 八叉树
/// 用于3D空间分割，支持点云/范围查询/碰撞检测
/// 基于 Game Programming Gems 1 Polygonal 10 Ginsburg
/// </summary>
/// <typeparam name="T">存储的元素类型</typeparam>
public class Octree<T>
    where T : notnull
{
    /// <summary>默认最大深度</summary>
    private const int DefaultMaxDepth = 6;

    /// <summary>默认每个节点最大元素数</summary>
    private const int DefaultMaxElements = 8;

    /// <summary>八个子节点的索引
    /// 八叉树命名规则：按轴方向组合
    /// L/R = Left/Right (X轴)
    /// D/U = Down/Up (Y轴)
    /// B/F = Back/Front (Z轴)
    /// </summary>
    private enum Octant
    {
        LDB = 0,  // 左下后 (-X, -Y, -Z)
        LDF = 1,  // 左下前 (-X, -Y, +Z)
        LUB = 2,  // 左上后 (-X, +Y, -Z)
        LUF = 3,  // 左上前 (-X, +Y, +Z)
        RDB = 4,  // 右下后 (+X, -Y, -Z)
        RDF = 5,  // 右下前 (+X, -Y, +Z)
        RUB = 6,  // 右上后 (+X, +Y, -Z)
        RUF = 7,  // 右上前 (+X, +Y, +Z)
    }

    /// <summary>节点边界</summary>
    public Bounds Bounds { get; }

    /// <summary>当前节点深度（根节点为0）</summary>
    public int Depth { get; }

    /// <summary>该节点存储的元素（带边界，用于精确碰撞）</summary>
    private readonly List<(Bounds Bounds, T Value)> _elements = [];

    /// <summary>八个子节点（null 表示叶子节点）</summary>
    private Octree<T>?[]? _children;

    /// <summary>最大深度</summary>
    private readonly int _maxDepth;

    /// <summary>每个节点最大元素数</summary>
    private readonly int _maxElements;

    /// <summary>是否是叶子节点（无子节点）</summary>
    public bool IsLeaf => _children == null;

    /// <summary>元素总数（递归统计）</summary>
    public int TotalElements
    {
        get
        {
            int count = _elements.Count;
            if (_children != null)
            {
                foreach (var child in _children)
                {
                    if (child != null)
                    {
                        count += child.TotalElements;
                    }
                }
            }

            return count;
        }
    }

    /// <summary>节点总数（递归统计）</summary>
    public int TotalNodes
    {
        get
        {
            int count = 1;
            if (_children != null)
            {
                foreach (var child in _children)
                {
                    if (child != null)
                    {
                        count += child.TotalNodes;
                    }
                }
            }

            return count;
        }
    }

    /// <summary>创建根节点八叉树</summary>
    public Octree(Bounds bounds, int maxDepth = DefaultMaxDepth, int maxElements = DefaultMaxElements)
        : this(bounds, 0, maxDepth, maxElements)
    {
    }

    /// <summary>创建子节点</summary>
    private Octree(Bounds bounds, int depth, int maxDepth, int maxElements)
    {
        Bounds = bounds;
        Depth = depth;
        _maxDepth = maxDepth;
        _maxElements = maxElements;
    }

    /// <summary>插入元素（带边界）</summary>
    public bool Insert(Bounds elementBounds, T value)
    {
        // 检查元素边界是否完全在节点内
        if (!Bounds.Contains(elementBounds))
        {
            return false;
        }

        // 如果还能容纳，或者已达到最大深度，直接存储
        if (IsLeaf && (_elements.Count < _maxElements || Depth >= _maxDepth))
        {
            _elements.Add((elementBounds, value));
            return true;
        }

        // 需要分裂
        if (IsLeaf)
        {
            Split();
        }

        // 尝试插入子节点
        // 如果元素跨多个子节点，保留在父节点中
        bool inserted = false;
        if (_children != null)
        {
            foreach (var child in _children)
            {
                if (child != null && child.Bounds.Contains(elementBounds))
                {
                    if (child.Insert(elementBounds, value))
                    {
                        inserted = true;
                        break;
                    }
                }
            }
        }

        // 如果不能放入任何子节点，则存在父节点中
        if (!inserted)
        {
            _elements.Add((elementBounds, value));
        }

        return true;
    }

    /// <summary>简化版：按位置插入（零大小点）</summary>
    public bool Insert(Vector3 position, T value)
    {
        return Insert(new Bounds(position, Vector3.Zero), value);
    }

    /// <summary>分裂节点为八个子节点</summary>
    private void Split()
    {
        float halfX = Bounds.Extents.X / 2;
        float halfY = Bounds.Extents.Y / 2;
        float halfZ = Bounds.Extents.Z / 2;
        Vector3 center = Bounds.Center;

        _children = new Octree<T>?[8];
        var halfExtents = new Vector3(halfX, halfY, halfZ);

        // 八个卦限
        _children[(int)Octant.LDB] = CreateChild(center + new Vector3(-halfX, -halfY, -halfZ), halfExtents);
        _children[(int)Octant.LDF] = CreateChild(center + new Vector3(-halfX, -halfY, +halfZ), halfExtents);
        _children[(int)Octant.LUB] = CreateChild(center + new Vector3(-halfX, +halfY, -halfZ), halfExtents);
        _children[(int)Octant.LUF] = CreateChild(center + new Vector3(-halfX, +halfY, +halfZ), halfExtents);
        _children[(int)Octant.RDB] = CreateChild(center + new Vector3(+halfX, -halfY, -halfZ), halfExtents);
        _children[(int)Octant.RDF] = CreateChild(center + new Vector3(+halfX, -halfY, +halfZ), halfExtents);
        _children[(int)Octant.RUB] = CreateChild(center + new Vector3(+halfX, +halfY, -halfZ), halfExtents);
        _children[(int)Octant.RUF] = CreateChild(center + new Vector3(+halfX, +halfY, +halfZ), halfExtents);

        // 将当前节点的元素重新分配到子节点
        var toReinsert = new List<(Bounds, T)>(_elements);
        _elements.Clear();

        foreach (var (bounds, val) in toReinsert)
        {
            bool inserted = false;
            if (_children != null)
            {
                foreach (var child in _children)
                {
                    if (child != null && child.Bounds.Contains(bounds))
                    {
                        if (child.Insert(bounds, val))
                        {
                            inserted = true;
                            break;
                        }
                    }
                }
            }

            // 跨子节点的元素保留在父节点
            if (!inserted)
            {
                _elements.Add((bounds, val));
            }
        }
    }

    private Octree<T> CreateChild(Vector3 childCenter, Vector3 halfExtents)
    {
        return new Octree<T>(
            new Bounds(childCenter, halfExtents),
            Depth + 1,
            _maxDepth,
            _maxElements
        );
    }

    /// <summary>范围查询：查找与给定边界相交的所有元素</summary>
    public List<T> QueryRange(Bounds range)
    {
        var results = new List<T>();
        QueryRangeInternal(range, results);
        return results;
    }

    private void QueryRangeInternal(Bounds range, List<T> results)
    {
        // 如果边界不相交，直接返回
        if (!Bounds.Intersects(range))
        {
            return;
        }

        // 检查当前节点的元素
        foreach (var (bounds, val) in _elements)
        {
            if (range.Intersects(bounds))
            {
                results.Add(val);
            }
        }

        // 递归查询子节点
        if (_children != null)
        {
            foreach (var child in _children)
            {
                child?.QueryRangeInternal(range, results);
            }
        }
    }

    /// <summary>射线查询</summary>
    public List<T> QueryRay(Vector3 rayOrigin, Vector3 rayDirection, float maxDistance = float.MaxValue)
    {
        var results = new List<T>();
        QueryRayInternal(rayOrigin, rayDirection, maxDistance, results);
        return results;
    }

    private void QueryRayInternal(Vector3 rayOrigin, Vector3 rayDirection, float maxDistance, List<T> results)
    {
        // 简单AABB射线检测
        if (!IntersectRayAABB(rayOrigin, rayDirection, Bounds, maxDistance))
        {
            return;
        }

        // 检查当前节点的元素
        foreach (var (bounds, val) in _elements)
        {
            if (IntersectRayAABB(rayOrigin, rayDirection, bounds, maxDistance))
            {
                results.Add(val);
            }
        }

        // 递归查询子节点
        if (_children != null)
        {
            foreach (var child in _children)
            {
                child?.QueryRayInternal(rayOrigin, rayDirection, maxDistance, results);
            }
        }
    }

    /// <summary>AABB 射线相交检测</summary>
    private static bool IntersectRayAABB(Vector3 origin, Vector3 direction, Bounds aabb, float maxDistance)
    {
        float tMin = 0;
        float tMax = maxDistance;

        // X轴
        float invDirX = 1.0f / direction.X;
        float t1 = (aabb.Min.X - origin.X) * invDirX;
        float t2 = (aabb.Max.X - origin.X) * invDirX;
        if (invDirX < 0) (t1, t2) = (t2, t1);
        tMin = MathF.Max(tMin, t1);
        tMax = MathF.Min(tMax, t2);
        if (tMin > tMax) return false;

        // Y轴
        float invDirY = 1.0f / direction.Y;
        float t1y = (aabb.Min.Y - origin.Y) * invDirY;
        float t2y = (aabb.Max.Y - origin.Y) * invDirY;
        if (invDirY < 0) (t1y, t2y) = (t2y, t1y);
        tMin = MathF.Max(tMin, t1y);
        tMax = MathF.Min(tMax, t2y);
        if (tMin > tMax) return false;

        // Z轴
        float invDirZ = 1.0f / direction.Z;
        float t1z = (aabb.Min.Z - origin.Z) * invDirZ;
        float t2z = (aabb.Max.Z - origin.Z) * invDirZ;
        if (invDirZ < 0) (t1z, t2z) = (t2z, t1z);
        tMin = MathF.Max(tMin, t1z);
        tMax = MathF.Min(tMax, t2z);
        if (tMin > tMax) return false;

        return tMin <= maxDistance;
    }

    /// <summary>获取所有节点（用于可视化）</summary>
    public List<Octree<T>> GetAllNodes()
    {
        var nodes = new List<Octree<T>>();
        GetAllNodesInternal(nodes);
        return nodes;
    }

    private void GetAllNodesInternal(List<Octree<T>> nodes)
    {
        nodes.Add(this);
        if (_children != null)
        {
            foreach (var child in _children)
            {
                child?.GetAllNodesInternal(nodes);
            }
        }
    }

    /// <summary>获取所有叶子节点（用于可视化）</summary>
    public List<Octree<T>> GetAllLeafNodes()
    {
        var leaves = new List<Octree<T>>();
        GetAllLeafNodesInternal(leaves);
        return leaves;
    }

    private void GetAllLeafNodesInternal(List<Octree<T>> leaves)
    {
        if (IsLeaf)
        {
            leaves.Add(this);
        }
        else if (_children != null)
        {
            foreach (var child in _children)
            {
                child?.GetAllLeafNodesInternal(leaves);
            }
        }
    }

    /// <summary>清空八叉树</summary>
    public void Clear()
    {
        _elements.Clear();
        if (_children != null)
        {
            foreach (var child in _children)
            {
                child?.Clear();
            }
            _children = null;
        }
    }
}
