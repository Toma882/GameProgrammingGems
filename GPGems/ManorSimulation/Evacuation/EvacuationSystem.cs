using System.Numerics;
using GPGems.AI.CollisionAvoidance;

namespace GPGems.ManorSimulation;

/// <summary>
/// 紧急疏散系统
/// 使用社会力模型模拟人群疏散
/// </summary>
public class EvacuationSystem
{
    private readonly SocialForceSimulation _simulation;
    private readonly Vector2 _exitPosition;
    private readonly float _mapWidth;
    private readonly float _mapHeight;
    private readonly Random _random = new();

    private int _evacuatedCount;
    private int _maxNearExit;

    public int EvacuatedCount => _evacuatedCount;
    public int MaxNearExit => _maxNearExit;
    public int AgentCount => _simulation.Agents.Count;

    public EvacuationSystem(int agentCount, int mapWidth, int mapHeight)
    {
        _mapWidth = mapWidth;
        _mapHeight = mapHeight;
        _exitPosition = new Vector2(mapWidth - 5, mapHeight / 2f);
        _simulation = new SocialForceSimulation();

        BuildWalls();

        for (int i = 0; i < agentCount; i++)
        {
            var agent = _simulation.AddAgent(
                new Vector2(_random.Next(0, 80), _random.Next(0, 80)),
                radius: 0.45f,
                desiredSpeed: 2.5f);

            agent.Target = _exitPosition;
        }
    }

    private void BuildWalls()
    {
        for (int y = 0; y < _mapHeight; y++)
        {
            if (y < 38 || y > 47)
            {
                _simulation.AddWall(85, y, 86, y);
            }
        }

        for (int y = 0; y < _mapHeight; y++)
            _simulation.AddWall(0, y, 1, y);

        for (int x = 0; x < 85; x++)
        {
            _simulation.AddWall(x, 0, x, 1);
            _simulation.AddWall(x, _mapHeight - 1, x, _mapHeight);
        }
    }

    public void Update(float deltaTime)
    {
        _simulation.Update(deltaTime);

        int nearExit = 0;

        for (int i = _simulation.Agents.Count - 1; i >= 0; i--)
        {
            var agent = _simulation.Agents[i];

            if (agent.Position.X > 88)
            {
                agent.Position = new Vector2(5, agent.Position.Y);
                agent.Velocity = Vector2.Zero;
                _evacuatedCount++;
            }
            else if (agent.Position.X > 75)
            {
                nearExit++;
            }
        }

        _maxNearExit = Math.Max(_maxNearExit, nearExit);
    }

    public SocialForceAgent GetAgent(int index) => _simulation.Agents[index];

    public Vector2 ExitPosition => _exitPosition;
}
