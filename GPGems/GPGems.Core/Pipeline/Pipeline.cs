using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace GPGems.Core.PipelineHub;

/// <summary>
/// 管线 - 纯数据驱动的流程编排
///
/// 核心设计原则：
/// 1. 管线无知原则：管线只负责"流"，不知道为什么流、流的是什么
/// 2. 节点无知原则：节点只知道 requires/provides/when/execute，不知道其他节点
/// 3. 数据契约：requires = 输入，provides = 输出，代码即文档
/// 4. 原子节点：节点可以启动子管线，这是原生能力，不需要框架特殊支持
///
/// 通讯集成：
/// - 节点直接访问 CommunicationBus.Instance（全局单例）
/// - 副作用隔离：通过 subscriber（可以是 context 或 pipeline）实现
/// - PushChannel 积累-批量处理：节点 PushData，管线执行完统一 ProcessData
/// </summary>
public class Pipeline
{
    /// <summary>管线类型标识</summary>
    public string PipelineType { get; }

    /// <summary>已注册的节点池</summary>
    private readonly Dictionary<string, IPipelineNode> _nodeRegistry = new();

    /// <summary>执行列表：可以是节点名（string）或分支对象（IPipelineBranch）</summary>
    private readonly List<object> _executionList = new();

    public Pipeline(string pipelineType)
    {
        PipelineType = pipelineType;
    }

    #region 节点注册

    /// <summary>
    /// 注册单个节点
    /// </summary>
    public void RegisterNode(IPipelineNode node)
    {
        _nodeRegistry[node.Name] = node;
    }

    /// <summary>
    /// 批量注册节点
    /// </summary>
    public void RegisterNodes(IEnumerable<IPipelineNode> nodes)
    {
        foreach (var node in nodes)
        {
            _nodeRegistry[node.Name] = node;
        }
    }

    #endregion

    #region 管线配置

    /// <summary>
    /// 添加普通节点到执行序列
    /// </summary>
    public void AddNode(string nodeName)
    {
        _executionList.Add(nodeName);
    }

    /// <summary>
    /// 批量添加普通节点
    /// </summary>
    public void AddNodes(IEnumerable<string> nodeNames)
    {
        foreach (var name in nodeNames)
        {
            _executionList.Add(name);
        }
    }

    /// <summary>
    /// 添加分支到执行序列
    /// </summary>
    public void AddBranch(IPipelineBranch branch)
    {
        _executionList.Add(branch);
    }

    #endregion

    #region 执行引擎

    /// <summary>
    /// 同步执行整个管线
    /// </summary>
    public PipelineResult Execute(object? subject = null, Dictionary<string, object>? initialData = null)
    {
        var context = new PipelineContext(subject);
        if (initialData != null)
        {
            context.SetInitialData(initialData);
        }

        var result = new PipelineResult();
        var sw = Stopwatch.StartNew();

        foreach (var item in _executionList)
        {
            IPipelineNode? nodeToExecute = null;

            if (item is string nodeName)
            {
                // 普通节点：检查 When
                if (_nodeRegistry.TryGetValue(nodeName, out var node))
                {
                    if (node.When(context))
                    {
                        nodeToExecute = node;
                    }
                    else
                    {
                        // 跳过
                        result.ExecutedNodes.Add(new NodeExecutionLog
                        {
                            NodeName = nodeName,
                            Skipped = true,
                            Success = true,
                            Reason = "When condition not met"
                        });
                        continue;
                    }
                }
            }
            else if (item is IPipelineBranch branch)
            {
                // 分支：由分支选择要执行的节点
                nodeToExecute = branch.SelectNode(context, name =>
                    _nodeRegistry.TryGetValue(name, out var n) ? n : null);

                if (nodeToExecute == null)
                {
                    // 分支没有匹配的节点
                    result.ExecutedNodes.Add(new NodeExecutionLog
                    {
                        NodeName = branch.Name,
                        Skipped = true,
                        Success = true,
                        Reason = "No branch condition matched"
                    });
                    continue;
                }
            }

            if (nodeToExecute != null)
            {
                // 执行节点
                var executionResult = ExecuteNode(nodeToExecute, context);
                result.ExecutedNodes.Add(executionResult);

                // 错误处理
                if (!executionResult.Success && nodeToExecute.OnError == ErrorHandlingStrategy.Terminate)
                {
                    result.Success = false;
                    break;
                }
            }
        }

        sw.Stop();
        result.DurationMs = (int)sw.ElapsedMilliseconds;
        result.Success = result.Success || result.ExecutedNodes.TrueForAll(n => n.Success);
        return result;
    }

    /// <summary>
    /// 执行单个节点（含重试逻辑）
    /// </summary>
    private NodeExecutionLog ExecuteNode(IPipelineNode node, PipelineContext context)
    {
        var log = new NodeExecutionLog { NodeName = node.Name };
        var sw = Stopwatch.StartNew();

        int maxAttempts = node.OnError == ErrorHandlingStrategy.Retry ? node.RetryCount + 1 : 1;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            try
            {
                // 检查前置数据
                if (!context.HasAllData(node.Requires))
                {
                    log.Success = false;
                    log.Reason = $"Missing required data: {string.Join(", ", node.Requires)}";
                    continue;
                }

                // 执行
                var output = node.Execute(context);

                // 保存输出
                context.SaveOutputFromNode(node, output);

                log.Success = true;
                break;
            }
            catch (Exception ex)
            {
                log.Success = false;
                log.Reason = ex.Message;

                // 不是最后一次尝试，继续重试
                if (attempt < maxAttempts - 1)
                {
                    continue;
                }

                // 最后一次尝试失败
                if (node.OnError == ErrorHandlingStrategy.Skip)
                {
                    log.Skipped = true;
                }
            }
        }

        sw.Stop();
        log.DurationMs = (int)sw.ElapsedMilliseconds;
        return log;
    }

    #endregion

    /// <summary>
    /// 查找已注册的节点
    /// </summary>
    public IPipelineNode? GetNode(string name)
    {
        return _nodeRegistry.TryGetValue(name, out var node) ? node : null;
    }
}

/// <summary>
/// 管线执行结果
/// </summary>
public class PipelineResult
{
    public bool Success { get; set; }
    public int DurationMs { get; set; }
    public List<NodeExecutionLog> ExecutedNodes { get; } = new();
}

/// <summary>
/// 节点执行日志
/// </summary>
public class NodeExecutionLog
{
    public string NodeName { get; set; } = string.Empty;
    public bool Success { get; set; }
    public bool Skipped { get; set; }
    public int DurationMs { get; set; }
    public string? Reason { get; set; }
}
