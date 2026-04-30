/*
 * AVL 树 Adelson-Velsky and Landis Tree
 * 时间复杂度: O(log n) 插入/删除/查找
 * 严格平衡 BST，左右子树高度差不超过 1
 *
 * 经营游戏核心用途:
 *   - 玩家排名: 严格平衡保证最坏情况下性能
 *   - 物品数据库: 物品 ID 快速查找
 *   - 成就系统: 成就条件快速匹配
 *   - 地图区块索引: 快速查找地图元素
 */

using System;
using System.Collections;
using System.Collections.Generic;

namespace GPGems.Core.DataStructures;

/// <summary>
/// AVL 树节点
/// </summary>
internal class AVLNode<TKey, TValue>
    where TKey : IComparable<TKey>
{
    public TKey Key { get; set; }
    public TValue Value { get; set; }
    public AVLNode<TKey, TValue>? Left { get; set; }
    public AVLNode<TKey, TValue>? Right { get; set; }
    public int Height { get; set; }

    public AVLNode(TKey key, TValue value)
    {
        Key = key;
        Value = value;
        Height = 1;  // 新节点高度为 1
    }
}

/// <summary>
/// AVL 树 - 严格平衡二叉搜索树
/// 每个节点的左右子树高度差（平衡因子）绝对值 <= 1
/// 比红黑树查找更快（更平衡），但插入删除稍慢（更多旋转）
/// </summary>
/// <typeparam name="TKey">键类型</typeparam>
/// <typeparam name="TValue">值类型</typeparam>
public class AVLTree<TKey, TValue> : IEnumerable<(TKey key, TValue value)>
    where TKey : IComparable<TKey>
{
    #region 字段与属性

    private AVLNode<TKey, TValue>? _root;
    private int _count;

    /// <summary>节点数量</summary>
    public int Count => _count;

    /// <summary>是否为空</summary>
    public bool IsEmpty => _root == null;

    /// <summary>树的高度</summary>
    public int Height => GetHeight(_root);

    #endregion

    #region 构造函数

    public AVLTree()
    {
        _root = null;
        _count = 0;
    }

    #endregion

    #region 工具方法

    /// <summary>
    /// 获取节点高度（空节点高度为 0）
    /// </summary>
    private int GetHeight(AVLNode<TKey, TValue>? node)
    {
        return node?.Height ?? 0;
    }

    /// <summary>
    /// 获取节点平衡因子
    /// = 左子树高度 - 右子树高度
    /// </summary>
    private int GetBalance(AVLNode<TKey, TValue>? node)
    {
        return node == null ? 0 : GetHeight(node.Left) - GetHeight(node.Right);
    }

    /// <summary>
    /// 更新节点高度
    /// </summary>
    private void UpdateHeight(AVLNode<TKey, TValue> node)
    {
        node.Height = 1 + Math.Max(
            GetHeight(node.Left),
            GetHeight(node.Right));
    }

    #endregion

    #region 旋转操作

    /// <summary>
    /// 右旋（LL 情况）
    /// </summary>
    private AVLNode<TKey, TValue> RightRotate(AVLNode<TKey, TValue> y)
    {
        /*
         *       y                 x
         *      / \               / \
         *     x   T3    -->     T1  y
         *    / \                   / \
         *   T1  T2               T2  T3
         */

        var x = y.Left!;
        var T2 = x.Right;

        // 旋转
        x.Right = y;
        y.Left = T2;

        // 更新高度
        UpdateHeight(y);
        UpdateHeight(x);

        return x;
    }

    /// <summary>
    /// 左旋（RR 情况）
    /// </summary>
    private AVLNode<TKey, TValue> LeftRotate(AVLNode<TKey, TValue> x)
    {
        /*
         *     x                   y
         *    / \                 / \
         *   T1  y     -->       x   T3
         *      / \             / \
         *     T2  T3          T1  T2
         */

        var y = x.Right!;
        var T2 = y.Left;

        // 旋转
        y.Left = x;
        x.Right = T2;

        // 更新高度
        UpdateHeight(x);
        UpdateHeight(y);

        return y;
    }

    #endregion

    #region 核心操作

    /// <summary>
    /// 插入键值对
    /// </summary>
    public void Insert(TKey key, TValue value)
    {
        _root = InsertRecursive(_root, key, value);
    }

    private AVLNode<TKey, TValue> InsertRecursive(
        AVLNode<TKey, TValue>? node, TKey key, TValue value)
    {
        // 标准 BST 插入
        if (node == null)
        {
            _count++;
            return new AVLNode<TKey, TValue>(key, value);
        }

        int cmp = key.CompareTo(node.Key);
        if (cmp < 0)
            node.Left = InsertRecursive(node.Left, key, value);
        else if (cmp > 0)
            node.Right = InsertRecursive(node.Right, key, value);
        else
        {
            // 键已存在，更新值
            node.Value = value;
            return node;
        }

        // 更新高度
        UpdateHeight(node);

        // 获取平衡因子检查是否失衡
        int balance = GetBalance(node);

        // 4 种失衡情况

        // 情况 1: LL (左左) - 右旋
        if (balance > 1 && key.CompareTo(node.Left!.Key) < 0)
            return RightRotate(node);

        // 情况 2: RR (右右) - 左旋
        if (balance < -1 && key.CompareTo(node.Right!.Key) > 0)
            return LeftRotate(node);

        // 情况 3: LR (左右) - 先左旋左子树，再右旋
        if (balance > 1 && key.CompareTo(node.Left!.Key) > 0)
        {
            node.Left = LeftRotate(node.Left);
            return RightRotate(node);
        }

        // 情况 4: RL (右左) - 先右旋右子树，再左旋
        if (balance < -1 && key.CompareTo(node.Right!.Key) < 0)
        {
            node.Right = RightRotate(node.Right);
            return LeftRotate(node);
        }

        // 已平衡，直接返回
        return node;
    }

    /// <summary>
    /// 删除指定键
    /// </summary>
    public bool Delete(TKey key)
    {
        int before = _count;
        _root = DeleteRecursive(_root, key);
        return _count < before;
    }

    private AVLNode<TKey, TValue>? DeleteRecursive(
        AVLNode<TKey, TValue>? node, TKey key)
    {
        // 标准 BST 删除
        if (node == null)
            return null;

        int cmp = key.CompareTo(node.Key);
        if (cmp < 0)
        {
            node.Left = DeleteRecursive(node.Left, key);
        }
        else if (cmp > 0)
        {
            node.Right = DeleteRecursive(node.Right, key);
        }
        else
        {
            // 找到要删除的节点
            _count--;

            // 情况 1: 没有子节点或只有一个子节点
            if (node.Left == null || node.Right == null)
            {
                var temp = node.Left ?? node.Right;

                // 没有子节点
                if (temp == null)
                    return null;

                // 一个子节点：复制内容
                node = temp;
            }
            else
            {
                // 情况 2: 两个子节点 - 取中序后继（右子树最小值）
                var temp = FindMin(node.Right);
                node.Key = temp!.Key;
                node.Value = temp.Value;
                node.Right = DeleteRecursive(node.Right, temp.Key);
                _count++;  // 这里会多减一次，修正
            }
        }

        // 如果只有一个节点，直接返回
        if (node == null)
            return node;

        // 更新高度
        UpdateHeight(node);

        // 获取平衡因子检查是否失衡
        int balance = GetBalance(node);

        // 4 种失衡情况

        // LL
        if (balance > 1 && GetBalance(node.Left) >= 0)
            return RightRotate(node);

        // LR
        if (balance > 1 && GetBalance(node.Left) < 0)
        {
            node.Left = LeftRotate(node.Left!);
            return RightRotate(node);
        }

        // RR
        if (balance < -1 && GetBalance(node.Right) <= 0)
            return LeftRotate(node);

        // RL
        if (balance < -1 && GetBalance(node.Right) > 0)
        {
            node.Right = RightRotate(node.Right!);
            return LeftRotate(node);
        }

        return node;
    }

    /// <summary>
    /// 查找节点
    /// </summary>
    private AVLNode<TKey, TValue>? FindNode(TKey key)
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

    #region 查找最小值/最大值

    /// <summary>
    /// 查找子树最小值节点
    /// </summary>
    private AVLNode<TKey, TValue>? FindMin(AVLNode<TKey, TValue>? node)
    {
        while (node?.Left != null)
            node = node.Left;
        return node;
    }

    /// <summary>
    /// 查找子树最大值节点
    /// </summary>
    private AVLNode<TKey, TValue>? FindMax(AVLNode<TKey, TValue>? node)
    {
        while (node?.Right != null)
            node = node.Right;
        return node;
    }

    /// <summary>
    /// 获取最小值
    /// </summary>
    public (TKey key, TValue value)? Minimum()
    {
        var min = FindMin(_root);
        return min != null ? (min.Key, min.Value) : null;
    }

    /// <summary>
    /// 获取最大值
    /// </summary>
    public (TKey key, TValue value)? Maximum()
    {
        var max = FindMax(_root);
        return max != null ? (max.Key, max.Value) : null;
    }

    #endregion

    #region 有序查询

    /// <summary>
    /// 获取大于或等于指定键的最小值（ceiling）
    /// </summary>
    public (TKey key, TValue value)? Ceiling(TKey key)
    {
        var current = _root;
        AVLNode<TKey, TValue>? result = null;

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
        AVLNode<TKey, TValue>? result = null;

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
        AVLNode<TKey, TValue>? node, TKey low, TKey high,
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

    private void TakeRecursive(AVLNode<TKey, TValue>? node, int count,
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

    private AVLNode<TKey, TValue>? GetByRankRecursive(
        AVLNode<TKey, TValue>? node, int targetRank, ref int currentRank)
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

    #region 验证与统计

    /// <summary>
    /// 验证 AVL 树性质（用于调试）
    /// </summary>
    public bool Validate()
    {
        return ValidateRecursive(_root) != -1;
    }

    private int ValidateRecursive(AVLNode<TKey, TValue>? node)
    {
        if (node == null)
            return 0;

        int leftHeight = ValidateRecursive(node.Left);
        if (leftHeight == -1)
            return -1;

        int rightHeight = ValidateRecursive(node.Right);
        if (rightHeight == -1)
            return -1;

        // 检查 BST 性质
        if (node.Left != null && node.Left.Key.CompareTo(node.Key) >= 0)
            return -1;
        if (node.Right != null && node.Right.Key.CompareTo(node.Key) <= 0)
            return -1;

        // 检查平衡因子
        if (Math.Abs(leftHeight - rightHeight) > 1)
            return -1;

        // 检查高度值是否正确
        int expectedHeight = 1 + Math.Max(leftHeight, rightHeight);
        if (node.Height != expectedHeight)
            return -1;

        return expectedHeight;
    }

    #endregion

    #region 枚举器

    /// <summary>
    /// 中序遍历（升序）
    /// </summary>
    public IEnumerator<(TKey key, TValue value)> GetEnumerator()
    {
        var stack = new Stack<AVLNode<TKey, TValue>>();
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
