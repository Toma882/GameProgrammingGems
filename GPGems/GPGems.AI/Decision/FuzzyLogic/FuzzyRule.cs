/*
 * GPGems.AI - Fuzzy Logic: Fuzzy Rule
 * 模糊规则：IF (条件) THEN (结论) 规则定义
 */

namespace GPGems.AI.Decision.FuzzyLogic;

/// <summary>
/// 模糊规则条件项
/// </summary>
public class FuzzyCondition
{
    /// <summary>输入变量名</summary>
    public string VariableName { get; }

    /// <summary>模糊集合名</summary>
    public string SetName { get; }

    public FuzzyCondition(string variableName, string setName)
    {
        VariableName = variableName ?? throw new ArgumentNullException(nameof(variableName));
        SetName = setName ?? throw new ArgumentNullException(nameof(setName));
    }
}

/// <summary>
/// 模糊规则结论项
/// </summary>
public class FuzzyConclusion
{
    /// <summary>输出变量名</summary>
    public string VariableName { get; }

    /// <summary>模糊集合名</summary>
    public string SetName { get; }

    public FuzzyConclusion(string variableName, string setName)
    {
        VariableName = variableName ?? throw new ArgumentNullException(nameof(variableName));
        SetName = setName ?? throw new ArgumentNullException(nameof(setName));
    }
}

/// <summary>
/// 模糊规则
/// 格式：IF Variable1 IS Set1 AND Variable2 IS Set2 THEN Output IS Set3
/// </summary>
public class FuzzyRule
{
    /// <summary>规则名称</summary>
    public string Name { get; }

    /// <summary>条件列表（AND 关系）</summary>
    public IReadOnlyList<FuzzyCondition> Conditions => _conditions;

    /// <summary>结论列表</summary>
    public IReadOnlyList<FuzzyConclusion> Conclusions => _conclusions;

    /// <summary>规则权重</summary>
    public float Weight { get; set; } = 1f;

    private readonly List<FuzzyCondition> _conditions = new();
    private readonly List<FuzzyConclusion> _conclusions = new();

    public FuzzyRule(string name)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
    }

    /// <summary>
    /// 添加条件（AND）
    /// </summary>
    public FuzzyRule If(string variableName, string setName)
    {
        _conditions.Add(new FuzzyCondition(variableName, setName));
        return this;
    }

    /// <summary>
    /// 添加结论
    /// </summary>
    public FuzzyRule Then(string variableName, string setName)
    {
        _conclusions.Add(new FuzzyConclusion(variableName, setName));
        return this;
    }

    /// <summary>
    /// 设置权重
    /// </summary>
    public FuzzyRule WithWeight(float weight)
    {
        Weight = weight;
        return this;
    }

    /// <summary>
    /// 计算规则触发强度（Mamdani 推理：取所有条件隶属度的最小值）
    /// </summary>
    /// <param name="inputMemberships">输入隶属度字典: [变量名][集合名] → 隶属度</param>
    /// <returns>规则触发强度</returns>
    public float CalculateFiringStrength(Dictionary<string, Dictionary<string, float>> inputMemberships)
    {
        if (_conditions.Count == 0) return 0f;

        var minMembership = float.MaxValue;

        foreach (var condition in _conditions)
        {
            if (!inputMemberships.TryGetValue(condition.VariableName, out var setDict))
                return 0f;

            if (!setDict.TryGetValue(condition.SetName, out var membership))
                return 0f;

            // AND 关系：取最小值
            if (membership < minMembership)
                minMembership = membership;
        }

        return minMembership * Weight;
    }

    public override string ToString()
    {
        var conditions = string.Join(" AND ", _conditions.Select(c => $"{c.VariableName} IS {c.SetName}"));
        var conclusions = string.Join(", ", _conclusions.Select(c => $"{c.VariableName} IS {c.SetName}"));
        return $"IF {conditions} THEN {conclusions} (Weight: {Weight})";
    }
}
