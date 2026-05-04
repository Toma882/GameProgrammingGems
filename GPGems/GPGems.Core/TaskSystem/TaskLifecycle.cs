namespace GPGems.Core.TaskSystem;

/// <summary>
/// 任务生命周期状态
/// 由任务调度器统一定义
/// </summary>
public enum TaskLifecycle
{
    Pending,      // 等待调度
    Scheduled,    // 已入队
    Running,      // 执行中
    Suspended,    // 暂停
    Completed,    // 完成
    Cancelled,    // 取消
    Failed        // 失败
}
