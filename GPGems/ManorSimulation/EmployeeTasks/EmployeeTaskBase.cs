using System.Numerics;
using GPGems.Core.TaskSystem;

namespace GPGems.ManorSimulation;

/// <summary>
/// 员工任务基类
/// 基于通用 TaskBase，扩展员工任务特有的属性和行为
///
/// 设计原则：
/// 1. 继承 TaskBase - 复用通用任务系统的生命周期和调度机制
/// 2. 扩展员工特有属性 - 位置、员工ID、路径等
/// 3. 定义模板方法 - 具体任务类型实现业务逻辑
/// </summary>
public abstract class EmployeeTaskBase : TaskBase
{
    /// <summary>
    /// 关联的员工ID
    /// </summary>
    public int EmployeeId { get; set; } = -1;

    /// <summary>
    /// 任务目标位置
    /// </summary>
    public Vector2 TargetPosition { get; set; }

    /// <summary>
    /// 任务进度
    /// </summary>
    public float Progress { get; protected set; }

    /// <summary>
    /// 任务总时长
    /// </summary>
    public float Duration { get; set; }

    /// <summary>
    /// 任务是否已完成
    /// </summary>
    public override bool IsCompleted() => Progress >= Duration;

    /// <summary>
    /// 任务起始位置（用于移动任务）
    /// </summary>
    public Vector2 SourcePosition { get; set; }

    /// <summary>
    /// 关联的建筑ID（可选）
    /// </summary>
    public int? BuildingId { get; set; }

    /// <summary>
    /// 当前进度百分比 (0-1)
    /// </summary>
    public float ProgressPercent => Duration > 0 ? MathF.Min(1f, Progress / Duration) : 0f;

    /// <summary>
    /// 执行任务（模板方法）
    /// 具体任务类型可以重写此方法实现特定逻辑
    /// </summary>
    public override void Execute(float deltaTime)
    {
        Progress += deltaTime;
        OnProgress?.Invoke(this, ProgressPercent);
    }
}
