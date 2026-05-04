/*
 * GPGems.AI - Fuzzy Logic: Fuzzy Engine
 * 模糊推理引擎：Mamdani 推理 + 去模糊化
 */

namespace GPGems.AI.Decision.FuzzyLogic;

/// <summary>
/// 去模糊化方法
/// </summary>
public enum DefuzzificationMethod
{
    /// <summary>质心法（Centroid / Area Bisector）</summary>
    Centroid,

    /// <summary>最大值平均法（Mean of Maxima）</summary>
    MeanOfMaxima,

    /// <summary>最大值最小值法（Smallest of Maxima）</summary>
    SmallestOfMaxima,

    /// <summary>最大值最大值法（Largest of Maxima）</summary>
    LargestOfMaxima
}

/// <summary>
/// 模糊变量：输入或输出变量及其模糊集合定义
/// </summary>
public class FuzzyVariable
{
    /// <summary>变量名称</summary>
    public string Name { get; }

    /// <summary>最小值</summary>
    public float MinValue { get; }

    /// <summary>最大值</summary>
    public float MaxValue { get; }

    /// <summary>模糊集合</summary>
    public IReadOnlyDictionary<string, FuzzySet> Sets => _sets;

    private readonly Dictionary<string, FuzzySet> _sets = new();

    public FuzzyVariable(string name, float minValue, float maxValue)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        MinValue = minValue;
        MaxValue = maxValue;
    }

    /// <summary>
    /// 添加模糊集合
    /// </summary>
    public FuzzyVariable AddSet(FuzzySet set)
    {
        _sets[set.Name] = set ?? throw new ArgumentNullException(nameof(set));
        return this;
    }

    /// <summary>
    /// 计算输入值在所有集合中的隶属度
    /// </summary>
    public Dictionary<string, float> CalculateAllMemberships(float value)
    {
        var result = new Dictionary<string, float>();
        foreach (var (name, set) in _sets)
        {
            result[name] = set.CalculateMembership(value);
        }
        return result;
    }

    /// <summary>
    /// 获取所有集合的峰值
    /// </summary>
    public Dictionary<string, float> GetAllPeaks()
    {
        var result = new Dictionary<string, float>();
        foreach (var (name, set) in _sets)
        {
            result[name] = set.GetPeak();
        }
        return result;
    }
}

/// <summary>
/// 模糊推理引擎
/// </summary>
public class FuzzyEngine
{
    /// <summary>引擎名称</summary>
    public string Name { get; }

    /// <summary>输入变量</summary>
    public IReadOnlyDictionary<string, FuzzyVariable> InputVariables => _inputVariables;

    /// <summary>输出变量</summary>
    public IReadOnlyDictionary<string, FuzzyVariable> OutputVariables => _outputVariables;

    /// <summary>规则库</summary>
    public IReadOnlyList<FuzzyRule> Rules => _rules;

    /// <summary>去模糊化方法</summary>
    public DefuzzificationMethod DefuzzMethod { get; set; } = DefuzzificationMethod.Centroid;

    private readonly Dictionary<string, FuzzyVariable> _inputVariables = new();
    private readonly Dictionary<string, FuzzyVariable> _outputVariables = new();
    private readonly List<FuzzyRule> _rules = new();

