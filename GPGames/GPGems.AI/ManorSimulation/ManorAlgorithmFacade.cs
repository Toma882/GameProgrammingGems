/*
 * 庄园经营模拟 - 算法门面入口
 * 统一封装所有经营模拟所需的算法，方便上层调用
 *
 * 依赖关系:
 *   ┌─────────────────────────────────────────────────────────────────┐
 *   │                     ManorAlgorithmFacade                       │
 *   ├───────────┬───────────┬────────────┬────────────┬─────────────┤
 *   │   寻路    │  群体避障 │   Boids   │ 任务调度  │  社会力模型 │
 *   │  FlowField│   ORCA   │           │    A*     │             │
 *   └───────────┴───────────┴────────────┴────────────┴─────────────┘
 */

using System;
using System.Collections.Generic;
using GPGems.AI.Pathfinding;
using GPGems.AI.CollisionAvoidance;
using GPGems.AI.Boids;
using GPGems.AI.ManorSimulation.Placement;
using System.Numerics;
using GPGems.Core.Math;

namespace GPGems.AI.ManorSimulation
{
    /// <summary>
    /// 庄园经营模拟算法门面
    /// 所有经营模拟的算法调用从这里入口，上层业务无需关心具体算法实现
    /// </summary>
    public class ManorAlgorithmFacade
    {
        #region 单例
        public static ManorAlgorithmFacade Instance { get; } = new ManorAlgorithmFacade();
        private ManorAlgorithmFacade() { }
        #endregion

        #region 子系统引用
        public GridMap? Map { get; private set; }
        public LayeredGridMap? LayeredMap { get; private set; }
        #endregion

        #region 初始化

        /// <summary>
        /// 初始化庄园地图尺寸和基础算法
        /// </summary>
        /// <param name="mapWidth">地图格子数X</param>
        /// <param name="mapHeight">地图格子数Y</param>
        public void Initialize(int mapWidth = 50, int mapHeight = 50)
        {
            Map = new GridMap(mapWidth, mapHeight);
            LayeredMap = new LayeredGridMap(mapWidth, mapHeight);
        }

        #endregion

        #region ===== 放置系统 =====

        /// <summary>
        /// 检查建筑物是否可以放置在指定位置
        /// </summary>
        /// <param name="floor">楼层（0=一楼，1=二楼，2=三楼）</param>
        public PlacementResult CanPlace(BuildingFootprint footprint, int anchorX, int anchorY, int floor = 0)
        {
            if (LayeredMap == null)
                throw new InvalidOperationException("请先调用 Initialize()");

            return LayeredMap.CanPlace(footprint, anchorX, anchorY, floor);
        }

        /// <summary>
        /// 放置建筑物
        /// </summary>
        /// <param name="floor">楼层（0=一楼，1=二楼，2=三楼）</param>
        /// <returns>建筑物唯一ID，失败返回0</returns>
        public int PlaceObject(BuildingFootprint footprint, int anchorX, int anchorY, int floor = 0)
        {
            if (LayeredMap == null)
                throw new InvalidOperationException("请先调用 Initialize()");

            return LayeredMap.PlaceObject(footprint, anchorX, anchorY, floor);
        }

        /// <summary>
        /// 移除建筑物
        /// </summary>
        public bool RemoveObject(int objectId)
        {
            if (LayeredMap == null)
                throw new InvalidOperationException("请先调用 Initialize()");

            return LayeredMap.RemoveObject(objectId);
        }

        /// <summary>
        /// 获取指定位置的建筑物
        /// </summary>
        public PlacedObject? GetObjectAt(int x, int y, int floor = 0)
        {
            if (LayeredMap == null)
                throw new InvalidOperationException("请先调用 Initialize()");

            return LayeredMap.GetObjectAt(x, y, floor);
        }

        /// <summary>
        /// 获取指定楼层的所有建筑物
        /// </summary>
        public List<PlacedObject> GetAllObjectsOnFloor(int floor)
        {
            if (LayeredMap == null)
                throw new InvalidOperationException("请先调用 Initialize()");

            return LayeredMap.GetAllObjectsOnFloor(floor);
        }

        /// <summary>
        /// 获取所有建筑物
        /// </summary>
        public IReadOnlyCollection<PlacedObject> GetAllObjects()
        {
            if (LayeredMap == null)
                throw new InvalidOperationException("请先调用 Initialize()");

            return LayeredMap.GetAllObjects();
        }

