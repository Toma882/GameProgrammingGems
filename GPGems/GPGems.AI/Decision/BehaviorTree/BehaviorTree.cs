/*
 * GPGems.AI - Behavior Tree Core
 * 行为树主类：管理根节点、执行、调试
 */


using GPGems.AI.Decision.Blackboards;
namespace GPGems.AI.Decision.BehaviorTree;

/// <summary>
/// 行为树
/// </summary>
public class BehaviorTree
{
    /// <summary>行为树名称</summary>
    public string Name { get; }

    /// <summary>关联的黑板</summary>
    public Blackboard Blackboard { get; }

    /// <summary>根节点</summary>
    public Node? Root { get; private set; }

    /// <summary>是否正在运行</summary>
    public bool IsRunning { get; private set; }

    /// <summary>上一次执行结果</summary>
    public NodeStatus LastStatus { get; private set; }

    public BehaviorTree(string name, Blackboard? blackboard = null)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Blackboard = blackboard ?? new Blackboard($"{name}_BB");
    }

    /// <summary>设置根节点</summary>
    public void SetRoot(Node root)
    {
        if (root == null) throw new ArgumentNullException(nameof(root));
        Root = root;
        SetBlackboardRecursive(root, Blackboard);
    }

    private void SetBlackboardRecursive(Node node, Blackboard bb)
    {
        node.Blackboard = bb;
        foreach (var child in node.Children)
            SetBlackboardRecursive(child, bb);
    }

    /// <summary>启动行为树</summary>
    public void Start()
    {
        Root?.Reset();
        IsRunning = true;
    }

    /// <summary>停止行为树</summary>
    public void Stop()
    {
        IsRunning = false;
        Root?.Reset();
    }

    /// <summary>每帧更新</summary>
    public NodeStatus Update()
    {
        if (!IsRunning || Root == null)
            return NodeStatus.Failure;

        LastStatus = Root.Execute();

        if (LastStatus != NodeStatus.Running)
            IsRunning = false;

        return LastStatus;
    }

    /// <summary>重置行为树</summary>
    public void Reset()
    {
        Root?.Reset();
        IsRunning = false;
    }

    /// <summary>获取行为树结构（用于调试）</summary>
    public string GetStructure()
    {
        if (Root == null) return "(empty)";
        return GetNodeStructure(Root, 0);
    }

    private string GetNodeStructure(Node node, int indent)
    {
        var prefix = new string(' ', indent * 2);
        var result = $"{prefix}{node.Name} [{node.GetType().Name}]\n";
        foreach (var child in node.Children)
            result += GetNodeStructure(child, indent + 1);
        return result;
    }
}

/// <summary>
/// 行为树构建器（Fluent API）
/// </summary>
public class BehaviorTreeBuilder
{
    private readonly BehaviorTree _tree;
    private readonly Stack<Node> _nodeStack = new();

    private BehaviorTreeBuilder(string name, Blackboard? blackboard = null)
    {
        _tree = new BehaviorTree(name, blackboard);
    }

    public static BehaviorTreeBuilder Create(string name, Blackboard? blackboard = null)
    {
        return new BehaviorTreeBuilder(name, blackboard);
    }

    public BehaviorTreeBuilder Selector(string name = "Selector")
    {
        var node = new Selector(name);
        AddToParent(node);
        _nodeStack.Push(node);
        return this;
    }

    public BehaviorTreeBuilder Sequence(string name = "Sequence")
    {
        var node = new Sequence(name);
        AddToParent(node);
        _nodeStack.Push(node);
        return this;
    }

    public BehaviorTreeBuilder Parallel(
        ParallelPolicy success = ParallelPolicy.AllSuccess,
        ParallelPolicy failure = ParallelPolicy.OneFailure,
        string name = "Parallel")
    {
        var node = new Parallel(success, failure, name);
        AddToParent(node);
        _nodeStack.Push(node);
        return this;
    }

    public BehaviorTreeBuilder Inverter(string name = "Inverter")
    {
        var node = new Inverter(null, name);
        AddToParent(node);
        _nodeStack.Push(node);
        return this;
    }

    public BehaviorTreeBuilder Repeat(int count = 0, string name = "Repeat")
    {
        var node = new Repeat(count, null, name);
        AddToParent(node);
        _nodeStack.Push(node);
        return this;
    }

    public BehaviorTreeBuilder UntilFail(string name = "UntilFail")
    {
        var node = new UntilFail(null, name);
        AddToParent(node);
        _nodeStack.Push(node);
        return this;
    }

    public BehaviorTreeBuilder Action(Func<Blackboard, NodeStatus> action, string name = "Action")
    {
        AddToParent(new FuncAction(action, name));
        return this;
    }

    public BehaviorTreeBuilder Condition(Func<Blackboard, bool> condition, string name = "Condition")
    {
        AddToParent(new FuncCondition(condition, name));
        return this;
    }

    public BehaviorTreeBuilder Condition(string key, bool expected = true)
    {
        AddToParent(new BoolCondition(key, expected));
        return this;
    }

    public BehaviorTreeBuilder Condition(string key, bool expected, Func<Blackboard, bool> condition, string? name = null)
    {
        AddToParent(new FuncCondition(condition, name ?? $"Condition({key})"));
        return this;
    }

    public BehaviorTreeBuilder Wait(float duration, string name = "Wait")
    {
        AddToParent(new WaitAction(duration, name));
        return this;
    }

    public BehaviorTreeBuilder Log(string message, string name = "Log")
    {
        AddToParent(new LogAction(message, name));
        return this;
    }

    public BehaviorTreeBuilder SetValue(string key, object value, float? ttl = null)
    {
        AddToParent(new SetValueAction(key, value, ttl));
        return this;
    }

    public BehaviorTreeBuilder Success(string name = "Success")
    {
        AddToParent(new SuccessAction(name));
        return this;
    }

    public BehaviorTreeBuilder Failure(string name = "Failure")
    {
        AddToParent(new FailureAction(name));
        return this;
    }

    public BehaviorTreeBuilder End()
    {
        _nodeStack.Pop();
        return this;
    }

    private void AddToParent(Node node)
    {
        if (_nodeStack.Count > 0)
            _nodeStack.Peek().AddChild(node);
        else
            _tree.SetRoot(node);
    }

    public BehaviorTree Build()
    {
        while (_nodeStack.Count > 0)
            _nodeStack.Pop();
        return _tree;
    }
}
