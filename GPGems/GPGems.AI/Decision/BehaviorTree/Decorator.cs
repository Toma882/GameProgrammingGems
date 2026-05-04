/*
 * GPGems.AI - Behavior Tree Decorator Nodes
 * 装饰节点：控制子节点执行行为
 */

namespace GPGems.AI.Decision.BehaviorTree;

/// <summary>
/// 装饰节点基类
/// </summary>
public abstract class Decorator : Node
{
    protected Node Child => _children.FirstOrDefault();

    protected Decorator(string name, Node? child = null) : base(name)
    {
        if (child != null)
            AddChild(child);
    }
}

/// <summary>
/// 反转器：将子节点结果取反（Running 保持不变）
/// </summary>
public class Inverter : Decorator
{
    public Inverter(Node? child = null, string name = "Inverter")
        : base(name, child)
    {
    }

    public override NodeStatus Execute()
    {
        var status = Child?.Execute() ?? NodeStatus.Failure;
        return status switch
        {
            NodeStatus.Success => NodeStatus.Failure,
            NodeStatus.Failure => NodeStatus.Success,
            _ => status
        };
    }
}

/// <summary>
/// 重复执行：重复执行子节点指定次数
/// </summary>
public class Repeat : Decorator
{
    private readonly int _count;
    private int _currentCount;

    /// <summary>0 表示无限重复</summary>
    public Repeat(int count = 0, Node? child = null, string name = "Repeat")
        : base(name, child)
    {
        _count = count;
    }

    public override NodeStatus Execute()
    {
        while (true)
        {
            var status = Child?.Execute() ?? NodeStatus.Failure;

            if (status == NodeStatus.Running)
                return NodeStatus.Running;

            _currentCount++;
            if (_count > 0 && _currentCount >= _count)
            {
                Reset();
                return NodeStatus.Success;
            }

            Child?.Reset();
        }
    }

    public override void Reset()
    {
        base.Reset();
        _currentCount = 0;
    }
}

/// <summary>
/// 直到失败：重复执行子节点直到失败
/// </summary>
public class UntilFail : Decorator
{
    public UntilFail(Node? child = null, string name = "UntilFail")
        : base(name, child)
    {
    }

    public override NodeStatus Execute()
    {
        var status = Child?.Execute() ?? NodeStatus.Failure;

        if (status == NodeStatus.Running)
            return NodeStatus.Running;

        if (status == NodeStatus.Failure)
        {
            Reset();
            return NodeStatus.Success;
        }

        Child?.Reset();
        return NodeStatus.Running;
    }
}

/// <summary>
/// 直到成功：重复执行子节点直到成功
/// </summary>
public class UntilSuccess : Decorator
{
    public UntilSuccess(Node? child = null, string name = "UntilSuccess")
        : base(name, child)
    {
    }

    public override NodeStatus Execute()
    {
        var status = Child?.Execute() ?? NodeStatus.Failure;

        if (status == NodeStatus.Running)
            return NodeStatus.Running;

        if (status == NodeStatus.Success)
        {
            Reset();
            return NodeStatus.Success;
        }

        Child?.Reset();
        return NodeStatus.Running;
    }
}

/// <summary>
/// 强制成功：无论子节点结果如何，都返回成功
/// </summary>
public class ForceSuccess : Decorator
{
    public ForceSuccess(Node? child = null, string name = "ForceSuccess")
        : base(name, child)
    {
    }

    public override NodeStatus Execute()
    {
        var status = Child?.Execute() ?? NodeStatus.Failure;
        return status == NodeStatus.Running ? NodeStatus.Running : NodeStatus.Success;
    }
}

/// <summary>
/// 强制失败：无论子节点结果如何，都返回失败
/// </summary>
public class ForceFailure : Decorator
{
    public ForceFailure(Node? child = null, string name = "ForceFailure")
        : base(name, child)
    {
    }

    public override NodeStatus Execute()
    {
        var status = Child?.Execute() ?? NodeStatus.Failure;
        return status == NodeStatus.Running ? NodeStatus.Running : NodeStatus.Failure;
    }
}
