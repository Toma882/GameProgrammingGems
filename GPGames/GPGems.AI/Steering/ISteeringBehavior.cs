using System.Numerics;
using GPGems.Core.Math;

namespace GPGems.AI.Steering;

/// <summary>
/// 定向行为接口
/// 所有的转向力计算都实现这个接口
/// </summary>
public interface ISteeringBehavior
{
    /// <summary>计算转向力</summary>
    /// <param name="agent">智能体当前状态</param>
    /// <returns>转向加速度向量</returns>
    Vector3 Calculate(SteeringAgent agent);
}

/// <summary>
/// 定向行为类型
/// </summary>
public enum SteeringBehaviorType
{
    None,
    Seek,           // 寻找目标
    Flee,           // 逃离目标
    Arrive,         // 到达目标（减速）
    Wander,         // 随机漫游
    Pursue,         // 追击移动目标
    Evade,          // 躲避移动目标
    PathFollow,     // 路径跟随
    ObstacleAvoid,  // 障碍物躲避
}
