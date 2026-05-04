/*
 * 四叉树 Quad Tree
 * 时间复杂度: O(log n) 插入/查询, O(n) 构建
 * 空间复杂度: 递归分区存储
 *
 * 经营游戏核心用途:
 *   - 单位碰撞检测: 千级单位范围查询
 *   - 视野剔除: 摄像机可见区域对象快速获取
 *   - 建筑布局检测: 重叠/距离检查
 *   - 寻路区域优化: 分块路径查询
 */

using System;
using System.Collections.Generic;

namespace GPGems.MathPhysics.Spatial;

/// <summary>
/// 矩形边界
/// </summary>
public struct Rect
{
    public float X;
    public float Y;
    public float Width;
    public float Height;

    public float Right => X + Width;
    public float Bottom => Y + Height;
    public float CenterX => X + Width * 0.5f;
    public float CenterY => Y + Height * 0.5f;

    public Rect(float x, float y, float width, float height)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    /// <summary>
    /// 检查是否包含点
    /// </summary>
    public bool Contains(float px, float py)
    {
        return px >= X && px <= Right && py >= Y && py <= Bottom;
    }

    /// <summary>
    /// 检查是否包含另一个矩形
    /// </summary>
    public bool Contains(in Rect other)
    {
        return other.X >= X && other.Right <= Right &&
               other.Y >= Y && other.Bottom <= Bottom;
    }

    /// <summary>
    /// 检查是否与另一个矩形相交
    /// </summary>
    public bool Intersects(in Rect other)
    {
        return !(other.Right < X || other.X > Right ||
                 other.Bottom < Y || other.Y > Bottom);
    }

    /// <summary>
    /// 检查是否与圆形相交
    /// </summary>
    public bool IntersectsCircle(float cx, float cy, float radius)
    {
        // 找到矩形上离圆心最近的点
        float closestX = Math.Clamp(cx, X, Right);
        float closestY = Math.Clamp(cy, Y, Bottom);
        float dx = cx - closestX;
        float dy = cy - closestY;
        return dx * dx + dy * dy <= radius * radius;
    }
}

/// <summary>
/// 四叉树元素接口
/// </summary>
public interface IQuadTreeElement
{
    float X { get; }
    float Y { get; }
    Rect Bounds { get; }
}

/// <summary>
/// 四叉树 - 2D 空间索引
/// 递归将空间分为四象限，优化范围查询
/// </summary>
/// <typeparam name="T">元素类型，必须实现 IQuadTreeElement</typeparam>
public class QuadTree<T> where T : class, IQuadTreeElement
{
    #region 常量

    /// <summary>每个节点最大元素数（超过则分裂）</summary>
    private const int DefaultMaxElements = 8;

    /// <summary>最大深度</summary>
    private const int DefaultMaxDepth = 8;

    #endregion

    #region 字段与属性

    private readonly Rect _bounds;
    private readonly List<T> _elements;
    private readonly int _maxElements;
    private readonly int _maxDepth;
    private readonly int _currentDepth;

    /// <summary>四个子节点: 左上(0), 右上(1), 左下(2), 右下(3)</summary>
    private QuadTree<T>?[]? _children;

    public bool IsLeaf => _children == null;
    public int Count => CountElements();
    public Rect Bounds => _bounds;

    #endregion

    #region 构造函数

    public QuadTree(Rect bounds)
        : this(bounds, DefaultMaxElements, DefaultMaxDepth, 0)
    {
    }

    public QuadTree(Rect bounds, int maxElements, int maxDepth)
        : this(bounds, maxElements, maxDepth, 0)
    {
    }

    private QuadTree(Rect bounds, int maxElements, int maxDepth, int currentDepth)
    {
        _bounds = bounds;
        _maxElements = maxElements;
        _maxDepth = maxDepth;
        _currentDepth = currentDepth;
        _elements = new List<T>(maxElements);
    }

    #endregion

    #region 核心操作

