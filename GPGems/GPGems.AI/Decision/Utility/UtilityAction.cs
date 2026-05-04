/*
 * GPGems.AI - Utility System Action
 * 效用动作：包含多个考虑因素，综合计算得分并执行
 */
using GPGems.AI.Decision.Blackboards;
namespace GPGems.AI.Decision.Utility;

/// <summary>
/// 效用动作
/// </summary>
public class UtilityAction
{
    /// <summary>动作名称</summary>
    public string Name { get; }

    /// <summary>基础得分</summary>
    public float BaseScore { get; set; } = 0.5f;

    /// <summary>是否启用</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>最近一次得分</summary>
    public float LastScore { get; private set; }

    /// <summary>考虑因素列表</summary>
    public IReadOnlyList<IConsideration> Considerations => _considerations;

    private readonly List<IConsideration> _considerations = new();
    private readonly Func<Blackboard, float>? _execute;

    public UtilityAction(string name, Func<Blackboard, float>? execute = null)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        _execute = execute;
    }

    /// <summary>添加考虑因素</summary>
    public UtilityAction AddConsideration(IConsideration consideration)
    {
        if (consideration == null) throw new ArgumentNullException(nameof(consideration));
        _considerations.Add(consideration);
        return this;
    }

    /// <summary>添加黑板值考虑因素</summary>
    public UtilityAction AddConsideration(string name, string key, UtilityCurve curve, float weight = 1f)
    {
        return AddConsideration(new BlackboardConsideration(name, key, curve) { Weight = weight });
    }

    /// <summary>添加函数考虑因素</summary>
    public UtilityAction AddConsideration(string name, Func<Blackboard, float> evaluator, float weight = 1f)
    {
        return AddConsideration(new FuncConsideration(name, evaluator, weight));
    }

    /// <summary>计算综合得分 (0-1)</summary>
    public float CalculateScore(Blackboard blackboard)
    {
        if (!Enabled || _considerations.Count == 0)
        {
            LastScore = Enabled ? BaseScore : 0f;
            return LastScore;
        }

        var totalWeight = 0f;
        var weightedScore = 0f;

        foreach (var consideration in _considerations)
        {
            var score = consideration.Evaluate(blackboard);
            weightedScore += score * consideration.Weight;
            totalWeight += consideration.Weight;
        }

        LastScore = totalWeight > 0 ? weightedScore / totalWeight * BaseScore : BaseScore;
        return LastScore;
    }

    /// <summary>执行动作</summary>
    public float Execute(Blackboard blackboard)
    {
        return _execute?.Invoke(blackboard) ?? 1f;
    }
}
