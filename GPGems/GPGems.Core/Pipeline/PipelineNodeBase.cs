using System;
using System.Collections.Generic;

namespace GPGems.Core.PipelineHub;

/// <summary>
/// 节点基类 - 提供默认实现，减少样板代码
/// </summary>
public abstract class PipelineNodeBase : IPipelineNode
{
    public abstract string Name { get; }

    public virtual IReadOnlyList<string> Requires => Array.Empty<string>();

    public virtual IReadOnlyList<string> Provides => Array.Empty<string>();

    public virtual ErrorHandlingStrategy OnError => ErrorHandlingStrategy.Terminate;

    public virtual int RetryCount => 0;

    public virtual bool SupportsAsync => false;

    public abstract Dictionary<string, object> Execute(PipelineContext context);

    public virtual bool When(PipelineContext context) => true;

    public virtual void ExecuteAsync(PipelineContext context, Action<Dictionary<string, object>> onComplete)
    {
        // 默认实现：同步执行后调用回调
        var result = Execute(context);
        onComplete(result);
    }

    /// <summary>
    /// 便捷方法：构造输出字典
    /// </summary>
    protected Dictionary<string, object> Output(params (string key, object? value)[] items)
    {
        var result = new Dictionary<string, object>();
        foreach (var (key, value) in items)
        {
            if (value != null)
            {
                result[key] = value;
            }
        }
        return result;
    }
}
