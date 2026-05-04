using System.Numerics;
using GPGems.Core;

namespace GPGems.ManorSimulation;

/// <summary>
/// 基于评分的任务分配策略
/// 通过 CommunicationBus 获取所需数据，与其他模块完全解耦
/// </summary>
public class ScoredAssignmentStrategy : IWorkerAssignmentStrategy
{
    private readonly Dictionary<int, EmployeeData> _workers = new();

    // 评分权重配置
    public float DistanceWeight { get; set; } = 1f;
    public float SkillWeight { get; set; } = 5f;
    public float LoadBalanceWeight { get; set; } = 0.1f;
    public float PreferenceWeight { get; set; } = 10f;

    public void RegisterWorker(EmployeeData employee)
    {
        _workers[employee.Id] = employee;
    }

    public void UnregisterWorker(int employeeId)
    {
        _workers.Remove(employeeId);
    }

    public int? AssignWorker(EmployeeTaskBase task, List<int> assignedEmployees)
    {
        var idleWorkers = CommunicationBus.Instance.Query<IEnumerable<EmployeeData>>(ManorQueries.GetIdleEmployees);
        if (idleWorkers == null)
            return null;

        float bestScore = float.MinValue;
        int? bestWorkerId = null;

        foreach (var worker in idleWorkers)
        {
            // 跳过已分配到该建筑的员工
            if (assignedEmployees.Contains(worker.Id))
                continue;

            float score = CalculateScore(worker, task);
            if (score > bestScore)
            {
                bestScore = score;
                bestWorkerId = worker.Id;
            }
        }

        return bestWorkerId;
    }

    /// <summary>
    /// 计算员工-任务匹配分数
    /// </summary>
    private float CalculateScore(EmployeeData worker, EmployeeTaskBase task)
    {
        float score = 0;

        // 1. 距离分（越近越好
        float distance = Vector2.Distance(worker.Position, task.TargetPosition);
        score -= distance * DistanceWeight;

        // 2. 技能匹配分
        float skillLevel = worker.GetSkillLevel(task.Type);
        score += skillLevel * SkillWeight;

        // 3. 工作量分（越闲越好
        score -= worker.TaskCountToday * LoadBalanceWeight;

        // 4. 任务类型偏好
        if (worker.PreferredTaskType == task.Type)
        {
            score += PreferenceWeight;
        }

        return score;
    }
}
