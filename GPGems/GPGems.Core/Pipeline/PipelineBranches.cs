using System;
using System.Collections.Generic;

namespace GPGems.Core.PipelineHub;

/// <summary>
/// 互斥分支 - switch-case 等效结构
/// 按顺序检查条件，执行第一个匹配的节点
/// 所有节点互斥，只有一个会被执行
/// </summary>
public class MutexBranch : IPipelineBranch
{
    private readonly List<(string nodeName, Func<PipelineContext, bool>? condition)> _branches = new();
    private readonly string? _fallbackNodeName;

    public string Name { get; }

    /// <summary>
    /// 创建互斥分支
    /// </summary>
    /// <param name="fallback">所有条件都不满足时的兜底节点</param>
    public MutexBranch(string? name = null, string? fallback = null)
    {
        Name = name ?? "MutexBranch";
        _fallbackNodeName = fallback;
    }

    /// <summary>
    /// 添加分支
    /// </summary>
    public MutexBranch Add(string nodeName, Func<PipelineContext, bool>? condition = null)
    {
        _branches.Add((nodeName, condition));
        return this;
    }

    /// <summary>
    /// 选择要执行的节点（纯同步，只做条件检查）
    /// </summary>
    public IPipelineNode? SelectNode(PipelineContext context, Func<string, IPipelineNode?> nodeResolver)
    {
        // 按顺序检查每个分支
        foreach (var (nodeName, condition) in _branches)
        {
            var node = nodeResolver(nodeName);
            if (node == null) continue;

            // 条件满足？
            bool conditionSatisfied = condition == null || condition(context);

            // 节点自己的 When 也要检查
            bool nodeWhenSatisfied = node.When(context);

            if (conditionSatisfied && nodeWhenSatisfied)
            {
                return node;
            }
        }

        // 没有匹配，返回兜底
        if (_fallbackNodeName != null)
        {
            return nodeResolver(_fallbackNodeName);
        }

        return null;
    }
}
