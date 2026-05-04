/*
 * GPGems.AI - GOAP: Goal
 * GOAP 目标：期望达成的状态 + 优先级
 */

namespace GPGems.AI.Decision.GOAP;

/// <summary>
/// GOAP 目标
/// </summary>
public class GoapGoal
{
    /// <summary>目标名称</summary>
    public string Name { get; }

    /// <summary>目标状态（期望达成的条件）</summary>
    public WorldState TargetState { get; } = new();

    /// <summary>优先级（值越大越重要）</summary>
    public float Priority { get; set; } = 1f;

    /// <summary>动态优先级计算（可选）</summary>
    public Func<WorldState, float>? CalculatePriority { get; set; }

    public GoapGoal(string name)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
    }

    /// <summary>
    /// 添加目标条件
    /// </summary>
    public GoapGoal AddCondition(string key, object value)
    {
        TargetState.Set(key, value);
        return this;
    }

    /// <summary>
    /// 设置基础优先级
    /// </summary>
    public GoapGoal SetPriority(float priority)
    {
        Priority = priority;
        return this;
    }

    /// <summary>
    /// 设置动态优先级计算
    /// </summary>
    public GoapGoal SetPriorityCalculator(Func<WorldState, float> calculator)
    {
        CalculatePriority = calculator;
        return this;
    }

    /// <summary>
    /// 获取当前优先级（动态计算或基础值）
    /// </summary>
    public float GetCurrentPriority(WorldState currentState)
    {
        return CalculatePriority != null
            ? CalculatePriority(currentState)
            : Priority;
    }

    /// <summary>
    /// 检查在给定状态下是否已达成目标
    /// </summary>
    public bool IsSatisfied(WorldState state)
    {
        return state.MeetsCondition(TargetState);
    }

    public override string ToString() => $"{Name} (Priority: {Priority})";
}
