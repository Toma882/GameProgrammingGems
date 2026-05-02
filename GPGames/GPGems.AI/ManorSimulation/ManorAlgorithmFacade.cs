/*
 * 庄园经营模拟 - 算法门面入口
 * 统一封装所有经营模拟所需的算法，方便上层调用
 *
 * 依赖关系:
 *   ┌─────────────────────────────────────────────────────────────────┐
 *   │                     ManorAlgorithmFacade                       │
 *   ├───────────┬───────────┬────────────┬────────────┬─────────────┤
 *   │   寻路    │  群体避障 │   Boids   │ 任务调度  │  影响场计算  │
 *   │  FlowField│   ORCA   │           │           │             │
 *   │  A*       │  社会力   │           │           │             │
 *   └───────────┴───────────┴────────────┴────────────┴─────────────┘
 */

using System;
using System.Collections.Generic;
using GPGems.AI.Pathfinding;
using GPGems.AI.CollisionAvoidance;
using GPGems.AI.Boids;
using GPGems.AI.Presets;
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
        public FlowFieldPathfinder? FlowField { get; private set; }
        public ORCASimulation? ORCA { get; private set; }
        public Flock? AnimalFlock { get; private set; }
        #endregion

        #region 初始化

        /// <summary>
        /// 初始化庄园地图尺寸和基础算法
        /// </summary>
        /// <param name="mapWidth">地图格子数X</param>
        /// <param name="mapHeight">地图格子数Y</param>
        public void Initialize(int mapWidth = 100, int mapHeight = 100)
        {
            // 流场寻路初始化
            var dummyMap = new GridMap(mapWidth, mapHeight);
            FlowField = new FlowFieldPathfinder(dummyMap);

            // ORCA群体避障初始化
            ORCA = new ORCASimulation();

            // 动物群体初始化（懒加载，需要时创建
        }

        #endregion

        #region ===== 游客人流系统 =====

        /// <summary>
        /// 创建游客人流模拟
        /// </summary>
        public VisitorFlowSystem CreateVisitorFlowSystem(int visitorCount = 100)
        {
            if (ORCA == null)
                throw new InvalidOperationException("请先调用 Initialize()");

            return new VisitorFlowSystem(ORCA, FlowField!, visitorCount);
        }

        #endregion

        #region ===== 动物群体系统 =====

        /// <summary>
        /// 创建鱼群模拟
        /// </summary>
        public Flock CreateFishFlock(int fishCount = 50)
        {
            var flock = new Flock();
            var settings = ManorGamePresets.FishSchool;

            for (int i = 0; i < fishCount; i++)
            {
                flock.AddBoid(
                    new Vector3(Random.Shared.Next(0, 50), 0, Random.Shared.Next(0, 50)),
                    Vector3.UnitX * 2
                );
            }

            AnimalFlock = flock;
            return flock;
        }

        /// <summary>
        /// 创建放牧牛羊群
        /// </summary>
        public Flock CreateGrazingFlock(int animalCount = 20)
        {
            var flock = new Flock();
            var settings = ManorGamePresets.GrazingAnimal;

            for (int i = 0; i < animalCount; i++)
            {
                flock.AddBoid(
                    new Vector3(Random.Shared.Next(0, 100), 0, Random.Shared.Next(0, 100)),
                    new Vector3(Random.Shared.Next(-1, 2), 0, Random.Shared.Next(-1, 2))
                );
            }

            return flock;
        }

        /// <summary>
        /// 创建蝴蝶群
        /// </summary>
        public Flock CreateButterflyFlock(int butterflyCount = 30)
        {
            var flock = new Flock();
            var settings = ManorGamePresets.Butterfly;

            for (int i = 0; i < butterflyCount; i++)
            {
                flock.AddBoid(
                    new Vector3(Random.Shared.Next(20, 80), 0, Random.Shared.Next(20, 80)),
                    new Vector3(Random.Shared.Next(-2, 3), 0, Random.Shared.Next(-2, 3))
                );
            }

            return flock;
        }

        #endregion

        #region ===== 员工任务系统 =====

        /// <summary>
        /// 创建员工任务调度器
        /// </summary>
        public EmployeeTaskSystem CreateEmployeeTaskSystem(int employeeCount = 5)
        {
            return new EmployeeTaskSystem(employeeCount);
        }

        #endregion

        #region ===== 紧急疏散系统 =====

        /// <summary>
        /// 创建紧急疏散模拟
        /// </summary>
        public EvacuationSystem CreateEvacuationSystem(int agentCount = 300)
        {
            return new EvacuationSystem(agentCount);
        }

        #endregion

        #region ===== 性能统计 =====

        public string GetPerformanceReport()
        {
            return $"算法系统状态:\n" +
                 $"  流场寻路: {(FlowField != null ? "✅ 已初始化" : "❌ 未初始化")}\n" +
                 $"  ORCA避障: {(ORCA != null ? $"✅ {ORCA.Agents.Count}单位" : "❌ 未初始化")}\n" +
                 $"  动物群体: {(AnimalFlock != null ? $"✅ {AnimalFlock.Boids.Count}只" : "❌ 未初始化")}";
        }

        #endregion
    }

    #region ===== 子系统实现骨架 =====

    /// <summary>
    /// 游客人流系统
    /// </summary>
    public class VisitorFlowSystem
    {
        private readonly ORCASimulation _orca;
        private readonly FlowFieldPathfinder _flowField;
        public int VisitorCount { get; }

        public VisitorFlowSystem(ORCASimulation orca, FlowFieldPathfinder flowField, int visitorCount)
        {
            _orca = orca;
            _flowField = flowField;
            VisitorCount = visitorCount;
        }

        public void Update(float deltaTime)
        {
            _orca.Update(deltaTime);
        }

        public void SetAttractionPoint(int gridX, int gridY)
        {
            _flowField.CalculateFlowField(gridX, gridY);
        }
    }

    /// <summary>
    /// 员工任务调度系统
    /// </summary>
    public class EmployeeTaskSystem
    {
        public int EmployeeCount { get; }
        public List<EmployeeTask> Tasks { get; } = new();

        public EmployeeTaskSystem(int employeeCount)
        {
            EmployeeCount = employeeCount;
        }

        public void AddTask(EmployeeTask task) => Tasks.Add(task);
        public void Update(float deltaTime) { }
    }

    public class EmployeeTask
    {
        public string Type { get; set; } = string.Empty;
        public Vector2 Position { get; set; }
        public float Duration { get; set; }
    }

    /// <summary>
    /// 紧急疏散系统
    /// </summary>
    public class EvacuationSystem
    {
        public int AgentCount { get; }

        public EvacuationSystem(int agentCount)
        {
            AgentCount = agentCount;
        }

        public void Update(float deltaTime) { }
    }

    #endregion
}