        /// <summary>
        /// 统计指定楼层的建筑数量
        /// </summary>
        public int CountObjectsOnFloor(int floor)
        {
            if (LayeredMap == null)
                throw new InvalidOperationException("请先调用 Initialize()");

            return LayeredMap.CountObjectsOnFloor(floor);
        }

        #endregion

        #region ===== 游客人流系统 =====

        /// <summary>
        /// 创建游客人流模拟
        /// </summary>
        public VisitorFlowSystem CreateVisitorFlowSystem(
            int visitorCount = 100,
            int entranceX = 0,
            int entranceY = 23,
            float speedMultiplier = 1.0f)
        {
            if (Map == null)
                throw new InvalidOperationException("请先调用 Initialize()");

            return new VisitorFlowSystem(Map, visitorCount, entranceX, entranceY, speedMultiplier);
        }

        #endregion

        #region ===== 动物群体系统 =====

        /// <summary>
        /// 创建鱼群模拟
        /// </summary>
        public AnimalGroupSystem CreateAnimalSystem()
        {
            return new AnimalGroupSystem();
        }

        #endregion

        #region ===== 员工任务系统 =====

        /// <summary>
        /// 创建员工任务调度器
        /// </summary>
        public EmployeeTaskSystem CreateEmployeeTaskSystem(
            int employeeCount = 5,
            int mapWidth = 100,
            int mapHeight = 100)
        {
            if (Map == null)
                throw new InvalidOperationException("请先调用 Initialize()");

            return new EmployeeTaskSystem(Map, employeeCount);
        }

        #endregion

        #region ===== 紧急疏散系统 =====

        /// <summary>
        /// 创建紧急疏散模拟
        /// </summary>
        public EvacuationSystem CreateEvacuationSystem(
            int agentCount = 300,
            int mapWidth = 105,
            int mapHeight = 80)
        {
            return new EvacuationSystem(agentCount, mapWidth, mapHeight);
        }

        #endregion

        #region ===== 性能统计 =====

        public string GetPerformanceReport()
        {
            return "算法系统已就绪，所有子系统已初始化";
        }

        #endregion
    }

    #region ===== 游客人流系统 =====

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

        // 统计
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

            // 定义景点位置
            _entrance = new Vector2(entranceX, entranceY);
            _spotA = new Vector2(50, 10);
            _spotB = new Vector2(50, 40);
            _exit = new Vector2(99, 25);

            // 初始化流场
            _flowFieldA = new FlowFieldPathfinder(_map);
            _flowFieldB = new FlowFieldPathfinder(_map);
            _flowFieldExit = new FlowFieldPathfinder(_map);
            _flowFieldA.CalculateFlowField((int)_spotA.X, (int)_spotA.Y);
            _flowFieldB.CalculateFlowField((int)_spotB.X, (int)_spotB.Y);
            _flowFieldExit.CalculateFlowField((int)_exit.X, (int)_exit.Y);

            // 初始化ORCA
            _orca = new ORCASimulation();
            _visitorState = new Dictionary<int, int>();

