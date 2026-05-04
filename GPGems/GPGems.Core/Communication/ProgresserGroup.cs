namespace GPGems.Core;

/// <summary>
/// 处理器上下文 - 包装处理器和处理函数映射
/// </summary>
public class ProgressHandlerContext
{
    /// <summary>
    /// 订阅者对象
    /// </summary>
    public object Subscriber { get; set; } = null!;

    /// <summary>
    /// 处理器对象（支持 IsDirty 检查）
    /// </summary>
    public object? Handler { get; set; }

    /// <summary>
    /// 数据类型 -> 处理函数的映射
    /// </summary>
    public Dictionary<string, Action<object>> ProcessFunctionMap { get; set; } = new();
}

/// <summary>
/// 查询委托上下文
/// </summary>
public class QueryDelegateContext
{
    /// <summary>
    /// 订阅者对象
    /// </summary>
    public object Subscriber { get; set; } = null!;

    /// <summary>
    /// 数据类型
    /// </summary>
    public string DataType { get; set; } = string.Empty;

    /// <summary>
    /// 查询函数
    /// </summary>
    public Func<object?[], object?> Func { get; set; } = null!;
}

/// <summary>
/// 处理器组 - 管理所有订阅者、处理器和查询委托
/// 这是 PushChannel 和 QueryChannel 共享的核心管理组件
/// </summary>
public class ProgresserGroup
{
    /// <summary>
    /// 订阅者集合
    /// </summary>
    private readonly HashSet<object> _subscriberSet = new();

    /// <summary>
    /// 订阅者 -> 处理器列表 映射
    /// </summary>
    private readonly Dictionary<object, List<ProgressHandlerContext>> _linkHandlerMap = new();

    /// <summary>
    /// 订阅者 -> (数据类型 -> 查询委托) 映射
    /// </summary>
    private readonly Dictionary<object, Dictionary<string, Func<object?[], object?>>> _queryDelegates = new();

    /// <summary>
    /// 订阅
    /// </summary>
    public void Subscribe(object subscriber)
    {
        if (subscriber == null) return;
        _subscriberSet.Add(subscriber);
    }

    /// <summary>
    /// 取消订阅
    /// </summary>
    public void Unsubscribe(object subscriber)
    {
        if (subscriber == null) return;
        _subscriberSet.Remove(subscriber);
        _linkHandlerMap.Remove(subscriber);
        _queryDelegates.Remove(subscriber);
    }

    /// <summary>
    /// 添加处理器
    /// </summary>
    public void AddHandler(ProgressHandlerContext context)
    {
        if (context.Subscriber == null) return;

        // 自动订阅
        if (!_subscriberSet.Contains(context.Subscriber))
        {
            Subscribe(context.Subscriber);
        }

        if (!_linkHandlerMap.TryGetValue(context.Subscriber, out var handlers))
        {
            handlers = new List<ProgressHandlerContext>();
            _linkHandlerMap[context.Subscriber] = handlers;
        }

        handlers.Add(context);
    }

    /// <summary>
    /// 获取订阅者的所有处理器
    /// </summary>
    public List<ProgressHandlerContext> GetHandlers(object subscriber)
    {
        return _linkHandlerMap.TryGetValue(subscriber, out var handlers)
            ? handlers
            : new List<ProgressHandlerContext>();
    }

    /// <summary>
    /// 移除处理器
    /// </summary>
    public void RemoveHandler(object subscriber, object handler)
    {
        if (!_linkHandlerMap.TryGetValue(subscriber, out var handlers)) return;

        for (int i = handlers.Count - 1; i >= 0; i--)
        {
            if (handlers[i].Handler == handler)
            {
                handlers.RemoveAt(i);
                return;
            }
        }
    }

    /// <summary>
    /// 添加查询委托
    /// </summary>
    public void AddQueryDelegate(QueryDelegateContext context)
    {
        if (context.Subscriber == null || string.IsNullOrEmpty(context.DataType) || context.Func == null)
            return;

        if (!_queryDelegates.TryGetValue(context.Subscriber, out var delegateMap))
        {
            delegateMap = new Dictionary<string, Func<object?[], object?>>();
            _queryDelegates[context.Subscriber] = delegateMap;
        }

        if (delegateMap.ContainsKey(context.DataType))
        {
            System.Diagnostics.Debug.WriteLine($"ProgresserGroup:AddQueryDelegate dataType: {context.DataType} 已经存在委托");
        }

        delegateMap[context.DataType] = context.Func;
    }

    /// <summary>
    /// 获取查询委托
    /// </summary>
    public Func<object?[], object?>? GetQueryDelegate(QueryDelegateContext context)
    {
        if (!_queryDelegates.TryGetValue(context.Subscriber, out var delegateMap))
            return null;

        delegateMap.TryGetValue(context.DataType, out var func);
        return func;
    }

    /// <summary>
    /// 检查订阅者是否有指定数据类型的待处理数据
    /// </summary>
    public bool HasPendingData(object subscriber, string dataType)
    {
        // 这个方法由 PushChannel 调用，用于检查队列是否有数据
        return true; // 实际检查在 PushChannel 中进行
    }

    /// <summary>
    /// 清空所有数据
    /// </summary>
    public void Clear()
    {
        _subscriberSet.Clear();
        _linkHandlerMap.Clear();
        _queryDelegates.Clear();
    }
}
