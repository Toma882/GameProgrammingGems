/*
 * GPGems.AI - Blackboard System
 * Blackboard: 核心黑板实现
 * 线程安全、类型安全、支持观察者、自动过期清理
 */

using System.Collections.Concurrent;

namespace GPGems.AI.Decision.Blackboards;

/// <summary>
/// 黑板：全局知识共享中心
/// 决策系统所有数据交换的中转站
/// </summary>
public class Blackboard : IBlackboardObserver
{
    /// <summary>全局默认黑板实例</summary>
    public static Blackboard Default { get; } = new Blackboard("Global");

    /// <summary>黑板名称（用于调试）</summary>
    public string Name { get; }

    /// <summary>条目数量</summary>
    public int Count => _entries.Count;

    private readonly ConcurrentDictionary<string, BlackboardEntry> _entries = new();
    private readonly ConcurrentDictionary<string, List<object>> _subscribers = new();
    private readonly object _subscriberLock = new();

    // 自动过期清理
    private readonly Timer? _cleanupTimer;
    private const float DefaultCleanupInterval = 5.0f; // 每5秒清理一次

    /// <summary>创建一个新黑板</summary>
    /// <param name="name">黑板名称</param>
    /// <param name="enableAutoCleanup">是否启用自动过期清理</param>
    public Blackboard(string name, bool enableAutoCleanup = true)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));

        if (enableAutoCleanup)
        {
            _cleanupTimer = new Timer(CleanupExpiredEntries, null,
                TimeSpan.FromSeconds(DefaultCleanupInterval),
                TimeSpan.FromSeconds(DefaultCleanupInterval));
        }
    }

    #region 写操作

    /// <summary>设置键值</summary>
    /// <typeparam name="T">值类型</typeparam>
    /// <param name="key">键名</param>
    /// <param name="value">值</param>
    /// <param name="ttl">生存时间（秒），-1 表示永不过期</param>
    /// <param name="priority">优先级</param>
    /// <param name="writer">写入者标识（调试用）</param>
    public void Set<T>(string key, T value, float ttl = -1, int priority = 0, string? writer = null)
    {
        if (key == null) throw new ArgumentNullException(nameof(key));

        var entry = _entries.GetOrAdd(key, k => new BlackboardEntry(k, typeof(T)));

        if (entry.ValueType != typeof(T))
            throw new InvalidCastException($"Key '{key}' was created with type {entry.ValueType.Name}, cannot set to {typeof(T).Name}");

        var oldValue = entry.Value is T old ? old : default;
        entry.UpdateValue(value, ttl, priority, writer);

        NotifySubscribers(key, oldValue, value, writer);
    }

    /// <summary>仅当键不存在时设置值</summary>
    public bool TrySetIfNotExists<T>(string key, T value, float ttl = -1, int priority = 0, string? writer = null)
    {
        if (key == null) throw new ArgumentNullException(nameof(key));
        if (_entries.ContainsKey(key)) return false;

        Set(key, value, ttl, priority, writer);
        return true;
    }

    /// <summary>原子性更新值</summary>
    public void Update<T>(string key, Func<T?, T> updateFunc, float ttl = -1, int priority = 0, string? writer = null)
    {
        if (key == null) throw new ArgumentNullException(nameof(key));
        if (updateFunc == null) throw new ArgumentNullException(nameof(updateFunc));

        lock (_entries)
        {
            TryGetValue<T>(key, out var oldValue);
            var newValue = updateFunc(oldValue);
            Set(key, newValue, ttl, priority, writer);
        }
    }

    #endregion

    #region 读操作

    /// <summary>获取键值</summary>
    /// <typeparam name="T">值类型</typeparam>
    /// <param name="key">键名</param>
    /// <returns>值</returns>
    /// <exception cref="KeyNotFoundException">键不存在时抛出</exception>
    /// <exception cref="InvalidCastException">类型不匹配时抛出</exception>
    public T? Get<T>(string key)
    {
        if (key == null) throw new ArgumentNullException(nameof(key));

        if (!_entries.TryGetValue(key, out var entry))
            throw new KeyNotFoundException($"Key '{key}' not found in blackboard '{Name}'");

        if (!entry.TryGetValue<T>(out var value))
            throw new KeyNotFoundException($"Key '{key}' is expired or type mismatch in blackboard '{Name}'");

        return value;
    }

    /// <summary>尝试获取键值，不抛异常</summary>
    public bool TryGetValue<T>(string key, out T? value)
    {
        value = default;
        if (key == null) return false;

        if (!_entries.TryGetValue(key, out var entry))
            return false;

        return entry.TryGetValue(out value);
    }

    /// <summary>尝试获取键值，不抛异常（别名，兼容 Utility 系统）</summary>
    public bool TryGet<T>(string key, out T? value) => TryGetValue(key, out value);

    /// <summary>获取键值，如果不存在则返回默认值</summary>
    public T? GetOrDefault<T>(string key, T? defaultValue = default)
    {
        return TryGetValue<T>(key, out var value) ? value : defaultValue;
    }

    /// <summary>检查键是否存在且未过期</summary>
    public bool HasKey(string key)
    {
        if (key == null) throw new ArgumentNullException(nameof(key));
        return _entries.TryGetValue(key, out var entry) && !entry.IsExpired;
    }

    /// <summary>获取键的类型</summary>
    public Type? GetKeyType(string key)
    {
        return _entries.TryGetValue(key, out var entry) ? entry.ValueType : null;
    }

    #endregion

    #region 删除操作

    /// <summary>删除键</summary>
    public bool Remove(string key)
    {
        if (key == null) throw new ArgumentNullException(nameof(key));

        var removed = _entries.TryRemove(key, out _);
        if (removed)
        {
            NotifySubscribersRemoved(key);
        }
        return removed;
    }

    /// <summary>清空所有条目</summary>
    public void Clear()
    {
        var keys = _entries.Keys.ToList();
        _entries.Clear();

        foreach (var key in keys)
        {
            NotifySubscribersRemoved(key);
        }
    }

    #endregion

    #region 观察者模式

    /// <inheritdoc />
    public IDisposable Subscribe<T>(string key, Action<BlackboardValueChangedEventArgs<T>> callback)
    {
        if (key == null) throw new ArgumentNullException(nameof(key));
        if (callback == null) throw new ArgumentNullException(nameof(callback));

        lock (_subscriberLock)
        {
            var callbacks = _subscribers.GetOrAdd(key, _ => new List<object>());
            callbacks.Add(callback);

            return new BlackboardSubscriptionToken(key, typeof(T), () =>
            {
                lock (_subscriberLock)
                {
                    if (_subscribers.TryGetValue(key, out var cbs))
                    {
                        cbs.Remove(callback);
                    }
                }
            });
        }
    }

    /// <inheritdoc />
    public void Unsubscribe(IDisposable subscriptionToken)
    {
        if (subscriptionToken == null) throw new ArgumentNullException(nameof(subscriptionToken));
        subscriptionToken.Dispose();
    }

    /// <inheritdoc />
    public void UnsubscribeAll(string key)
    {
        if (key == null) throw new ArgumentNullException(nameof(key));
        lock (_subscriberLock)
        {
            _subscribers.TryRemove(key, out _);
        }
    }

    /// <inheritdoc />
    public void UnsubscribeAll()
    {
        lock (_subscriberLock)
        {
            _subscribers.Clear();
        }
    }

    private void NotifySubscribers<T>(string key, T? oldValue, T? newValue, string? writer)
    {
        lock (_subscriberLock)
        {
            if (!_subscribers.TryGetValue(key, out var callbacks))
                return;

            var args = new BlackboardValueChangedEventArgs<T>(key, oldValue, newValue, false, writer);

            foreach (var callback in callbacks.OfType<Action<BlackboardValueChangedEventArgs<T>>>().ToList())
            {
                try
                {
                    callback(args);
                }
                catch
                {
                    // 忽略回调异常，避免影响其他订阅者
                }
            }
        }
    }

    private void NotifySubscribersRemoved(string key)
    {
        // 简化处理：键被移除时不通知，订阅者应该在读取时检查是否存在
    }

    #endregion

    #region 过期清理

    /// <summary>手动清理所有过期条目</summary>
    public int CleanupExpiredEntries()
    {
        var expiredKeys = _entries.Where(kvp => kvp.Value.IsExpired)
                                   .Select(kvp => kvp.Key)
                                   .ToList();

        foreach (var key in expiredKeys)
        {
            _entries.TryRemove(key, out _);
        }

        return expiredKeys.Count;
    }

    private void CleanupExpiredEntries(object? state)
    {
        CleanupExpiredEntries();
    }

    #endregion

    #region 调试与诊断

    /// <summary>获取所有键的快照</summary>
    public IReadOnlyList<string> GetAllKeys()
    {
        return _entries.Keys.ToList();
    }

    /// <summary>获取所有条目的调试信息</summary>
    public IReadOnlyList<(string Key, string Value, Type Type, bool IsExpired, float RemainingTime)> GetDebugSnapshot()
    {
        return _entries.Select(kvp => (
            Key: kvp.Key,
            Value: kvp.Value.Value?.ToString() ?? "null",
            Type: kvp.Value.ValueType,
            IsExpired: kvp.Value.IsExpired,
            RemainingTime: kvp.Value.RemainingTime
        )).ToList();
    }

    /// <summary>格式化输出黑板内容</summary>
    public string Dump()
    {
        var snapshot = GetDebugSnapshot();
        var lines = new List<string>
        {
            $"=== Blackboard '{Name}' ({snapshot.Count} entries) ==="
        };

        foreach (var entry in snapshot.OrderBy(e => e.Key))
        {
            var expired = entry.IsExpired ? " [EXPIRED]" : "";
            var ttl = entry.IsExpired ? "" : $" (TTL: {entry.RemainingTime:F1}s)";
            lines.Add($"  {entry.Key}: {entry.Value} ({entry.Type.Name}){ttl}{expired}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    #endregion
}
