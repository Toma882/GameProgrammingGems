using GPGems.Core.Math;

namespace GPGems.AI.Steering;

#region Seek - 寻找目标

/// <summary>
/// Seek 行为：朝着目标位置移动
/// </summary>
public class SeekBehavior : ISteeringBehavior
{
    public float Weight { get; set; } = 1f;

    public Vector3 Calculate(SteeringAgent agent)
    {
        if (agent.TargetPosition == null) return Vector3.Zero;

        Vector3 desired = agent.TargetPosition.Value - agent.Position;
        desired = desired.SetMagnitude(agent.MaxSpeed);

        Vector3 steering = desired - agent.Velocity;
        return steering;
    }
}

#endregion

#region Flee - 逃离目标

/// <summary>
/// Flee 行为：远离目标位置
/// </summary>
public class FleeBehavior : ISteeringBehavior
{
    /// <summary>恐慌半径 - 超出范围就不逃了</summary>
    public float PanicRadius { get; set; } = 50f;

    public Vector3 Calculate(SteeringAgent agent)
    {
        if (agent.TargetPosition == null) return Vector3.Zero;

        Vector3 offset = agent.Position - agent.TargetPosition.Value;
        float distance = offset.Length();

        // 超出恐慌半径，不需要逃离
        if (distance > PanicRadius) return Vector3.Zero;

        Vector3 desired = offset.SetMagnitude(agent.MaxSpeed);
        Vector3 steering = desired - agent.Velocity;
        return steering;
    }
}

#endregion

#region Arrive - 到达目标（减速）

/// <summary>
/// Arrive 行为：平滑到达目标，在接近时减速
/// </summary>
public class ArriveBehavior : ISteeringBehavior
{
    /// <summary>减速半径 - 进入这个范围开始减速</summary>
    public float SlowingRadius { get; set; } = 10f;

    /// <summary>停止半径 - 小于这个距离就完全停下</summary>
    public float StopRadius { get; set; } = 0.5f;

    public Vector3 Calculate(SteeringAgent agent)
    {
        if (agent.TargetPosition == null) return Vector3.Zero;

        Vector3 offset = agent.TargetPosition.Value - agent.Position;
        float distance = offset.Length();

        // 已经到达
        if (distance < StopRadius)
        {
            return -agent.Velocity * 0.5f; // 刹车
        }

        // 计算期望速度
        float desiredSpeed = agent.MaxSpeed;

        // 在减速半径内，速度与距离成正比
        if (distance < SlowingRadius)
        {
            desiredSpeed = agent.MaxSpeed * (distance / SlowingRadius);
        }

        Vector3 desired = offset.SetMagnitude(desiredSpeed);
        Vector3 steering = desired - agent.Velocity;
        return steering;
    }
}

#endregion

#region Wander - 随机漫游

/// <summary>
/// Wander 行为：自然的随机漫游
/// 使用 "转向圆" 算法，每帧在圆上随机偏移一点
/// </summary>
public class WanderBehavior : ISteeringBehavior
{
    private static readonly Random Rand = new();
    private float _wanderAngle;

    /// <summary>漫游圆半径（越大转弯越平缓）</summary>
    public float WanderRadius { get; set; } = 1.2f;

    /// <summary>漫游圆距离（在智能体前方多远）</summary>
    public float WanderDistance { get; set; } = 2f;

    /// <summary>每帧最大角度变化（越大越随机）</summary>
    public float WanderJitter { get; set; } = 0.3f;

    public Vector3 Calculate(SteeringAgent agent)
    {
        // 随机偏移角度
        _wanderAngle += (float)(Rand.NextDouble() * 2 - 1) * WanderJitter;

        // 计算漫游圆上的目标点
        Vector3 circleTarget = new Vector3(
            MathF.Cos(_wanderAngle) * WanderRadius,
            0, // 2D 漫游，Y 轴为 0
            MathF.Sin(_wanderAngle) * WanderRadius
        );

        // 将圆移到智能体前方
        Vector3 forward = agent.Velocity.Length() > 0.01f
            ? agent.Velocity.Normalize()
            : Vector3.UnitZ;

        Vector3 wanderCenter = agent.Position + forward * WanderDistance;

        // 最终漫游目标
        Vector3 target = wanderCenter + circleTarget;

        // 转化为 Seek 行为
        Vector3 desired = (target - agent.Position).SetMagnitude(agent.MaxSpeed);
        Vector3 steering = desired - agent.Velocity;
        return steering;
    }
}

#endregion

#region Pursue - 追击移动目标

/// <summary>
/// Pursue 行为：预测目标位置并追击移动的目标
/// </summary>
public class PursueBehavior : ISteeringBehavior
{
    public Vector3 Calculate(SteeringAgent agent)
    {
        if (agent.TargetAgent == null) return Vector3.Zero;

        Vector3 targetPos = agent.TargetAgent.Position;
        Vector3 targetVel = agent.TargetAgent.Velocity;

        // 预测时间 = 距离 / 自身最大速度
        float distance = Vector3.Distance(agent.Position, targetPos);
        float predictTime = distance / agent.MaxSpeed;

        // 预测目标未来位置
        Vector3 predictedTarget = targetPos + targetVel * predictTime;

        // 对预测位置执行 Seek
        Vector3 desired = (predictedTarget - agent.Position).SetMagnitude(agent.MaxSpeed);
        Vector3 steering = desired - agent.Velocity;
        return steering;
    }
}

#endregion

#region Evade - 躲避移动目标

/// <summary>
/// Evade 行为：预测目标位置并躲避移动的威胁
/// </summary>
public class EvadeBehavior : ISteeringBehavior
{
    /// <summary>恐慌半径 - 超出范围就不躲了</summary>
    public float PanicRadius { get; set; } = 30f;

    public Vector3 Calculate(SteeringAgent agent)
    {
        if (agent.TargetAgent == null) return Vector3.Zero;

        Vector3 targetPos = agent.TargetAgent.Position;
        Vector3 targetVel = agent.TargetAgent.Velocity;

        float distance = Vector3.Distance(agent.Position, targetPos);

        // 超出恐慌半径，不需要躲避
        if (distance > PanicRadius) return Vector3.Zero;

        // 预测时间
        float predictTime = distance / agent.MaxSpeed;
        Vector3 predictedTarget = targetPos + targetVel * predictTime;

        // 对预测位置执行 Flee
        Vector3 offset = agent.Position - predictedTarget;
        Vector3 desired = offset.SetMagnitude(agent.MaxSpeed);
        Vector3 steering = desired - agent.Velocity;
        return steering;
    }
}

#endregion
