/*
 * 并发状态机 Parallel Finite State Machine
 * 时间复杂度: O(n) 更新 n 个状态机, 每个 O(1)
 *
 * 经营游戏核心用途:
 *   - 千级 NPC AI: 同时更新所有村民/单位
 *   - 事件系统: 多个并行的游戏事件
 *   - 建筑状态: 所有生产/居住建筑状态更新
 *   - 粒子系统: 大量独立粒子状态管理
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace GPGems.AI.Decision;

/// <summary>
/// 状态机实例接口
/// </summary>
public interface IStateMachineInstance<TState, TEvent, TContext>
{
    /// <summary>实例 ID</summary>
    int InstanceId { get; }

    /// <summary>当前状态</summary>
    TState CurrentState { get; }

    /// <summary>上下文数据</summary>
    TContext Context { get; }

    /// <summary>上次更新时间</summary>
    float LastUpdateTime { get; }

    /// <summary>更新频率（秒，0=每帧更新）</summary>
    float UpdateInterval { get; set; }

    /// <summary>是否活跃</summary>
    bool IsActive { get; set; }

    /// <summary>发送事件</summary>
    void SendEvent(TEvent ev);

    /// <summary>更新状态机</summary>
    void Update(float deltaTime, float currentTime);
}

/// <summary>
/// 状态机定义（共享，多个实例共用）
/// </summary>
public class StateMachineDefinition<TState, TEvent, TContext>
    where TState : struct, Enum
    where TEvent : struct, Enum
{
    /// <summary>状态进入回调</summary>
    public delegate void StateAction(TContext context, float deltaTime);

    /// <summary>转换条件回调</summary>
    public delegate bool TransitionCondition(TContext context);

    /// <summary>状态行为定义</summary>
    private class StateBehavior
    {
        public StateAction? OnEnter { get; set; }
        public StateAction? OnUpdate { get; set; }
        public StateAction? OnExit { get; set; }
    }

    /// <summary>转换定义</summary>
    private class Transition
    {
        public TState From { get; set; }
        public TState To { get; set; }
        public TransitionCondition? Condition { get; set; }
        public TEvent? TriggerEvent { get; set; }
    }

    private readonly Dictionary<TState, StateBehavior> _stateBehaviors = new();
    private readonly List<Transition> _transitions = new();
    private readonly Dictionary<TState, List<Transition>> _transitionLookup = new();
    private TState _initialState;

    /// <summary>
    /// 设置初始状态
    /// </summary>
    public StateMachineDefinition<TState, TEvent, TContext>
        InitialState(TState state)
    {
        _initialState = state;
        return this;
    }

    /// <summary>
    /// 定义状态行为
    /// </summary>
    public StateMachineDefinition<TState, TEvent, TContext>
        State(TState state,
            StateAction? onEnter = null,
            StateAction? onUpdate = null,
            StateAction? onExit = null)
    {
        _stateBehaviors[state] = new StateBehavior
        {
            OnEnter = onEnter,
            OnUpdate = onUpdate,
            OnExit = onExit
        };
        return this;
    }

    /// <summary>
    /// 定义条件转换
    /// </summary>
    public StateMachineDefinition<TState, TEvent, TContext>
        Transition(TState from, TState to, TransitionCondition condition)
    {
        var transition = new Transition
        {
            From = from,
            To = to,
            Condition = condition
        };
        _transitions.Add(transition);

        if (!_transitionLookup.TryGetValue(from, out var list))
        {
            list = new List<Transition>();
            _transitionLookup[from] = list;
        }
        list.Add(transition);

        return this;
    }

    /// <summary>
    /// 定义事件触发转换
    /// </summary>
    public StateMachineDefinition<TState, TEvent, TContext>
        OnEvent(TState from, TState to, TEvent eventTrigger)
    {
        var transition = new Transition
        {
            From = from,
            To = to,
            TriggerEvent = eventTrigger
        };
        _transitions.Add(transition);

        if (!_transitionLookup.TryGetValue(from, out var list))
        {
            list = new List<Transition>();
            _transitionLookup[from] = list;
        }
        list.Add(transition);

        return this;
    }

    /// <summary>
    /// 创建实例
    /// </summary>
    public IStateMachineInstance<TState, TEvent, TContext>
        CreateInstance(int instanceId, TContext context, float updateInterval = 0)
    {
        return new Instance(this, instanceId, context, _initialState, updateInterval);
    }

    /// <summary>
    /// 获取状态进入回调
    /// </summary>
    internal StateAction? GetOnEnter(TState state)
    {
        _stateBehaviors.TryGetValue(state, out var behavior);
        return behavior?.OnEnter;
    }

    /// <summary>
    /// 获取状态更新回调
    /// </summary>
    internal StateAction? GetOnUpdate(TState state)
    {
        _stateBehaviors.TryGetValue(state, out var behavior);
        return behavior?.OnUpdate;
    }

    /// <summary>
    /// 获取状态退出回调
    /// </summary>
    internal StateAction? GetOnExit(TState state)
    {
        _stateBehaviors.TryGetValue(state, out var behavior);
        return behavior?.OnExit;
    }

    /// <summary>
    /// 获取从指定状态出发的所有转换
    /// </summary>
    internal List<Transition> GetTransitionsFrom(TState state)
    {
        _transitionLookup.TryGetValue(state, out var list);
        return list ?? new List<Transition>();
    }

    /// <summary>
    /// 状态机实例实现
    /// </summary>
    private class Instance : IStateMachineInstance<TState, TEvent, TContext>
    {
        private readonly StateMachineDefinition<TState, TEvent, TContext> _definition;
        private TState _currentState;
        private readonly Queue<TEvent> _eventQueue = new();

        public int InstanceId { get; }
        public TContext Context { get; }
        public float LastUpdateTime { get; private set; }
        public float UpdateInterval { get; set; }
        public bool IsActive { get; set; } = true;

        public TState CurrentState => _currentState;

        public Instance(
            StateMachineDefinition<TState, TEvent, TContext> definition,
            int instanceId,
            TContext context,
            TState initialState,
            float updateInterval)
        {
            _definition = definition;
            InstanceId = instanceId;
            Context = context;
            _currentState = initialState;
            UpdateInterval = updateInterval;

            // 触发初始状态 Enter
            _definition.GetOnEnter(initialState)?.Invoke(context, 0);
        }

        public void SendEvent(TEvent ev)
        {
            _eventQueue.Enqueue(ev);
        }

        public void Update(float deltaTime, float currentTime)
        {
            if (!IsActive)
                return;

            LastUpdateTime = currentTime;

            // 处理事件
            while (_eventQueue.Count > 0)
            {
                var ev = _eventQueue.Dequeue();
                ProcessEvent(ev, deltaTime);
            }

            // 更新当前状态
            _definition.GetOnUpdate(_currentState)?.Invoke(Context, deltaTime);

            // 检查条件转换
            var transitions = _definition.GetTransitionsFrom(_currentState);
            foreach (var transition in transitions)
            {
                if (transition.Condition != null && transition.Condition(Context))
                {
                    TransitionTo(transition.To, deltaTime);
                    break;  // 每次只做一个转换
                }
            }
        }

        private void ProcessEvent(TEvent ev, float deltaTime)
        {
            var transitions = _definition.GetTransitionsFrom(_currentState);
            foreach (var transition in transitions)
            {
                if (transition.TriggerEvent.HasValue &&
                    transition.TriggerEvent.Value.Equals(ev))
                {
                    TransitionTo(transition.To, deltaTime);
                    break;
                }
            }
        }

        private void TransitionTo(TState newState, float deltaTime)
        {
            if (newState.Equals(_currentState))
                return;

            _definition.GetOnExit(_currentState)?.Invoke(Context, deltaTime);
            var oldState = _currentState;
            _currentState = newState;
            _definition.GetOnEnter(_currentState)?.Invoke(Context, deltaTime);
        }
    }
}

/// <summary>
/// 并发状态机管理器
/// 批量更新大量状态机实例，支持并行更新
/// </summary>
/// <typeparam name="TState">状态类型</typeparam>
/// <typeparam name="TEvent">事件类型</typeparam>
/// <typeparam name="TContext">上下文类型</typeparam>
public class ParallelFSMManager<TState, TEvent, TContext> : IEnumerable<IStateMachineInstance<TState, TEvent, TContext>>
    where TState : struct, Enum
    where TEvent : struct, Enum
{
    #region 字段与属性

    private readonly List<IStateMachineInstance<TState, TEvent, TContext>> _instances = new();
    private readonly Dictionary<int, IStateMachineInstance<TState, TEvent, TContext>> _instanceLookup = new();
    private int _nextInstanceId = 1;

    /// <summary>实例总数</summary>
    public int Count => _instances.Count;

    /// <summary>活跃实例数</summary>
    public int ActiveCount
    {
        get
        {
            int count = 0;
            foreach (var inst in _instances)
                if (inst.IsActive) count++;
            return count;
        }
    }

    /// <summary>是否启用并行更新</summary>
    public bool EnableParallel { get; set; } = true;

    /// <summary>并行度（0 = 自动检测）</summary>
    public int MaxDegreeOfParallelism { get; set; } = 0;

    /// <summary>状态改变事件</summary>
    public event Action<int, TState, TState>? OnStateChanged;

    #endregion

    #region 实例管理

    /// <summary>
    /// 添加实例
    /// </summary>
    public int AddInstance(IStateMachineInstance<TState, TEvent, TContext> instance)
    {
        _instances.Add(instance);
        _instanceLookup[instance.InstanceId] = instance;
        return instance.InstanceId;
    }

    /// <summary>
    /// 创建并添加实例
    /// </summary>
    public int CreateInstance(
        StateMachineDefinition<TState, TEvent, TContext> definition,
        TContext context,
        float updateInterval = 0)
    {
        int id = _nextInstanceId++;
        var instance = definition.CreateInstance(id, context, updateInterval);
        AddInstance(instance);
        return id;
    }

    /// <summary>
    /// 批量创建实例
    /// </summary>
    public List<int> CreateInstances(
        StateMachineDefinition<TState, TEvent, TContext> definition,
        IEnumerable<TContext> contexts,
        float updateInterval = 0)
    {
        var ids = new List<int>();
        foreach (var context in contexts)
        {
            ids.Add(CreateInstance(definition, context, updateInterval));
        }
        return ids;
    }

    /// <summary>
    /// 移除实例
    /// </summary>
    public bool RemoveInstance(int instanceId)
    {
        if (_instanceLookup.TryGetValue(instanceId, out var instance))
        {
            _instances.Remove(instance);
            _instanceLookup.Remove(instanceId);
            return true;
        }
        return false;
    }

    /// <summary>
    /// 获取实例
    /// </summary>
    public IStateMachineInstance<TState, TEvent, TContext>? GetInstance(int instanceId)
    {
        _instanceLookup.TryGetValue(instanceId, out var instance);
        return instance;
    }

    /// <summary>
    /// 清空所有实例
    /// </summary>
    public void Clear()
    {
        _instances.Clear();
        _instanceLookup.Clear();
    }

    #endregion

    #region 更新操作

    /// <summary>
    /// 更新所有状态机
    /// </summary>
    public void UpdateAll(float deltaTime, float currentTime)
    {
        if (EnableParallel && _instances.Count > 100)
        {
            // 并行更新（超过 100 个实例时）
            var options = new ParallelOptions
            {
                MaxDegreeOfParallelism = MaxDegreeOfParallelism > 0
                    ? MaxDegreeOfParallelism
                    : Environment.ProcessorCount
            };

            Parallel.ForEach(_instances, options, instance =>
            {
                if (instance.IsActive)
                {
                    if (instance.UpdateInterval <= 0 ||
                        currentTime - instance.LastUpdateTime >= instance.UpdateInterval)
                    {
                        instance.Update(deltaTime, currentTime);
                    }
                }
            });
        }
        else
        {
            // 串行更新
            foreach (var instance in _instances)
            {
                if (instance.IsActive)
                {
                    if (instance.UpdateInterval <= 0 ||
                        currentTime - instance.LastUpdateTime >= instance.UpdateInterval)
                    {
                        instance.Update(deltaTime, currentTime);
                    }
                }
            }
        }
    }

    /// <summary>
    /// 只更新指定的实例组
    /// </summary>
    public void UpdateSubset(IEnumerable<int> instanceIds, float deltaTime, float currentTime)
    {
        foreach (var id in instanceIds)
        {
            if (_instanceLookup.TryGetValue(id, out var instance) && instance.IsActive)
            {
                if (instance.UpdateInterval <= 0 ||
                    currentTime - instance.LastUpdateTime >= instance.UpdateInterval)
                {
                    instance.Update(deltaTime, currentTime);
                }
            }
        }
    }

    #endregion

    #region 广播操作

    /// <summary>
    /// 向所有实例广播事件
    /// </summary>
    public void BroadcastEvent(TEvent ev)
    {
        foreach (var instance in _instances)
        {
            instance.SendEvent(ev);
        }
    }

    /// <summary>
    /// 向指定实例组广播事件
    /// </summary>
    public void BroadcastEventTo(IEnumerable<int> instanceIds, TEvent ev)
    {
        foreach (var id in instanceIds)
        {
            if (_instanceLookup.TryGetValue(id, out var instance))
            {
                instance.SendEvent(ev);
            }
        }
    }

    /// <summary>
    /// 向特定状态的所有实例广播事件
    /// </summary>
    public void BroadcastEventToState(TState state, TEvent ev)
    {
        foreach (var instance in _instances)
        {
            if (instance.CurrentState.Equals(state))
            {
                instance.SendEvent(ev);
            }
        }
    }

    #endregion

    #region 查询操作

    /// <summary>
    /// 获取处于指定状态的所有实例 ID
    /// </summary>
    public List<int> GetInstancesInState(TState state)
    {
        var result = new List<int>();
        foreach (var instance in _instances)
        {
            if (instance.CurrentState.Equals(state))
            {
                result.Add(instance.InstanceId);
            }
        }
        return result;
    }

    /// <summary>
    /// 统计各状态实例数
    /// </summary>
    public Dictionary<TState, int> CountByState()
    {
        var result = new Dictionary<TState, int>();
        foreach (var state in Enum.GetValues<TState>())
        {
            result[state] = 0;
        }

        foreach (var instance in _instances)
        {
            result[instance.CurrentState]++;
        }

        return result;
    }

    /// <summary>
    /// 按状态查找实例
    /// </summary>
    public List<IStateMachineInstance<TState, TEvent, TContext>> FindAllInState(TState state)
    {
        var result = new List<IStateMachineInstance<TState, TEvent, TContext>>();
        foreach (var instance in _instances)
        {
            if (instance.CurrentState.Equals(state))
            {
                result.Add(instance);
            }
        }
        return result;
    }

    #endregion

    #region IEnumerable 实现

    public IEnumerator<IStateMachineInstance<TState, TEvent, TContext>> GetEnumerator()
    {
        return _instances.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    #endregion
}
