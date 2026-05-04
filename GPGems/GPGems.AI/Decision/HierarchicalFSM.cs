/*
 * 层次状态机 Hierarchical Finite State Machine
 * 时间复杂度: O(1) 单步更新
 *
 * 经营游戏核心用途:
 *   - NPC AI: 村民/工人复杂行为控制
 *   - 怪物行为: 分层决策（全局 → 局部）
 *   - 任务系统: 任务状态嵌套管理
 *   - 建筑状态: 生产/休息/维护多层状态
 */

using System;
using System.Collections.Generic;

namespace GPGems.AI.Decision;

/// <summary>
/// 状态转换结果
/// </summary>
public enum TransitionResult
{
    /// <summary>保持当前状态</summary>
    Stay,
    /// <summary>转换到新状态</summary>
    Transition,
    /// <summary>弹出到父状态</summary>
    PopToParent,
}

/// <summary>
/// 状态接口
/// </summary>
/// <typeparam name="TContext">上下文类型</typeparam>
public interface IState<TContext>
{
    /// <summary>状态名称</summary>
    string Name { get; }

    /// <summary>进入状态</summary>
    void OnEnter(TContext context);

    /// <summary>更新状态</summary>
    /// <returns>转换结果和目标状态（如果需要转换）</returns>
    (TransitionResult result, IState<TContext>? nextState) OnUpdate(TContext context, float deltaTime);

    /// <summary>退出状态</summary>
    void OnExit(TContext context);
}

/// <summary>
/// 基础状态实现
/// </summary>
public abstract class State<TContext> : IState<TContext>
{
    public abstract string Name { get; }

    public virtual void OnEnter(TContext context) { }

    public abstract (TransitionResult result, IState<TContext>? nextState) OnUpdate(TContext context, float deltaTime);

    public virtual void OnExit(TContext context) { }
}

/// <summary>
/// 超状态（可包含子状态的状态）
/// </summary>
public abstract class SuperState<TContext> : State<TContext>
{
    private readonly Dictionary<string, IState<TContext>> _subStates
        = new Dictionary<string, IState<TContext>>();

    private IState<TContext>? _currentSubState;

    /// <summary>当前子状态</summary>
    public IState<TContext>? CurrentSubState => _currentSubState;

    /// <summary>默认子状态名称</summary>
    protected abstract string DefaultSubStateName { get; }

    /// <summary>添加子状态</summary>
    public void AddSubState(IState<TContext> state)
    {
        _subStates[state.Name] = state;
    }

    /// <summary>获取子状态</summary>
    public IState<TContext>? GetSubState(string name)
    {
        return _subStates.TryGetValue(name, out var state) ? state : null;
    }

    /// <summary>
    /// 转换到子状态
    /// </summary>
    protected void TransitionToSubState(TContext context, string subStateName)
    {
        if (_currentSubState != null)
        {
            _currentSubState.OnExit(context);
        }

        if (_subStates.TryGetValue(subStateName, out var nextState))
        {
            _currentSubState = nextState;
            _currentSubState.OnEnter(context);
        }
    }

    public override void OnEnter(TContext context)
    {
        // 进入超状态时，进入默认子状态
        TransitionToSubState(context, DefaultSubStateName);
    }

    public override (TransitionResult result, IState<TContext>? nextState) OnUpdate(TContext context, float deltaTime)
    {
        // 先更新子状态
        if (_currentSubState != null)
        {
            var (result, nextSubState) = _currentSubState.OnUpdate(context, deltaTime);

            switch (result)
            {
                case TransitionResult.Transition when nextSubState != null:
                    // 子状态内部转换
                    _currentSubState.OnExit(context);
                    _currentSubState = nextSubState;
                    _currentSubState.OnEnter(context);
                    break;

                case TransitionResult.PopToParent:
                    // 子状态请求返回到父状态
                    _currentSubState.OnExit(context);
                    _currentSubState = null;
                    return OnSuperStateComplete(context);
            }
        }

        // 超状态自己的逻辑
        return OnSuperStateUpdate(context, deltaTime);
    }

    public override void OnExit(TContext context)
    {
        _currentSubState?.OnExit(context);
        _currentSubState = null;
    }

    /// <summary>
    /// 超状态自己的更新逻辑
    /// </summary>
    protected abstract (TransitionResult result, IState<TContext>? nextState) OnSuperStateUpdate(
        TContext context, float deltaTime);

    /// <summary>
    /// 所有子状态完成后的处理
    /// </summary>
    protected virtual (TransitionResult result, IState<TContext>? nextState) OnSuperStateComplete(
        TContext context)
    {
        return (TransitionResult.Stay, null);
    }
}

/// <summary>
/// 层次状态机
/// </summary>
/// <typeparam name="TContext">上下文类型</typeparam>
public class HierarchicalFSM<TContext>
{
    #region 字段与属性

