/*
 * GPGems.AI - Utility System Reasoner
 * 效用推理器：选择得分最高的动作并执行
 */
using GPGems.AI.Decision.Blackboards;
namespace GPGems.AI.Decision.Utility;

/// <summary>
/// 选择策略
/// </summary>
public enum SelectionStrategy
{
    /// <summary>选择最高分</summary>
    HighestScore,
    /// <summary>加权随机（高分概率大）</summary>
    WeightedRandom,
    /// <summary>从 TOP N 中随机选</summary>
    TopNRandom
}

/// <summary>
/// 效用推理器
/// </summary>
public class UtilityReasoner
{
    /// <summary>推理器名称</summary>
    public string Name { get; }

    /// <summary>关联的黑板</summary>
    public Blackboard Blackboard { get; }

    /// <summary>选择策略</summary>
    public SelectionStrategy Strategy { get; set; } = SelectionStrategy.HighestScore;

    /// <summary>TOP N 数量</summary>
    public int TopN { get; set; } = 3;

    /// <summary>噪声幅度（防止相同分数永远选同一个）</summary>
    public float NoiseAmount { get; set; } = 0.01f;

    /// <summary>当前执行的动作</summary>
    public UtilityAction? CurrentAction { get; private set; }

    /// <summary>所有动作列表</summary>
    public IReadOnlyList<UtilityAction> Actions => _actions;

    private readonly List<UtilityAction> _actions = new();

    public UtilityReasoner(string name, Blackboard? blackboard = null)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Blackboard = blackboard ?? new Blackboard($"{name}_BB");
    }

    /// <summary>添加动作</summary>
    public UtilityReasoner AddAction(UtilityAction action)
    {
        if (action == null) throw new ArgumentNullException(nameof(action));
        _actions.Add(action);
        return this;
    }

    /// <summary>选择动作</summary>
    public UtilityAction? SelectAction()
    {
        if (_actions.Count == 0) return null;

        // 计算所有动作得分
        var scores = new List<(UtilityAction Action, float Score)>();
        foreach (var action in _actions)
        {
            var score = action.CalculateScore(Blackboard);
            if (score > 0f)
            {
                // 添加微小噪声
                score += (float)(Random.Shared.NextDouble() * NoiseAmount - NoiseAmount / 2);
                scores.Add((action, score));
            }
        }

        if (scores.Count == 0) return null;

        // 按得分降序排序
        scores.Sort((a, b) => b.Score.CompareTo(a.Score));

        return Strategy switch
        {
            SelectionStrategy.HighestScore => scores[0].Action,
            SelectionStrategy.WeightedRandom => SelectWeightedRandom(scores),
            SelectionStrategy.TopNRandom => SelectTopNRandom(scores),
            _ => scores[0].Action
        };
    }

    private UtilityAction? SelectWeightedRandom(List<(UtilityAction Action, float Score)> scores)
    {
        var totalScore = scores.Sum(s => s.Score);
        if (totalScore <= 0f) return scores[0].Action;

        var value = (float)(Random.Shared.NextDouble() * totalScore);
        var cumulative = 0f;

        foreach (var (action, score) in scores)
        {
            cumulative += score;
            if (value <= cumulative)
                return action;
        }

        return scores[0].Action;
    }

    private UtilityAction? SelectTopNRandom(List<(UtilityAction Action, float Score)> scores)
    {
        var count = Math.Min(TopN, scores.Count);
        return scores[Random.Shared.Next(count)].Action;
    }

    /// <summary>每帧更新</summary>
    public void Update()
    {
        var selected = SelectAction();
        if (selected != null && selected != CurrentAction)
        {
            Blackboard.Set("last_action", CurrentAction?.Name ?? "None");
            Blackboard.Set("current_action", selected.Name);
        }
        CurrentAction = selected;
        selected?.Execute(Blackboard);
    }

    /// <summary>获取决策调试信息</summary>
    public string GetDebugInfo()
    {
        var lines = new List<string>
        {
            $"=== Utility Reasoner '{Name}' ===",
            $"Strategy: {Strategy}",
            $"Current Action: {CurrentAction?.Name ?? "None"}",
            "",
            "Action Scores:"
        };

        foreach (var action in _actions.OrderByDescending(a => a.LastScore))
        {
            var marker = action == CurrentAction ? " ►" : "";
            lines.Add($"  {action.Name,20}: {action.LastScore:F3} [{action.Considerations.Count} cons]{marker}");
        }

        return string.Join(Environment.NewLine, lines);
    }
}