    /// <summary>
    /// 插入元素
    /// </summary>
    public bool Insert(T element)
    {
        if (element == null)
            throw new ArgumentNullException(nameof(element));

        // 元素不在当前节点范围内
        if (!_bounds.Intersects(element.Bounds))
            return false;

        // 未达上限且是叶子节点，直接添加
        if (_elements.Count < _maxElements || _currentDepth >= _maxDepth)
        {
            _elements.Add(element);
            return true;
        }

        // 需要分裂
        if (IsLeaf)
        {
            Split();
        }

        // 尝试插入子节点
        bool inserted = false;
        for (int i = 0; i < 4; i++)
        {
            if (_children![i]!.Insert(element))
            {
                inserted = true;
            }
        }

        // 如果任何子节点都不能完全包含，留在父节点
        if (!inserted)
        {
            _elements.Add(element);
        }

        return true;
    }

    /// <summary>
    /// 批量插入
    /// </summary>
    public void InsertRange(IEnumerable<T> elements)
    {
        foreach (var element in elements)
        {
            Insert(element);
        }
    }

    /// <summary>
    /// 移除元素
    /// </summary>
    public bool Remove(T element)
    {
        if (element == null)
            throw new ArgumentNullException(nameof(element));

        if (!_bounds.Intersects(element.Bounds))
            return false;

        // 尝试从当前节点移除
        if (_elements.Remove(element))
        {
            return true;
        }

        // 尝试从子节点移除
        if (!IsLeaf)
        {
            for (int i = 0; i < 4; i++)
            {
                if (_children![i]!.Remove(element))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// 清空树
    /// </summary>
    public void Clear()
    {
        _elements.Clear();
        if (!IsLeaf)
        {
            for (int i = 0; i < 4; i++)
            {
                _children![i]!.Clear();
                _children[i] = null;
            }
            _children = null;
        }
    }

    #endregion

    #region 分裂操作

    /// <summary>
    /// 分裂为四个子节点
    /// </summary>
    private void Split()
    {
        float halfWidth = _bounds.Width * 0.5f;
        float halfHeight = _bounds.Height * 0.5f;
        float midX = _bounds.X + halfWidth;
        float midY = _bounds.Y + halfHeight;
        int nextDepth = _currentDepth + 1;

        _children = new QuadTree<T>[4];

        // 左上
        _children[0] = new QuadTree<T>(
            new Rect(_bounds.X, _bounds.Y, halfWidth, halfHeight),
            _maxElements, _maxDepth, nextDepth);

        // 右上
        _children[1] = new QuadTree<T>(
            new Rect(midX, _bounds.Y, halfWidth, halfHeight),
            _maxElements, _maxDepth, nextDepth);

        // 左下
        _children[2] = new QuadTree<T>(
            new Rect(_bounds.X, midY, halfWidth, halfHeight),
            _maxElements, _maxDepth, nextDepth);

        // 右下
        _children[3] = new QuadTree<T>(
            new Rect(midX, midY, halfWidth, halfHeight),
            _maxElements, _maxDepth, nextDepth);

        // 将当前元素重新分配给子节点
        var remaining = new List<T>();
        foreach (var element in _elements)
        {
            bool assigned = false;
            for (int i = 0; i < 4; i++)
            {
                if (_children[i]!._bounds.Contains(element.Bounds))
                {
                    _children[i]!._elements.Add(element);
                    assigned = true;
                    break;
                }
            }
            if (!assigned)
            {
                remaining.Add(element);
            }
        }
        _elements.Clear();
        _elements.AddRange(remaining);
    }

    #endregion

    #region 查询操作

    /// <summary>
    /// 查询指定范围内的所有元素
    /// </summary>
    public List<T> Query(Rect range)
    {
        var result = new List<T>();
        QueryInternal(range, result);
        return result;
    }

    /// <summary>
    /// 查询指定圆形范围内的所有元素
    /// </summary>
    public List<T> QueryCircle(float cx, float cy, float radius)
    {
        var result = new List<T>();
        QueryCircleInternal(cx, cy, radius, result);
        return result;
    }

    /// <summary>
    /// 查询指定点附近的所有元素
    /// </summary>
    public List<T> QueryPoint(float x, float y)
    {
        var result = new List<T>();
        QueryPointInternal(x, y, result);
        return result;
    }

    private void QueryInternal(Rect range, List<T> result)
    {
        if (!_bounds.Intersects(range))
            return;

        // 添加当前节点中与范围相交的元素
        foreach (var element in _elements)
        {
            if (range.Intersects(element.Bounds))
            {
                result.Add(element);
            }
        }

        // 递归查询子节点
        if (!IsLeaf)
        {
            for (int i = 0; i < 4; i++)
            {
                _children![i]!.QueryInternal(range, result);
            }
        }
    }

    private void QueryCircleInternal(float cx, float cy, float radius, List<T> result)
    {
        // 快速排除：圆形与当前节点边界不相交
        if (!_bounds.IntersectsCircle(cx, cy, radius))
            return;

        // 添加当前节点中与圆形相交的元素
        foreach (var element in _elements)
        {
            float dx = element.X - cx;
            float dy = element.Y - cy;
            if (dx * dx + dy * dy <= radius * radius)
            {
                result.Add(element);
            }
        }

        // 递归查询子节点
        if (!IsLeaf)
        {
            for (int i = 0; i < 4; i++)
            {
                _children![i]!.QueryCircleInternal(cx, cy, radius, result);
            }
        }
    }

    private void QueryPointInternal(float x, float y, List<T> result)
    {
        if (!_bounds.Contains(x, y))
            return;

        // 添加当前节点中包含该点的元素
        foreach (var element in _elements)
        {
            if (element.Bounds.Contains(x, y))
            {
                result.Add(element);
            }
        }

        // 递归查询子节点
        if (!IsLeaf)
        {
            for (int i = 0; i < 4; i++)
            {
                _children![i]!.QueryPointInternal(x, y, result);
            }
        }
    }

    #endregion

    #region 最近邻查询

    /// <summary>
    /// 查找离指定点最近的元素
    /// </summary>
    public T? FindNearest(float x, float y, float maxSearchRadius = float.MaxValue)
    {
        float bestDistSq = maxSearchRadius * maxSearchRadius;
        T? best = null;
        FindNearestInternal(x, y, ref bestDistSq, ref best);
        return best;
    }

    private void FindNearestInternal(float x, float y, ref float bestDistSq, ref T? best)
    {
        // 检查当前节点的元素
        foreach (var element in _elements)
        {
            float dx = element.X - x;
            float dy = element.Y - y;
            float distSq = dx * dx + dy * dy;
            if (distSq < bestDistSq)
            {
                bestDistSq = distSq;
                best = element;
            }
        }

        if (!IsLeaf)
        {
            // 按距离排序子节点，优先查询更近的
            var searchOrder = new (int index, float distSq)[4];
            for (int i = 0; i < 4; i++)
            {
                var child = _children![i]!;
                float dx = child._bounds.CenterX - x;
                float dy = child._bounds.CenterY - y;
                searchOrder[i] = (i, dx * dx + dy * dy);
            }
            Array.Sort(searchOrder, (a, b) => a.distSq.CompareTo(b.distSq));

            // 只查询可能包含更近元素的子节点
            foreach (var (index, _) in searchOrder)
            {
                var child = _children![index]!;
                // 计算点到子节点边界的最小距离
                float closestX = Math.Clamp(x, child._bounds.X, child._bounds.Right);
                float closestY = Math.Clamp(y, child._bounds.Y, child._bounds.Bottom);
                float minDistX = x - closestX;
                float minDistY = y - closestY;
                float minDistSq = minDistX * minDistX + minDistY * minDistY;

                if (minDistSq < bestDistSq)
                {
                    child.FindNearestInternal(x, y, ref bestDistSq, ref best);
                }
            }
        }
    }

    #endregion

    #region 辅助方法

    /// <summary>
    /// 统计所有元素数量
    /// </summary>
    private int CountElements()
    {
        int count = _elements.Count;
        if (!IsLeaf)
        {
            for (int i = 0; i < 4; i++)
            {
                count += _children![i]!.Count;
            }
        }
        return count;
    }

    /// <summary>
    /// 获取树的深度
    /// </summary>
    public int GetMaxDepth()
    {
        if (IsLeaf)
            return _currentDepth;

        int max = 0;
        for (int i = 0; i < 4; i++)
        {
            max = Math.Max(max, _children![i]!.GetMaxDepth());
        }
        return max;
    }

    #endregion

}
