using GPGems.Core;

namespace GPGems.ManorSimulation;

/// <summary>
/// Manor 模块事件定义
/// </summary>
public static class ManorEvents
{
    // ===== 员工相关 =====
    public const string EmployeeRegistered = "Employee.Registered";
    public const string EmployeeUnregistered = "Employee.Unregistered";
    public const string EmployeeStateChanged = "Employee.StateChanged";
    public const string TaskAssigned = "Employee.TaskAssigned";
    public const string TaskCompleted = "Employee.TaskCompleted";

    // ===== 建筑相关 =====
    public const string BuildingAdded = "Building.Added";
    public const string BuildingRemoved = "Building.Removed";

    // ===== 任务相关 =====
    public const string TaskGenerated = "Task.Generated";
}

/// <summary>
/// Manor 模块查询定义
/// </summary>
public static class ManorQueries
{
    public const string GetIdleEmployees = "Query.GetIdleEmployees";
    public const string GetEmployeeById = "Query.GetEmployeeById";
    public const string GetBuildingById = "Query.GetBuildingById";
}

/// <summary>
/// 任务生成事件参数
/// </summary>
public class TaskGeneratedEvent
{
    public EmployeeTaskBase Task { get; }
    public int BuildingId { get; }
    public List<int> AssignedEmployees { get; }

    public TaskGeneratedEvent(EmployeeTaskBase task, int buildingId, List<int> assignedEmployees)
    {
        Task = task;
        BuildingId = buildingId;
        AssignedEmployees = assignedEmployees;
    }
}

/// <summary>
/// 任务分配事件参数
/// </summary>
public class TaskAssignedEvent
{
    public int EmployeeId { get; }
    public int? BuildingId { get; }
    public EmployeeTaskBase Task { get; }

    public TaskAssignedEvent(int employeeId, int? buildingId, EmployeeTaskBase task)
    {
        EmployeeId = employeeId;
        BuildingId = buildingId;
        Task = task;
    }
}

/// <summary>
/// 任务完成事件参数
/// </summary>
public class TaskCompletedEvent
{
    public int EmployeeId { get; }
    public int? BuildingId { get; }
    public string TaskType { get; }

    public TaskCompletedEvent(int employeeId, int? buildingId, string taskType)
    {
        EmployeeId = employeeId;
        BuildingId = buildingId;
        TaskType = taskType;
    }
}
