/* Copyright (C) Steven Woodcock, 2000.
 * Ported to C# from Game Programming Gems 1
 */

using GPGems.Core.Graphics;
using GPGems.Core.Math;

namespace GPGems.AI.Boids;

/// <summary>
/// 群体管理器
/// 管理一群 Boid 的创建、更新和状态
/// </summary>
public class Flock
{
    /// <summary>所有 Boid 列表</summary>
    public List<Boid> Boids { get; } = [];

    /// <summary>群体 ID</summary>
    public int Id { get; }

    /// <summary>群体颜色（用于可视化）</summary>
    public RgbColor Color { get; set; }

    /// <summary>世界边界</summary>
    public BoundingBox WorldBounds { get; set; }

    /// <summary>群体行为参数</summary>
    public BoidSettings Settings { get; set; } = new();

    private static int _nextId = 0;
    private static readonly Random Rand = new();

    public Flock()
    {
        Id = _nextId++;

        // 默认世界大小
        WorldBounds = new BoundingBox(-50, 50, -50, 50, -50, 50);
    }

    /// <summary>创建指定数量的 Boid，随机分布</summary>
    public void SpawnBoids(int count)
    {
        for (int i = 0; i < count; i++)
        {
            // 随机位置
            float x = RandomRange(WorldBounds.MinX * 0.5f, WorldBounds.MaxX * 0.5f);
            float y = RandomRange(WorldBounds.MinY * 0.5f, WorldBounds.MaxY * 0.5f);
            float z = RandomRange(WorldBounds.MinZ * 0.5f, WorldBounds.MaxZ * 0.5f);

            // 随机初始速度
            float vx = RandomRange(-1, 1) * Settings.DesiredSpeed * 0.5f;
            float vy = RandomRange(-0.3f, 0.3f) * Settings.DesiredSpeed;
            float vz = RandomRange(-1, 1) * Settings.DesiredSpeed * 0.5f;

            var boid = new Boid(
                id: Boids.Count,
                initialPosition: new Vector3(x, y, z),
                initialVelocity: new Vector3(vx, vy, vz)
            );

            Boids.Add(boid);
        }
    }

    /// <summary>更新整个群体一帧（两阶段：先算力，再统一更新位置）</summary>
    public void Update()
    {
        // Phase 1: 所有 Boid 基于当前帧状态计算转向力（互不干扰）
        foreach (var boid in Boids)
        {
            boid.ComputeForces(Settings, this);
        }

        // Phase 2: 所有 Boid 统一应用转向力并更新位置（消除顺序依赖）
        foreach (var boid in Boids)
        {
            boid.Integrate(Settings, WorldBounds);
        }
    }

    /// <summary>获取群体的质心位置</summary>
    public Vector3 GetCenter()
    {
        if (Boids.Count == 0) return Vector3.Zero;

        Vector3 sum = Vector3.Zero;
        foreach (var boid in Boids)
            sum += boid.Position;
        return sum / Boids.Count;
    }

    /// <summary>获取群体的平均速度</summary>
    public Vector3 GetAverageVelocity()
    {
        if (Boids.Count == 0) return Vector3.Zero;

        Vector3 sum = Vector3.Zero;
        foreach (var boid in Boids)
            sum += boid.Velocity;
        return sum / Boids.Count;
    }

    /// <summary>重置整个群体</summary>
    public void Reset()
    {
        int count = Boids.Count;
        Boids.Clear();
        if (count > 0)
            SpawnBoids(count);
    }

    private static float RandomRange(float min, float max)
    {
        return min + (float)Rand.NextDouble() * (max - min);
    }
}
