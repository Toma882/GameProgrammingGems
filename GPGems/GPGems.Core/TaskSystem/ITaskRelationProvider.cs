namespace GPGems.Core.TaskSystem;

/// <summary>
/// 任务关系提供者接口
/// 反向依赖：业务类实现此接口来维护自己的任务关系
/// 任务调度器只负责执行规则，不关心具体业务逻辑
/// </summary>
public interface ITaskRelationProvider
{
    IEnumerable<TaskRelation> GetRelations(TaskBase task);
}
