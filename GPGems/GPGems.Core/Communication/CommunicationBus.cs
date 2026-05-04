namespace GPGems.Core;

/// <summary>
/// 频道类型
/// </summary>
public enum ChannelType
{
    Event,
    Message,
    Push,
    Query
}

/// <summary>
/// 广播订阅上下文
/// </summary>
public class BroadcastSubscribeContext
{
    public ChannelType ChannelType { get; set; }
    public string BroadcastId { get; set; } = string.Empty;
    public object? Subscriber { get; set; }
    public Action<object?> SubscribeFunction { get; set; } = null!;
}

/// <summary>
/// 广播上下文
/// </summary>
public class BroadcastContext
{
    public ChannelType ChannelType { get; set; }
    public string BroadcastId { get; set; } = string.Empty;
}

/// <summary>
/// 通讯总线
/// 统一管理所有频道类型，提供统一的接口
///
/// 包含四个核心频道：
/// 1. EventChannel - 发布-订阅模式，一对多
/// 2. MessageChannel - 点对点模式，一对一
/// 3. PushChannel - 数据推送模式，积累-批量处理
/// 4. QueryChannel - 请求-响应模式，同步查询
/// </summary>
public class CommunicationBus
{
    /// <summary>
    /// 全局单例
    /// </summary>
    public static CommunicationBus Instance { get; } = new();

    /// <summary>
    /// 处理器组（Push 和 Query 共享）
    /// </summary>
    private readonly ProgresserGroup _progresserGroup = new();

    /// <summary>
    /// 频道字典
    /// </summary>
    private readonly Dictionary<ChannelType, object> _channels = new();

    private CommunicationBus()
    {
        // 注册所有频道
        _channels[ChannelType.Event] = new EventChannel();
        _channels[ChannelType.Message] = new MessageChannel();
        _channels[ChannelType.Push] = new PushChannel(_progresserGroup);
        _channels[ChannelType.Query] = new QueryChannel(_progresserGroup);
    }

    #region Event Channel - 事件通道（发布-订阅）

    /// <summary>
    /// 发布事件
    /// </summary>
    public void Publish(string eventType, object? args = null)
    {
        if (_channels.TryGetValue(ChannelType.Event, out var channel) && channel is EventChannel eventChannel)
        {
            eventChannel.Publish(eventType, args);
        }
    }

    /// <summary>
    /// 订阅事件
    /// </summary>
    public void Subscribe(string eventType, Action<object?> handler)
    {
        if (_channels.TryGetValue(ChannelType.Event, out var channel) && channel is EventChannel eventChannel)
        {
            eventChannel.Subscribe(eventType, handler);
        }
    }

    /// <summary>
    /// 取消订阅事件
    /// </summary>
    public void Unsubscribe(string eventType, Action<object?> handler)
    {
        if (_channels.TryGetValue(ChannelType.Event, out var channel) && channel is EventChannel eventChannel)
        {
            eventChannel.Unsubscribe(eventType, handler);
        }
    }

    #endregion

    #region Message Channel - 消息通道（点对点）

    /// <summary>
    /// 发送消息
    /// </summary>
    public void SendMessage(string messageId, object? args = null)
    {
        if (_channels.TryGetValue(ChannelType.Message, out var channel) && channel is MessageChannel messageChannel)
        {
            messageChannel.Send(messageId, args);
        }
    }

    /// <summary>
    /// 接收消息（注册处理器）
    /// </summary>
    public void RegisterMessageHandler(string messageId, Action<object?> handler)
    {
        if (_channels.TryGetValue(ChannelType.Message, out var channel) && channel is MessageChannel messageChannel)
        {
            messageChannel.RegisterHandler(messageId, handler);
        }
    }

    /// <summary>
    /// 取消注册消息处理器
    /// </summary>
    public void UnregisterMessageHandler(string messageId)
    {
        if (_channels.TryGetValue(ChannelType.Message, out var channel) && channel is MessageChannel messageChannel)
        {
            messageChannel.UnregisterHandler(messageId);
        }
    }

    #endregion

    #region Push Channel - 推送通道（积累-批量处理）

