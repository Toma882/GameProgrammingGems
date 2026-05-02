using System.Numerics;
using GPGems.Core.Math;

namespace GPGems.AI.Steering;

/// <summary>
/// 定向行为智能体
/// 可以组合多个转向行为
/// </summary>
public class SteeringAgent
{
    /// <summary>当前位置</summary>
    public Vector3 Position { get; set; }

    /// <summary>当前速度</summary>
    public Vector3 Velocity { get; set; }

    /// <summary>最大速度</summary>
    public float MaxSpeed { get; set; } = 4f;

    /// <summary>最大转向力（加速度上限）</summary>
    public float MaxForce { get; set; } = 5f;

    /// <summary>质量（用于力的衰减）</summary>
    public float Mass { get; set; } = 1f;

    /// <summary>目标位置（Seek/Arrive/Flee使用）</summary>
    public Vector3? TargetPosition { get; set; }

    /// <summary>目标智能体（Pursue/Evade使用）</summary>
    public SteeringAgent? TargetAgent { get; set; }

    /// <summary>是否启用的定向行为列表</summary>
    public List<ISteeringBehavior> Behaviors { get; } = [];

    public SteeringAgent(Vector3 position)
    {
        Position = position;
        Velocity = Vector3.Zero;
    }

    /// <summary>添加定向行为</summary>
    public T AddBehavior<T>() where T : ISteeringBehavior, new()
    {
        var behavior = new T();
        Behaviors.Add(behavior);
        return behavior;
    }

    /// <summary>添加带权重的定向行为</summary>
    public WeightedBehavior AddWeightedBehavior(ISteeringBehavior behavior, float weight = 1f)
    {
        var weighted = new WeightedBehavior(behavior, weight);
        Behaviors.Add(weighted);
        return weighted;
    }

    /// <summary>清除所有定向行为</summary>
    public void ClearBehaviors()
    {
        Behaviors.Clear();
    }

    /// <summary>更新一帧：计算所有转向力并应用</summary>
    public void Update(float deltaTime = 1f)
    {
        Vector3 totalForce = Vector3.Zero;

        // 累加所有定向行为的转向力
        foreach (var behavior in Behaviors)
        {
            totalForce += behavior.Calculate(this);
        }

        // 限制最大转向力
        if (totalForce.Length() > MaxForce)
        {
            totalForce = totalForce.SetMagnitude(MaxForce);
        }

        // 应用力：加速度 = 力 / 质量
        Vector3 acceleration = totalForce / Mass;

        // 更新速度
        Velocity += acceleration * deltaTime;

        // 限制最大速度
        if (Velocity.Length() > MaxSpeed)
        {
            Velocity = Velocity.SetMagnitude(MaxSpeed);
        }

        // 更新位置
        Position += Velocity * deltaTime;
    }
}

/// <summary>
/// 带权重的定向行为包装
/// </summary>
public class WeightedBehavior : ISteeringBehavior
{
    public ISteeringBehavior Behavior { get; }
    public float Weight { get; set; }

    public WeightedBehavior(ISteeringBehavior behavior, float weight)
    {
        Behavior = behavior;
        Weight = weight;
    }

    public Vector3 Calculate(SteeringAgent agent)
    {
        return Behavior.Calculate(agent) * Weight;
    }
}
