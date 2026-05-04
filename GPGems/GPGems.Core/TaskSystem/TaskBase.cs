namespace GPGems.Core.TaskSystem;

/// <summary>
/// 任务基类
/// 定义所有任务的通用抽象行为
/// 具体业务逻辑由子类实现
/// </summary>
public abstract class TaskBase
{
    public Guid TaskId { get; } = Guid.NewGuid();
    public string Type { get; protected set; } = string.Empty;
    public TaskLifecycle State { get; internal set; } = TaskLifecycle.Pending;
    public int Priority { get; set; } = 0;

    // 任务执行回调（由调度器调用）
    internal Action<TaskBase>? OnComplete { get; set; }
    internal Action<TaskBase, Exception>? OnFail { get; set; }

    /// <summary>
    /// 由具体任务类实现的执行逻辑
    /// </summary>
    public abstract void Execute(float deltaTime);

    /// <summary>
    /// 任务是否完成（由具体任务类判断）
    /// </summary>
    public abstract bool IsCompleted();

    /// <summary>
    /// 取消任务
    /// </summary>
    public virtual void Cancel()
    {
        if (State is TaskLifecycle.Running or TaskLifecycle.Scheduled or TaskLifecycle.Pending)
        {
            State = TaskLifecycle.Cancelled;
        }
    }
}
