/*
 * GPGems.AI - Blackboard System
 * BlackboardEntry: 黑板条目封装
 * 支持 TTL 过期、时间戳、优先级
 */

namespace GPGems.AI.Decision.Blackboards;

/// <summary>
/// 黑板条目
/// 封装值及其元数据（过期时间、优先级、写入时间）
/// </summary>
public class BlackboardEntry
{
    /// <summary>条目标识键</summary>
    public string Key { get; }

    /// <summary>存储的值（装箱）</summary>
    public object? Value { get; private set; }

    /// <summary>值的类型</summary>
    public Type ValueType { get; private set; }

    /// <summary>生存时间（秒），-1 表示永不过期</summary>
    public float TTL { get; private set; }

    /// <summary>写入时间戳（秒）</summary>
    public float WriteTime { get; private set; }

    /// <summary>优先级，用于冲突解决</summary>
    public int Priority { get; private set; }

    /// <summary>写入者标识，用于调试</summary>
    public string? Writer { get; private set; }

    /// <summary>是否已过期</summary>
    public bool IsExpired => TTL > 0 && (CurrentTime - WriteTime) > TTL;

    /// <summary>剩余有效时间（秒）</summary>
    public float RemainingTime => TTL < 0 ? float.MaxValue : Math.Max(0, TTL - (CurrentTime - WriteTime));

    /// <summary>当前时间注入点（便于单元测试）</summary>
    public static Func<float> CurrentTimeProvider { get; set; } = () =>
        (float)(DateTime.UtcNow - DateTime.UnixEpoch).TotalSeconds;

    private static float CurrentTime => CurrentTimeProvider();

    public BlackboardEntry(string key, Type valueType)
    {
        Key = key ?? throw new ArgumentNullException(nameof(key));
        ValueType = valueType ?? throw new ArgumentNullException(nameof(valueType));
        TTL = -1;
        Priority = 0;
        WriteTime = CurrentTime;
    }

    /// <summary>更新值及元数据</summary>
    public void UpdateValue(object? value, float ttl = -1, int priority = 0, string? writer = null)
    {
        if (value != null && value.GetType() != ValueType)
            throw new InvalidCastException($"Blackboard key '{Key}' expects type {ValueType.Name}, got {value.GetType().Name}");

        Value = value;
        TTL = ttl;
        Priority = priority;
        Writer = writer;
        WriteTime = CurrentTime;
    }

    /// <summary>尝试获取类型安全的值</summary>
    public bool TryGetValue<T>(out T? value)
    {
        if (IsExpired)
        {
            value = default;
            return false;
        }

        if (Value is T typedValue)
        {
            value = typedValue;
            return true;
        }

        value = default;
        return false;
    }

    /// <summary>刷新条目的写入时间</summary>
    public void Touch()
    {
        WriteTime = CurrentTime;
    }

    public override string ToString()
    {
        var expired = IsExpired ? "[EXPIRED]" : "";
        var writer = string.IsNullOrEmpty(Writer) ? "" : $" by {Writer}";
        return $"'{Key}' = {Value} ({ValueType.Name}){writer}{expired}";
    }
}
