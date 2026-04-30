/*
 * 红黑树 Red-Black Tree
 * 时间复杂度: O(log n) 插入/删除/查找
 * 自平衡二叉搜索树，保证最坏情况 O(log n)
 *
 * 经营游戏核心用途:
 *   - 有序排行榜: 玩家分数/财富排名
 *   - 事件调度器: 按时间排序的延迟事件
 *   - 物品稀有度: 稀有物品排序查询
 *   - 任务系统: 按优先级排序任务队列
 */

using System;
using System.Collections;
using System.Collections.Generic;

namespace GPGems.Core.DataStructures;

/// <summary>
/// 节点颜色
/// </summary>
public enum NodeColor
{
    Red,
    Black
}

/// <summary>
/// 红黑树节点
/// </summary>
internal class RedBlackNode<TKey, TValue>
    where TKey : IComparable<TKey>
{
    public TKey Key { get; set; }
    public TValue Value { get; set; }
    public NodeColor Color { get; set; }
    public RedBlackNode<TKey, TValue>? Left { get; set; }
    public RedBlackNode<TKey, TValue>? Right { get; set; }
    public RedBlackNode<TKey, TValue>? Parent { get; set; }

    public RedBlackNode(TKey key, TValue value)
    {
        Key = key;
        Value = value;
        Color = NodeColor.Red;  // 新节点默认为红色
    }
}

