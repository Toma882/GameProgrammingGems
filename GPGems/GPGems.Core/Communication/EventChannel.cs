namespace GPGems.Core;

/// <summary>
/// 事件频道
/// 实现发布-订阅模式（一对多）
/// </summary>
public class EventChannel
{
    /// <summary>
    /// 事件监听器字典：事件类型 -> 处理函数列表
    /// </summary>
    private readonly Dictionary<string, List<Action<object?>>> _subscribers = new();

    /// <summary>
    /// 发布事件（多播）
    /// </summary>
    public void Publish(string eventType, object? args = null)
    {
        if (_subscribers.TryGetValue(eventType, out var handlers))
        {
            foreach (var handler in handlers)
            {
                try
                {
                    handler(args);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Event handler error [{eventType}]: {ex.Message}");
                }
            }
        }
    }

    /// <summary>
    /// 订阅事件
    /// </summary>
    public void Subscribe(string eventType, Action<object?> handler)
    {
        if (!_subscribers.TryGetValue(eventType, out var handlers))
        {
            handlers = new List<Action<object?>>();
            _subscribers[eventType] = handlers;
        }

        if (!handlers.Contains(handler))
        {
            handlers.Add(handler);
        }
    }

    /// <summary>
    /// 取消订阅事件
    /// </summary>
    public void Unsubscribe(string eventType, Action<object?> handler)
    {
        if (_subscribers.TryGetValue(eventType, out var handlers))
        {
            handlers.Remove(handler);
        }
    }

    /// <summary>
    /// 清空所有订阅者
    /// </summary>
    public void Clear()
    {
        _subscribers.Clear();
    }
}