    /// <summary>
    /// 订阅推送处理（在 ProgresserGroup 中注册）
    /// </summary>
    public void SubscribeProgress(object subscriber)
    {
        _progresserGroup.Subscribe(subscriber);
    }

    /// <summary>
    /// 取消订阅推送处理
    /// </summary>
    public void UnsubscribeProgress(object subscriber)
    {
        _progresserGroup.Unsubscribe(subscriber);
        if (_channels.TryGetValue(ChannelType.Push, out var channel) && channel is PushChannel pushChannel)
        {
            pushChannel.ClearSubscriber(subscriber);
        }
    }

    /// <summary>
    /// 添加推送处理器
    /// </summary>
    public void AddHandler(ProgressHandlerContext context)
    {
        _progresserGroup.AddHandler(context);
    }

    /// <summary>
    /// 推送数据（只积累，不立即处理）
    /// </summary>
    public void PushData(PushDataContext context)
    {
        if (_channels.TryGetValue(ChannelType.Push, out var channel) && channel is PushChannel pushChannel)
        {
            pushChannel.PushData(context);
        }
    }

    /// <summary>
    /// 便捷方法：推送数据
    /// </summary>
    public void PushData(object subscriber, string dataType, object? data = null)
    {
        PushData(new PushDataContext
        {
            Subscriber = subscriber,
            DataType = dataType,
            Data = data
        });
    }

    /// <summary>
    /// 处理订阅者的所有推送数据（批量处理）
    /// </summary>
    public void ProcessData(object subscriber)
    {
        if (_channels.TryGetValue(ChannelType.Push, out var channel) && channel is PushChannel pushChannel)
        {
            pushChannel.ProcessData(subscriber);
        }
    }

    #endregion

    #region Query Channel - 查询通道（请求-响应）

    /// <summary>
    /// 添加查询委托
    /// </summary>
    public void AddQueryDelegate(QueryDelegateContext context)
    {
        _progresserGroup.AddQueryDelegate(context);
    }

    /// <summary>
    /// 便捷方法：添加查询委托
    /// </summary>
    public void AddQueryDelegate(object subscriber, string dataType, Func<object?[], object?> func)
    {
        AddQueryDelegate(new QueryDelegateContext
        {
            Subscriber = subscriber,
            DataType = dataType,
            Func = func
        });
    }

    /// <summary>
    /// 查询数据
    /// </summary>
    public object? QueryData(QueryDelegateContext context, params object?[] args)
    {
        if (_channels.TryGetValue(ChannelType.Query, out var channel) && channel is QueryChannel queryChannel)
        {
            return queryChannel.QueryData(context, args);
        }
        return null;
    }

    /// <summary>
    /// 泛型便捷方法：查询数据
    /// </summary>
    public T? QueryData<T>(object subscriber, string dataType, params object?[] args)
    {
        if (_channels.TryGetValue(ChannelType.Query, out var channel) && channel is QueryChannel queryChannel)
        {
            return queryChannel.QueryData<T>(subscriber, dataType, args);
        }
        return default;
    }

    #endregion

    /// <summary>
    /// 清空所有频道
    /// </summary>
    public void Clear()
    {
        // 清空 EventChannel
        if (_channels.TryGetValue(ChannelType.Event, out var eventChannelObj) && eventChannelObj is EventChannel eventChannel)
        {
            eventChannel.Clear();
        }

        // 清空 MessageChannel
        if (_channels.TryGetValue(ChannelType.Message, out var messageChannelObj) && messageChannelObj is MessageChannel messageChannel)
        {
            messageChannel.Clear();
        }

        // 清空 PushChannel
        if (_channels.TryGetValue(ChannelType.Push, out var pushChannelObj) && pushChannelObj is PushChannel pushChannel)
        {
            pushChannel.Clear();
        }

        // 清空 ProgresserGroup（包含 Query 的委托）
        _progresserGroup.Clear();
    }

    /// <summary>
    /// 获取指定类型的频道（用于高级操作）
    /// </summary>
    public T? GetChannel<T>(ChannelType channelType) where T : class
    {
        if (_channels.TryGetValue(channelType, out var channel) && channel is T typedChannel)
        {
            return typedChannel;
        }
        return null;
    }
}
