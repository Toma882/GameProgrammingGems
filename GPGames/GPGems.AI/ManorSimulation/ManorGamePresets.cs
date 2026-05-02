/*
 * 庄园游戏预设配置
 * 包含不同类型群体行为的参数设置
 */

using GPGems.AI.Boids;

namespace GPGems.AI.ManorSimulation;

/// <summary>
/// 庄园游戏中各种群体行为的预设配置
/// </summary>
public static class ManorGamePresets
{
    /// <summary>鱼群配置 - 快速、紧密聚集、有一定漫游</summary>
    public static BoidSettings FishSchool => new()
    {
        PerceptionRange = 15.0f,
        SeparationDist = 3.0f,
        DesiredSpeed = 2.0f,
        MaxSpeed = 4.0f,
        MaxAcceleration = 8.0f,
        SeparationWeight = 1.8f,
        AlignmentWeight = 1.2f,
        CohesionWeight = 1.0f,
        WanderWeight = 0.8f,
        SeekTargetWeight = 1.5f,
        VerticalDamping = 1.0f
    };

    /// <summary>放牧动物配置 - 缓慢、松散聚集、高漫游倾向</summary>
    public static BoidSettings GrazingAnimal => new()
    {
        PerceptionRange = 20.0f,
        SeparationDist = 5.0f,
        DesiredSpeed = 0.5f,
        MaxSpeed = 1.5f,
        MaxAcceleration = 3.0f,
        SeparationWeight = 1.0f,
        AlignmentWeight = 0.5f,
        CohesionWeight = 0.3f,
        WanderWeight = 2.5f,
        SeekTargetWeight = 0f,
        VerticalDamping = 1.0f
    };

    /// <summary>蝴蝶配置 - 非常快速、分散、高随机运动</summary>
    public static BoidSettings Butterfly => new()
    {
        PerceptionRange = 8.0f,
        SeparationDist = 2.0f,
        DesiredSpeed = 1.5f,
        MaxSpeed = 3.0f,
        MaxAcceleration = 6.0f,
        SeparationWeight = 0.8f,
        AlignmentWeight = 0.2f,
        CohesionWeight = 0.3f,
        WanderWeight = 3.0f,
        SeekTargetWeight = 0f,
        VerticalDamping = 0.95f
    };

    /// <summary>游客配置 - 中等速度、保持距离、目标导向</summary>
    public static BoidSettings Visitor => new()
    {
        PerceptionRange = 10.0f,
        SeparationDist = 2.5f,
        DesiredSpeed = 1.2f,
        MaxSpeed = 2.5f,
        MaxAcceleration = 4.0f,
        SeparationWeight = 1.5f,
        AlignmentWeight = 0.3f,
        CohesionWeight = 0.1f,
        WanderWeight = 0.2f,
        SeekTargetWeight = 2.0f,
        VerticalDamping = 1.0f
    };

    /// <summary>员工配置 - 快速、目标导向、低聚集</summary>
    public static BoidSettings Employee => new()
    {
        PerceptionRange = 8.0f,
        SeparationDist = 1.5f,
        DesiredSpeed = 2.0f,
        MaxSpeed = 4.0f,
        MaxAcceleration = 6.0f,
        SeparationWeight = 1.2f,
        AlignmentWeight = 0.1f,
        CohesionWeight = 0.1f,
        WanderWeight = 0.1f,
        SeekTargetWeight = 3.0f,
        VerticalDamping = 1.0f
    };
}
