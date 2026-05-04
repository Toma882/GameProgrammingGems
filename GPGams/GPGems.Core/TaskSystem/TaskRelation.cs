namespace GPGems.Core.TaskSystem;

/// <summary>
/// 任务关系描述
/// </summary>
public class TaskRelation
{
    public TaskBase SourceTask { get; }
    public TaskBase TargetTask { get; }
    public TaskRelationType RelationType { get; }

    public TaskRelation(TaskBase source, TaskBase target, TaskRelationType type)
    {
        SourceTask = source;
        TargetTask = target;
        RelationType = type;
    }
}
