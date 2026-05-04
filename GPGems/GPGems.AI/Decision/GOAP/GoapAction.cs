/*
 * GPGems.AI - GOAP: Action
 * GOAP 动作：前置条件 + 效果 + 执行逻辑
 */

namespace GPGems.AI.Decision.GOAP;

/// <summary>
/// GOAP 动作
/// </summary>
public class GoapAction
{
    /// <summary>动作名称</summary>
    public string Name { get; }

    /// <summary>前置条件</summary>
    public WorldState Preconditions { get; } = new();

    /// <summary>执行效果</summary>
    public WorldState Effects { get; } = new();

    /// <summary>动作代价（A* g 值）</summary>
    public float Cost { get; set; } = 1f;

    /// <summary>动作是否可用（可选的额外检查）</summary>
    public Func<WorldState, bool>? IsAvailable { get; set; }

    /// <summary>执行动作的回调</summary>
    public Func<WorldState, WorldState>? Execute { get; set; }

    public GoapAction(string name)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
    }

    /// <summary>
    /// 添加前置条件
    /// </summary>
    public GoapAction AddPrecondition(string key, object value)
    {
        Preconditions.Set(key, value);
        return this;
    }

    /// <summary>
    /// 添加执行效果
    /// </summary>
    public GoapAction AddEffect(string key, object value)
    {
        Effects.Set(key, value);
        return this;
    }

    /// <summary>
    /// 设置代价
    /// </summary>
    public GoapAction SetCost(float cost)
    {
        Cost = cost;
        return this;
    }

    /// <summary>
    /// 设置可用性检查函数
    /// </summary>
    public GoapAction SetAvailable(Func<WorldState, bool> check)
    {
        IsAvailable = check;
        return this;
    }

    /// <summary>
    /// 设置执行回调
    /// </summary>
    public GoapAction SetExecute(Func<WorldState, WorldState> execute)
    {
        Execute = execute;
        return this;
    }

    /// <summary>
    /// 检查在给定状态下是否满足前置条件
    /// </summary>
    public bool CanRunInState(WorldState state)
    {
        if (IsAvailable != null && !IsAvailable(state))
            return false;

        return state.MeetsCondition(Preconditions);
    }

    public override string ToString() => Name;
}
