/*
 * GPGems.AI - GOAP: Planner
 * A* 符号规划器（《F.E.A.R.》同款算法）
 */

namespace GPGems.AI.Decision.GOAP;

/// <summary>
/// A* 搜索节点
/// </summary>
internal class SearchNode
{
    public WorldState State { get; }
    public GoapAction? Action { get; }
    public SearchNode? Parent { get; }
    public float G { get; } // 累计代价（从起点到当前节点）
    public float H { get; } // 启发式估计（当前节点到目标的估计代价）
    public float F => G + H; // 总代价

    public SearchNode(WorldState state, GoapAction? action, SearchNode? parent, float g, float h)
    {
        State = state;
        Action = action;
        Parent = parent;
        G = g;
        H = h;
    }
}

/// <summary>
/// GOAP 规划结果
/// </summary>
public class PlanResult
{
    public bool Success { get; }
    public IReadOnlyList<GoapAction> Actions { get; }
    public float TotalCost { get; }
    public int NodesExpanded { get; }

    public PlanResult(bool success, IReadOnlyList<GoapAction> actions, float totalCost, int nodesExpanded)
    {
        Success = success;
        Actions = actions;
        TotalCost = totalCost;
        NodesExpanded = nodesExpanded;
    }

    public static PlanResult Failed(int nodesExpanded) => new(false, Array.Empty<GoapAction>(), 0f, nodesExpanded);

    public static PlanResult Succeeded(IReadOnlyList<GoapAction> actions, float totalCost, int nodesExpanded)
        => new(true, actions, totalCost, nodesExpanded);
}

/// <summary>
/// GOAP A* 规划器
/// </summary>
public class GoapPlanner
{
    /// <summary>最大搜索节点数（防止无限搜索）</summary>
    public int MaxNodes { get; set; } = 1000;

    /// <summary>
    /// 规划动作序列：从初始状态到达目标状态
    /// </summary>
    public PlanResult Plan(WorldState startState, GoapGoal goal, IEnumerable<GoapAction> allActions)
    {
        var actions = allActions.ToList();
        var openList = new List<SearchNode>();
        var closedSet = new HashSet<WorldState>();
        var nodesExpanded = 0;

        // 检查是否已经达成目标
        if (goal.IsSatisfied(startState))
            return PlanResult.Succeeded(Array.Empty<GoapAction>(), 0f, 0);

        // 初始化起始节点
        var startH = startState.CountMismatches(goal.TargetState);
        openList.Add(new SearchNode(new WorldState(startState), null, null, 0f, startH));

        while (openList.Count > 0 && nodesExpanded < MaxNodes)
        {
            // 选择 F 值最小的节点
            openList.Sort((a, b) => a.F.CompareTo(b.F));
            var current = openList[0];
            openList.RemoveAt(0);

            nodesExpanded++;

            // 检查是否到达目标
            if (goal.IsSatisfied(current.State))
                return BuildPlan(current, nodesExpanded);

            closedSet.Add(current.State);

            // 扩展所有可用动作
            foreach (var action in actions)
            {
                // 检查动作是否可以在当前状态执行
                if (!action.CanRunInState(current.State))
                    continue;

                // 应用动作效果，得到新状态
                var newState = new WorldState(current.State);
                newState.ApplyEffect(action.Effects);

                // 如果该状态已经处理过，跳过
                if (closedSet.Contains(newState))
                    continue;

                // 计算新节点的代价
                var newG = current.G + action.Cost;
                var newH = newState.CountMismatches(goal.TargetState);

                // 检查开放列表中是否已有相同状态但代价更高
                var existing = openList.FirstOrDefault(n => n.State.Equals(newState));
                if (existing != null)
                {
                    if (newG < existing.G)
                    {
                        openList.Remove(existing);
                        openList.Add(new SearchNode(newState, action, current, newG, newH));
                    }
                }
                else
                {
                    openList.Add(new SearchNode(newState, action, current, newG, newH));
                }
            }
        }

        // 找不到路径
        return PlanResult.Failed(nodesExpanded);
    }

    /// <summary>
    /// 从搜索节点回溯构建动作序列
    /// </summary>
    private PlanResult BuildPlan(SearchNode endNode, int nodesExpanded)
    {
        var actions = new List<GoapAction>();
        var current = endNode;

        while (current?.Parent != null)
        {
            if (current.Action != null)
                actions.Add(current.Action);
            current = current.Parent;
        }

        actions.Reverse();

        return PlanResult.Succeeded(actions, endNode.G, nodesExpanded);
    }

    /// <summary>
    /// 找出当前最紧急的目标并规划
    /// </summary>
    public (GoapGoal BestGoal, PlanResult Result) PlanBestGoal(
        WorldState startState,
        IEnumerable<GoapGoal> goals,
        IEnumerable<GoapAction> actions)
    {
        var goalList = goals.OrderByDescending(g => g.GetCurrentPriority(startState)).ToList();
        var actionList = actions.ToList();

        foreach (var goal in goalList)
        {
            var result = Plan(startState, goal, actionList);
            if (result.Success)
                return (goal, result);
        }

        // 所有目标都无法达成，返回第一个目标的失败结果
        var firstGoal = goalList.FirstOrDefault();
        return (firstGoal!, PlanResult.Failed(0));
    }
}
