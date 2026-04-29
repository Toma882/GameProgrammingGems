/*
 * GPGems.AI - FSM Core
 * StateMachine: 有限状态机核心实现
 * 集成消息路由、黑板上下文、状态转换
 */

using System.Collections.Concurrent;
using GPGems.AI.Decision.Blackboards;
using GPGems.Core.Messages;

namespace GPGems.AI.Decision.FSM;

/// <summary>
/// 有限状态机
/// </summary>
public class StateMachine : IMessageReceiver
{
    /// <summary>状态机名称</summary>
    public string Name { get; }

    /// <summary>接收器ID（实现 IMessageReceiver
    public string ReceiverId => Name;

    /// <summary>上下文黑板
    public Blackboard Context { get; }

    private MessageRouter? _router;

    /// <summary>关联的消息路由器
    public MessageRouter? Router
    {
        get => _router;
        set
        {
            if (_router != null)
                _router.MessageLogged -= OnMessageLogged;

            _router = value;

            if (_router != null)
                _router.MessageLogged += OnMessageLogged;
        }
    }

    private void OnMessageLogged(Message message)
    {
        Context.Set($"msg_{message.Timestamp}_{Guid.NewGuid():N}", message.ToString(), ttl: 30f);
    }

    /// <summary>当前状态</summary>
    public IState? CurrentState { get; private set; }

    /// <summary>前一个状态</summary>
    public IState? PreviousState { get; private set; }

    /// <summary>初始状态</summary>
    public IState? InitialState { get; private set; }

    /// <summary>状态机是否已启动</summary>
    public bool IsStarted => CurrentState != null;

    // 状态注册表
    private readonly Dictionary<string, IState> _states = new();

    // 转换表：fromStateName -> list of transitions
    private readonly Dictionary<string, List<StateTransition>> _transitions = new();

    // 状态进入/退出事件
    public event Action<IState, IState?>? OnStateEnter;
    public event Action<IState, IState?>? OnStateExit;
    public event Action<IState, IState>? OnStateChanged;

