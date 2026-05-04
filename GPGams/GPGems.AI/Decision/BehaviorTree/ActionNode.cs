/*
 * GPGems.AI - Behavior Tree Action Nodes
 * 动作节点：执行具体行为
 */

using GPGems.AI.Decision.Blackboards;

namespace GPGems.AI.Decision.BehaviorTree;

/// <summary>
/// 动作节点基类
/// </summary>
public abstract class ActionNode : Node
{
    protected ActionNode(string name = "Action") : base(name)
    {
    }
}

/// <summary>
/// 函数动作：使用自定义函数执行
/// </summary>
public class FuncAction : ActionNode
{
    private readonly Func<Blackboard, NodeStatus> _action;

    public FuncAction(Func<Blackboard, NodeStatus> action, string name = "FuncAction")
        : base(name)
    {
        _action = action ?? throw new ArgumentNullException(nameof(action));
    }

    public override NodeStatus Execute()
    {
        return _action(Blackboard);
    }
}

/// <summary>
/// 设置黑板值动作
/// </summary>
public class SetValueAction : ActionNode
{
    private readonly string _key;
    private readonly object _value;
    private readonly float? _ttl;

    public SetValueAction(string key, object value, float? ttl = null, string name = "SetValue")
        : base($"{name}({key})")
    {
        _key = key ?? throw new ArgumentNullException(nameof(key));
        _value = value;
        _ttl = ttl;
    }

    public override NodeStatus Execute()
    {
        Blackboard.Set(_key, _value, _ttl ?? -1f);
        return NodeStatus.Success;
    }
}

/// <summary>
/// 等待动作：等待指定秒数后返回成功
/// </summary>
public class WaitAction : ActionNode
{
    private readonly float _duration;
    private float _startTime = -1;

    public WaitAction(float duration, string name = "Wait")
        : base($"{name}({duration}s)")
    {
        _duration = duration;
    }

    public override NodeStatus Execute()
    {
        var currentTime = (float)(DateTime.UtcNow - DateTime.UnixEpoch).TotalSeconds;

        if (_startTime < 0)
            _startTime = currentTime;

        if (currentTime - _startTime >= _duration)
        {
            Reset();
            return NodeStatus.Success;
        }

        return NodeStatus.Running;
    }

    public override void Reset()
    {
        base.Reset();
        _startTime = -1;
    }
}

/// <summary>
/// 日志动作
/// </summary>
public class LogAction : ActionNode
{
    private readonly string _message;

    public LogAction(string message, string name = "Log")
        : base(name)
    {
        _message = message ?? throw new ArgumentNullException(nameof(message));
    }

    public override NodeStatus Execute()
    {
        Console.WriteLine($"[BT Log] {_message}");
        return NodeStatus.Success;
    }
}

/// <summary>
/// 立即成功动作
/// </summary>
public class SuccessAction : ActionNode
{
    public SuccessAction(string name = "Success") : base(name)
    {
    }

    public override NodeStatus Execute() => NodeStatus.Success;
}

/// <summary>
/// 立即失败动作
/// </summary>
public class FailureAction : ActionNode
{
    public FailureAction(string name = "Failure") : base(name)
    {
    }

    public override NodeStatus Execute() => NodeStatus.Failure;
}

/// <summary>
/// 持续运行动作
/// </summary>
public class RunningAction : ActionNode
{
    public RunningAction(string name = "Running") : base(name)
    {
    }

    public override NodeStatus Execute() => NodeStatus.Running;
}