    private readonly Dictionary<string, IState<TContext>> _states
        = new Dictionary<string, IState<TContext>>();

    private IState<TContext>? _currentState;
    private readonly Stack<IState<TContext>> _stateStack = new Stack<IState<TContext>>();

    /// <summary>当前状态</summary>
    public IState<TContext>? CurrentState => _currentState;

    /// <summary>上下文</summary>
    public TContext Context { get; }

    /// <summary>当前状态名称</summary>
    public string CurrentStateName => _currentState?.Name ?? "(null)";

    /// <summary>状态改变事件</summary>
    public event Action<string, string>? OnStateChanged;  // (from, to)

    #endregion

    #region 构造函数

    public HierarchicalFSM(TContext context)
    {
        Context = context;
    }

    #endregion

    #region 状态管理

    /// <summary>
    /// 注册状态
    /// </summary>
    public void RegisterState(IState<TContext> state)
    {
        _states[state.Name] = state;
    }

    /// <summary>
    /// 注册多个状态
    /// </summary>
    public void RegisterStates(IEnumerable<IState<TContext>> states)
    {
        foreach (var state in states)
        {
            RegisterState(state);
        }
    }

    /// <summary>
    /// 获取已注册的状态
    /// </summary>
    public IState<TContext>? GetState(string name)
    {
        return _states.TryGetValue(name, out var state) ? state : null;
    }

    #endregion

    #region 核心操作

    /// <summary>
    /// 初始化到指定状态
    /// </summary>
    public void Initialize(string initialStateName)
    {
        if (_states.TryGetValue(initialStateName, out var initialState))
        {
            TransitionTo(initialState);
        }
        else
        {
            throw new InvalidOperationException($"Initial state '{initialStateName}' not registered");
        }
    }

    /// <summary>
    /// 更新状态机
    /// </summary>
    public void Update(float deltaTime = 1.0f)
    {
        if (_currentState == null)
            return;

        var (result, nextState) = _currentState.OnUpdate(Context, deltaTime);

        switch (result)
        {
            case TransitionResult.Transition when nextState != null:
                TransitionTo(nextState);
                break;

            case TransitionResult.PopToParent:
                PopState();
                break;
        }
    }

    /// <summary>
    /// 转换到新状态
    /// </summary>
    public void TransitionTo(IState<TContext> nextState)
    {
        var previousName = _currentState?.Name ?? "(none)";
        _currentState?.OnExit(Context);

        // 保存当前状态到栈（如果需要返回）
        if (_currentState != null)
        {
            _stateStack.Push(_currentState);
        }

        _currentState = nextState;
        _currentState.OnEnter(Context);

        OnStateChanged?.Invoke(previousName, _currentState.Name);
    }

    /// <summary>
    /// 转换到指定名称的状态
    /// </summary>
    public void TransitionTo(string stateName)
    {
        if (_states.TryGetValue(stateName, out var state))
        {
            TransitionTo(state);
        }
    }

    /// <summary>
    /// 弹出到上一个状态
    /// </summary>
    public bool PopState()
    {
        if (_stateStack.Count == 0)
            return false;

        var previousName = _currentState?.Name ?? "(none)";
        _currentState?.OnExit(Context);

        _currentState = _stateStack.Pop();
        // 注意：这里不调用 OnEnter，因为是恢复到之前的状态
        // 如果需要重新进入的逻辑，应该在状态设计中处理

        OnStateChanged?.Invoke(previousName, _currentState.Name);
        return true;
    }

    /// <summary>
    /// 清空状态栈
    /// </summary>
    public void ClearStack()
    {
        _stateStack.Clear();
    }

    #endregion

    #region 辅助方法

    /// <summary>
    /// 检查当前是否在指定状态（包括子状态）
    /// </summary>
    public bool IsInState(string stateName)
    {
        if (_currentState?.Name == stateName)
            return true;

        // 检查子状态
        if (_currentState is SuperState<TContext> superState)
        {
            return IsInSubState(superState, stateName);
        }

        return false;
    }

    private bool IsInSubState(SuperState<TContext> superState, string stateName)
    {
        if (superState.CurrentSubState?.Name == stateName)
            return true;

        if (superState.CurrentSubState is SuperState<TContext> nestedSuper)
        {
            return IsInSubState(nestedSuper, stateName);
        }

        return false;
    }

    /// <summary>
    /// 获取当前完整的状态路径
    /// </summary>
    public List<string> GetStatePath()
    {
        var path = new List<string>();
        AddToPath(_currentState, path);
        return path;
    }

    private void AddToPath(IState<TContext>? state, List<string> path)
    {
        if (state == null) return;
        path.Add(state.Name);

        if (state is SuperState<TContext> superState)
        {
            AddToPath(superState.CurrentSubState, path);
        }
    }

    #endregion
}
