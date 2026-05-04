using GPGems.Core.TaskSystem;

namespace GPGames.ManorSimulation;

/// <summary>
/// 员工任务关系提供者
/// 实现 ITaskRelationProvider 接口，由业务层自己维护任务关系
///
/// 反向依赖设计：
/// - 任务系统定义接口和关系规则
/// - 业务层实现具体关系维护逻辑
/// - 员工可以根据自己的状态、任务历史、偏好来动态调整任务关系
/// </summary>
public class EmployeeTaskRelationProvider : ITaskRelationProvider
{
    private readonly Dictionary<Guid, List<TaskRelation>> _taskRelations = new();

    /// <summary>
    /// 添加任务关系
    /// 由业务逻辑（如员工类、任务编排器）调用此方法来建立任务间的依赖/互斥关系
    /// </summary>
    public void AddRelation(TaskBase source, TaskBase target, TaskRelationType relationType)
    {
        if (!_taskRelations.TryGetValue(target.TaskId, out var relations))
        {
            relations = new List<TaskRelation>();
            _taskRelations[target.TaskId] = relations;
        }

        relations.Add(new TaskRelation(source, target, relationType));
    }

    /// <summary>
    /// 获取指定任务的所有关系
    /// 由 TaskScheduler 在调度时调用
    /// </summary>
    public IEnumerable<TaskRelation> GetRelations(TaskBase task)
    {
        if (_taskRelations.TryGetValue(task.TaskId, out var relations))
        {
            return relations;
        }
        return Enumerable.Empty<TaskRelation>();
    }

    /// <summary>
    /// 注册到全局调度器
    /// </summary>
    public void RegisterToScheduler()
    {
        TaskScheduler.Instance.RegisterRelationProvider(typeof(EmployeeTaskBase), this);
        TaskScheduler.Instance.RegisterRelationProvider(typeof(HarvestTask), this);
        TaskScheduler.Instance.RegisterRelationProvider(typeof(FeedTask), this);
        TaskScheduler.Instance.RegisterRelationProvider(typeof(ServeTask), this);
    }
}
