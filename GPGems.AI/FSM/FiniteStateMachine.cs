/* Copyright (C) Eric Dybsand, 2000.
 * 从《游戏编程精粹 1》移植到 C#
 * 有限状态机核心实现
 */

namespace GPGems.AI.FSM;

/// <summary>
/// 状态转换条件
/// 当条件满足时触发状态切换
/// </summary>
public delegate bool TransitionCondition();

/// <summary>
/// FSM 状态
/// 包含进入、更新、退出三个阶段的回调
/// </summary>
public class FsmState
{
    public string Name { get; }
    public Action? OnEnter { get; init; }
    public Action? OnUpdate { get; init; }
    public Action? OnExit { get; init; }

    public FsmState(string name)
    {
        Name = name;
    }

    public override string ToString() => Name;
}

/// <summary>
/// 状态转换
/// 定义从源状态到目标状态的条件转换
/// </summary>
public class FsmTransition
{
    public FsmState From { get; }
    public FsmState To { get; }
    public TransitionCondition? Condition { get; init; }
    public string? TriggerName { get; init; }

    public FsmTransition(FsmState from, FsmState to)
    {
        From = from;
        To = to;
    }

    public override string ToString() => $"{From.Name} -> {To.Name}";
}

/// <summary>
/// 有限状态机
/// 管理状态集合、转换规则和当前状态
/// </summary>
public class FiniteStateMachine
{
    public FsmState CurrentState { get; private set; }
    public FsmState? PreviousState { get; private set; }
    public string Name { get; }

    public List<FsmState> States { get; } = [];
    public List<FsmTransition> Transitions { get; } = [];

    /// <summary>状态改变事件</summary>
    public event Action<FsmState, FsmState>? OnStateChanged;

    public FiniteStateMachine(string name, FsmState initialState)
    {
        Name = name;
        CurrentState = initialState;
        States.Add(initialState);
    }

    /// <summary>添加状态</summary>
    public FiniteStateMachine AddState(FsmState state)
    {
        if (!States.Contains(state))
            States.Add(state);
        return this;
    }

    /// <summary>添加转换规则</summary>
    public FiniteStateMachine AddTransition(FsmTransition transition)
    {
        if (!Transitions.Contains(transition))
            Transitions.Add(transition);
        return this;
    }

    /// <summary>添加简单转换（从A到B加条件）</summary>
    public FiniteStateMachine AddTransition(FsmState from, FsmState to, TransitionCondition? condition = null, string? trigger = null)
    {
        var t = new FsmTransition(from, to)
        {
            Condition = condition,
            TriggerName = trigger
        };
        Transitions.Add(t);
        return this;
    }

    /// <summary>每帧更新：检查转换条件，执行当前状态更新</summary>
    public void Update()
    {
        // 检查所有从当前状态出发的转换
        foreach (var transition in Transitions)
        {
            if (transition.From != CurrentState) continue;

            if (transition.Condition == null || transition.Condition())
            {
                SwitchState(transition.To);
                break; // 一帧最多切换一次
            }
        }

        // 执行当前状态的更新逻辑
        CurrentState.OnUpdate?.Invoke();
    }

    /// <summary>通过触发器名称强制转换状态</summary>
    public bool Trigger(string triggerName)
    {
        foreach (var transition in Transitions)
        {
            if (transition.From == CurrentState && transition.TriggerName == triggerName)
            {
                SwitchState(transition.To);
                return true;
            }
        }
        return false;
    }

    /// <summary>切换状态</summary>
    private void SwitchState(FsmState newState)
    {
        CurrentState.OnExit?.Invoke();
        PreviousState = CurrentState;
        CurrentState = newState;
        OnStateChanged?.Invoke(PreviousState, newState);
        CurrentState.OnEnter?.Invoke();
    }

    /// <summary>重置到初始状态</summary>
    public void Reset(FsmState initialState)
    {
        CurrentState = initialState;
        PreviousState = null;
    }
}
