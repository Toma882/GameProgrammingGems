using System.Numerics;
using GPGems.Core;
using GPGems.Core.TaskSystem;
using TaskScheduler = GPGems.Core.TaskSystem.TaskScheduler;

namespace GPGems.ManorSimulation;

/// <summary>
/// 员工管理层
/// 负责员工的生命周期管理、任务分配、状态追踪
/// 通过 CommunicationBus 与其他模块解耦
/// </summary>
public class EmployeeManager
{
    private readonly Dictionary<int, EmployeeData> _employees = new();
    private readonly IWorkerAssignmentStrategy _assignmentStrategy;

    public int TotalEmployees => _employees.Count;
    public int WorkingCount => _employees.Values.Count(e => e.State == EmployeeState.Working);
    public int IdleCount => _employees.Values.Count(e => e.State == EmployeeState.Idle);

    public EmployeeManager(IWorkerAssignmentStrategy assignmentStrategy)
    {
        _assignmentStrategy = assignmentStrategy;

        // 注册查询处理器
        var bus = CommunicationBus.Instance;
        bus.AddQueryDelegate(this, ManorQueries.GetIdleEmployees, _ => GetIdleEmployees());
        bus.AddQueryDelegate(this, ManorQueries.GetEmployeeById, args => args.FirstOrDefault() is int employeeId ? GetEmployee(employeeId) : null);

        // 订阅任务生成事件
        bus.Subscribe(ManorEvents.TaskGenerated, OnTaskGenerated);
    }

    /// <summary>
    /// 注册员工
    /// </summary>
    public void RegisterEmployee(EmployeeData employee)
    {
        _employees[employee.Id] = employee;
        _assignmentStrategy.RegisterWorker(employee);
        CommunicationBus.Instance.Publish(ManorEvents.EmployeeRegistered, employee.Id);
    }

    /// <summary>
    /// 移除员工
    /// </summary>
    public void UnregisterEmployee(int employeeId)
    {
        if (_employees.Remove(employeeId, out var employee))
        {
            // 取消当前任务
            if (employee.CurrentTaskId.HasValue)
            {
                TaskScheduler.Instance.Cancel(employee.CurrentTaskId.Value);
            }
            _assignmentStrategy.UnregisterWorker(employeeId);
            CommunicationBus.Instance.Publish(ManorEvents.EmployeeUnregistered, employeeId);
        }
    }

    /// <summary>
    /// 获取员工数据
    /// </summary>
    public EmployeeData? GetEmployee(int employeeId)
    {
        return _employees.TryGetValue(employeeId, out var e) ? e : null;
    }

    /// <summary>
    /// 获取所有员工
    /// </summary>
    public IEnumerable<EmployeeData> GetAllEmployees() => _employees.Values;

    /// <summary>
    /// 获取所有空闲员工
    /// </summary>
    public IEnumerable<EmployeeData> GetIdleEmployees()
    {
        return _employees.Values.Where(e => e.State == EmployeeState.Idle);
    }

    /// <summary>
    /// 处理任务生成事件 - 分配员工到任务
    /// </summary>
    private void OnTaskGenerated(object? args)
    {
        if (args is TaskGeneratedEvent evt)
        {
            // 通过分配策略找到最合适的员工
            var assignedEmployee = _assignmentStrategy.AssignWorker(evt.Task, evt.AssignedEmployees);
            if (assignedEmployee.HasValue)
            {
                AssignTaskToEmployee(assignedEmployee.Value, evt.Task);

                // 通过总线通知建筑更新员工列表
                CommunicationBus.Instance.Publish(ManorEvents.TaskAssigned,
                    new TaskAssignedEvent(assignedEmployee.Value, evt.BuildingId, evt.Task));
            }
        }
    }

    /// <summary>
    /// 直接分配任务给指定员工
    /// </summary>
    public void AssignTaskToEmployee(int employeeId, EmployeeTaskBase task)
    {
        if (!_employees.TryGetValue(employeeId, out var employee))
            return;

        var oldState = employee.State;
        employee.State = EmployeeState.Working;
        employee.CurrentTaskId = task.TaskId;
        task.EmployeeId = employeeId;
        task.SourcePosition = employee.Position;

        TaskScheduler.Instance.Submit(task);

        CommunicationBus.Instance.Publish(ManorEvents.EmployeeStateChanged,
            new EmployeeStateChangedEvent(employeeId, oldState, employee.State));
        CommunicationBus.Instance.Publish(ManorEvents.TaskAssigned,
            new TaskAssignedEvent(employeeId, null, task));
    }

    /// <summary>
    /// 标记员工任务完成
    /// </summary>
    public void CompleteTask(int employeeId)
    {
        if (!_employees.TryGetValue(employeeId, out var employee))
            return;

        var oldState = employee.State;
        var task = TaskScheduler.Instance.FindTask(employee.CurrentTaskId ?? Guid.Empty) as EmployeeTaskBase;

        employee.State = EmployeeState.Idle;
        employee.CurrentTaskId = null;
        employee.TaskCountToday++;

        CommunicationBus.Instance.Publish(ManorEvents.EmployeeStateChanged,
            new EmployeeStateChangedEvent(employeeId, oldState, employee.State));
        CommunicationBus.Instance.Publish(ManorEvents.TaskCompleted,
            new TaskCompletedEvent(employeeId, task?.BuildingId, task?.Type ?? string.Empty));
    }

    /// <summary>
    /// 更新员工位置
    /// </summary>
    public void UpdatePosition(int employeeId, Vector2 newPosition)
    {
        if (_employees.TryGetValue(employeeId, out var employee))
        {
            employee.Position = newPosition;
        }
    }

    /// <summary>
    /// 让员工休息
    /// </summary>
    public void StartRest(int employeeId)
    {
        if (!_employees.TryGetValue(employeeId, out var employee))
            return;

        var oldState = employee.State;
        employee.State = EmployeeState.Resting;
        CommunicationBus.Instance.Publish(ManorEvents.EmployeeStateChanged,
            new EmployeeStateChangedEvent(employeeId, oldState, EmployeeState.Resting));
    }

    /// <summary>
    /// 结束休息
    /// </summary>
    public void EndRest(int employeeId)
    {
        if (!_employees.TryGetValue(employeeId, out var employee))
            return;

        var oldState = employee.State;
        employee.State = EmployeeState.Idle;
        CommunicationBus.Instance.Publish(ManorEvents.EmployeeStateChanged,
            new EmployeeStateChangedEvent(employeeId, oldState, EmployeeState.Idle));
    }

    /// <summary>
    /// 获取员工统计信息
    /// </summary>
    public (int working, int idle, int resting) GetStats()
    {
        int working = 0, idle = 0, resting = 0;
        foreach (var e in _employees.Values)
        {
            switch (e.State)
            {
                case EmployeeState.Working: working++; break;
                case EmployeeState.Idle: idle++; break;
                case EmployeeState.Resting: resting++; break;
            }
        }
        return (working, idle, resting);
    }
}

/// <summary>
/// 员工状态变更事件参数
/// </summary>
public class EmployeeStateChangedEvent
{
    public int EmployeeId { get; }
    public EmployeeState OldState { get; }
    public EmployeeState NewState { get; }

    public EmployeeStateChangedEvent(int employeeId, EmployeeState oldState, EmployeeState newState)
    {
        EmployeeId = employeeId;
        OldState = oldState;
        NewState = newState;
    }
}

