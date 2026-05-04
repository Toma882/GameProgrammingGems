/*
 * GPGems.AI - Behavior Tree Node
 * 行为树节点基类定义
 */
using GPGems.AI.Decision.Blackboards;

namespace GPGems.AI.Decision.BehaviorTree;

/// <summary>
/// 节点执行状态
/// </summary>
public enum NodeStatus
{
    /// <summary>执行中</summary>
    Running,
    /// <summary>执行成功</summary>
    Success,
    /// <summary>执行失败</summary>
    Failure
}

/// <summary>
/// 行为树节点基类
/// </summary>
public abstract class Node
{
    /// <summary>节点名称</summary>
    public string Name { get; }

    /// <summary>关联的黑板</summary>
    public Blackboard Blackboard { get; set; }

    /// <summary>父节点</summary>
    public Node? Parent { get; internal set; }

    /// <summary>子节点</summary>
    public IReadOnlyList<Node> Children => _children;

    protected readonly List<Node> _children = new();

    protected Node(string name)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Blackboard = Blackboard.Default;
    }

    /// <summary>添加子节点</summary>
    public void AddChild(Node child)
    {
        if (child == null) throw new ArgumentNullException(nameof(child));
        child.Parent = this;
        _children.Add(child);
    }

    /// <summary>添加多个子节点</summary>
    public void AddChildren(params Node[] children)
    {
        foreach (var child in children)
            AddChild(child);
    }

    /// <summary>执行节点</summary>
    public abstract NodeStatus Execute();

    /// <summary>重置节点状态</summary>
    public virtual void Reset()
    {
        foreach (var child in _children)
            child.Reset();
    }

    public override string ToString() => Name;
}
