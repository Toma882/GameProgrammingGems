using System.Numerics;
using GPGems.AI.Boids;
using GPGems.Core.Math;

namespace GPGems.ManorSimulation;

/// <summary>
/// 动物群体系统
/// 使用Boids算法实现多种群体行为
/// </summary>
public class AnimalGroupSystem
{
    private readonly List<Flock> _flocks = new();

    public List<Flock> Flocks => _flocks;

    /// <summary>
    /// 创建鱼群
    /// </summary>
    public Flock CreateFishSchool(int count, Vector3 pondCenter)
    {
        var flock = new Flock();
        var settings = ManorGamePresets.FishSchool;

        for (int i = 0; i < count; i++)
        {
            flock.AddBoid(
                new Vector3(
                    pondCenter.X + (float)(Random.Shared.NextDouble() - 0.5) * 20,
                    pondCenter.Y,
                    pondCenter.Z + (float)(Random.Shared.NextDouble() - 0.5) * 20),
                new Vector3(1, 0, 0) * settings.DesiredSpeed);
        }

        _flocks.Add(flock);
        return flock;
    }

    /// <summary>
    /// 创建放牧动物群
    /// </summary>
    public Flock CreateGrazingHerd(int count, Vector3 areaCenter)
    {
        var flock = new Flock();
        var settings = ManorGamePresets.GrazingAnimal;

        for (int i = 0; i < count; i++)
        {
            flock.AddBoid(
                new Vector3(
                    areaCenter.X + (float)(Random.Shared.NextDouble() - 0.5) * 40,
                    areaCenter.Y,
                    areaCenter.Z + (float)(Random.Shared.NextDouble() - 0.5) * 40),
                new Vector3(Random.Shared.Next(-1, 2), 0, Random.Shared.Next(-1, 2)));
        }

        _flocks.Add(flock);
        return flock;
    }

    /// <summary>
    /// 创建蝴蝶群
    /// </summary>
    public Flock CreateButterflySwarm(int count, Vector3 gardenCenter)
    {
        var flock = new Flock();
        var settings = ManorGamePresets.Butterfly;

        for (int i = 0; i < count; i++)
        {
            flock.AddBoid(
                new Vector3(
                    gardenCenter.X + (float)(Random.Shared.NextDouble() - 0.5) * 30,
                    gardenCenter.Y,
                    gardenCenter.Z + (float)(Random.Shared.NextDouble() - 0.5) * 30),
                new Vector3(Random.Shared.Next(-2, 3), 0, Random.Shared.Next(-2, 3)));
        }

        _flocks.Add(flock);
        return flock;
    }

    public void Update(float deltaTime)
    {
        var defaultSettings = ManorGamePresets.FishSchool;
        var bounds = new BoundingBox(-50, 50, -50, 50, -50, 50);
        foreach (var flock in _flocks)
        {
            flock.Update(defaultSettings, bounds);
        }
    }

    public void UpdateWithSettings(float deltaTime, BoidSettings settings, Vector3? worldBoundsMin = null, Vector3? worldBoundsMax = null)
    {
        foreach (var flock in _flocks)
        {
            var min = worldBoundsMin ?? new Vector3(-50, -50, -50);
            var max = worldBoundsMax ?? new Vector3(50, 50, 50);
            var bounds = new BoundingBox(min.X, max.X, min.Y, max.Y, min.Z, max.Z);
            flock.Update(settings, bounds);
        }
    }
}
