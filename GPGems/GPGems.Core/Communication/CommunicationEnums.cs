namespace GPGems.Core;

/// <summary>
/// 通用事件定义常量
/// 各模块可自定义自己的事件常量
/// </summary>
public static class CommonEvents
{
    // ===== 对象生命周期 =====
    public const string ObjectRegistered = "Object.Registered";
    public const string ObjectUnregistered = "Object.Unregistered";
    public const string ObjectStateChanged = "Object.StateChanged";

    // ===== 任务相关 =====
    public const string TaskGenerated = "Task.Generated";
    public const string TaskAssigned = "Task.Assigned";
    public const string TaskCompleted = "Task.Completed";
}

/// <summary>
/// 通用查询定义常量
/// 各模块可自定义自己的查询常量
/// </summary>
public static class CommonQueries
{
    public const string GetById = "Query.GetById";
    public const string GetAll = "Query.GetAll";
}
