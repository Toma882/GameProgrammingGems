using GPGems.Core.Math;

namespace GPGems.Graphics.SpatialPartitioning;

/// <summary>
/// 四叉树节点
/// 用于2D空间分割，支持点云/范围查询
/// 基于 Game Programming Gems 1 Polygonal 章节
/// </summary>
/// <typeparam name="T">存储的元素类型</typeparam>
public class Quadtree<T>
    where T : notnull
{
    /// <summary>默认最大深度</summary>
    private const int DefaultMaxDepth = 8;

    /// <summary>默认每个节点最大元素数</summary>
    private const int DefaultMaxElements = 4;

    /// <summary>四个子节点的索引</summary>
    private enum Quadrant
    {
        TopLeft = 0,      // 左上（-X, +Y）
        TopRight = 1,     // 右上（+X, +Y）
        BottomLeft = 2,    // 左下（-X, -Y）
        BottomRight = 3     // 右下（+X, -Y）
    }

    /// <summary>节点边界</summary>
    public Bounds Bounds { get; }

    /// <summary>当前节点深度（根节点为0）</summary>
    public int Depth { get; }

    /// <summary>该节点存储的元素</summary>
    private readonly List<(Vector3 Position, T Value)> _elements = [];

    /// <summary>四个子节点（null 表示叶子节点）</summary>
    private Quadtree<T>?[]? _children;

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

    /// <summary>创建根节点四叉树</summary>
    public Quadtree(Bounds bounds, int maxDepth = DefaultMaxDepth, int maxElements = DefaultMaxElements)
        : this(bounds, 0, maxDepth, maxElements)
    {
    }

    /// <summary>创建子节点</summary>
    private Quadtree(Bounds bounds, int depth, int maxDepth, int maxElements)
    {
        Bounds = bounds;
        Depth = depth;
        _maxDepth = maxDepth;
        _maxElements = maxElements;
    }

    /// <summary>插入元素</summary>
    public bool Insert(Vector3 position, T value)
    {
        // 检查点是否在边界内
        if (!Bounds.Contains(position))
        {
            return false;
        }

        // 如果还能容纳，或者已达到最大深度，直接存储
        if (IsLeaf && (_elements.Count < _maxElements || Depth >= _maxDepth))
        {
            _elements.Add((position, value));
            return true;
        }

        // 需要分裂
        if (IsLeaf)
        {
            Split();
        }

        // 尝试插入子节点
        var quadrant = GetQuadrant(position);
        return _children![(int)quadrant]!.Insert(position, value);
    }

    /// <summary>分裂节点为四个子节点</summary>
    private void Split()
    {
        float halfWidth = Bounds.Extents.X / 2;
        float halfHeight = Bounds.Extents.Y / 2;
        Vector3 center = Bounds.Center;

        _children = new Quadtree<T>?[4];

        // 左上 (-X, +Y)
        _children[(int)Quadrant.TopLeft] = new Quadtree<T>(
            new Bounds(
                new Vector3(center.X - halfWidth, center.Y + halfHeight, center.Z),
                new Vector3(halfWidth, halfHeight, Bounds.Extents.Z)
            ),
            Depth + 1,
            _maxDepth,
            _maxElements
        );

        // 右上 (+X, +Y)
        _children[(int)Quadrant.TopRight] = new Quadtree<T>(
            new Bounds(
                new Vector3(center.X + halfWidth, center.Y + halfHeight, center.Z),
                new Vector3(halfWidth, halfHeight, Bounds.Extents.Z)
            ),
            Depth + 1,
            _maxDepth,
            _maxElements
        );

        // 左下 (-X, -Y)
        _children[(int)Quadrant.BottomLeft] = new Quadtree<T>(
            new Bounds(
                new Vector3(center.X - halfWidth, center.Y - halfHeight, center.Z),
                new Vector3(halfWidth, halfHeight, Bounds.Extents.Z)
            ),
            Depth + 1,
            _maxDepth,
            _maxElements
        );

        // 右下 (+X, -Y)
        _children[(int)Quadrant.BottomRight] = new Quadtree<T>(
            new Bounds(
                new Vector3(center.X + halfWidth, center.Y - halfHeight, center.Z),
                new Vector3(halfWidth, halfHeight, Bounds.Extents.Z)
            ),
            Depth + 1,
            _maxDepth,
            _maxElements
        );

        // 将当前节点的元素重新分配到子节点
        foreach (var (pos, val) in _elements)
        {
            var quadrant = GetQuadrant(pos);
            _children[(int)quadrant]!.Insert(pos, val);
        }

        _elements.Clear();
    }

    /// <summary>获取点所在的象限</summary>
    private Quadrant GetQuadrant(Vector3 position)
    {
        bool isLeft = position.X < Bounds.Center.X;
        bool isTop = position.Y > Bounds.Center.Y;

        if (isTop && isLeft) return Quadrant.TopLeft;
        if (isTop && !isLeft) return Quadrant.TopRight;
        if (!isTop && isLeft) return Quadrant.BottomLeft;
        return Quadrant.BottomRight;
    }

    /// <summary>范围查询</summary>
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
        foreach (var (pos, val) in _elements)
        {
            if (range.Contains(pos))
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

    /// <summary>最近邻搜索</summary>
    public (T? Value, float Distance, bool Found) FindNearest(Vector3 position, float maxSearchRadius = float.MaxValue)
    {
        T? best = default;
        float bestDist = maxSearchRadius;
        bool found = false;
        FindNearestInternal(position, ref best, ref bestDist, ref found);
        return (best, bestDist, found);
    }

    private void FindNearestInternal(Vector3 position, ref T? best, ref float bestDist, ref bool found)
    {
        // 检查此节点不可能有更近的点，剪枝
        float distToBounds = DistanceToBounds(position);
        if (distToBounds >= bestDist)
        {
            return;
        }

        // 检查当前节点的元素
        foreach (var (pos, val) in _elements)
        {
            float dist = Vector3.Distance(pos, position);
            if (dist < bestDist)
            {
                bestDist = dist;
                best = val;
                found = true;
            }
        }

        // 递归查询子节点（按距离排序，优先查询更近的）
        if (_children != null)
        {
            // 按距离排序子节点中心排序
            var sortedChildren = _children
                .Where(c => c != null)
                .Select(c => new { Child = c, Dist = Vector3.Distance(c!.Bounds.Center, position) })
                .OrderBy(x => x.Dist);

            foreach (var item in sortedChildren)
            {
                item.Child!.FindNearestInternal(position, ref best, ref bestDist, ref found);
            }
        }
    }

    /// <summary>计算点到边界的最近距离</summary>
    private float DistanceToBounds(Vector3 position)
    {
        float dx = MathF.Max(0, MathF.Max(Bounds.Min.X - position.X, position.X - Bounds.Max.X));
        float dy = MathF.Max(0, MathF.Max(Bounds.Min.Y - position.Y, position.Y - Bounds.Max.Y));
        float dz = MathF.Max(0, MathF.Max(Bounds.Min.Z - position.Z, position.Z - Bounds.Max.Z));
        return MathF.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    /// <summary>获取所有叶子节点（用于可视化）</summary>
    public List<Quadtree<T>> GetAllLeafNodes()
    {
        var leaves = new List<Quadtree<T>>();
        GetAllLeafNodesInternal(leaves);
        return leaves;
    }

    private void GetAllLeafNodesInternal(List<Quadtree<T>> leaves)
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

    /// <summary>清空四叉树</summary>
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
