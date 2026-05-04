namespace GPGems.Core.TaskSystem;

/// <summary>
/// 全局任务调度器（单例）
/// 只负责机制：生命周期管理、队列调度、执行关系规则
/// 不关心任何具体业务（不知道"员工"、"建筑"等概念）
///
/// 设计原则：
/// 1. 任务生命周期 - 统一定义所有任务的状态流转
/// 2. 任务执行队列 - 优先级队列 + FIFO 双队列支持
/// 3. 任务关系规则 - 业务类通过 ITaskRelationProvider 自行维护
/// 4. 反向依赖 - 调度器只执行规则，不定义业务
/// </summary>
public class TaskScheduler
{
    public static TaskScheduler Instance { get; } = new TaskScheduler();

    // 执行队列
    private readonly Queue<TaskBase> _pendingQueue = new();
    private readonly List<TaskBase> _runningTasks = new();
    private readonly PriorityQueue<TaskBase, int> _priorityQueue = new();
    private readonly List<TaskBase> _suspendedTasks = new();

    // 关系注册表（业务类注册自己的关系提供者）
    private readonly Dictionary<Type, ITaskRelationProvider> _relationProviders = new();

    // 任务组注册表
    private readonly Dictionary<Guid, TaskGroup> _taskGroups = new();

    // 统计
    public int TotalSubmitted { get; private set; }
    public int TotalCompleted { get; private set; }
    public int RunningCount => _runningTasks.Count;
    public int PendingCount => _pendingQueue.Count + _priorityQueue.UnorderedItems.Count();
    public int SuspendedCount => _suspendedTasks.Count;

    private TaskScheduler() { }

    /// <summary>
    /// 业务类注册自己的关系提供者
    /// 反向依赖：业务告诉调度器"我的任务关系是什么"
    /// </summary>
    public void RegisterRelationProvider(Type taskType, ITaskRelationProvider provider)
    {
        _relationProviders[taskType] = provider;
    }

    /// <summary>
    /// 提交任务到调度器
    /// </summary>
    public void Submit(TaskBase task)
    {
        if (task.State != TaskLifecycle.Pending)
            throw new InvalidOperationException("只能提交等待中的任务");

        TotalSubmitted++;
        task.State = TaskLifecycle.Scheduled;

        if (task.Priority != 0)
            _priorityQueue.Enqueue(task, -task.Priority);
        else
            _pendingQueue.Enqueue(task);
    }

    /// <summary>
    /// 批量提交任务
    /// </summary>
    public void SubmitAll(IEnumerable<TaskBase> tasks)
    {
        foreach (var task in tasks)
            Submit(task);
    }

    /// <summary>
    /// 暂停指定任务
    /// </summary>
    public void Suspend(Guid taskId)
    {
        var task = _runningTasks.FirstOrDefault(t => t.TaskId == taskId);
        if (task != null)
        {
            task.State = TaskLifecycle.Suspended;
            _runningTasks.Remove(task);
            _suspendedTasks.Add(task);
        }
    }

    /// <summary>
    /// 恢复指定任务
    /// </summary>
    public void Resume(Guid taskId)
    {
        var task = _suspendedTasks.FirstOrDefault(t => t.TaskId == taskId);
        if (task != null)
        {
            _suspendedTasks.Remove(task);
            task.State = TaskLifecycle.Running;
            _runningTasks.Add(task);
        }
    }

    /// <summary>
    /// 取消指定任务
    /// </summary>
    public void Cancel(Guid taskId)
    {
        var running = _runningTasks.FirstOrDefault(t => t.TaskId == taskId);
        running?.Cancel();

        var suspended = _suspendedTasks.FirstOrDefault(t => t.TaskId == taskId);
        suspended?.Cancel();
    }

    /// <summary>
    /// 调度器主循环
    /// 只做机制：出队 -> 检查关系约束 -> 执行
    /// </summary>
    public void Update(float deltaTime)
    {
        // 1. 从队列取出可执行的任务
        ProcessPendingQueue();

        // 2. 执行所有运行中的任务
        foreach (var task in _runningTasks.ToList())
        {
            if (task.State == TaskLifecycle.Running)
            {
                try
                {
                    task.Execute(deltaTime);

                    if (task.IsCompleted())
                    {
                        CompleteTask(task);
                    }
                }
                catch (Exception ex)
                {
                    FailTask(task, ex);
                }
            }
        }

        // 3. 清理已完成的任务
        _runningTasks.RemoveAll(t => t.State is TaskLifecycle.Completed or TaskLifecycle.Cancelled or TaskLifecycle.Failed);
    }

