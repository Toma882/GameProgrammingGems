/*
 * GPGems.AI - GOAP: World State
 * 世界状态：符号化的键值对表示
 */

namespace GPGems.AI.Decision.GOAP;

/// <summary>
/// 世界状态：键值对集合
/// </summary>
public class WorldState : IEquatable<WorldState>
{
    private readonly Dictionary<string, object> _values = new();

    public WorldState() { }

    public WorldState(WorldState other)
    {
        foreach (var (key, value) in other._values)
        {
            _values[key] = value;
        }
    }

    /// <summary>
    /// 设置状态值
    /// </summary>
    public WorldState Set(string key, object value)
    {
        _values[key] = value;
        return this;
    }

    /// <summary>
    /// 设置布尔状态值（简写）
    /// </summary>
    public WorldState Set(string key, bool value)
    {
        _values[key] = value;
        return this;
    }

    /// <summary>
    /// 设置浮点状态值（简写）
    /// </summary>
    public WorldState Set(string key, float value)
    {
        _values[key] = value;
        return this;
    }

    /// <summary>
    /// 获取状态值
    /// </summary>
    public T? Get<T>(string key)
    {
        if (_values.TryGetValue(key, out var value) && value is T typed)
            return typed;
        return default;
    }

    /// <summary>
    /// 尝试获取状态值
    /// </summary>
    public bool TryGet<T>(string key, out T value)
    {
        if (_values.TryGetValue(key, out var obj) && obj is T typed)
        {
            value = typed;
            return true;
        }
        value = default!;
        return false;
    }

    /// <summary>
    /// 检查是否满足条件
    /// </summary>
    public bool MeetsCondition(WorldState condition)
    {
        foreach (var (key, required) in condition._values)
        {
            if (!_values.TryGetValue(key, out var actual))
                return false;

            if (!Equals(actual, required))
                return false;
        }
        return true;
    }

    /// <summary>
    /// 应用效果到当前状态
    /// </summary>
    public void ApplyEffect(WorldState effect)
    {
        foreach (var (key, value) in effect._values)
        {
            _values[key] = value;
        }
    }

    /// <summary>
    /// 计算与目标状态的不匹配数量（启发式函数）
    /// </summary>
    public int CountMismatches(WorldState target)
    {
        var count = 0;
        foreach (var (key, required) in target._values)
        {
            if (!_values.TryGetValue(key, out var actual) || !Equals(actual, required))
                count++;
        }
        return count;
    }

    /// <summary>
    /// 获取所有键值对
    /// </summary>
    public IEnumerable<KeyValuePair<string, object>> GetAll() => _values;

    public bool Equals(WorldState? other)
    {
        if (other == null) return false;
        if (_values.Count != other._values.Count) return false;

        foreach (var (key, value) in _values)
        {
            if (!other._values.TryGetValue(key, out var otherValue))
                return false;
            if (!Equals(value, otherValue))
                return false;
        }
        return true;
    }

    public override bool Equals(object? obj) => obj is WorldState state && Equals(state);

    public override int GetHashCode()
    {
        var hash = 0;
        foreach (var (key, value) in _values.OrderBy(kv => kv.Key))
        {
            hash = HashCode.Combine(hash, key.GetHashCode(), value?.GetHashCode() ?? 0);
        }
        return hash;
    }

    public override string ToString()
    {
        var pairs = _values.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}={kv.Value}");
        return $"{{{string.Join(", ", pairs)}}}";
    }
}
