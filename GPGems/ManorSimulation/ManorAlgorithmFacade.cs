/*
 * 庄园经营模拟 - 算法门面入口
 * 统一封装所有经营模拟所需的算法，方便上层调用
 *
 * 依赖关系:
 *   ┌─────────────────────────────────────────────────────────────────┐
 *   │                     ManorAlgorithmFacade                       │
 *   ├───────────┬───────────┬────────────┬────────────┬─────────────┤
 *   │   寻路    │  群体避障 │   Boids   │ 任务调度  │  社会力模型 │
 *   │  FlowField│   ORCA   │           │ Core.TaskSystem          │
 *   └───────────┴───────────┴────────────┴────────────┴─────────────┘
 */

using System;
using System.Collections.Generic;
using GPGems.AI.Pathfinding;
using GPGems.AI.CollisionAvoidance;
using GPGems.AI.Boids;
using System.Numerics;
using GPGems.Core;
using GPGems.Core.Math;
using GPGems.Core.TaskSystem;
using GPGems.ManorSimulation.Building;
using TaskScheduler = GPGems.Core.TaskSystem.TaskScheduler;

namespace GPGems.ManorSimulation
{
    /// <summary>
    /// 庄园经营模拟算法门面
    /// 所有经营模拟的算法调用从这里入口，上层业务无需关心具体算法实现
    /// </summary>
    public class ManorAlgorithmFacade
    {
        #region 单例

        public static ManorAlgorithmFacade Instance { get; } = new ManorAlgorithmFacade();

        private ManorAlgorithmFacade()
        {
            AssignmentStrategy = new ScoredAssignmentStrategy();
            EmployeeManager = new EmployeeManager(AssignmentStrategy);
            BuildingManager = new BuildingManager();

            // 注册建筑查询处理器
            var bus = CommunicationBus.Instance;
            bus.AddQueryDelegate(BuildingManager, ManorQueries.GetBuildingById, args =>
                args.FirstOrDefault() is int buildingId ? BuildingManager.GetBuilding(buildingId) : null);
        }

        #endregion

        #region 子系统引用

        public GridMap? Map { get; private set; }
        public LayeredGridMap? LayeredMap { get; private set; }

        public IWorkerAssignmentStrategy AssignmentStrategy { get; }
        public EmployeeManager EmployeeManager { get; }
        public BuildingManager BuildingManager { get; }

        #endregion

        #region 初始化

        /// <summary>
        /// 初始化庄园地图尺寸
        /// </summary>
        public void Initialize(int mapWidth = 50, int mapHeight = 50)
        {
            Map = new GridMap(mapWidth, mapHeight);
            LayeredMap = new LayeredGridMap(mapWidth, mapHeight);
        }

        #endregion

        #region 核心调度

        /// <summary>
        /// 任务调度主循环 - 跨子系统协调
        /// </summary>
        public void UpdateTaskSystem(float deltaTime)
        {
            TaskScheduler.Instance.Update(deltaTime);

            var completedTasks = TaskScheduler.Instance.GetRunningTasks()
                .Where(t => t.State == TaskLifecycle.Completed && t is EmployeeTaskBase)
                .Cast<EmployeeTaskBase>()
                .ToList();

            foreach (var task in completedTasks)
            {
                if (task.EmployeeId >= 0)
                {
                    EmployeeManager.CompleteTask(task.EmployeeId);
                }
            }
        }

        #endregion
    }
}
