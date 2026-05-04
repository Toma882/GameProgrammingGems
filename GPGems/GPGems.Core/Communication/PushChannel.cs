namespace GPGems.Core;

/// <summary>
/// 数据推送频道
/// 实现"积累-批量处理"模式，支持推送链（链式数据流）
///
/// 设计核心：
/// 1. Push 只积累数据，不立即处理
/// 2. ProcessData 时批量处理所有积累的数据
/// 3. 处理过程中可以继续 Push 新数据，形成推送链
/// 4. 每个订阅者有独立的数据队列
/// </summary>
public class PushChannel
{
    /// <summary>
    /// 处理器组引用
    /// </summary>
    private readonly ProgresserGroup _progresserGroup;

    /// <summary>
    /// 数据队列池: 订阅者 -> (数据类型 -> 数据列表)
    /// </summary>
    private readonly Dictionary<object, Dictionary<string, List<object>>> _dataQueuePool = [];

    /// <summary>
    /// 处理中集合 - 防止递归处理
    /// </summary>
    private readonly HashSet<object> _processingSet = [];

    /// <summary>
    /// 最大迭代次数 - 防止无限推送链
    /// </summary>
    private const int MaxIterations = 16;

    public PushChannel(ProgresserGroup progresserGroup)
    {
        _progresserGroup = progresserGroup;
    }

    /// <summary>
    /// 推送数据（只积累，不处理）
    /// </summary>
    public void PushData(PushDataContext context)
    {
        var subscriber = context.Subscriber;
        var dataType = context.DataType;
        var data = context.Data;

        if (subscriber == null || string.IsNullOrEmpty(dataType)) return;

        // 确保订阅者队列存在
        if (!_dataQueuePool.TryGetValue(subscriber, out var subscriberQueues))
        {
            subscriberQueues = [];
            _dataQueuePool[subscriber] = subscriberQueues;
        }

        // 确保数据类型队列存在
        if (!subscriberQueues.TryGetValue(dataType, out var dataQueue))
        {
            dataQueue = [];
            subscriberQueues[dataType] = dataQueue;
        }

        // 添加数据到队列
        if (data != null)
        {
            dataQueue.Add(data);
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
    /// 弹出并清除订阅者指定类型的所有数据
    /// </summary>
    public List<object> PopAllData(object subscriber, string dataType)
    {
        if (!_dataQueuePool.TryGetValue(subscriber, out var subscriberQueues))
            return [];

        if (!subscriberQueues.TryGetValue(dataType, out var dataQueue))
            return [];

        var result = new List<object>(dataQueue);
        dataQueue.Clear();
        return result;
    }

    /// <summary>
    /// 处理订阅者的所有数据（批量处理）
    /// 支持推送链：处理过程中产生的新数据会继续被处理
    /// </summary>
    public void ProcessData(object subscriber)
    {
        if (subscriber == null) return;

        // 防止递归处理
        if (_processingSet.Contains(subscriber))
            return;

        _processingSet.Add(subscriber);

        try
        {
            var handlers = _progresserGroup.GetHandlers(subscriber);
            int iteration = 0;

            do
            {
                bool hasData = false;

                foreach (var handlerContext in handlers)
                {
                    if (HasPendingData(handlerContext))
                    {
                        hasData = true;
                        ProcessHandler(handlerContext);
                    }
                }

                iteration++;

                // 如果还有数据且没达到最大迭代次数，继续处理
                if (!hasData || iteration >= MaxIterations)
                    break;

            } while (true);

            if (iteration >= MaxIterations)
            {
                System.Diagnostics.Debug.WriteLine($"PushChannel:ProcessData 达到最大迭代次数 {MaxIterations}，可能存在无限推送链");
            }
        }
        finally
        {
            _processingSet.Remove(subscriber);
        }
    }

    /// <summary>
    /// 检查处理器是否有待处理数据
    /// </summary>
    private bool HasPendingData(ProgressHandlerContext handlerContext)
    {
        var subscriber = handlerContext.Subscriber;
        var processFunctionMap = handlerContext.ProcessFunctionMap;

        foreach (var dataType in processFunctionMap.Keys)
        {
            if (GetDataQueueSize(subscriber, dataType) > 0)
                return true;
        }

        return false;
    }

    /// <summary>
    /// 处理单个处理器
    /// </summary>
    private void ProcessHandler(ProgressHandlerContext handlerContext)
    {
        var handler = handlerContext.Handler;
        var subscriber = handlerContext.Subscriber;
        var processFunctionMap = handlerContext.ProcessFunctionMap;

        // 检查是否需要处理（IsDirty 机制）
        bool isDirty = true;

        // 如果处理器有 IsDirty 属性或方法，调用它
        // 支持：1) bool IsDirty 属性  2) bool IsDirty() 方法  3) Func<bool> IsDirty 委托属性
        if (handler != null)
        {
            var handlerType = handler.GetType();

            // 尝试：1. IsDirty 属性
            var isDirtyProp = handlerType.GetProperty("IsDirty");
            if (isDirtyProp != null)
            {
                var result = isDirtyProp.GetValue(handler);
                if (result is bool b)
                    isDirty = b;
                // 如果是 Func<bool> 委托，调用它
                else if (result is Func<bool> func)
                    isDirty = func();
            }
            else
            {
                // 尝试：2. IsDirty() 方法
                var isDirtyMethod = handlerType.GetMethod("IsDirty", Type.EmptyTypes);
                if (isDirtyMethod != null)
                {
                    var result = isDirtyMethod.Invoke(handler, null);
                    if (result is bool b)
                        isDirty = b;
                }
            }
        }

        if (!isDirty)
            return;

        // 处理每种数据类型
        foreach (var (dataType, processFunc) in processFunctionMap)
        {
            var queueSize = GetDataQueueSize(subscriber, dataType);
            if (queueSize > 0)
            {
                var allData = PopAllData(subscriber, dataType);
                foreach (var data in allData)
                {
                    processFunc(data);
                }

                // 调用 MarkDirty
                if (handler != null)
                {
                    var markDirtyMethod = handler.GetType().GetMethod("MarkDirty");
                    markDirtyMethod?.Invoke(handler, null);
                }
            }
        }
    }

    /// <summary>
    /// 获取数据队列大小
    /// </summary>
    public int GetDataQueueSize(object subscriber, string dataType)
    {
        if (!_dataQueuePool.TryGetValue(subscriber, out var subscriberQueues))
            return 0;

        if (!subscriberQueues.TryGetValue(dataType, out var dataQueue))
            return 0;

        return dataQueue.Count;
    }

    /// <summary>
    /// 清空指定订阅者的数据队列
    /// </summary>
    public void ClearSubscriber(object subscriber)
    {
        _dataQueuePool.Remove(subscriber);
        _processingSet.Remove(subscriber);
    }

    /// <summary>
    /// 清空所有数据队列
    /// </summary>
    public void Clear()
    {
        _dataQueuePool.Clear();
        _processingSet.Clear();
    }
}
