/* Copyright (C) Steven Woodcock, 2000.
 * Ported to C# from Game Programming Gems 1
 */

using GPGems.Core.Math;

namespace GPGems.AI.Boids;

/// <summary>
/// Boid 群体行为参数
/// 控制群体行为的各种特性
/// </summary>
public record BoidSettings
{
    /// <summary>感知范围（能看到多远的同伴）</summary>
    public float PerceptionRange { get; init; } = 25f;

    /// <summary>期望分离距离（与同伴保持的距离）</summary>
    public float SeparationDist { get; init; } = 8f;

    /// <summary>期望巡航速度</summary>
    public float DesiredSpeed { get; init; } = 15f;

    /// <summary>最大速度限制</summary>
    public float MaxSpeed { get; init; } = 25f;

    /// <summary>最大加速度变化</summary>
    public float MaxAcceleration { get; init; } = 5f;

    /// <summary>最小紧急程度（用于平滑转向）</summary>
    public float MinUrgency { get; init; } = 0.02f;

    /// <summary>最大紧急程度</summary>
    public float MaxUrgency { get; init; } = 0.1f;

    /// <summary>最大可见同伴数量</summary>
    public int MaxVisibleFriends { get; init; } = 10;
}

/// <summary>
/// 单个 Boid（鸟类/鱼类/群体单位）
/// 实现 Reynolds 三大规则：分离、对齐、凝聚
/// </summary>
public class Boid
{
    /// <summary>唯一标识</summary>
    public int Id { get; }

    /// <summary>当前位置</summary>
    public Vector3 Position { get; private set; }

    /// <summary>当前速度</summary>
    public Vector3 Velocity { get; private set; }

    /// <summary>当前朝向（Roll, Pitch, Yaw 弧度）</summary>
    public Vector3 Orientation { get; private set; }

    /// <summary>当前速度大小</summary>
    public float Speed { get; private set; }

    /// <summary>上一帧位置</summary>
    public Vector3 OldPosition { get; private set; }

    /// <summary>上一帧速度</summary>
    public Vector3 OldVelocity { get; private set; }

    /// <summary>可见的同伴列表</summary>
    public List<Boid> VisibleFriends { get; } = [];

    /// <summary>最近的同伴</summary>
    public Boid? NearestFriend { get; private set; }

    /// <summary>到最近同伴的距离</summary>
    public float NearestFriendDist { get; private set; } = float.PositiveInfinity;

    private static readonly Random Rand = new();

    public Boid(int id, Vector3 initialPosition, Vector3 initialVelocity)
    {
        Id = id;
        Position = initialPosition;
        Velocity = initialVelocity;
        Speed = initialVelocity.Length();
        OldPosition = initialPosition;
        OldVelocity = initialVelocity;
    }

    /// <summary>
    /// 单帧更新：执行群体行为
    /// Reynolds 三大规则 + 巡航 + 边界处理
    /// </summary>
    public void Update(BoidSettings settings, Flock flock, BoundingBox worldBounds)
    {
        // 保存上一帧状态
        OldPosition = Position;
        OldVelocity = Velocity;

        // Step 1: 感知 - 找出所有可见的同伴
        SeeFriends(flock, settings.PerceptionRange, settings.MaxVisibleFriends);

        Vector3 acceleration = Vector3.Zero;

        if (VisibleFriends.Count > 0)
        {
            // Rule #1: 分离 - 与最近同伴保持距离
            Accumulate(ref acceleration, KeepDistance(settings));

            // Rule #2: 对齐 - 匹配最近同伴的方向
            Accumulate(ref acceleration, MatchHeading(settings));

            // Rule #3: 凝聚 - 朝向群体质心移动
            Accumulate(ref acceleration, SteerToCenter(settings));
        }

        // Rule #4: 巡航 - 保持期望速度
        Accumulate(ref acceleration, Cruise(settings));

        // 限制最大加速度
        if (acceleration.Length() > settings.MaxAcceleration)
        {
            acceleration = acceleration.SetMagnitude(settings.MaxAcceleration);
        }

        // 应用加速度
        Velocity += acceleration;

        // 减弱垂直方向运动，让飞行更自然
        Velocity = new Vector3(Velocity.X, Velocity.Y * settings.MaxUrgency, Velocity.Z);

        // 限制最大速度
        Speed = Velocity.Length();
        if (Speed > settings.MaxSpeed)
        {
            Velocity = Velocity.SetMagnitude(settings.MaxSpeed);
            Speed = settings.MaxSpeed;
        }

        // 更新位置
        Position += Velocity;

        // 计算朝向（Roll/Pitch/Yaw）
        ComputeOrientation();

        // 边界处理：循环世界
        WorldBounds(worldBounds);
    }

    /// <summary>Rule #1: 分离 - 与最近同伴保持期望距离</summary>
    private Vector3 KeepDistance(BoidSettings settings)
    {
        if (NearestFriend == null) return Vector3.Zero;

        float ratio = NearestFriendDist / settings.SeparationDist;
        ratio = Math.Clamp(ratio, settings.MinUrgency, settings.MaxUrgency);

        // 指向同伴的向量
        Vector3 toFriend = NearestFriend.Position - Position;

        if (NearestFriendDist < settings.SeparationDist)
        {
            // 太近，远离
            return toFriend.SetMagnitude(-ratio);
        }
        else if (NearestFriendDist > settings.SeparationDist)
        {
            // 太远，靠近
            return toFriend.SetMagnitude(ratio);
        }

        return Vector3.Zero; // 距离正好
    }

