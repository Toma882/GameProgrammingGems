/*
 * GPGems.AI - GOAP: Agent
 * GOAP 代理：规划 + 执行一体化
 */

namespace GPGems.AI.Decision.GOAP;

/// <summary>
/// GOAP 代理状态
/// </summary>
public enum AgentState
{
    /// <summary>空闲，需要重新规划</summary>
    Idle,
    /// <summary>正在执行动作</summary>
    Executing,
    /// <summary>动作执行失败</summary>
    Failed
}

/// <summary>
/// GOAP 智能代理
/// </summary>
public class GoapAgent
{
    /// <summary>代理名称</summary>
    public string Name { get; }

    /// <summary>当前世界状态</summary>
    public WorldState CurrentState { get; private set; } = new();

    /// <summary>当前执行状态</summary>
    public AgentState State { get; private set; }

    /// <summary>当前目标</summary>
    public GoapGoal? CurrentGoal { get; private set; }

    /// <summary>当前执行的动作</summary>
    public GoapAction? CurrentAction { get; private set; }

    /// <summary>当前规划的动作序列</summary>
    public IReadOnlyList<GoapAction> CurrentPlan => _currentPlan;

    /// <summary>所有可用动作</summary>
    public ICollection<GoapAction> Actions => _actions;

    /// <summary>所有目标</summary>
    public ICollection<GoapGoal> Goals => _goals;

    /// <summary>规划器</summary>
    public GoapPlanner Planner { get; } = new();

    private readonly List<GoapAction> _actions = new();
    private readonly List<GoapGoal> _goals = new();
    private List<GoapAction> _currentPlan = new();
    private int _planIndex;

    /// <summary>状态变化事件</summary>
    public event Action<string, GoapAction?>? OnActionStarted;
    public event Action<string, GoapAction, bool>? OnActionCompleted;
    public event Action<string, GoapGoal>? OnNewGoal;
    public event Action<string, PlanResult>? OnPlanCreated;

    public GoapAgent(string name)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
    }

    /// <summary>
    /// 添加动作
    /// </summary>
    public GoapAgent AddAction(GoapAction action)
    {
        _actions.Add(action);
        return this;
    }

    /// <summary>
    /// 添加目标
    /// </summary>
    public GoapAgent AddGoal(GoapGoal goal)
    {
        _goals.Add(goal);
        return this;
    }

    /// <summary>
    /// 更新世界状态
    /// </summary>
    public void UpdateState(string key, object value)
    {
        CurrentState.Set(key, value);
    }

    /// <summary>
    /// 每帧更新
    /// </summary>
    public void Update()
    {
        switch (State)
        {
            case AgentState.Idle:
                FindNewPlan();
                break;

            case AgentState.Executing:
                ExecuteCurrentAction();
                break;

            case AgentState.Failed:
                // 失败后等待一帧再重试
                State = AgentState.Idle;
                break;
        }
    }

    /// <summary>
    /// 寻找新规划
    /// </summary>
    private void FindNewPlan()
    {
        if (_goals.Count == 0 || _actions.Count == 0)
            return;

        // 找出最佳目标和规划
        var (bestGoal, result) = Planner.PlanBestGoal(CurrentState, _goals, _actions);

        if (!result.Success)
        {
            State = AgentState.Failed;
            return;
        }

        CurrentGoal = bestGoal;
        _currentPlan = result.Actions.ToList();
        _planIndex = 0;

        OnNewGoal?.Invoke(Name, bestGoal);
        OnPlanCreated?.Invoke(Name, result);

        // 开始执行第一个动作
        StartNextAction();
    }

    /// <summary>
    /// 开始执行下一个动作
    /// </summary>
    private void StartNextAction()
    {
        if (_planIndex >= _currentPlan.Count)
        {
            // 计划完成，回到空闲状态重新规划
            State = AgentState.Idle;
            CurrentAction = null;
            return;
        }

        CurrentAction = _currentPlan[_planIndex];
        State = AgentState.Executing;

        OnActionStarted?.Invoke(Name, CurrentAction);
    }

    /// <summary>
    /// 执行当前动作
    /// </summary>
    private void ExecuteCurrentAction()
    {
        if (CurrentAction == null)
        {
            State = AgentState.Idle;
            return;
        }

        // 检查前置条件是否仍然满足（可能环境已变化）
        if (!CurrentAction.CanRunInState(CurrentState))
        {
            // 前置条件不满足，中止当前计划重新规划
            OnActionCompleted?.Invoke(Name, CurrentAction, false);
            State = AgentState.Idle;
            CurrentAction = null;
            _currentPlan.Clear();
            return;
        }

        // 执行动作
        var success = true;
        if (CurrentAction.Execute != null)
        {
            CurrentState = CurrentAction.Execute(CurrentState);
        }
        else
        {
            // 如果没有 Execute 回调，直接应用 Effects
            CurrentState.ApplyEffect(CurrentAction.Effects);
        }

        OnActionCompleted?.Invoke(Name, CurrentAction, success);

        // 移动到下一个动作
        _planIndex++;
        StartNextAction();
    }

    /// <summary>
    /// 强制中止当前计划并重新规划
    /// </summary>
    public void AbortPlan()
    {
        State = AgentState.Idle;
        CurrentAction = null;
        _currentPlan.Clear();
    }

    /// <summary>
    /// 获取调试信息
    /// </summary>
    public string GetDebugInfo()
    {
        var lines = new List<string>
        {
            $"=== GOAP Agent '{Name}' ===",
            $"State: {State}",
            $"Current Goal: {CurrentGoal?.Name ?? "None"}",
            $"Current Action: {CurrentAction?.Name ?? "None"}",
            ""
        };

        if (_currentPlan.Count > 0)
        {
            lines.Add("Current Plan:");
            for (var i = 0; i < _currentPlan.Count; i++)
            {
                var marker = i == _planIndex ? " ►" : "";
                lines.Add($"  {i + 1}. {_currentPlan[i].Name}{marker}");
            }
            lines.Add("");
        }

        lines.Add("Current World State:");
        foreach (var (key, value) in CurrentState.GetAll())
        {
            lines.Add($"  {key} = {value}");
        }

        lines.Add("");
        lines.Add("Goals (by priority):");
        foreach (var goal in _goals.OrderByDescending(g => g.GetCurrentPriority(CurrentState)))
        {
            var satisfied = goal.IsSatisfied(CurrentState) ? " ✓" : "";
            lines.Add($"  {goal.Name}: Priority={goal.GetCurrentPriority(CurrentState):F1}{satisfied}");
        }

        return string.Join(Environment.NewLine, lines);
    }
}
