using System;
using System.Collections.Generic;

namespace GPGems.Core.PipelineHub;

/// <summary>
/// 管线节点接口 - 节点无知原则
/// 节点只知道：需要什么(requires)、产出什么(provides)、什么时候执行(when)、怎么执行(execute)
/// 完全不知道其他节点的存在，也不知道管线结构
/// </summary>
public interface IPipelineNode
{
    /// <summary>节点名称</summary>
    string Name { get; }

    /// <summary>需要的输入数据键列表</summary>
    IReadOnlyList<string> Requires { get; }

    /// <summary>产出的输出数据键列表</summary>
    IReadOnlyList<string> Provides { get; }

    /// <summary>错误处理策略</summary>
    ErrorHandlingStrategy OnError { get; }

    /// <summary>重试次数（OnError = Retry 时有效）</summary>
    int RetryCount { get; }

    /// <summary>是否支持异步执行</summary>
    bool SupportsAsync { get; }

    /// <summary>
    /// 条件检查 - 决定这个节点是否需要执行
    /// </summary>
    bool When(PipelineContext context);

    /// <summary>
    /// 同步执行节点逻辑
    /// </summary>
    Dictionary<string, object> Execute(PipelineContext context);

    /// <summary>
    /// 异步执行节点逻辑
    /// </summary>
    void ExecuteAsync(PipelineContext context, Action<Dictionary<string, object>> onComplete);
}

/// <summary>
/// 分支接口 - 用于互斥分支等特殊流程控制
/// </summary>
public interface IPipelineBranch
{
    /// <summary>分支名称</summary>
    string Name { get; }

    /// <summary>
    /// 选择要执行的节点（纯同步，只做条件检查）
    /// </summary>
    IPipelineNode? SelectNode(PipelineContext context, Func<string, IPipelineNode?> nodeResolver);
}

/// <summary>
/// 错误处理策略
/// </summary>
public enum ErrorHandlingStrategy
{
    /// <summary>跳过错误，继续执行下一个节点</summary>
    Skip,
    /// <summary>终止整个管线</summary>
    Terminate,
    /// <summary>重试（最多 RetryCount 次）</summary>
    Retry,
}
