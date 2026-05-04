namespace GPGems.Core.TaskSystem;

/// <summary>
/// 任务组 - 批量管理一组相关任务
/// 支持批量暂停、恢复、取消操作
/// </summary>
public class TaskGroup
{
    public Guid GroupId { get; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;

    private readonly List<TaskBase> _tasks = new();

    public int TaskCount => _tasks.Count;
    public int CompletedCount => _tasks.Count(t => t.State == TaskLifecycle.Completed);
    public int RunningCount => _tasks.Count(t => t.State == TaskLifecycle.Running);

    /// <summary>
    /// 添加任务到组
    /// </summary>
    public void AddTask(TaskBase task)
    {
        task.GroupId = GroupId;
        _tasks.Add(task);
    }

    /// <summary>
    /// 批量添加任务
    /// </summary>
    public void AddTasks(IEnumerable<TaskBase> tasks)
    {
        foreach (var task in tasks)
            AddTask(task);
    }

    /// <summary>
    /// 移除任务
    /// </summary>
    public void RemoveTask(TaskBase task)
    {
        task.GroupId = null;
        _tasks.Remove(task);
    }

    /// <summary>
    /// 获取组内所有任务
    /// </summary>
    public IEnumerable<TaskBase> GetAllTasks() => _tasks.AsReadOnly();

    /// <summary>
    /// 暂停组内所有运行中的任务
    /// </summary>
    public void SuspendAll()
    {
        foreach (var task in _tasks.Where(t => t.State == TaskLifecycle.Running))
        {
            TaskScheduler.Instance.Suspend(task.TaskId);
        }
    }

    /// <summary>
    /// 恢复组内所有暂停的任务
    /// </summary>
    public void ResumeAll()
    {
        foreach (var task in _tasks.Where(t => t.State == TaskLifecycle.Suspended))
        {
            TaskScheduler.Instance.Resume(task.TaskId);
        }
    }

    /// <summary>
    /// 取消组内所有任务
    /// </summary>
    public void CancelAll()
    {
        foreach (var task in _tasks.Where(t =>
            t.State is TaskLifecycle.Running or TaskLifecycle.Scheduled or TaskLifecycle.Pending or TaskLifecycle.Suspended))
        {
            TaskScheduler.Instance.Cancel(task.TaskId);
        }
    }

    /// <summary>
    /// 组内所有任务是否已完成
    /// </summary>
    public bool IsAllCompleted() => _tasks.All(t => t.State == TaskLifecycle.Completed);

    /// <summary>
    /// 获取组任务整体进度 (0-1)
    /// </summary>
    public float GetOverallProgress()
    {
        if (_tasks.Count == 0) return 1f;
        return (float)CompletedCount / _tasks.Count;
    }

    /// <summary>
    /// 提交组内所有任务到调度器
    /// </summary>
    public void SubmitAll()
    {
        TaskScheduler.Instance.SubmitAll(_tasks.Where(t => t.State == TaskLifecycle.Pending));
    }
}
