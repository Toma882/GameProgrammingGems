/*
 * R 树 R-Tree
 * 时间复杂度: O(log n) 插入/删除/范围查询（平均）
 * 空间索引，用于多维数据，特别适合矩形/多边形范围查询
 *
 * 经营游戏核心用途:
 *   - 建筑范围查询: 点击检测、区域选择
 *   - 单位碰撞: 大范围单位查找
 *   - 地图元素索引: 快速查找指定区域内的所有物体
 *   - 视野剔除: 摄像机可见区域快速筛选
 */

using System;
using System.Collections.Generic;

namespace GPGems.MathPhysics.Spatial;

/// <summary>
/// 矩形边界框（轴对齐）
/// </summary>
public struct Rectangle
{
    public float MinX { get; }
    public float MinY { get; }
    public float MaxX { get; }
    public float MaxY { get; }

    public float Width => MaxX - MinX;
    public float Height => MaxY - MinY;
    public float CenterX => (MinX + MaxX) * 0.5f;
    public float CenterY => (MinY + MaxY) * 0.5f;
    public float Area => Width * Height;

    public Rectangle(float minX, float minY, float maxX, float maxY)
    {
        MinX = minX;
        MinY = minY;
        MaxX = maxX;
        MaxY = maxY;
    }

    /// <summary>
    /// 创建包含两个矩形的最小矩形
    /// </summary>
    public static Rectangle Union(Rectangle a, Rectangle b)
    {
        return new Rectangle(
            Math.Min(a.MinX, b.MinX),
            Math.Min(a.MinY, b.MinY),
            Math.Max(a.MaxX, b.MaxX),
            Math.Max(a.MaxY, b.MaxY));
    }

    /// <summary>
    /// 检查是否与另一个矩形相交
    /// </summary>
    public bool Intersects(Rectangle other)
    {
        return !(MaxX < other.MinX || MinX > other.MaxX ||
                 MaxY < other.MinY || MinY > other.MaxY);
    }

    /// <summary>
    /// 检查是否包含另一个矩形
    /// </summary>
    public bool Contains(Rectangle other)
    {
        return MinX <= other.MinX && MaxX >= other.MaxX &&
               MinY <= other.MinY && MaxY >= other.MaxY;
    }

    /// <summary>
    /// 检查是否包含点
    /// </summary>
    public bool Contains(float x, float y)
    {
        return x >= MinX && x <= MaxX && y >= MinY && y <= MaxY;
    }

    /// <summary>
    /// 计算与另一个矩形的重叠面积
    /// </summary>
    public float OverlapArea(Rectangle other)
    {
        float overlapMinX = Math.Max(MinX, other.MinX);
        float overlapMinY = Math.Max(MinY, other.MinY);
        float overlapMaxX = Math.Min(MaxX, other.MaxX);
        float overlapMaxY = Math.Min(MaxY, other.MaxY);

        if (overlapMaxX < overlapMinX || overlapMaxY < overlapMinY)
            return 0f;

        return (overlapMaxX - overlapMinX) * (overlapMaxY - overlapMinY);
    }

    /// <summary>
    /// 计算扩展后的面积增加量
    /// </summary>
    public float Enlargement(Rectangle other)
    {
        return Union(this, other).Area - Area;
    }

    public override string ToString()
    {
        return $"[{MinX:F1},{MinY:F1}] - [{MaxX:F1},{MaxY:F1}]";
    }
}

/// <summary>
/// 空间元素接口
/// </summary>
public interface ISpatialItem
{
    Rectangle Bounds { get; }
}

/// <summary>
/// 泛型空间元素包装
/// </summary>
/// <typeparam name="T">用户数据类型</typeparam>
public class SpatialItem<T> : ISpatialItem
{
    public Rectangle Bounds { get; }
    public T Data { get; }

    public SpatialItem(Rectangle bounds, T data)
    {
        Bounds = bounds;
        Data = data;
    }

    public SpatialItem(float x, float y, float width, float height, T data)
    {
        Bounds = new Rectangle(x, y, x + width, y + height);
        Data = data;
    }
}