    private void ProcessPendingQueue()
    {
        // 优先处理优先级队列
        while (_priorityQueue.TryDequeue(out var task, out _))
        {
            if (CanStartTask(task))
            {
                StartTask(task);
            }
            else
            {
                // 不能执行的放回队尾
                _priorityQueue.Enqueue(task, -task.Priority);
                break;
            }
        }

        // 处理普通队列
        while (_pendingQueue.Count > 0)
        {
            var task = _pendingQueue.Peek();
            if (CanStartTask(task))
            {
                _pendingQueue.Dequeue();
                StartTask(task);
            }
            else
            {
                break;
            }
        }
    }

    /// <summary>
    /// 检查任务是否可以开始（根据关系规则）
    /// 关系逻辑由业务类提供，调度器只做验证
    /// </summary>
    private bool CanStartTask(TaskBase task)
    {
        // 如果业务类注册了关系提供者，检查关系约束
        if (_relationProviders.TryGetValue(task.GetType(), out var provider))
        {
            var relations = provider.GetRelations(task);
            foreach (var relation in relations)
            {
                // 只关心当前任务作为 Target 的关系（谁依赖谁）
                if (relation.TargetTask.TaskId == task.TaskId)
                {
                    switch (relation.RelationType)
                    {
                        case TaskRelationType.Dependency:
                            if (relation.SourceTask.State != TaskLifecycle.Completed)
                                return false;
                            break;
                        case TaskRelationType.Exclusive:
                            if (relation.SourceTask.State == TaskLifecycle.Running)
                                return false;
                            break;
                    }
                }
            }
        }
        return true;
    }

    private void StartTask(TaskBase task)
    {
        task.State = TaskLifecycle.Running;
        _runningTasks.Add(task);
    }

    private void CompleteTask(TaskBase task)
    {
        task.State = TaskLifecycle.Completed;
        TotalCompleted++;
        task.OnComplete?.Invoke(task);
        task.OnProgress?.Invoke(task, 1f);
    }

    private void FailTask(TaskBase task, Exception ex)
    {
        task.State = TaskLifecycle.Failed;
        task.OnFail?.Invoke(task, ex);
    }

    #region 查询 API

    /// <summary>
    /// 获取所有运行中的任务
    /// </summary>
    public IEnumerable<TaskBase> GetRunningTasks() => _runningTasks.AsReadOnly();

    /// <summary>
    /// 获取所有暂停的任务
    /// </summary>
    public IEnumerable<TaskBase> GetSuspendedTasks() => _suspendedTasks.AsReadOnly();

    /// <summary>
    /// 根据条件查找任务
    /// </summary>
    public IEnumerable<TaskBase> FindTasks(Func<TaskBase, bool> predicate)
    {
        return _runningTasks.Concat(_suspendedTasks).Where(predicate);
    }

    /// <summary>
    /// 根据 TaskId 查找任务
    /// </summary>
    public TaskBase? FindTask(Guid taskId)
    {
        return FindTasks(t => t.TaskId == taskId).FirstOrDefault();
    }

    /// <summary>
    /// 根据类型查找任务
    /// </summary>
    public IEnumerable<T> FindTasksByType<T>() where T : TaskBase
    {
        return FindTasks(t => t is T).Cast<T>();
    }

    #endregion

    #region 任务组管理

    /// <summary>
    /// 注册任务组
    /// </summary>
    public void RegisterTaskGroup(TaskGroup group)
    {
        _taskGroups[group.GroupId] = group;
    }

    /// <summary>
    /// 获取任务组
    /// </summary>
    public TaskGroup? GetTaskGroup(Guid groupId)
    {
        return _taskGroups.TryGetValue(groupId, out var group) ? group : null;
    }

    /// <summary>
    /// 暂停整个任务组
    /// </summary>
    public void SuspendGroup(Guid groupId)
    {
        if (_taskGroups.TryGetValue(groupId, out var group))
            group.SuspendAll();
    }

    /// <summary>
    /// 恢复整个任务组
    /// </summary>
    public void ResumeGroup(Guid groupId)
    {
        if (_taskGroups.TryGetValue(groupId, out var group))
            group.ResumeAll();
    }

    /// <summary>
    /// 取消整个任务组
    /// </summary>
    public void CancelGroup(Guid groupId)
    {
        if (_taskGroups.TryGetValue(groupId, out var group))
            group.CancelAll();
    }

    #endregion
}
