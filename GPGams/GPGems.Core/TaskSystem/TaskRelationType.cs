namespace GPGems.Core.TaskSystem;

/// <summary>
/// 任务关系类型
/// 由任务系统定义的标准关系规则
/// 业务类通过实现接口来指定具体关系
/// </summary>
public enum TaskRelationType
{
    Sequential,   // 顺序执行: A -> B
    Parallel,     // 并行执行: A || B
    Dependency,   // 依赖: A 完成后 B 才能开始
    Exclusive     // 互斥: A 和 B 不能同时进行
}
