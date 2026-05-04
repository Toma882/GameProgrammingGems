using System.Numerics;

namespace GPGems.ManorSimulation;

/// <summary>
/// 员工数据层（纯数据，无业务逻辑）
/// </summary>
public class EmployeeData
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 当前位置
    /// </summary>
    public Vector2 Position { get; set; }

    /// <summary>
    /// 当前状态
    /// </summary>
    public EmployeeState State { get; set; } = EmployeeState.Idle;

    /// <summary>
    /// 当前执行的任务ID
    /// </summary>
    public Guid? CurrentTaskId { get; set; }

    /// <summary>
    /// 今日已完成任务数
    /// </summary>
    public int TaskCountToday { get; set; }

    /// <summary>
    /// 员工技能等级表
    /// </summary>
    public Dictionary<string, float> Skills { get; } = new();

    /// <summary>
    /// 偏好的任务类型
    /// </summary>
    public string? PreferredTaskType { get; set; }

    /// <summary>
    /// 体力值 (0-100)
    /// </summary>
    public float Stamina { get; set; } = 100f;

    /// <summary>
    /// 获取指定技能的等级
    /// </summary>
    public float GetSkillLevel(string skillType)
    {
        return Skills.TryGetValue(skillType, out var level) ? level : 0f;
    }
}
