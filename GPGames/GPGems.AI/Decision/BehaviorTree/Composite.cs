/*
 * GPGems.AI - Behavior Tree Composite Nodes
 * 组合节点：选择器、顺序器、并行器
 */

namespace GPGems.AI.Decision.BehaviorTree;

/// <summary>
/// 选择器（OR逻辑）：顺序执行子节点，一个成功则成功，全部失败则失败
/// </summary>
public class Selector : Node
{
    private int _currentIndex;

    public Selector(string name = "Selector") : base(name)
    {
    }

    public override NodeStatus Execute()
    {
        for (var i = _currentIndex; i < _children.Count; i++)
        {
            var status = _children[i].Execute();
            if (status == NodeStatus.Success)
            {
                Reset();
                return NodeStatus.Success;
            }
            if (status == NodeStatus.Running)
            {
                _currentIndex = i;
                return NodeStatus.Running;
            }
        }
        Reset();
        return NodeStatus.Failure;
    }

    public override void Reset()
    {
        base.Reset();
        _currentIndex = 0;
    }
}

/// <summary>
/// 顺序器（AND逻辑）：顺序执行子节点，一个失败则失败，全部成功则成功
/// </summary>
public class Sequence : Node
{
    private int _currentIndex;

    public Sequence(string name = "Sequence") : base(name)
    {
    }

    public override NodeStatus Execute()
    {
        for (var i = _currentIndex; i < _children.Count; i++)
        {
            var status = _children[i].Execute();
            if (status == NodeStatus.Failure)
            {
                Reset();
                return NodeStatus.Failure;
            }
            if (status == NodeStatus.Running)
            {
                _currentIndex = i;
                return NodeStatus.Running;
            }
        }
        Reset();
        return NodeStatus.Success;
    }

    public override void Reset()
    {
        base.Reset();
        _currentIndex = 0;
    }
}

/// <summary>
/// 并行策略
/// </summary>
public enum ParallelPolicy
{
    /// <summary>一个成功则成功</summary>
    OneSuccess,
    /// <summary>一个失败则失败</summary>
    OneFailure,
    /// <summary>全部成功才成功</summary>
    AllSuccess,
    /// <summary>全部失败才失败</summary>
    AllFailure
}

/// <summary>
/// 并行节点：同时执行所有子节点
/// </summary>
public class Parallel : Node
{
    private readonly ParallelPolicy _successPolicy;
    private readonly ParallelPolicy _failurePolicy;
    private readonly List<NodeStatus> _childStatus = new();

    public Parallel(
        ParallelPolicy successPolicy = ParallelPolicy.AllSuccess,
        ParallelPolicy failurePolicy = ParallelPolicy.OneFailure,
        string name = "Parallel")
        : base(name)
    {
        _successPolicy = successPolicy;
        _failurePolicy = failurePolicy;
    }

    public override NodeStatus Execute()
    {
        var successCount = 0;
        var failureCount = 0;

        for (var i = 0; i < _children.Count; i++)
        {
            if (_childStatus.Count <= i)
                _childStatus.Add(NodeStatus.Running);

            if (_childStatus[i] != NodeStatus.Running)
                continue;

            _childStatus[i] = _children[i].Execute();

            if (_childStatus[i] == NodeStatus.Success)
                successCount++;
            if (_childStatus[i] == NodeStatus.Failure)
                failureCount++;
        }

        // 检查成功策略
        if (ShouldReturn(successCount, failureCount, _successPolicy, NodeStatus.Success))
        {
            Reset();
            return NodeStatus.Success;
        }

        // 检查失败策略
        if (ShouldReturn(successCount, failureCount, _failurePolicy, NodeStatus.Failure))
        {
            Reset();
            return NodeStatus.Failure;
        }

        return NodeStatus.Running;
    }

    private bool ShouldReturn(int successCount, int failureCount, ParallelPolicy policy, NodeStatus target)
    {
        return policy switch
        {
            ParallelPolicy.OneSuccess => target == NodeStatus.Success && successCount > 0,
            ParallelPolicy.OneFailure => target == NodeStatus.Failure && failureCount > 0,
            ParallelPolicy.AllSuccess => target == NodeStatus.Success && successCount == _children.Count,
            ParallelPolicy.AllFailure => target == NodeStatus.Failure && failureCount == _children.Count,
            _ => false
        };
    }

    public override void Reset()
    {
        base.Reset();
        _childStatus.Clear();
    }
}