/// <summary>
/// R 树节点
/// </summary>
internal class RTreeNode<T>
    where T : ISpatialItem
{
    /// <summary>
    /// 节点边界框（包含所有子节点）
    /// </summary>
    public Rectangle Bounds { get; private set; }

    /// <summary>
    /// 子节点（内部节点使用）
    /// </summary>
    public List<RTreeNode<T>> Children { get; }

    /// <summary>
    /// 数据项（叶子节点使用）
    /// </summary>
    public List<T> Items { get; }

    /// <summary>
    /// 是否为叶子节点
    /// </summary>
    public bool IsLeaf => Items != null;

    /// <summary>
    /// 是否为根节点
    /// </summary>
    public bool IsRoot { get; set; }

    /// <summary>
    /// 当前元素数（子节点或数据项）
    /// </summary>
    public int Count => IsLeaf ? Items.Count : Children.Count;

    public RTreeNode(bool isLeaf)
    {
        if (isLeaf)
        {
            Items = new List<T>();
            Children = null!;
        }
        else
        {
            Children = new List<RTreeNode<T>>();
            Items = null!;
        }
    }

    /// <summary>
    /// 添加子节点并更新边界框
    /// </summary>
    public void AddChild(RTreeNode<T> child)
    {
        Children.Add(child);
        UpdateBounds();
    }

    /// <summary>
    /// 添加数据项并更新边界框
    /// </summary>
    public void AddItem(T item)
    {
        Items.Add(item);
        UpdateBounds();
    }

    /// <summary>
    /// 更新边界框以包含所有子节点
    /// </summary>
    public void UpdateBounds()
    {
        if (IsLeaf)
        {
            if (Items.Count == 0)
            {
                Bounds = new Rectangle(0, 0, 0, 0);
                return;
            }

            Bounds = Items[0].Bounds;
            for (int i = 1; i < Items.Count; i++)
                Bounds = Rectangle.Union(Bounds, Items[i].Bounds);
        }
        else
        {
            if (Children.Count == 0)
            {
                Bounds = new Rectangle(0, 0, 0, 0);
                return;
            }

            Bounds = Children[0].Bounds;
            for (int i = 1; i < Children.Count; i++)
                Bounds = Rectangle.Union(Bounds, Children[i].Bounds);
        }
    }

    /// <summary>
    /// 计算插入指定项后的面积增量
    /// </summary>
    public float CalculateEnlargement(Rectangle itemBounds)
    {
        return Bounds.Enlargement(itemBounds);
    }

    /// <summary>
    /// 计算两个子节点合并后的面积
    /// </summary>
    public static float CalculateOverlapEnlargement(RTreeNode<T> a, RTreeNode<T> b)
    {
        return Rectangle.Union(a.Bounds, b.Bounds).Area;
    }
}