    /// <summary>创建一个状态机
    /// <param name="name">状态机名称</param>
    /// <param name="context">可选的黑板，null 表示创建新的</param>
    public StateMachine(string name, Blackboard? context = null)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Context = context ?? new Blackboard($"{name}_Context");
    }

    #region 状态管理

    /// <summary>添加状态</summary>
    public StateMachine AddState(IState state)
    {
        if (state == null) throw new ArgumentNullException(nameof(state));
        _states[state.Name] = state;
        return this;
    }

    /// <summary>设置初始状态</summary>
    public StateMachine SetInitialState(IState state)
    {
        if (state == null) throw new ArgumentNullException(nameof(state));
        AddState(state);
        InitialState = state;
        return this;
    }

    /// <summary>获取状态</summary>
    public IState? GetState(string name)
    {
        _states.TryGetValue(name, out var state);
        return state;
    }

    #endregion

    #region 转换管理

    /// <summary>添加状态转换
    public StateMachine AddTransition(StateTransition transition)
    {
        if (transition == null) throw new ArgumentNullException(nameof(transition));

        var fromName = transition.From.Name;
        if (!_transitions.TryGetValue(fromName, out var list))
        {
            list = new List<StateTransition>();
            _transitions[fromName] = list;
        }
        list.Add(transition);
        return this;
    }

    /// <summary>为状态添加转换（Fluent API 入口</summary>
    public TransitionBuilder From(IState fromState)
    {
        AddState(fromState);
        return new TransitionBuilder(fromState);
    }

    /// <summary>构建并添加转换</summary>
    public StateMachine AddTransitions(TransitionBuilder builder)
    {
        if (builder == null) throw new ArgumentNullException(nameof(builder));
        foreach (var transition in builder.Build())
        {
            AddTransition(transition);
        }
        return this;
    }

    #endregion

    #region 状态机生命周期

    /// <summary>启动状态机
    public void Start()
    {
        if (InitialState == null)
            throw new InvalidOperationException("Initial state not set");

        CurrentState = InitialState;
        CurrentState.OnEnter(Context, null);
        OnStateEnter?.Invoke(CurrentState, null);

        // 发送状态进入消息
        SendStateMessage(StandardMessages.StateEnter, CurrentState);
    }

    /// <summary>停止状态机
    public void Stop()
    {
        if (CurrentState != null)
        {
            CurrentState.OnExit(Context, null);
            OnStateExit?.Invoke(CurrentState, null);
            SendStateMessage(StandardMessages.StateExit, CurrentState);
        }

        PreviousState = CurrentState;
        CurrentState = null;
    }

    /// <summary>每帧更新
    public void Update()
    {
        if (CurrentState == null) return;

        // 1. 检查自动转换
        CheckAutomaticTransitions();

        // 2. 更新当前状态
        CurrentState.OnUpdate(Context);

        // 发送更新消息
        SendStateMessage(StandardMessages.StateUpdate, CurrentState);
    }

    #endregion

    #region 状态转换

    /// <summary>手动触发转换
    public bool TransitionTo(IState targetState)
    {
        if (CurrentState == null) return false;
        if (targetState == null) throw new ArgumentNullException(nameof(targetState));

        PerformTransition(targetState);
        return true;
    }

    /// <summary>手动触发转换（按名称</summary>
    public bool TransitionTo(string stateName)
    {
        var state = GetState(stateName);
        return state != null && TransitionTo(state);
    }

    private void CheckAutomaticTransitions()
    {
        if (CurrentState == null) return;
        if (!_transitions.TryGetValue(CurrentState.Name, out var transitions)) return;

        // 按优先级排序
        foreach (var transition in transitions
            .Where(t => t.Type == TransitionType.Automatic)
            .OrderByDescending(t => t.Priority))
        {
            if (transition.CanTransition(Context))
            {
                PerformTransition(transition.To);
                transition.OnTransition?.Invoke(Context);
                return; // 一帧只转换一次
            }
        }
    }

    private void PerformTransition(IState nextState)
    {
        var previousState = CurrentState;

        // 退出当前状态
        CurrentState?.OnExit(Context, nextState);
        OnStateExit?.Invoke(CurrentState!, nextState);
        SendStateMessage(StandardMessages.StateExit, CurrentState!);

        // 切换状态
        PreviousState = CurrentState;
        CurrentState = nextState;

        // 进入新状态
        CurrentState.OnEnter(Context, previousState);
        OnStateEnter?.Invoke(CurrentState, previousState);
        OnStateChanged?.Invoke(previousState!, CurrentState);
        SendStateMessage(StandardMessages.StateEnter, CurrentState);

        // 发送转换消息
        var transitionMsg = new Message(StandardMessages.StateTransition, new
        {
            From = previousState?.Name,
            To = CurrentState.Name
        });
        Router?.Broadcast(transitionMsg);
    }

    #endregion

    #region 消息处理（实现 IMessageReceiver）

    /// <inheritdoc />
    public bool ReceiveMessage(Message message)
    {
        // 先让当前状态处理消息
        var result = CurrentState?.HandleMessage(message, Context) ?? MessageResult.Unhandled;

        // 如果消息没被消费，检查是否触发转换
        if (result != MessageResult.Consumed)
        {
            CheckMessageTransitions(message);
        }

        return result != MessageResult.Unhandled;
    }

    private void CheckMessageTransitions(Message message)
    {
        if (CurrentState == null) return;
        if (!_transitions.TryGetValue(CurrentState.Name, out var transitions)) return;

        foreach (var transition in transitions
            .Where(t => t.Type == TransitionType.OnMessage)
            .OrderByDescending(t => t.Priority))
        {
            if (transition.CanTransition(Context, message))
            {
                PerformTransition(transition.To);
                transition.OnTransition?.Invoke(Context);
                return;
            }
        }
    }

    private void SendStateMessage(string messageType, IState state)
    {
        if (Router == null) return;

        var msg = new Message(messageType, state.Name)
        {
            SenderId = ReceiverId
        };
        Router.Broadcast(msg);
    }

    #endregion

    #region 调试与诊断

    /// <summary>获取状态机状态快照</summary>
    public string Dump()
    {
        var lines = new List<string>
        {
            $"=== StateMachine '{Name}' ===",
            $"Current: {CurrentState?.Name ?? "(stopped)"}",
            $"Previous: {PreviousState?.Name ?? "(none)"}",
            $"States: {_states.Count}",
            $"Transitions: {_transitions.Values.Sum(l => l.Count)}"
        };

        if (_states.Count > 0)
        {
            lines.Add("States:");
            foreach (var state in _states.Values)
            {
                var mark = state == CurrentState ? " [CURRENT]" : "";
                var mark2 = state == InitialState ? " [INITIAL]" : "";
                lines.Add($"  - {state.Name}{mark}{mark2}");
            }
        }

        return string.Join(Environment.NewLine, lines);
    }

    #endregion
}