    /// <summary>Rule #2: 对齐 - 匹配最近同伴的方向</summary>
    private Vector3 MatchHeading(BoidSettings settings)
    {
        if (NearestFriend == null) return Vector3.Zero;

        // 复制最近同伴的方向，然后缩放到最小紧急程度
        return NearestFriend.Velocity.SetMagnitude(settings.MinUrgency);
    }

    /// <summary>Rule #3: 凝聚 - 朝向可见同伴的质心移动</summary>
    private Vector3 SteerToCenter(BoidSettings settings)
    {
        if (VisibleFriends.Count == 0) return Vector3.Zero;

        // 计算感知质心
        Vector3 center = Vector3.Zero;
        foreach (var friend in VisibleFriends)
            center += friend.Position;
        center /= VisibleFriends.Count;

        // 朝向质心的向量
        Vector3 toCenter = center - Position;
        return toCenter.SetMagnitude(settings.MinUrgency);
    }

    /// <summary>Rule #4: 巡航 - 调整到期望速度</summary>
    private Vector3 Cruise(BoidSettings settings)
    {
        Vector3 change = Velocity;
        float diff = (Speed - settings.DesiredSpeed) / settings.MaxSpeed;
        float urgency = MathF.Abs(diff);

        // 限制紧急程度
        urgency = Math.Clamp(urgency, settings.MinUrgency, settings.MaxUrgency);

        // 添加随机抖动，让群体看起来更生动
        float jitter = (float)Rand.NextDouble();
        if (jitter < 0.45f)
            change += new Vector3(settings.MinUrgency * Math.Sign(diff), 0, 0);
        else if (jitter < 0.90f)
            change += new Vector3(0, 0, settings.MinUrgency * Math.Sign(diff));
        else
            change += new Vector3(0, settings.MinUrgency * Math.Sign(diff) * 0.5f, 0);

        // 计算需要的速度变化
        return change.SetMagnitude(urgency * (diff > 0 ? -1 : 1));
    }

    /// <summary>感知：找出所有可见的同伴</summary>
    private void SeeFriends(Flock flock, float perceptionRange, int maxVisible)
    {
        VisibleFriends.Clear();
        NearestFriend = null;
        NearestFriendDist = float.PositiveInfinity;

        foreach (var boid in flock.Boids)
        {
            if (boid == this) continue;

            float dist = Vector3.Distance(Position, boid.Position);
            if (dist < perceptionRange)
            {
                if (VisibleFriends.Count < maxVisible)
                    VisibleFriends.Add(boid);

                // 记录最近的同伴
                if (dist < NearestFriendDist)
                {
                    NearestFriendDist = dist;
                    NearestFriend = boid;
                }
            }
        }
    }

    /// <summary>
    /// 计算朝向 Roll/Pitch/Yaw（弧度）
    /// X = Pitch (俯仰), Y = Yaw (偏航), Z = Roll (翻滚)
    /// </summary>
    private void ComputeOrientation()
    {
        if (Speed < 0.1f) return;

        // 横向加速度方向
        Vector3 cross1 = Vector3.Cross(Velocity, Velocity - OldVelocity);
        Vector3 lateralDir = Vector3.Cross(cross1, Velocity).Normalize();

        // 横向加速度大小
        float lateralMag = Vector3.Dot(Velocity - OldVelocity, lateralDir);

        // Roll: 根据横向加速度计算倾斜
        float roll = lateralMag == 0 ? 0f :
            -MathF.Atan2(9.8f, lateralMag) + MathF.PI / 2f;

        // Pitch: 俯仰角
        float pitch = -MathF.Atan2(Velocity.Y, MathF.Sqrt(Velocity.Z * Velocity.Z + Velocity.X * Velocity.X));

        // Yaw: 偏航角（水平方向）
        float yaw = MathF.Atan2(Velocity.X, Velocity.Z);

        Orientation = new Vector3(pitch, yaw, roll);
    }

    /// <summary>边界处理：循环世界（从一边出去从另一边进来）</summary>
    private void WorldBounds(BoundingBox bounds)
    {
        Vector3 newPos = Position;

        if (newPos.X > bounds.MaxX) newPos = new Vector3(bounds.MinX, newPos.Y, newPos.Z);
        else if (newPos.X < bounds.MinX) newPos = new Vector3(bounds.MaxX, newPos.Y, newPos.Z);

        if (newPos.Y > bounds.MaxY) newPos = new Vector3(newPos.X, bounds.MinY, newPos.Z);
        else if (newPos.Y < bounds.MinY) newPos = new Vector3(newPos.X, bounds.MaxY, newPos.Z);

        if (newPos.Z > bounds.MaxZ) newPos = new Vector3(newPos.X, newPos.Y, bounds.MinZ);
        else if (newPos.Z < bounds.MinZ) newPos = new Vector3(newPos.X, newPos.Y, bounds.MaxZ);

        Position = newPos;
    }

    /// <summary>累加力向量</summary>
    private static void Accumulate(ref Vector3 accumulator, Vector3 change)
    {
        accumulator += change;
    }
}

/// <summary>世界边界框</summary>
public record BoundingBox(float MinX, float MaxX, float MinY, float MaxY, float MinZ, float MaxZ);