/// <summary>
/// 红黑树 - 自平衡二叉搜索树
/// 5 条规则:
/// 1. 每个节点是红色或黑色
/// 2. 根节点是黑色
/// 3. 所有叶子节点(NIL)是黑色
/// 4. 如果一个节点是红色，则它的两个子节点都是黑色
/// 5. 从任一节点到其每个叶子的所有路径都包含相同数目的黑色节点
/// </summary>
/// <typeparam name="TKey">键类型</typeparam>
/// <typeparam name="TValue">值类型</typeparam>
public class RedBlackTree<TKey, TValue> : IEnumerable<(TKey key, TValue value)>
    where TKey : IComparable<TKey>
{
    #region 字段与属性

    private RedBlackNode<TKey, TValue>? _root;
    private int _count;

    /// <summary>节点数量</summary>
    public int Count => _count;

    /// <summary>是否为空</summary>
    public bool IsEmpty => _root == null;

    #endregion

    #region 构造函数

    public RedBlackTree()
    {
        _root = null;
        _count = 0;
    }

    #endregion

    #region 核心操作

    /// <summary>
    /// 插入键值对
    /// </summary>
    public void Insert(TKey key, TValue value)
    {
        var newNode = new RedBlackNode<TKey, TValue>(key, value);

        // 标准 BST 插入
        RedBlackNode<TKey, TValue>? parent = null;
        var current = _root;

        while (current != null)
        {
            parent = current;
            int cmp = newNode.Key.CompareTo(current.Key);
            if (cmp < 0)
                current = current.Left;
            else if (cmp > 0)
                current = current.Right;
            else
            {
                // 键已存在，更新值
                current.Value = value;
                return;
            }
        }

        newNode.Parent = parent;
        if (parent == null)
        {
            _root = newNode;
        }
        else if (newNode.Key.CompareTo(parent.Key) < 0)
        {
            parent.Left = newNode;
        }
        else
        {
            parent.Right = newNode;
        }

        _count++;

        // 如果是根节点，设为黑色
        if (newNode.Parent == null)
        {
            newNode.Color = NodeColor.Black;
            return;
        }

        // 如果祖父节点为 null，直接返回
        if (newNode.Parent.Parent == null)
            return;

        // 修复红黑树性质
        InsertFixup(newNode);
    }

    /// <summary>
    /// 删除指定键
    /// </summary>
    public bool Delete(TKey key)
    {
        var node = FindNode(key);
        if (node == null)
            return false;

        DeleteNode(node);
        _count--;
        return true;
    }

    /// <summary>
    /// 查找值
    /// </summary>
    public bool TryGetValue(TKey key, out TValue value)
    {
        var node = FindNode(key);
        if (node != null)
        {
            value = node.Value;
            return true;
        }
        value = default!;
        return false;
    }

    /// <summary>
    /// 检查是否包含指定键
    /// </summary>
    public bool ContainsKey(TKey key)
    {
        return FindNode(key) != null;
    }

    /// <summary>
    /// 索引器
    /// </summary>
    public TValue this[TKey key]
    {
        get
        {
            if (TryGetValue(key, out var value))
                return value;
            throw new KeyNotFoundException($"Key '{key}' not found");
        }
        set => Insert(key, value);
    }

    /// <summary>
    /// 清空树
    /// </summary>
    public void Clear()
    {
        _root = null;
        _count = 0;
    }

    #endregion

    #region 旋转与修复

    /// <summary>
    /// 左旋
    /// </summary>
    private void LeftRotate(RedBlackNode<TKey, TValue> x)
    {
        var y = x.Right;
        x.Right = y!.Left;

        if (y.Left != null)
            y.Left.Parent = x;

        y.Parent = x.Parent;

        if (x.Parent == null)
            _root = y;
        else if (x == x.Parent.Left)
            x.Parent.Left = y;
        else
            x.Parent.Right = y;

        y.Left = x;
        x.Parent = y;
    }

    /// <summary>
    /// 右旋
    /// </summary>
    private void RightRotate(RedBlackNode<TKey, TValue> y)
    {
        var x = y.Left;
        y.Left = x!.Right;

        if (x.Right != null)
            x.Right.Parent = y;

        x.Parent = y.Parent;

        if (y.Parent == null)
            _root = x;
        else if (y == y.Parent.Right)
            y.Parent.Right = x;
        else
            y.Parent.Left = x;

        x.Right = y;
        y.Parent = x;
    }

    /// <summary>
    /// 插入后修复红黑树性质
    /// </summary>
    private void InsertFixup(RedBlackNode<TKey, TValue> z)
    {
        while (z.Parent != null && z.Parent.Color == NodeColor.Red)
        {
            if (z.Parent == z.Parent.Parent?.Right)
            {
                // 叔节点
                var u = z.Parent.Parent.Left;

                if (u != null && u.Color == NodeColor.Red)
                {
                    // 情况 1: 叔叔是红色
                    z.Parent.Color = NodeColor.Black;
                    u.Color = NodeColor.Black;
                    z.Parent.Parent.Color = NodeColor.Red;
                    z = z.Parent.Parent;
                }
                else
                {
                    if (z == z.Parent.Left)
                    {
                        // 情况 2: 叔叔是黑色且 z 是左孩子
                        z = z.Parent;
                        RightRotate(z);
                    }
                    // 情况 3: 叔叔是黑色且 z 是右孩子
                    z.Parent!.Color = NodeColor.Black;
                    z.Parent.Parent!.Color = NodeColor.Red;
                    LeftRotate(z.Parent.Parent);
                }
            }
            else
            {
                // 对称情况
                var u = z.Parent.Parent?.Right;

                if (u != null && u.Color == NodeColor.Red)
                {
                    z.Parent.Color = NodeColor.Black;
                    u.Color = NodeColor.Black;
                    z.Parent.Parent!.Color = NodeColor.Red;
                    z = z.Parent.Parent;
                }
                else
                {
                    if (z == z.Parent.Right)
                    {
                        z = z.Parent;
                        LeftRotate(z);
                    }
                    z.Parent!.Color = NodeColor.Black;
                    z.Parent.Parent!.Color = NodeColor.Red;
                    RightRotate(z.Parent.Parent);
                }
            }

            if (z == _root)
                break;
        }

        _root!.Color = NodeColor.Black;
    }

    #endregion

    #region 删除操作

    /// <summary>
    /// 查找节点
    /// </summary>
    private RedBlackNode<TKey, TValue>? FindNode(TKey key)
    {
        var current = _root;
        while (current != null)
        {
            int cmp = key.CompareTo(current.Key);
            if (cmp < 0)
                current = current.Left;
            else if (cmp > 0)
                current = current.Right;
            else
                return current;
        }
        return null;
    }

    /// <summary>
    /// 查找最小值节点
    /// </summary>
    private RedBlackNode<TKey, TValue> FindMin(RedBlackNode<TKey, TValue> node)
    {
        while (node.Left != null)
            node = node.Left;
        return node;
    }

    /// <summary>
    /// 替换子树
    /// </summary>
    private void Transplant(RedBlackNode<TKey, TValue> u, RedBlackNode<TKey, TValue>? v)
    {
        if (u.Parent == null)
            _root = v;
        else if (u == u.Parent.Left)
            u.Parent.Left = v;
        else
            u.Parent.Right = v;

        if (v != null)
            v.Parent = u.Parent;
    }

    /// <summary>
    /// 删除节点
    /// </summary>
    private void DeleteNode(RedBlackNode<TKey, TValue> z)
    {
        RedBlackNode<TKey, TValue>? x;
        var y = z;
        var yOriginalColor = y.Color;

        if (z.Left == null)
        {
            x = z.Right;
            Transplant(z, z.Right);
        }
        else if (z.Right == null)
        {
            x = z.Left;
            Transplant(z, z.Left);
        }
        else
        {
            y = FindMin(z.Right);
            yOriginalColor = y.Color;
            x = y.Right;

            if (y.Parent == z && x != null)
                x.Parent = y;
            else
            {
                Transplant(y, y.Right);
                y.Right = z.Right;
                if (y.Right != null)
                    y.Right.Parent = y;
            }

            Transplant(z, y);
            y.Left = z.Left;
            y.Left!.Parent = y;
            y.Color = z.Color;
        }

        if (yOriginalColor == NodeColor.Black && x != null)
            DeleteFixup(x);
    }

    /// <summary>
    /// 删除后修复红黑树性质
    /// </summary>
    private void DeleteFixup(RedBlackNode<TKey, TValue> x)
    {
        while (x != _root && x.Color == NodeColor.Black)
        {
            if (x == x.Parent?.Left)
            {
                var w = x.Parent.Right;
                if (w != null && w.Color == NodeColor.Red)
                {
                    w.Color = NodeColor.Black;
                    x.Parent.Color = NodeColor.Red;
                    LeftRotate(x.Parent);
                    w = x.Parent.Right;
                }

                if (w != null &&
                    (w.Left == null || w.Left.Color == NodeColor.Black) &&
                    (w.Right == null || w.Right.Color == NodeColor.Black))
                {
                    w.Color = NodeColor.Red;
                    x = x.Parent;
                }
                else if (w != null)
                {
                    if (w.Right == null || w.Right.Color == NodeColor.Black)
                    {
                        if (w.Left != null)
                            w.Left.Color = NodeColor.Black;
                        w.Color = NodeColor.Red;
                        RightRotate(w);
                        w = x.Parent.Right;
                    }

                    w.Color = x.Parent.Color;
                    x.Parent.Color = NodeColor.Black;
                    if (w.Right != null)
                        w.Right.Color = NodeColor.Black;
                    LeftRotate(x.Parent);
                    x = _root!;
                }
                else
                {
                    break;
                }
            }
            else
            {
                var w = x.Parent?.Left;
                if (w != null && w.Color == NodeColor.Red)
                {
                    w.Color = NodeColor.Black;
                    x.Parent!.Color = NodeColor.Red;
                    RightRotate(x.Parent);
                    w = x.Parent.Left;
                }

                if (w != null &&
                    (w.Right == null || w.Right.Color == NodeColor.Black) &&
                    (w.Left == null || w.Left.Color == NodeColor.Black))
                {
                    w.Color = NodeColor.Red;
                    x = x.Parent!;
                }
                else if (w != null)
                {
                    if (w.Left == null || w.Left.Color == NodeColor.Black)
                    {
                        if (w.Right != null)
                            w.Right.Color = NodeColor.Black;
                        w.Color = NodeColor.Red;
                        LeftRotate(w);
                        w = x.Parent?.Left;
                    }

                    if (w != null)
                    {
                        w.Color = x.Parent!.Color;
                        x.Parent.Color = NodeColor.Black;
                        if (w.Left != null)
                            w.Left.Color = NodeColor.Black;
                        RightRotate(x.Parent);
                        x = _root!;
                    }
                    else
                    {
                        break;
                    }
                }
                else
                {
                    break;
                }
            }
        }
        x.Color = NodeColor.Black;
    }

    #endregion

    #region 有序查询

    /// <summary>
    /// 获取最小值
    /// </summary>
    public (TKey key, TValue value)? Minimum()
    {
        if (_root == null)
            return null;

        var min = FindMin(_root);
        return (min.Key, min.Value);
    }

    /// <summary>
    /// 获取最大值
    /// </summary>
    public (TKey key, TValue value)? Maximum()
    {
        if (_root == null)
            return null;

        var node = _root;
        while (node.Right != null)
            node = node.Right;

        return (node.Key, node.Value);
    }

    /// <summary>
    /// 获取大于或等于指定键的最小值（ceiling）
    /// </summary>
    public (TKey key, TValue value)? Ceiling(TKey key)
    {
        var current = _root;
        RedBlackNode<TKey, TValue>? result = null;

        while (current != null)
        {
            int cmp = key.CompareTo(current.Key);
            if (cmp == 0)
                return (current.Key, current.Value);
            if (cmp < 0)
            {
                result = current;
                current = current.Left;
            }
            else
            {
                current = current.Right;
            }
        }

        return result != null ? (result.Key, result.Value) : null;
    }

    /// <summary>
    /// 获取小于或等于指定键的最大值（floor）
    /// </summary>
    public (TKey key, TValue value)? Floor(TKey key)
    {
        var current = _root;
        RedBlackNode<TKey, TValue>? result = null;

        while (current != null)
        {
            int cmp = key.CompareTo(current.Key);
            if (cmp == 0)
                return (current.Key, current.Value);
            if (cmp > 0)
            {
                result = current;
                current = current.Right;
            }
            else
            {
                current = current.Left;
            }
        }

        return result != null ? (result.Key, result.Value) : null;
    }

    /// <summary>
    /// 获取范围内的所有元素 [low, high]
    /// </summary>
    public List<(TKey key, TValue value)> RangeQuery(TKey low, TKey high)
    {
        var result = new List<(TKey, TValue)>();
        RangeQueryRecursive(_root, low, high, result);
        return result;
    }

    private void RangeQueryRecursive(
        RedBlackNode<TKey, TValue>? node, TKey low, TKey high,
        List<(TKey key, TValue value)> result)
    {
        if (node == null)
            return;

        int cmpLow = low.CompareTo(node.Key);
        int cmpHigh = high.CompareTo(node.Key);

        if (cmpLow < 0)
            RangeQueryRecursive(node.Left, low, high, result);

        if (cmpLow <= 0 && cmpHigh >= 0)
            result.Add((node.Key, node.Value));

        if (cmpHigh > 0)
            RangeQueryRecursive(node.Right, low, high, result);
    }

    /// <summary>
    /// 获取前 N 个最小元素
    /// </summary>
    public List<(TKey key, TValue value)> Take(int count)
    {
        var result = new List<(TKey, TValue)>();
        TakeRecursive(_root, count, result);
        return result;
    }

    private void TakeRecursive(RedBlackNode<TKey, TValue>? node, int count,
        List<(TKey key, TValue value)> result)
    {
        if (node == null || result.Count >= count)
            return;

        TakeRecursive(node.Left, count, result);
        if (result.Count < count)
        {
            result.Add((node.Key, node.Value));
            TakeRecursive(node.Right, count, result);
        }
    }

    /// <summary>
    /// 按排名获取元素（0-based, 升序）
    /// </summary>
    public (TKey key, TValue value)? GetByRank(int rank)
    {
        if (rank < 0 || rank >= _count)
            return null;

        int currentRank = 0;
        var node = GetByRankRecursive(_root, rank, ref currentRank);
        return node != null ? (node.Key, node.Value) : null;
    }

    private RedBlackNode<TKey, TValue>? GetByRankRecursive(
        RedBlackNode<TKey, TValue>? node, int targetRank, ref int currentRank)
    {
        if (node == null)
            return null;

        var left = GetByRankRecursive(node.Left, targetRank, ref currentRank);
        if (left != null)
            return left;

        if (currentRank == targetRank)
            return node;
        currentRank++;

        return GetByRankRecursive(node.Right, targetRank, ref currentRank);
    }

    #endregion

    #region 树属性验证与统计

    /// <summary>
    /// 获取树的高度
    /// </summary>
    public int GetHeight()
    {
        return GetHeightRecursive(_root);
    }

    private int GetHeightRecursive(RedBlackNode<TKey, TValue>? node)
    {
        if (node == null)
            return 0;
        return 1 + global::System.Math.Max(
            GetHeightRecursive(node.Left),
            GetHeightRecursive(node.Right));
    }

    /// <summary>
    /// 验证红黑树性质（用于调试）
    /// </summary>
    public bool Validate()
    {
        if (_root == null)
            return true;

        // 规则 2: 根节点必须是黑色
        if (_root.Color != NodeColor.Black)
            return false;

        // 规则 4 和 5: 递归验证
        int blackCount = 0;
        return ValidateRecursive(_root, ref blackCount);
    }

    private bool ValidateRecursive(RedBlackNode<TKey, TValue>? node, ref int blackCount)
    {
        if (node == null)
        {
            blackCount = 1;  // NIL 叶子是黑色
            return true;
        }

        // 规则 4: 红色节点的子节点必须是黑色
        if (node.Color == NodeColor.Red)
        {
            if ((node.Left != null && node.Left.Color == NodeColor.Red) ||
                (node.Right != null && node.Right.Color == NodeColor.Red))
                return false;
        }

        int leftBlack = 0, rightBlack = 0;
        if (!ValidateRecursive(node.Left, ref leftBlack) ||
            !ValidateRecursive(node.Right, ref rightBlack))
            return false;

        // 规则 5: 所有路径黑色节点数相同
        if (leftBlack != rightBlack)
            return false;

        blackCount = leftBlack + (node.Color == NodeColor.Black ? 1 : 0);
        return true;
    }

    #endregion

    #region 枚举器

    /// <summary>
    /// 中序遍历（升序）
    /// </summary>
    public IEnumerator<(TKey key, TValue value)> GetEnumerator()
    {
        var stack = new Stack<RedBlackNode<TKey, TValue>>();
        var current = _root;

        while (current != null || stack.Count > 0)
        {
            while (current != null)
            {
                stack.Push(current);
                current = current.Left;
            }

            current = stack.Pop();
            yield return (current.Key, current.Value);
            current = current.Right;
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    #endregion
}
