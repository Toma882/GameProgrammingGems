/*
 * GPGems.AI - Behavior Tree Condition Nodes
 * 条件节点：检查黑板值或自定义条件
 */
using GPGems.AI.Decision.Blackboards;
namespace GPGems.AI.Decision.BehaviorTree;

/// <summary>
/// 条件节点基类
/// </summary>
public abstract class Condition : Node
{
    protected Condition(string name = "Condition") : base(name)
    {
    }
}

/// <summary>
/// 函数条件：使用自定义函数判断
/// </summary>
public class FuncCondition : Condition
{
    private readonly Func<Blackboard, bool> _condition;

    public FuncCondition(Func<Blackboard, bool> condition, string name = "FuncCondition")
        : base(name)
    {
        _condition = condition ?? throw new ArgumentNullException(nameof(condition));
    }

    public override NodeStatus Execute()
    {
        return _condition(Blackboard) ? NodeStatus.Success : NodeStatus.Failure;
    }
}

/// <summary>
/// 黑板值存在条件
/// </summary>
public class HasValueCondition : Condition
{
    private readonly string _key;

    public HasValueCondition(string key, string name = "HasValue")
        : base($"{name}({key})")
    {
        _key = key ?? throw new ArgumentNullException(nameof(key));
    }

    public override NodeStatus Execute()
    {
        return Blackboard.HasKey(_key) ? NodeStatus.Success : NodeStatus.Failure;
    }
}

/// <summary>
/// 黑板值等于条件
/// </summary>
public class ValueEqualCondition<T> : Condition
{
    private readonly string _key;
    private readonly T _value;

    public ValueEqualCondition(string key, T value, string name = "ValueEqual")
        : base($"{name}({key} = {value})")
    {
        _key = key ?? throw new ArgumentNullException(nameof(key));
        _value = value;
    }

    public override NodeStatus Execute()
    {
        if (!Blackboard.TryGet<T>(_key, out var val))
            return NodeStatus.Failure;

        return EqualityComparer<T>.Default.Equals(val, _value) ? NodeStatus.Success : NodeStatus.Failure;
    }
}

/// <summary>
/// 布尔值条件
/// </summary>
public class BoolCondition : Condition
{
    private readonly string _key;
    private readonly bool _expected;

    public BoolCondition(string key, bool expected = true, string name = "Bool")
        : base($"{name}({key} = {expected})")
    {
        _key = key ?? throw new ArgumentNullException(nameof(key));
        _expected = expected;
    }

    public override NodeStatus Execute()
    {
        if (!Blackboard.TryGet<bool>(_key, out var val))
            return NodeStatus.Failure;

        return val == _expected ? NodeStatus.Success : NodeStatus.Failure;
    }
}

/// <summary>
/// 数值比较条件
/// </summary>
public class CompareCondition : Condition
{
    private readonly string _key;
    private readonly float _threshold;
    private readonly ComparisonType _type;

    public CompareCondition(string key, float threshold, ComparisonType type, string name = "Compare")
        : base($"{name}({key} {GetOperator(type)} {threshold})")
    {
        _key = key ?? throw new ArgumentNullException(nameof(key));
        _threshold = threshold;
        _type = type;
    }

    private static string GetOperator(ComparisonType type) => type switch
    {
        ComparisonType.Greater => ">",
        ComparisonType.GreaterOrEqual => ">=",
        ComparisonType.Less => "<",
        ComparisonType.LessOrEqual => "<=",
        _ => "=="
    };

    public override NodeStatus Execute()
    {
        if (!Blackboard.TryGet<float>(_key, out var val))
            return NodeStatus.Failure;

        var result = _type switch
        {
            ComparisonType.Greater => val > _threshold,
            ComparisonType.GreaterOrEqual => val >= _threshold,
            ComparisonType.Less => val < _threshold,
            ComparisonType.LessOrEqual => val <= _threshold,
            _ => false
        };

        return result ? NodeStatus.Success : NodeStatus.Failure;
    }
}

public enum ComparisonType
{
    Greater,
    GreaterOrEqual,
    Less,
    LessOrEqual
}
