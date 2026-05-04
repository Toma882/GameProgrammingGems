/*
 * LRU 缓存 LRU Cache
 * 时间复杂度: O(1) get/put, 最近最少使用淘汰策略
 *
 * 经营游戏核心用途:
 *   - 资源缓存: 纹理/模型缓存, 优先保留最近使用
 *   - 玩家数据缓存: 在线玩家信息 LRU 淘汰
 *   - 寻路路径缓存: 常用路径复用
 *   - UI 元素缓存: 列表滚动复用
 */

using System;
using System.Collections.Generic;

namespace GPGems.Core.DataStructures;

/// <summary>
/// LRU 缓存 - 最近最少使用淘汰策略
/// 实现: 哈希表 + 双向链表 = O(1) get/put
/// </summary>
/// <typeparam name="TKey">键类型</typeparam>
/// <typeparam name="TValue">值类型</typeparam>
public class LRUCache<TKey, TValue>
    where TKey : notnull
{
    #region 内部节点类

    private class Node
    {
        public TKey Key = default!;
        public TValue Value = default!;
        public Node? Prev;
        public Node? Next;
    }

    #endregion

    #region 字段与属性

    private readonly Dictionary<TKey, Node> _cache;
    private readonly Node _head;
    private readonly Node _tail;
    private readonly int _capacity;
    private int _count;

    public int Capacity => _capacity;
    public int Count => _count;

    /// <summary>缓存命中事件</summary>
    public event Action<TKey>? OnHit;

    /// <summary>缓存未命中事件</summary>
    public event Action<TKey>? OnMiss;

    /// <summary>元素被淘汰事件</summary>
    public event Action<TKey, TValue>? OnEvicted;

    #endregion

    #region 构造函数

    public LRUCache(int capacity)
    {
        if (capacity <= 0)
            throw new ArgumentException("Capacity must be positive", nameof(capacity));

        _capacity = capacity;
        _cache = new Dictionary<TKey, Node>(capacity);
        _head = new Node();
        _tail = new Node();
        _head.Next = _tail;
        _tail.Prev = _head;
        _count = 0;
    }

    #endregion

    #region 核心操作

    /// <summary>
    /// 获取缓存值
    /// </summary>
    /// <returns>如果存在返回值，否则返回默认值</returns>
    public TValue? Get(TKey key)
    {
        if (_cache.TryGetValue(key, out var node))
        {
            MoveToHead(node);
            OnHit?.Invoke(key);
            return node.Value;
        }

        OnMiss?.Invoke(key);
        return default;
    }

    /// <summary>
    /// 尝试获取缓存值
    /// </summary>
    public bool TryGet(TKey key, out TValue value)
    {
        if (_cache.TryGetValue(key, out var node))
        {
            MoveToHead(node);
            value = node.Value;
            OnHit?.Invoke(key);
            return true;
        }

        value = default!;
        OnMiss?.Invoke(key);
        return false;
    }

    /// <summary>
    /// 插入或更新缓存
    /// </summary>
    public void Put(TKey key, TValue value)
    {
        if (_cache.TryGetValue(key, out var node))
        {
            node.Value = value;
            MoveToHead(node);
        }
        else
        {
            var newNode = new Node { Key = key, Value = value };
            _cache.Add(key, newNode);
            AddNode(newNode);
            _count++;

            if (_count > _capacity)
            {
                var tail = PopTail();
                _cache.Remove(tail.Key);
                OnEvicted?.Invoke(tail.Key, tail.Value);
                _count--;
            }
        }
    }

    /// <summary>
    /// 获取或创建值（工厂方法模式）
    /// </summary>
    public TValue GetOrCreate(TKey key, Func<TKey, TValue> factory)
    {
        if (TryGet(key, out var value))
            return value;

        value = factory(key);
        Put(key, value);
        return value;
    }

    /// <summary>
    /// 删除指定键
    /// </summary>
    public bool Remove(TKey key)
    {
        if (_cache.TryGetValue(key, out var node))
        {
            RemoveNode(node);
            _cache.Remove(key);
            _count--;
            return true;
        }
        return false;
    }

    /// <summary>
    /// 检查是否包含指定键
    /// </summary>
    public bool ContainsKey(TKey key) => _cache.ContainsKey(key);

    /// <summary>
    /// 清空缓存
    /// </summary>
    public void Clear()
    {
        _cache.Clear();
        _head.Next = _tail;
        _tail.Prev = _head;
        _count = 0;
    }

    #endregion

    #region 双向链表操作

    /// <summary>
    /// 添加节点到头部（最近使用位置）
    /// </summary>
    private void AddNode(Node node)
    {
        node.Prev = _head;
        node.Next = _head.Next;

        _head.Next!.Prev = node;
        _head.Next = node;
    }

    /// <summary>
    /// 从链表中移除节点
    /// </summary>
    private void RemoveNode(Node node)
    {
        var prev = node.Prev;
        var next = node.Next;

        prev!.Next = next;
        next!.Prev = prev;
    }

    /// <summary>
    /// 将节点移动到头部（标记为最近使用）
    /// </summary>
    private void MoveToHead(Node node)
    {
        RemoveNode(node);
        AddNode(node);
    }

    /// <summary>
    /// 弹出尾部节点（最久未使用）
    /// </summary>
    private Node PopTail()
    {
        var res = _tail.Prev!;
        RemoveNode(res);
        return res;
    }

    #endregion

    #region 统计与遍历

    /// <summary>
    /// 获取最近使用的键列表
    /// </summary>
    public List<TKey> GetOrderedKeys(int? limit = null)
    {
        var result = new List<TKey>();
        var current = _head.Next;
        int count = 0;

        while (current != _tail)
        {
            result.Add(current!.Key);
            count++;
            if (limit.HasValue && count >= limit.Value)
                break;
            current = current.Next;
        }

        return result;
    }

    /// <summary>
    /// 获取最近最少使用的键（即将被淘汰）
    /// </summary>
    public TKey? GetOldestKey()
    {
        if (_count == 0)
            return default;
        return _tail.Prev!.Key;
    }

    /// <summary>
    /// 获取最近使用的键
    /// </summary>
    public TKey? GetNewestKey()
    {
        if (_count == 0)
            return default;
        return _head.Next!.Key;
    }

    #endregion
}
