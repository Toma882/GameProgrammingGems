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
    public float PerceptionRange { get; init; } = 25.0f;

    /// <summary>期望分离距离（与同伴保持的距离）</summary>
    public float SeparationDist { get; init; } = 10.0f;

    /// <summary>期望巡航速度</summary>
    public float DesiredSpeed { get; init; } = 2.0f;

    /// <summary>最大速度限制</summary>
    public float MaxSpeed { get; init; } = 4.0f;

    /// <summary>最大加速度变化（转向力上限）</summary>
    public float MaxAcceleration { get; init; } = 5.0f;

    /// <summary>最大可见同伴数量</summary>
    public int MaxVisibleFriends { get; init; } = 30;

    // ---- 三大规则权重 ----
    /// <summary>分离权重</summary>
    public float SeparationWeight { get; init; } = 1.5f;

    /// <summary>对齐权重</summary>
    public float AlignmentWeight { get; init; } = 1.0f;

    /// <summary>凝聚权重</summary>
    public float CohesionWeight { get; init; } = 1.0f;

    /// <summary>巡航速度调节增益（越大越快调整到期望速度）</summary>
    public float CruiseGain { get; init; } = 0.5f;

    /// <summary>垂直阻尼（每帧 Y 速度衰减，1.0 = 无阻尼，0.98 = 轻微阻尼）</summary>
    public float VerticalDamping { get; init; } = 0.98f;
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

    // 两阶段更新：Flock 先用 ComputeForces 再统一 Integrate
    private Vector3 _acceleration;

    public Boid(int id, Vector3 initialPosition, Vector3 initialVelocity)
    {
        Id = id;
        Position = initialPosition;
        Velocity = initialVelocity;
        Speed = initialVelocity.Length();
        OldPosition = initialPosition;
        OldVelocity = initialVelocity;
    }

    /// <summary>Phase 1: 基于当前状态计算转向力（Flock 两阶段 Update 用）</summary>
    public void ComputeForces(BoidSettings settings, Flock flock)
    {
        SeeFriends(flock, settings.PerceptionRange, settings.MaxVisibleFriends);

        _acceleration = Vector3.Zero;

        if (VisibleFriends.Count > 0)
        {
            _acceleration += KeepDistance(settings) * settings.SeparationWeight;
            _acceleration += MatchHeading(settings) * settings.AlignmentWeight;
            _acceleration += SteerToCenter(settings) * settings.CohesionWeight;
        }

        _acceleration += Cruise(settings);

        if (_acceleration.Length() > settings.MaxAcceleration)
            _acceleration = _acceleration.SetMagnitude(settings.MaxAcceleration);
    }

    /// <summary>Phase 2: 应用转向力、更新速度和位置（Flock 两阶段 Update 用）</summary>
    public void Integrate(BoidSettings settings, BoundingBox worldBounds)
    {
        OldVelocity = Velocity;
        OldPosition = Position;

        // 应用转向加速度
        Velocity += _acceleration;

        // 轻柔垂直阻尼（防止飞散，保留 3D 感）
        Velocity = new Vector3(Velocity.X, Velocity.Y * settings.VerticalDamping, Velocity.Z);

        // 限制最大速度
        Speed = Velocity.Length();
        if (Speed > settings.MaxSpeed)
        {
            Velocity = Velocity.SetMagnitude(settings.MaxSpeed);
            Speed = settings.MaxSpeed;
        }

        // 更新位置（Reynolds 标准顺序：先算力 → 更新速度 → 最后更新位置）
        Position += Velocity;

        // 计算朝向
        ComputeOrientation();

        // 边界处理
        WorldBounds(worldBounds);
    }

    /// <summary>
    /// 单帧更新（保持向后兼容，但建议 Flock 使用两阶段 ComputeForces + Integrate）
    /// </summary>
    public void Update(BoidSettings settings, Flock flock, BoundingBox worldBounds)
    {
        ComputeForces(settings, flock);
        Integrate(settings, worldBounds);
    }

    /// <summary>Rule #1: 分离 - 与所有可见同伴保持距离</summary>
    private Vector3 KeepDistance(BoidSettings settings)
    {
        Vector3 steer = Vector3.Zero;
        int count = 0;

        foreach (var other in VisibleFriends)
        {
            float dist = Vector3.Distance(Position, other.Position);
            if (dist > 0 && dist < settings.SeparationDist)
            {
                Vector3 diff = Position - other.Position;
                diff /= dist; // 归一化 + 距离加权
                steer += diff;
                count++;
            }
        }

        if (count > 0)
        {
            steer /= count;
            return SteerTo(steer, settings);
        }

        return Vector3.Zero;
    }

    /// <summary>Rule #2: 对齐 - 匹配所有可见同伴的平均方向</summary>
    private Vector3 MatchHeading(BoidSettings settings)
    {
        Vector3 avgVel = Vector3.Zero;
        int count = 0;

        foreach (var other in VisibleFriends)
        {
            avgVel += other.Velocity;
            count++;
        }

        if (count > 0)
        {
            avgVel /= count;
            return SteerTo(avgVel, settings);
        }

        return Vector3.Zero;
    }

    /// <summary>Rule #3: 凝聚 - 朝向所有可见同伴的质心移动</summary>
    private Vector3 SteerToCenter(BoidSettings settings)
    {
        if (VisibleFriends.Count == 0) return Vector3.Zero;

        Vector3 center = Vector3.Zero;
        foreach (var friend in VisibleFriends)
            center += friend.Position;
        center /= VisibleFriends.Count;

        Vector3 desired = center - Position;
        return SteerTo(desired, settings);
    }

    /// <summary>标准 Reynolds 转向力计算</summary>
    private Vector3 SteerTo(Vector3 target, BoidSettings settings)
    {
        float dist = target.Length();
        if (dist > 0)
        {
            Vector3 desired = target.Normalize() * settings.DesiredSpeed;
            Vector3 steer = desired - Velocity;
            if (steer.Length() > settings.MaxAcceleration)
                steer = steer.SetMagnitude(settings.MaxAcceleration);
            return steer;
        }
        return Vector3.Zero;
    }

    /// <summary>巡航速度调节（比例控制器，调整到期望速度）</summary>
    private Vector3 Cruise(BoidSettings settings)
    {
        float speedError = settings.DesiredSpeed - Speed;
        float correction = Math.Clamp(
            speedError * settings.CruiseGain,
            -settings.MaxAcceleration,
            settings.MaxAcceleration
        );
        return Velocity.Normalize() * correction;
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

        Vector3 cross1 = Vector3.Cross(Velocity, Velocity - OldVelocity);
        Vector3 lateralDir = Vector3.Cross(cross1, Velocity).Normalize();
        float lateralMag = Vector3.Dot(Velocity - OldVelocity, lateralDir);

        // Roll: 模拟飞行器倾斜 — 用横向加速度与重力的比值计算倾角
        float roll = lateralMag == 0 ? 0f :
            -MathF.Atan2(9.8f, lateralMag) + MathF.PI / 2f;

        float pitch = -MathF.Atan2(Velocity.Y,
            MathF.Sqrt(Velocity.Z * Velocity.Z + Velocity.X * Velocity.X));
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
}

/// <summary>世界边界框</summary>
public record BoundingBox(float MinX, float MaxX, float MinY, float MaxY, float MinZ, float MaxZ);