    public FuzzyEngine(string name)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
    }

    /// <summary>
    /// 添加输入变量
    /// </summary>
    public FuzzyEngine AddInputVariable(FuzzyVariable variable)
    {
        _inputVariables[variable.Name] = variable ?? throw new ArgumentNullException(nameof(variable));
        return this;
    }

    /// <summary>
    /// 添加输出变量
    /// </summary>
    public FuzzyEngine AddOutputVariable(FuzzyVariable variable)
    {
        _outputVariables[variable.Name] = variable ?? throw new ArgumentNullException(nameof(variable));
        return this;
    }

    /// <summary>
    /// 添加规则
    /// </summary>
    public FuzzyEngine AddRule(FuzzyRule rule)
    {
        _rules.Add(rule ?? throw new ArgumentNullException(nameof(rule)));
        return this;
    }

    /// <summary>
    /// 规则推理
    /// </summary>
    /// <param name="inputs">输入值字典: [变量名] → 值</param>
    /// <returns>推理结果: [输出变量名] → 模糊输出集合的触发强度</returns>
    public Dictionary<string, Dictionary<string, float>> Infer(Dictionary<string, float> inputs)
    {
        // 1. 模糊化：计算所有输入的隶属度
        var inputMemberships = new Dictionary<string, Dictionary<string, float>>();
        foreach (var (varName, value) in inputs)
        {
            if (_inputVariables.TryGetValue(varName, out var variable))
            {
                inputMemberships[varName] = variable.CalculateAllMemberships(value);
            }
        }

        // 2. 推理：计算每条规则的触发强度，并累积到输出集合
        var outputFiringStrengths = new Dictionary<string, Dictionary<string, float>>();
        foreach (var outputVar in _outputVariables.Values)
        {
            outputFiringStrengths[outputVar.Name] = new Dictionary<string, float>();
            foreach (var setName in outputVar.Sets.Keys)
            {
                outputFiringStrengths[outputVar.Name][setName] = 0f;
            }
        }

        foreach (var rule in _rules)
        {
            var firingStrength = rule.CalculateFiringStrength(inputMemberships);

            if (firingStrength > 0f)
            {
                foreach (var conclusion in rule.Conclusions)
                {
                    if (outputFiringStrengths.TryGetValue(conclusion.VariableName, out var setDict))
                    {
                        if (setDict.ContainsKey(conclusion.SetName))
                        {
                            // 取最大值（OR 聚合）
                            setDict[conclusion.SetName] = Math.Max(setDict[conclusion.SetName], firingStrength);
                        }
                    }
                }
            }
        }

        return outputFiringStrengths;
    }

    /// <summary>
    /// 去模糊化（质心法）
    /// </summary>
    /// <param name="outputVarName">输出变量名</param>
    /// <param name="firingStrengths">触发强度字典: [集合名] → 强度</param>
    /// <param name="samples">采样点数</param>
    /// <returns>精确输出值</returns>
    public float DefuzzifyCentroid(string outputVarName, Dictionary<string, float> firingStrengths, int samples = 100)
    {
        if (!_outputVariables.TryGetValue(outputVarName, out var variable))
            return 0f;

        var range = variable.MaxValue - variable.MinValue;
        var step = range / samples;

        double numerator = 0.0;
        double denominator = 0.0;

        for (var i = 0; i <= samples; i++)
        {
            var x = variable.MinValue + i * step;

            // 计算 x 点的聚合隶属度（取所有集合被裁剪后的最大值）
            var maxMembership = 0f;
            foreach (var (setName, strength) in firingStrengths)
            {
                if (variable.Sets.TryGetValue(setName, out var set))
                {
                    // Mamdani 推理：裁剪输出集合
                    var membership = Math.Min(set.CalculateMembership(x), strength);
                    maxMembership = Math.Max(maxMembership, membership);
                }
            }

            numerator += x * maxMembership;
            denominator += maxMembership;
        }

        return denominator > 0 ? (float)(numerator / denominator) : variable.MinValue;
    }

    /// <summary>
    /// 去模糊化（最大值平均法）
    /// </summary>
    public float DefuzzifyMeanOfMaxima(string outputVarName, Dictionary<string, float> firingStrengths)
    {
        if (!_outputVariables.TryGetValue(outputVarName, out var variable))
            return 0f;

        var peaks = variable.GetAllPeaks();
        float maxStrength = 0f;
        float sum = 0f;
        int count = 0;

        foreach (var (setName, strength) in firingStrengths)
        {
            if (strength > maxStrength)
            {
                maxStrength = strength;
                sum = peaks[setName];
                count = 1;
            }
            else if (Math.Abs(strength - maxStrength) < 0.0001f && maxStrength > 0f)
            {
                sum += peaks[setName];
                count++;
            }
        }

        return count > 0 ? sum / count : variable.MinValue;
    }

    /// <summary>
    /// 执行完整推理流程：模糊化 → 推理 → 去模糊化
    /// </summary>
    /// <param name="inputs">输入值字典</param>
    /// <returns>精确输出值字典</returns>
    public Dictionary<string, float> Process(Dictionary<string, float> inputs)
    {
        var fuzzyOutputs = Infer(inputs);
        var results = new Dictionary<string, float>();

        foreach (var (outputVarName, firingStrengths) in fuzzyOutputs)
        {
            results[outputVarName] = DefuzzMethod switch
            {
                DefuzzificationMethod.Centroid => DefuzzifyCentroid(outputVarName, firingStrengths),
                DefuzzificationMethod.MeanOfMaxima => DefuzzifyMeanOfMaxima(outputVarName, firingStrengths),
                _ => DefuzzifyCentroid(outputVarName, firingStrengths)
            };
        }

        return results;
    }

    /// <summary>
    /// 获取调试信息
    /// </summary>
    public string GetDebugInfo(Dictionary<string, float> inputs)
    {
        var lines = new List<string>
        {
            $"=== Fuzzy Engine '{Name}' ===",
            "",
            "Inputs:"
        };

        foreach (var (name, value) in inputs)
        {
            lines.Add($"  {name}: {value:F2}");
            if (_inputVariables.TryGetValue(name, out var variable))
            {
                var memberships = variable.CalculateAllMemberships(value);
                foreach (var (setName, mem) in memberships.Where(m => m.Value > 0.01f))
                {
                    lines.Add($"    {setName}: {mem:F3}");
                }
            }
        }

        lines.Add("");
        lines.Add("Rules Fired:");

        var inputMemberships = new Dictionary<string, Dictionary<string, float>>();
        foreach (var (varName, value) in inputs)
        {
            if (_inputVariables.TryGetValue(varName, out var variable))
            {
                inputMemberships[varName] = variable.CalculateAllMemberships(value);
            }
        }

        foreach (var rule in _rules)
        {
            var strength = rule.CalculateFiringStrength(inputMemberships);
            if (strength > 0.01f)
            {
                lines.Add($"  [{strength:F3}] {rule.Name}");
            }
        }

        var outputs = Process(inputs);
        lines.Add("");
        lines.Add("Defuzzified Outputs:");
        foreach (var (name, value) in outputs)
        {
            lines.Add($"  {name}: {value:F2}");
        }

        return string.Join(Environment.NewLine, lines);
    }
}
