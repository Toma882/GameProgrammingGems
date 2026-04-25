/*
 * GPGems.AI - 集成节点：将各决策算法封装为行为树节点
 * FuzzyNode / UtilityNode / GoapNode
 */

using GPGems.AI.Decision.Blackboards;
using GPGems.AI.Decision.FuzzyLogic;
using GPGems.AI.Decision.Utility;
using GPGems.AI.Decision.GOAP;

namespace GPGems.AI.Decision.BehaviorTree;

/// <summary>
/// 模糊逻辑节点：从黑板读取输入，推理后写回黑板
/// </summary>
public class FuzzyNode : Node
{
    private readonly FuzzyEngine _engine;
    private readonly string[] _inputKeys;
    private readonly string[] _outputKeys;

    public FuzzyNode(
        FuzzyEngine engine,
        string[] inputKeys,
        string[] outputKeys,
        string name = "Fuzzy")
        : base(name)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _inputKeys = inputKeys ?? throw new ArgumentNullException(nameof(inputKeys));
        _outputKeys = outputKeys ?? throw new ArgumentNullException(nameof(outputKeys));
    }

    public override NodeStatus Execute()
    {
        // 从黑板读取输入
        var inputs = new Dictionary<string, float>();
        foreach (var key in _inputKeys)
        {
            inputs[key] = Blackboard.GetOrDefault(key, 0f);
        }

        // 执行模糊推理
        var outputs = _engine.Process(inputs);

        // 写回黑板
        for (var i = 0; i < _outputKeys.Length && i < outputs.Count; i++)
        {
            var outputKey = _outputKeys[i];
            var outputValue = outputs.Values.ElementAt(i);
            Blackboard.Set(outputKey, outputValue);
        }

        return NodeStatus.Success;
    }
}

/// <summary>
/// 效用系统节点：计算所有动作得分，选择最佳动作并执行
/// </summary>
public class UtilityNode : Node
{
    private readonly UtilityReasoner _reasoner;
    private readonly string _resultKey;

    public UtilityNode(
        UtilityReasoner reasoner,
        string resultKey = "selected_action",
        string name = "Utility")
        : base(name)
    {
        _reasoner = reasoner ?? throw new ArgumentNullException(nameof(reasoner));
        _resultKey = resultKey;
    }

    public override NodeStatus Execute()
    {
        // 效用系统的 Update 会调用 SelectAction 并执行
        _reasoner.Update();

        // 将结果写回黑板
        if (_reasoner.CurrentAction != null)
        {
            Blackboard.Set(_resultKey, _reasoner.CurrentAction.Name);
            Blackboard.Set($"{_resultKey}_score", _reasoner.CurrentAction.LastScore);
        }

        return NodeStatus.Success;
    }
}

/// <summary>
/// GOAP 规划节点：从黑板读取世界状态，规划动作序列并写回
/// </summary>
public class GoapPlanNode : Node
{
    private readonly GoapAgent _agent;
    private readonly string _planKey;

    public GoapPlanNode(
        GoapAgent agent,
        string planKey = "goap_plan",
        string name = "GOAP Plan")
        : base(name)
    {
        _agent = agent ?? throw new ArgumentNullException(nameof(agent));
        _planKey = planKey;
    }

    public override NodeStatus Execute()
    {
        // GOAP Agent 的 Update 会自动规划并执行
        _agent.Update();

        // 将计划写回黑板
        var planNames = _agent.CurrentPlan.Select(a => a.Name).ToList();
        Blackboard.Set(_planKey, planNames);
        Blackboard.Set($"{_planKey}_goal", _agent.CurrentGoal?.Name ?? "None");
        Blackboard.Set($"{_planKey}_current", _agent.CurrentAction?.Name ?? "None");

        return NodeStatus.Success;
    }
}

/// <summary>
/// GOAP 执行节点：执行当前动作（如果已有规划）
/// </summary>
public class GoapExecuteNode : Node
{
    private readonly GoapAgent _agent;

    public GoapExecuteNode(GoapAgent agent, string name = "GOAP Execute")
        : base(name)
    {
        _agent = agent ?? throw new ArgumentNullException(nameof(agent));
    }

    public override NodeStatus Execute()
    {
        _agent.Update();
        return _agent.State == AgentState.Executing ? NodeStatus.Running : NodeStatus.Success;
    }
}