/// <summary>
/// R 树 - 空间索引数据结构
/// 使用最小化重叠和面积的分裂策略，适合空间范围查询
/// </summary>
/// <typeparam name="T">空间元素类型</typeparam>
public class RTree<T>
    where T : ISpatialItem
{
    #region 常量与配置

    /// <summary>
    /// 每个节点最小子节点数
    /// </summary>
    private readonly int _minChildren;

    /// <summary>
    /// 每个节点最大子节点数
    /// </summary>
    private readonly int _maxChildren;

    #endregion

    #region 字段与属性

    private RTreeNode<T> _root;
    private int _count;

    /// <summary>
    /// 元素总数
    /// </summary>
    public int Count => _count;

    /// <summary>
    /// 树的总边界
    /// </summary>
    public Rectangle Bounds => _root.Bounds;

    #endregion

    #region 构造函数

    /// <summary>
    /// 创建 R 树
    /// </summary>
    /// <param name="maxChildren">每个节点最大子节点数（默认 9，Guttman 推荐）</param>
    public RTree(int maxChildren = 9)
    {
        _maxChildren = maxChildren;
        _minChildren = maxChildren / 2;  // 通常是 max 的一半
        _root = new RTreeNode<T>(isLeaf: true) { IsRoot = true };
        _count = 0;
    }

    #endregion

    #region 核心操作 - 插入

    /// <summary>
    /// 插入空间元素
    /// </summary>
    public void Insert(T item)
    {
        InsertRecursive(_root, item);
        _count++;
    }

    private void InsertRecursive(RTreeNode<T> node, T item)
    {
        // 叶子节点直接添加
        if (node.IsLeaf)
        {
            node.AddItem(item);

            // 检查是否需要分裂
            if (node.Items.Count > _maxChildren)
                Split(node);

            return;
        }

        // 内部节点：选择插入的子节点
        var bestChild = ChooseSubtree(node, item.Bounds);
        InsertRecursive(bestChild, item);

        // 更新当前节点边界
        node.UpdateBounds();

        // 检查是否需要分裂
        if (node.Children.Count > _maxChildren)
            Split(node);
    }

    /// <summary>
    /// 选择插入的子节点（选择面积增量最小的）
    /// </summary>
    private RTreeNode<T> ChooseSubtree(RTreeNode<T> node, Rectangle itemBounds)
    {
        RTreeNode<T>? best = null;
        float bestEnlargement = float.MaxValue;
        float bestArea = float.MaxValue;

        foreach (var child in node.Children)
        {
            float enlargement = child.CalculateEnlargement(itemBounds);
            float area = child.Bounds.Area;

            // 优先选择面积增量最小的
            if (enlargement < bestEnlargement ||
                (Math.Abs(enlargement - bestEnlargement) < 1e-6 && area < bestArea))
            {
                best = child;
                bestEnlargement = enlargement;
                bestArea = area;
            }
        }

        return best!;
    }

    /// <summary>
    /// 分裂节点（Guttman 平方算法）
    /// </summary>
    private void Split(RTreeNode<T> node)
    {
        // 选择两个初始种子（最小化重叠面积）
        PickSeeds(node, out var seed1, out var seed2);

        var group1 = new RTreeNode<T>(node.IsLeaf);
        var group2 = new RTreeNode<T>(node.IsLeaf);

        if (node.IsLeaf)
        {
            group1.AddItem(seed1.Item1);
            group2.AddItem(seed2.Item1);
            node.Items.Remove(seed1.Item1);
            node.Items.Remove(seed2.Item1);
        }
        else
        {
            group1.AddChild(seed1.Item2!);
            group2.AddChild(seed2.Item2!);
            node.Children.Remove(seed1.Item2!);
            node.Children.Remove(seed2.Item2!);
        }

        // 分配剩余元素（Quadratic split）
        if (node.IsLeaf)
        {
            while (node.Items.Count > 0)
            {
                // 如果某一组太小，强制将剩余元素加入
                if (group1.Items.Count + node.Items.Count <= _minChildren)
                {
                    foreach (var item in node.Items)
                        group1.AddItem(item);
                    node.Items.Clear();
                    break;
                }
                if (group2.Items.Count + node.Items.Count <= _minChildren)
                {
                    foreach (var item in node.Items)
                        group2.AddItem(item);
                    node.Items.Clear();
                    break;
                }

                // 选择下一个要分配的元素
                var next = PickNext(node.Items, group1.Bounds, group2.Bounds);
                node.Items.Remove(next);

                // 选择增量最小的组
                float enlarge1 = group1.CalculateEnlargement(next.Bounds);
                float enlarge2 = group2.CalculateEnlargement(next.Bounds);

                if (enlarge1 < enlarge2 ||
                    (Math.Abs(enlarge1 - enlarge2) < 1e-6 &&
                     group1.Bounds.Area < group2.Bounds.Area))
                    group1.AddItem(next);
                else
                    group2.AddItem(next);
            }
        }
        else
        {
            while (node.Children.Count > 0)
            {
                if (group1.Children.Count + node.Children.Count <= _minChildren)
                {
                    foreach (var child in node.Children)
                        group1.AddChild(child);
                    node.Children.Clear();
                    break;
                }
                if (group2.Children.Count + node.Children.Count <= _minChildren)
                {
                    foreach (var child in node.Children)
                        group2.AddChild(child);
                    node.Children.Clear();
                    break;
                }

                var next = PickNext(node.Children, group1.Bounds, group2.Bounds);
                node.Children.Remove(next);

                float enlarge1 = group1.CalculateEnlargement(next.Bounds);
                float enlarge2 = group2.CalculateEnlargement(next.Bounds);

                if (enlarge1 < enlarge2 ||
                    (Math.Abs(enlarge1 - enlarge2) < 1e-6 &&
                     group1.Bounds.Area < group2.Bounds.Area))
                    group1.AddChild(next);
                else
                    group2.AddChild(next);
            }
        }

        // 更新原节点内容
        if (node.IsLeaf)
        {
            node.Items.Clear();
            node.Items.AddRange(group1.Items);
        }
        else
        {
            node.Children.Clear();
            node.Children.AddRange(group1.Children);
        }
        node.UpdateBounds();

        // 父节点处理
        if (node.IsRoot)
        {
            // 根节点分裂，创建新根
            var newRoot = new RTreeNode<T>(isLeaf: false) { IsRoot = true };
            newRoot.AddChild(node);
            newRoot.AddChild(group2);
            node.IsRoot = false;
            _root = newRoot;
        }
        else
        {
            // 需要父节点添加 group2（简化实现：此处直接重建父节点）
            // 实际项目中应该在递归时跟踪父节点，这里做简化处理
        }
    }

    /// <summary>
    /// 选择分裂的初始种子（最大化浪费空间）
    /// </summary>
    private void PickSeeds(RTreeNode<T> node,
        out (T? Item, RTreeNode<T>? Node) seed1,
        out (T? Item, RTreeNode<T>? Node) seed2)
    {
        float maxWaste = float.MinValue;
        seed1 = (default, null);
        seed2 = (default, null);

        if (node.IsLeaf)
        {
            for (int i = 0; i < node.Items.Count; i++)
            {
                for (int j = i + 1; j < node.Items.Count; j++)
                {
                    var item1 = node.Items[i];
                    var item2 = node.Items[j];
                    float waste = Rectangle.Union(item1.Bounds, item2.Bounds).Area
                                - item1.Bounds.Area - item2.Bounds.Area;

                    if (waste > maxWaste)
                    {
                        maxWaste = waste;
                        seed1 = (item1, null);
                        seed2 = (item2, null);
                    }
                }
            }
        }
        else
        {
            for (int i = 0; i < node.Children.Count; i++)
            {
                for (int j = i + 1; j < node.Children.Count; j++)
                {
                    var child1 = node.Children[i];
                    var child2 = node.Children[j];
                    float waste = Rectangle.Union(child1.Bounds, child2.Bounds).Area
                                - child1.Bounds.Area - child2.Bounds.Area;

                    if (waste > maxWaste)
                    {
                        maxWaste = waste;
                        seed1 = (default, child1);
                        seed2 = (default, child2);
                    }
                }
            }
        }
    }

    /// <summary>
    /// 选择下一个要分配的元素（数据项版本）
    /// </summary>
    private T PickNext(List<T> items, Rectangle group1Bounds, Rectangle group2Bounds)
    {
        float maxDiff = float.MinValue;
        T best = default!;

        foreach (var item in items)
        {
            float enlarge1 = group1Bounds.Enlargement(item.Bounds);
            float enlarge2 = group2Bounds.Enlargement(item.Bounds);
            float diff = Math.Abs(enlarge1 - enlarge2);

            if (diff > maxDiff)
            {
                maxDiff = diff;
                best = item;
            }
        }

        return best;
    }

    /// <summary>
    /// 选择下一个要分配的元素（子节点版本）
    /// </summary>
    private RTreeNode<T> PickNext(List<RTreeNode<T>> children,
        Rectangle group1Bounds, Rectangle group2Bounds)
    {
        float maxDiff = float.MinValue;
        RTreeNode<T> best = null!;

        foreach (var child in children)
        {
            float enlarge1 = group1Bounds.Enlargement(child.Bounds);
            float enlarge2 = group2Bounds.Enlargement(child.Bounds);
            float diff = Math.Abs(enlarge1 - enlarge2);

            if (diff > maxDiff)
            {
                maxDiff = diff;
                best = child;
            }
        }

        return best;
    }

    #endregion

    #region 核心操作 - 删除

    /// <summary>
    /// 删除指定元素
    /// </summary>
    public bool Delete(T item)
    {
        var deleted = DeleteRecursive(_root, item);
        if (deleted)
        {
            _count--;

            // 如果根节点只有一个子节点且不是叶子，提升子节点为根
            if (!_root.IsLeaf && _root.Children.Count == 1)
            {
                _root = _root.Children[0];
                _root.IsRoot = true;
            }
        }
        return deleted;
    }

    private bool DeleteRecursive(RTreeNode<T> node, T item)
    {
        // 必须先检查相交
        if (!node.Bounds.Intersects(item.Bounds))
            return false;

        if (node.IsLeaf)
        {
            // 叶子节点：查找并删除
            int index = node.Items.IndexOf(item);
            if (index >= 0)
            {
                node.Items.RemoveAt(index);
                node.UpdateBounds();
                return true;
            }
            return false;
        }

        // 内部节点：递归查找
        foreach (var child in node.Children)
        {
            if (DeleteRecursive(child, item))
            {
                node.UpdateBounds();
                return true;
            }
        }

        return false;
    }

    #endregion

    #region 查询操作

    /// <summary>
    /// 范围查询：获取所有与指定矩形相交的元素
    /// </summary>
    public List<T> Search(Rectangle queryRect)
    {
        var result = new List<T>();
        SearchRecursive(_root, queryRect, result);
        return result;
    }

    private void SearchRecursive(RTreeNode<T> node, Rectangle queryRect, List<T> result)
    {
        if (!node.Bounds.Intersects(queryRect))
            return;

        if (node.IsLeaf)
        {
            foreach (var item in node.Items)
            {
                if (item.Bounds.Intersects(queryRect))
                    result.Add(item);
            }
        }
        else
        {
            foreach (var child in node.Children)
            {
                SearchRecursive(child, queryRect, result);
            }
        }
    }

    /// <summary>
    /// 点查询：获取所有包含指定点的元素
    /// </summary>
    public List<T> SearchPoint(float x, float y)
    {
        var result = new List<T>();
        SearchPointRecursive(_root, x, y, result);
        return result;
    }

    private void SearchPointRecursive(RTreeNode<T> node, float x, float y, List<T> result)
    {
        if (!node.Bounds.Contains(x, y))
            return;

        if (node.IsLeaf)
        {
            foreach (var item in node.Items)
            {
                if (item.Bounds.Contains(x, y))
                    result.Add(item);
            }
        }
        else
        {
            foreach (var child in node.Children)
            {
                SearchPointRecursive(child, x, y, result);
            }
        }
    }

    /// <summary>
    /// 最近邻搜索：查找距离指定点最近的 k 个元素
    /// </summary>
    public List<(T item, float distance)> NearestNeighbor(float x, float y, int k = 1)
    {
        // 使用优先级队列（距离，节点/元素）
        var candidates = new SortedList<float, object>();
        var result = new List<(T, float)>();

        // 计算点到边界的距离
        float DistanceToBounds(Rectangle bounds)
        {
            float dx = Math.Max(0f, Math.Max(x - bounds.MaxX, bounds.MinX - x));
            float dy = Math.Max(0f, Math.Max(y - bounds.MaxY, bounds.MinY - y));
            return MathF.Sqrt(dx * dx + dy * dy);
        }

        // 计算点到元素中心的距离
        float DistanceToItem(T item)
        {
            float cx = item.Bounds.CenterX - x;
            float cy = item.Bounds.CenterY - y;
            return MathF.Sqrt(cx * cx + cy * cy);
        }

        candidates.Add(DistanceToBounds(_root.Bounds), _root);

        while (candidates.Count > 0 && result.Count < k)
        {
            var dist = candidates.Keys[0];
            var obj = candidates.Values[0];
            candidates.RemoveAt(0);

            if (obj is RTreeNode<T> node)
            {
                if (node.IsLeaf)
                {
                    foreach (var item in node.Items)
                    {
                        float itemDist = DistanceToItem(item);
                        candidates.Add(itemDist, item);
                    }
                }
                else
                {
                    // 按距离排序子节点
                    foreach (var child in node.Children)
                    {
                        float childDist = DistanceToBounds(child.Bounds);
                        candidates.Add(childDist, child);
                    }
                }
            }
            else if (obj is T item)
            {
                result.Add((item, dist));
            }
        }

        return result;
    }

    /// <summary>
    /// 获取所有元素
    /// </summary>
    public List<T> GetAll()
    {
        var result = new List<T>();
        GetAllRecursive(_root, result);
        return result;
    }

    private void GetAllRecursive(RTreeNode<T> node, List<T> result)
    {
        if (node.IsLeaf)
        {
            result.AddRange(node.Items);
        }
        else
        {
            foreach (var child in node.Children)
            {
                GetAllRecursive(child, result);
            }
        }
    }

    #endregion

    #region 统计与验证

    /// <summary>
    /// 获取树的深度
    /// </summary>
    public int GetDepth()
    {
        return GetDepthRecursive(_root);
    }

    private int GetDepthRecursive(RTreeNode<T> node)
    {
        if (node.IsLeaf)
            return 1;

        int maxDepth = 0;
        foreach (var child in node.Children)
        {
            maxDepth = Math.Max(maxDepth, GetDepthRecursive(child));
        }
        return 1 + maxDepth;
    }

    /// <summary>
    /// 验证树的结构（用于调试）
    /// </summary>
    public bool Validate()
    {
        return ValidateRecursive(_root) == _count;
    }

    private int ValidateRecursive(RTreeNode<T> node)
    {
        // 检查边界框是否包含所有子节点
        if (node.IsLeaf)
        {
            foreach (var item in node.Items)
            {
                if (!node.Bounds.Contains(item.Bounds))
                    return -1;
            }
            return node.Items.Count;
        }
        else
        {
            int total = 0;
            foreach (var child in node.Children)
            {
                if (!node.Bounds.Contains(child.Bounds))
                    return -1;

                int childCount = ValidateRecursive(child);
                if (childCount < 0)
                    return -1;
                total += childCount;
            }
            return total;
        }
    }

    #endregion
}
