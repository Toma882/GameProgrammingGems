/*
 * GPGems.AI - FSM State Transition
 * StateTransition: 状态转换定义
 * 支持条件转换、消息触发转换
 */

using GPGems.AI.Decision.Blackboards;
using GPGems.Core.Messages;

namespace GPGems.AI.Decision.FSM;

/// <summary>
/// 转换类型
/// </summary>
public enum TransitionType
{
    /// <summary>自动转换（每帧检查）
    Automatic,

    /// <summary>消息触发
    OnMessage,

    /// <summary>手动触发
    Manual
}

/// <summary>
/// 状态转换
/// </summary>
public class StateTransition
{
    /// <summary>源状态
    public IState From { get; }

    /// <summary>目标状态
    public IState To { get; }

    /// <summary>转换类型
    public TransitionType Type { get; }

    /// <summary>触发转换的消息类型（仅 Type = OnMessage 时有效）
    public string? TriggerMessageType { get; init; }

    /// <summary>转换条件
    public Func<Blackboard, bool>? Condition { get; init; }

    /// <summary>转换动作
    public Action<Blackboard>? OnTransition { get; init; }

    /// <summary>优先级（当有多个转换可能时选高优先级）
    public int Priority { get; init; }

    public StateTransition(IState from, IState to, TransitionType type)
    {
        From = from ?? throw new ArgumentNullException(nameof(from));
        To = to ?? throw new ArgumentNullException(nameof(to));
        Type = type;
    }

    /// <summary>检查是否可以转换
    public bool CanTransition(Blackboard context, Message? trigger = null)
    {
        // 如果是消息触发，检查消息类型是否匹配
        if (Type == TransitionType.OnMessage)
        {
            if (trigger == null || trigger.Type != TriggerMessageType)
                return false;
        }

        // 检查条件
        return Condition?.Invoke(context) ?? true;
    }

    public override string ToString() => $"{From.Name} -> {To.Name} ({Type})";
}

/// <summary>
/// 状态转换构建器（Fluent API）
/// </summary>
public class TransitionBuilder
{
    private readonly IState _from;
    private readonly List<StateTransition> _transitions = new();

    public TransitionBuilder(IState from)
    {
        _from = from ?? throw new ArgumentNullException(nameof(from));
    }

    /// <summary>自动转换（每帧检查条件）
    public TransitionBuilder To(IState to, Func<Blackboard, bool>? condition = null, int priority = 0)
    {
        _transitions.Add(new StateTransition(_from, to, TransitionType.Automatic)
        {
            Condition = condition,
            Priority = priority
        });
        return this;
    }

    /// <summary>消息触发转换
    public TransitionBuilder OnMessage(IState to, string messageType, Func<Blackboard, bool>? condition = null, int priority = 0)
    {
        _transitions.Add(new StateTransition(_from, to, TransitionType.OnMessage)
        {
            TriggerMessageType = messageType,
            Condition = condition,
            Priority = priority
        });
        return this;
    }

    /// <summary>构建所有转换
    public List<StateTransition> Build()
    {
        return _transitions;
    }
}
