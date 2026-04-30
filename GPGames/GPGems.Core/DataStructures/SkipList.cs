/*
 * 跳表 Skip List
 * 时间复杂度: O(log n) 平均查找/插入/删除, O(n) 最坏情况
 * 空间复杂度: O(n) 平均
 *
 * 经营游戏核心用途:
 *   - 排行榜: 玩家分数动态排序
 *   - 事件队列: 按时间排序的延迟事件
 *   - 资源稀有度: 按稀有度排序的物品列表
 *   - 价格排序: 商品价格区间快速查找
 */

using System;
using System.Collections;
using System.Collections.Generic;

namespace GPGems.Core.DataStructures;

/// <summary>
/// 跳表节点
/// </summary>
/// <typeparam name="TKey">键类型</typeparam>
/// <typeparam name="TValue">值类型</typeparam>
internal class SkipListNode<TKey, TValue>
{
    public TKey Key { get; }
    public TValue Value { get; set; }
    public SkipListNode<TKey, TValue>?[] Forward { get; }

    public SkipListNode(TKey key, TValue value, int level)
    {
        Key = key;
        Value = value;
        Forward = new SkipListNode<TKey, TValue>?[level + 1];  // levels are 0-based
    }
}

/// <summary>
/// 跳表 - 概率平衡有序数据结构
/// 比红黑树实现简单，性能相当
/// </summary>
/// <typeparam name="TKey">键类型（必须可比较）</typeparam>
/// <typeparam name="TValue">值类型</typeparam>
public class SkipList<TKey, TValue> : IEnumerable<(TKey key, TValue value)>
    where TKey : IComparable<TKey>
{
    #region 常量

    /// <summary>最大层数</summary>
    private const int MaxLevel = 32;

    /// <summary>概率因子 P (第 k 层晋升到 k+1 层的概率)</summary>
    private const double Probability = 0.5;

    #endregion

    #region 字段与属性

    private readonly SkipListNode<TKey, TValue> _head;
    private readonly Random _random;
    private int _level;
    private int _count;

    /// <summary>元素数量</summary>
    public int Count => _count;

    /// <summary>当前最大层数</summary>
    public int CurrentLevel => _level;

    /// <summary>是否为空</summary>
    public bool IsEmpty => _count == 0;

    #endregion

    #region 构造函数

    public SkipList()
    {
        _head = new SkipListNode<TKey, TValue>(default!, default!, MaxLevel - 1);
        _random = new Random();
        _level = 0;
        _count = 0;
    }

    #endregion

    #region 核心操作

    /// <summary>
    /// 随机生成层数
    /// </summary>
    private int RandomLevel()
    {
        int level = 0;
        while (_random.NextDouble() < Probability && level < MaxLevel - 1)
        {
            level++;
        }
        return level;
    }

    /// <summary>
    /// 插入键值对
    /// </summary>
    public void Insert(TKey key, TValue value)
    {
        // update[i] 保存第 i 层需要更新的节点（前驱）
        var update = new SkipListNode<TKey, TValue>?[MaxLevel];
        var current = _head;

        // 从最高层开始向下找
        for (int i = _level; i >= 0; i--)
        {
            while (current.Forward[i] != null &&
                   current.Forward[i].Key.CompareTo(key) < 0)
            {
                current = current.Forward[i];
            }
            update[i] = current;
        }

        current = current.Forward[0];

        // 如果键已存在，更新值
        if (current != null && current.Key.CompareTo(key) == 0)
        {
            current.Value = value;
            return;
        }

        // 随机生成新节点层数
        int newLevel = RandomLevel();

        // 如果新层数超过当前层数，初始化 update 中超出的部分
        if (newLevel > _level)
        {
            for (int i = _level + 1; i <= newLevel; i++)
            {
                update[i] = _head;
            }
            _level = newLevel;
        }

        // 创建新节点
        var newNode = new SkipListNode<TKey, TValue>(key, value, newLevel);

        // 更新每一层的指针
        for (int i = 0; i <= newLevel; i++)
        {
            newNode.Forward[i] = update[i]!.Forward[i];
            update[i]!.Forward[i] = newNode;
        }

        _count++;
    }

    /// <summary>
    /// 删除指定键
    /// </summary>
    public bool Delete(TKey key)
    {
        var update = new SkipListNode<TKey, TValue>?[MaxLevel];
        var current = _head;

        // 从最高层开始向下找
        for (int i = _level; i >= 0; i--)
        {
            while (current.Forward[i] != null &&
                   current.Forward[i].Key.CompareTo(key) < 0)
            {
                current = current.Forward[i];
            }
            update[i] = current;
        }

        current = current.Forward[0];

        // 找不到键
        if (current == null || current.Key.CompareTo(key) != 0)
            return false;

        // 更新每一层的指针
        for (int i = 0; i <= _level; i++)
        {
            if (update[i]!.Forward[i] != current)
                break;

            update[i]!.Forward[i] = current.Forward[i];
        }

        // 收缩层数（如果最高层已经没有节点）
        while (_level > 0 && _head.Forward[_level] == null)
        {
            _level--;
        }

        _count--;
        return true;
    }

    /// <summary>
    /// 查找指定键的值
    /// </summary>
    public bool TryGet(TKey key, out TValue value)
    {
        var current = _head;

        // 从最高层开始向下找
        for (int i = _level; i >= 0; i--)
        {
            while (current.Forward[i] != null &&
                   current.Forward[i].Key.CompareTo(key) < 0)
            {
                current = current.Forward[i];
            }
        }

        current = current.Forward[0];

        if (current != null && current.Key.CompareTo(key) == 0)
        {
            value = current.Value;
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
        return TryGet(key, out _);
    }

    /// <summary>
    /// 索引器
    /// </summary>
    public TValue this[TKey key]
    {
        get
        {
            if (TryGet(key, out var value))
                return value;
            throw new KeyNotFoundException($"Key '{key}' not found");
        }
        set => Insert(key, value);
    }

    /// <summary>
    /// 清空跳表
    /// </summary>
    public void Clear()
    {
        for (int i = 0; i <= MaxLevel - 1; i++)
        {
            _head.Forward[i] = null;
        }
        _level = 0;
        _count = 0;
    }

    #endregion

    #region 范围查询

    /// <summary>
    /// 获取第一个元素
    /// </summary>
    public (TKey key, TValue value)? First()
    {
        var first = _head.Forward[0];
        return first != null ? (first.Key, first.Value) : null;
    }

    /// <summary>
    /// 获取最后一个元素
    /// </summary>
    public (TKey key, TValue value)? Last()
    {
        if (_count == 0) return null;

        var current = _head;
        for (int i = _level; i >= 0; i--)
        {
            while (current.Forward[i] != null)
            {
                current = current.Forward[i];
            }
        }

        return (current.Key, current.Value);
    }

    /// <summary>
    /// 查找大于或等于指定键的第一个元素
    /// </summary>
    public (TKey key, TValue value)? Ceiling(TKey key)
    {
        var current = _head;

        for (int i = _level; i >= 0; i--)
        {
            while (current.Forward[i] != null &&
                   current.Forward[i].Key.CompareTo(key) < 0)
            {
                current = current.Forward[i];
            }
        }

        current = current.Forward[0];
        return current != null ? (current.Key, current.Value) : null;
    }

    /// <summary>
    /// 查找小于或等于指定键的最后一个元素
    /// </summary>
    public (TKey key, TValue value)? Floor(TKey key)
    {
        var current = _head;
        SkipListNode<TKey, TValue>? lastValid = null;

        for (int i = _level; i >= 0; i--)
        {
            while (current.Forward[i] != null &&
                   current.Forward[i].Key.CompareTo(key) <= 0)
            {
                current = current.Forward[i];
                lastValid = current;
            }
        }

        return lastValid != null ? (lastValid.Key, lastValid.Value) : null;
    }

    /// <summary>
    /// 获取指定范围内的所有元素 [minKey, maxKey]
    /// </summary>
    public List<(TKey key, TValue value)> GetRange(TKey minKey, TKey maxKey)
    {
        var result = new List<(TKey, TValue)>();

        // 先找到起点位置
        var current = _head;
        for (int i = _level; i >= 0; i--)
        {
            while (current.Forward[i] != null &&
                   current.Forward[i].Key.CompareTo(minKey) < 0)
            {
                current = current.Forward[i];
            }
        }

        current = current.Forward[0];

        // 遍历直到超过 maxKey
        while (current != null && current.Key.CompareTo(maxKey) <= 0)
        {
            result.Add((current.Key, current.Value));
            current = current.Forward[0];
        }

        return result;
    }

    /// <summary>
    /// 获取前 N 个元素
    /// </summary>
    public List<(TKey key, TValue value)> Take(int count)
    {
        var result = new List<(TKey, TValue)>();
        var current = _head.Forward[0];

        while (current != null && result.Count < count)
        {
            result.Add((current.Key, current.Value));
            current = current.Forward[0];
        }

        return result;
    }

    /// <summary>
    /// 获取按排名的元素（0-based）
    /// </summary>
    public (TKey key, TValue value)? GetByRank(int rank)
    {
        if (rank < 0 || rank >= _count)
            return null;

        var current = _head.Forward[0];
        for (int i = 0; i < rank; i++)
        {
            current = current!.Forward[0];
        }

        return current != null ? (current.Key, current.Value) : null;
    }

    #endregion

    #region 统计

    /// <summary>
    /// 获取各层的元素数量分布
    /// </summary>
    public int[] GetLevelDistribution()
    {
        var result = new int[_level + 1];
        for (int i = 0; i <= _level; i++)
        {
            int count = 0;
            var current = _head.Forward[i];
            while (current != null)
            {
                count++;
                current = current.Forward[i];
            }
            result[i] = count;
        }
        return result;
    }

    #endregion

    #region IEnumerable 实现

    public IEnumerator<(TKey key, TValue value)> GetEnumerator()
    {
        var current = _head.Forward[0];
        while (current != null)
        {
            yield return (current.Key, current.Value);
            current = current.Forward[0];
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    #endregion
}
