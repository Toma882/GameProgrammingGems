namespace GPGems.ManorSimulation;

/// <summary>
/// 员工任务分配策略接口
/// 可插拔的策略模式，支持不同的分配算法
/// </summary>
public interface IWorkerAssignmentStrategy
{
    /// <summary>
    /// 注册可用员工
    /// </summary>
    void RegisterWorker(EmployeeData employee);

    /// <summary>
    /// 注销员工
    /// </summary>
    void UnregisterWorker(int employeeId);

    /// <summary>
    /// 分配工人到任务
    /// 返回选中的员工ID，失败返回 null
    /// </summary>
    int? AssignWorker(EmployeeTaskBase task, List<int> assignedEmployees);
}
