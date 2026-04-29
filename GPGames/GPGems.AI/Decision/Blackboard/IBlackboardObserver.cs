/*
 * GPGems.AI - Blackboard System
 * IBlackboardObserver: 观察者模式接口
 * 支持订阅键变化通知
 */

namespace GPGems.AI.Decision.Blackboards;

/// <summary>
/// 黑板值变化事件参数
/// </summary>
/// <typeparam name="T">值类型</typeparam>
public class BlackboardValueChangedEventArgs<T> : EventArgs
{
    /// <summary>发生变化的键名</summary>
    public string Key { get; }

    /// <summary>旧值</summary>
    public T? OldValue { get; }

    /// <summary>新值</summary>
    public T? NewValue { get; }

    /// <summary>值是否刚过期</summary>
    public bool IsExpired { get; }

    /// <summary>写入者标识</summary>
    public string? Writer { get; }

    public BlackboardValueChangedEventArgs(string key, T? oldValue, T? newValue, bool isExpired = false, string? writer = null)
    {
        Key = key;
        OldValue = oldValue;
        NewValue = newValue;
        IsExpired = isExpired;
        Writer = writer;
    }
}

/// <summary>
/// 黑板观察者接口
/// </summary>
public interface IBlackboardObserver
{
    /// <summary>订阅键变化通知</summary>
    /// <typeparam name="T">值类型</typeparam>
    /// <param name="key">要监听的键名</param>
    /// <param name="callback">回调函数</param>
    /// <returns>订阅令牌，用于取消订阅</returns>
    IDisposable Subscribe<T>(string key, Action<BlackboardValueChangedEventArgs<T>> callback);

    /// <summary>取消订阅</summary>
    /// <param name="subscriptionToken">订阅时返回的令牌</param>
    void Unsubscribe(IDisposable subscriptionToken);

    /// <summary>取消指定键的所有订阅</summary>
    void UnsubscribeAll(string key);

    /// <summary>取消所有订阅</summary>
    void UnsubscribeAll();
}

/// <summary>
/// 订阅令牌实现
/// </summary>
internal class BlackboardSubscriptionToken : IDisposable
{
    private readonly Action _onDispose;
    private bool _disposed;

    public string Key { get; }
    public Type ValueType { get; }

    public BlackboardSubscriptionToken(string key, Type valueType, Action onDispose)
    {
        Key = key;
        ValueType = valueType;
        _onDispose = onDispose ?? throw new ArgumentNullException(nameof(onDispose));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _onDispose();
    }
}