            // 生成游客
            for (int i = 0; i < visitorCount; i++)
            {
                var pos = new Vector2(
                    _random.Next(0, 5),
                    _random.Next(20, 30));
                var agent = _orca.AddAgent(pos, radius: 0.5f, maxSpeed: 1.8f * speedMultiplier);
                agent.PreferredVel = new Vector2(1, 0);
                _visitorState[i] = _random.Next(2); // 随机分配目的地
            }
        }

        public void Update(float deltaTime)
        {
            _collisionCount = 0;

            for (int i = 0; i < _orca.Agents.Count; i++)
            {
                var agent = _orca.Agents[i];
                int state = _visitorState[i];

                // 获取流场方向
                Vector2 flowDir = state switch
                {
                    0 => _flowFieldA.GetDirection(agent.Position.X, agent.Position.Y),
                    1 => _flowFieldB.GetDirection(agent.Position.X, agent.Position.Y),
                    2 => _flowFieldExit.GetDirection(agent.Position.X, agent.Position.Y),
                    _ => Vector2.Zero
                };

                agent.PreferredVel = flowDir * agent.MaxSpeed;

                // 检查是否到达目的地
                float distToA = Vector2.Distance(agent.Position, _spotA);
                float distToB = Vector2.Distance(agent.Position, _spotB);
                float distToExit = Vector2.Distance(agent.Position, _exit);

                if (state == 0 && distToA < 5f)
                {
                    _visitorState[i] = 2; // 转向出口
                    _arrivedCount++;
                }
                else if (state == 1 && distToB < 5f)
                {
                    _visitorState[i] = 2; // 转向出口
                    _arrivedCount++;
                }
                else if (state == 2 && distToExit < 5f)
                {
                    // 回到入口重新开始（循环演示）
                    agent.Position = new Vector2(
                        _random.Next(0, 5),
                        _random.Next(20, 30));
                    _visitorState[i] = _random.Next(2);
                    _arrivedCount++;
                }
            }

            _orca.Update(deltaTime);

            // 统计碰撞
            for (int i = 0; i < _orca.Agents.Count; i++)
                for (int j = i + 1; j < _orca.Agents.Count; j++)
                    if (Vector2.Distance(_orca.Agents[i].Position, _orca.Agents[j].Position) < 0.9f)
                        _collisionCount++;
        }

        public ORCAAgent GetAgent(int index) => _orca.Agents[index];
        public int GetVisitorState(int index) => _visitorState.TryGetValue(index, out var state) ? state : 0;
    }

    #endregion

    #region ===== 动物群体系统 =====

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

    #endregion

    #region ===== 员工任务系统 =====

    /// <summary>
    /// 员工状态
    /// </summary>
    public enum EmployeeState
    {
        Idle,       // 空闲等待
        Moving,     // 移动中
        Working,    // 工作中
        Resting     // 休息中
    }

    /// <summary>
    /// 员工任务
    /// </summary>
    public class EmployeeTask
    {
        public string Type { get; set; } = string.Empty;
        public Vector2 Position { get; set; }
        public float Duration { get; set; }
        public float Progress { get; set; }
        public bool IsCompleted { get; set; }
        public int AssignedEmployeeId { get; set; } = -1;
    }

    /// <summary>
    /// 员工
    /// </summary>
    public class Employee
    {
        public int Id { get; set; }
        public Vector2 Position { get; set; }
        public Vector2 Velocity { get; set; }
        public EmployeeState State { get; set; } = EmployeeState.Idle;
        public EmployeeTask? CurrentTask { get; set; }
        public float Speed { get; set; } = 2.0f;
        public List<GridNode> CurrentPath { get; set; } = new();
        public int PathIndex { get; set; }

        // 工作中状态
        public float WorkProgress { get; set; }
        public float WorkDuration { get; set; }
    }

    /// <summary>
    /// 员工任务调度系统
    /// 使用A*寻路 + FSM状态机
    /// </summary>
    public class EmployeeTaskSystem
    {
        private readonly GridMap _map;
        private readonly AStarPathfinder _astar;
        private readonly List<Employee> _employees = new();
        private readonly List<EmployeeTask> _tasks = new();
        private readonly Random _random = new();

        private int _completedTasks;
        private int _totalArrivals;

        public int CompletedTasks => _completedTasks;
        public int TotalArrivals => _totalArrivals;
        public IReadOnlyList<Employee> Employees => _employees;
        public IReadOnlyList<EmployeeTask> Tasks => _tasks;

        public EmployeeTaskSystem(GridMap map, int employeeCount)
        {
            _map = map;
            _astar = new AStarPathfinder();

            // 创建员工
            for (int i = 0; i < employeeCount; i++)
            {
                _employees.Add(new Employee
                {
                    Id = i,
                    Position = new Vector2(_random.Next(10, 50), _random.Next(10, 50)),
                    State = EmployeeState.Idle,
                    Speed = 2.0f
                });
            }
        }

        public void AddTask(string type, Vector2 position, float duration)
        {
            _tasks.Add(new EmployeeTask
            {
                Type = type,
                Position = position,
                Duration = duration,
                IsCompleted = false
            });
        }

        /// <summary>
        /// 移除指定位置的任务
        /// </summary>
        public void RemoveTaskAt(Vector2 position)
        {
            var task = _tasks.FirstOrDefault(t =>
                Math.Abs(t.Position.X - position.X) < 0.1f &&
                Math.Abs(t.Position.Y - position.Y) < 0.1f);

            if (task != null)
            {
                // 如果任务已分配给员工，重置该员工状态
                if (task.AssignedEmployeeId >= 0)
                {
                    var emp = _employees.FirstOrDefault(e => e.Id == task.AssignedEmployeeId);
                    if (emp != null)
                    {
                        emp.State = EmployeeState.Idle;
                        emp.CurrentTask = null;
                        emp.CurrentPath.Clear();
                    }
                }
                _tasks.Remove(task);
            }
        }

        public void Update(float deltaTime)
        {
            // 1. 分配空闲员工到未分配任务
            AssignIdleEmployees();

            // 2. 更新所有员工状态
            foreach (var employee in _employees)
            {
                switch (employee.State)
                {
                    case EmployeeState.Idle:
                        // 等待分配
                        break;

                    case EmployeeState.Moving:
                        MoveAlongPath(employee, deltaTime);
                        break;

                    case EmployeeState.Working:
                        DoWork(employee, deltaTime);
                        break;

                    case EmployeeState.Resting:
                        // 休息完成后的处理
                        if (employee.WorkProgress <= 0)
                        {
                            employee.State = EmployeeState.Idle;
                            employee.CurrentTask = null;
                        }
                        break;
                }
            }

            // 3. 清理已完成任务
            _tasks.RemoveAll(t => t.IsCompleted);
        }

        private void AssignIdleEmployees()
        {
            foreach (var employee in _employees)
            {
                if (employee.State != EmployeeState.Idle) continue;

                // 找最近的任务
                EmployeeTask? nearestTask = null;
                float nearestDist = float.MaxValue;

                foreach (var task in _tasks)
                {
                    if (task.IsCompleted || task.AssignedEmployeeId >= 0) continue;

                    float dist = Vector2.Distance(employee.Position, task.Position);
                    if (dist < nearestDist)
                    {
                        nearestDist = dist;
                        nearestTask = task;
                    }
                }

                if (nearestTask != null)
                {
                    AssignTask(employee, nearestTask);
                }
            }
        }

        private void AssignTask(Employee employee, EmployeeTask task)
        {
            employee.CurrentTask = task;
            task.AssignedEmployeeId = employee.Id;

            // 使用A*寻路
            int startX = (int)employee.Position.X;
            int startY = (int)employee.Position.Y;
            int goalX = (int)task.Position.X;
            int goalY = (int)task.Position.Y;

            var start = _map.GetNode(startX, startY);
            var goal = _map.GetNode(goalX, goalY);
            var path = _astar.FindPath(_map, start, goal);

            if (path.Count > 0)
            {
                employee.CurrentPath = path;
                employee.PathIndex = 0;
                employee.State = EmployeeState.Moving;
            }
            else
            {
                // 无法到达，直接开始工作
                employee.State = EmployeeState.Working;
                employee.WorkProgress = 0;
                employee.WorkDuration = task.Duration;
            }
        }

        private void MoveAlongPath(Employee employee, float deltaTime)
        {
            if (employee.CurrentPath.Count == 0 || employee.PathIndex >= employee.CurrentPath.Count)
            {
                // 到达目标，开始工作
                employee.State = EmployeeState.Working;
                employee.WorkProgress = 0;
                if (employee.CurrentTask != null)
                    employee.WorkDuration = employee.CurrentTask.Duration;
                return;
            }

            var targetNode = employee.CurrentPath[employee.PathIndex];
            var targetPos = new Vector2(targetNode.X, targetNode.Y);
            var dir = targetPos - employee.Position;
            float dist = dir.Length();

            if (dist < 0.5f)
            {
                // 到达路径点
                employee.PathIndex++;
                _totalArrivals++;
            }
            else
            {
                // 移动
                employee.Velocity = dir.Normalized() * employee.Speed;
                employee.Position += employee.Velocity * deltaTime;
            }
        }

        private void DoWork(Employee employee, float deltaTime)
        {
            employee.WorkProgress += deltaTime;

            if (employee.WorkProgress >= employee.WorkDuration)
            {
                // 任务完成
                if (employee.CurrentTask != null)
                {
                    employee.CurrentTask.IsCompleted = true;
                    _completedTasks++;
                }
                employee.State = EmployeeState.Idle;
                employee.CurrentTask = null;
            }
        }

        public EmployeeState GetEmployeeState(int index)
        {
            return index < _employees.Count ? _employees[index].State : EmployeeState.Idle;
        }
    }

    #endregion

    #region ===== 紧急疏散系统 =====

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

            // 添加围墙
            BuildWalls();

            // 生成疏散人员
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
            // 出口在中间 (38-47)，两侧有墙
            for (int y = 0; y < _mapHeight; y++)
            {
                if (y < 38 || y > 47)
                {
                    _simulation.AddWall(85, y, 86, y); // 右侧墙
                }
            }

            // 左侧入口墙
            for (int y = 0; y < _mapHeight; y++)
                _simulation.AddWall(0, y, 1, y);

            // 上下墙
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
                    // 已疏散，重置到左边继续（循环演示）
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

    #endregion
}
