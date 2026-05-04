namespace GPGems.Core;

/// <summary>
/// 查询频道
/// 基于 ProgresserGroup 管理查询委托，每个订阅者独立
/// </summary>
public class QueryChannel
{
    /// <summary>
    /// 处理器组引用
    /// </summary>
    private readonly ProgresserGroup _progresserGroup;

    public QueryChannel(ProgresserGroup progresserGroup)
    {
        _progresserGroup = progresserGroup;
    }

    /// <summary>
    /// 查询数据
    /// </summary>
    public object? QueryData(QueryDelegateContext context, params object?[] args)
    {
        var func = _progresserGroup.GetQueryDelegate(context);
        return func != null ? func(args) : null;
    }

    /// <summary>
    /// 泛型便捷方法：查询数据
    /// </summary>
    public T? QueryData<T>(object subscriber, string dataType, params object?[] args)
    {
        var context = new QueryDelegateContext
        {
            Subscriber = subscriber,
            DataType = dataType
        };

        var result = QueryData(context, args);
        return result == null ? default : (T)result;
    }

    /// <summary>
    /// 清空（由 ProgresserGroup 管理）
    /// </summary>
    public void Clear()
    {
        // QueryChannel 不持有独立状态，由 ProgresserGroup 统一管理
    }
}
