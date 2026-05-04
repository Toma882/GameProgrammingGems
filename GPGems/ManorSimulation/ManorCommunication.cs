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
