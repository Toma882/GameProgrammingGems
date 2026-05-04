using System.Numerics;
using GPGems.AI.CollisionAvoidance;
using GPGems.AI.Pathfinding;

namespace GPGems.ManorSimulation;

/// <summary>
/// 游客人流系统
/// 使用流场寻路 + ORCA避障，实现大规模游客同时移动
/// </summary>
public class VisitorFlowSystem
{
    private readonly GridMap _map;
    private readonly ORCASimulation _orca;
    private readonly FlowFieldPathfinder _flowFieldA;
    private readonly FlowFieldPathfinder _flowFieldB;
    private readonly FlowFieldPathfinder _flowFieldExit;
    private readonly Dictionary<int, int> _visitorState; // 0→A景点, 1→B景点, 2→出口
    private readonly Random _random;
    private readonly Vector2 _entrance;
    private readonly Vector2 _spotA;
    private readonly Vector2 _spotB;
    private readonly Vector2 _exit;
    private readonly float _speedMultiplier;

    private int _arrivedCount;
    private int _collisionCount;

    public int ArrivedCount => _arrivedCount;
    public int CollisionCount => _collisionCount;
    public int AgentCount => _orca.Agents.Count;

    public VisitorFlowSystem(GridMap map, int visitorCount, int entranceX, int entranceY, float speedMultiplier)
    {
        _map = map;
        _random = new Random(42);
        _speedMultiplier = speedMultiplier;

        _entrance = new Vector2(entranceX, entranceY);
        _spotA = new Vector2(50, 10);
        _spotB = new Vector2(50, 40);
        _exit = new Vector2(99, 25);

        _flowFieldA = new FlowFieldPathfinder(_map); // 计算A景点的流场
        _flowFieldB = new FlowFieldPathfinder(_map); // 计算B景点的流场

        _flowFieldExit = new FlowFieldPathfinder(_map); // 计算出口的流场

        _flowFieldA.CalculateFlowField((int)_spotA.X, (int)_spotA.Y); 
        _flowFieldB.CalculateFlowField((int)_spotB.X, (int)_spotB.Y);
        _flowFieldExit.CalculateFlowField((int)_exit.X, (int)_exit.Y);

        _orca = new ORCASimulation();
        _visitorState = new Dictionary<int, int>();

        for (int i = 0; i < visitorCount; i++)
        {
            var pos = new Vector2(
                _random.Next(0, 5),
                _random.Next(20, 30));
            var agent = _orca.AddAgent(pos, radius: 0.5f, maxSpeed: 1.8f * speedMultiplier);
            agent.PreferredVel = new Vector2(1, 0);
            _visitorState[i] = _random.Next(2);
        }
    }

    public void Update(float deltaTime)
    {
        _collisionCount = 0;

        for (int i = 0; i < _orca.Agents.Count; i++)
        {
            var agent = _orca.Agents[i];
            int state = _visitorState[i];

            Vector2 flowDir = state switch
            {
                0 => _flowFieldA.GetDirection(agent.Position.X, agent.Position.Y),
                1 => _flowFieldB.GetDirection(agent.Position.X, agent.Position.Y),
                2 => _flowFieldExit.GetDirection(agent.Position.X, agent.Position.Y),
                _ => Vector2.Zero
            };

            agent.PreferredVel = flowDir * agent.MaxSpeed;

            float distToA = Vector2.Distance(agent.Position, _spotA);
            float distToB = Vector2.Distance(agent.Position, _spotB);
            float distToExit = Vector2.Distance(agent.Position, _exit);

            if (state == 0 && distToA < 5f)
            {
                _visitorState[i] = 2;
                _arrivedCount++;
            }
            else if (state == 1 && distToB < 5f)
            {
                _visitorState[i] = 2;
                _arrivedCount++;
            }
            else if (state == 2 && distToExit < 5f)
            {
                agent.Position = new Vector2(
                    _random.Next(0, 5),
                    _random.Next(20, 30));
                _visitorState[i] = _random.Next(2);
                _arrivedCount++;
            }
        }

        _orca.Update(deltaTime);

        for (int i = 0; i < _orca.Agents.Count; i++)
            for (int j = i + 1; j < _orca.Agents.Count; j++)
                if (Vector2.Distance(_orca.Agents[i].Position, _orca.Agents[j].Position) < 0.9f)
                    _collisionCount++;
    }

    public ORCAAgent GetAgent(int index) => _orca.Agents[index];
    public int GetVisitorState(int index) => _visitorState.TryGetValue(index, out var state) ? state : 0;
}
