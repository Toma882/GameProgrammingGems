namespace GPGems.Core;

/// <summary>
/// 消息频道
/// 实现点对点模式（一对一），后注册覆盖先注册
/// </summary>
public class MessageChannel
{
    /// <summary>
    /// 消息处理器字典：消息ID -> 处理函数
    /// </summary>
    private readonly Dictionary<string, Action<object?>> _handlers = new();

    /// <summary>
    /// 发送消息（点对点）
    /// </summary>
    public void Send(string messageId, object? args = null)
    {
        if (_handlers.TryGetValue(messageId, out var handler))
        {
            try
            {
                handler(args);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MessageChannel handler error [{messageId}]: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 注册消息处理器（点对点，覆盖模式）
    /// </summary>
    public void RegisterHandler(string messageId, Action<object?> handler)
    {
        _handlers[messageId] = handler;
    }

    /// <summary>
    /// 取消注册消息处理器
    /// </summary>
    public void UnregisterHandler(string messageId)
    {
        _handlers.Remove(messageId);
    }

    /// <summary>
    /// 清空所有处理器
    /// </summary>
    public void Clear()
    {
        _handlers.Clear();
    }
}
