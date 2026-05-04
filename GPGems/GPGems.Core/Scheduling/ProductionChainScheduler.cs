/*
 * 生产链调度器 Production Chain Scheduler
 * 核心用途:
 *   - 多建筑生产任务排队
 *   - 员工任务分配（就近优先、优先级优先
 *   - 资源竞争仲裁（多个建筑抢同一种原料
 *   - 批量生产优化
 *
 * 调度策略: 优先级(Priority) + 就近(Distance) + 负载均衡(LoadBalance)
 */

using System;
using System.Collections.Generic;
using System.Linq;

namespace GPGems.Core.Scheduling
{
    /// <summary>
    /// 生产链调度器 - 管理所有建筑/员工的任务队列
    /// </summary>
    public class ProductionChainScheduler
    {
        private readonly PriorityQueue<ProductionTask, int> _taskQueue = new();
        private readonly Dictionary<int, Worker> _workers = new();
        private readonly Dictionary<int, Building> _buildings = new();
        private readonly Dictionary<int, TaskAssignment> _activeAssignments = new();
        private int _nextTaskId = 1;

        #region 任务管理

        /// <summary>
        /// 添加生产任务
        /// </summary>
        public int AddTask(ProductionTask task)
        {
            task.Id = _nextTaskId++;
            task.SubmitTime = DateTime.Now;
            _taskQueue.Enqueue(task, task.Priority);
            return task.Id;
        }

        /// <summary>
        /// 批量添加任务（同一建筑多个生产任务
        /// </summary>
        public void AddBatchTasks(int buildingId, IEnumerable<ProductionTask> tasks)
        {
            foreach (var task in tasks)
            {
                task.BuildingId = buildingId;
                AddTask(task);
            }
        }

        /// <summary>
        /// 取消任务
        /// </summary>
        public bool CancelTask(int taskId)
        {
            // 注意: .NET 6 PriorityQueue 不支持随机删除，用懒标记
            var assignment = _activeAssignments.Values
                .FirstOrDefault(a => a.TaskId == taskId);
            if (assignment != null)
            {
                assignment.IsCancelled = true;
                return true;
            }
            return false;
        }

        #endregion

        #region 员工/建筑管理

        public void RegisterWorker(Worker worker)
        {
            _workers[worker.Id] = worker;
        }

        public void RegisterBuilding(Building building)
        {
            _buildings[building.Id] = building;
        }

        #endregion

        #region 调度算法

        /// <summary>
        /// 执行一次调度（每帧调用
        /// </summary>
        public void Tick(float deltaTime)
        {
            // 1. 更新进行中的任务进度
            UpdateActiveTasks(deltaTime);

            // 2. 找出空闲员工
            var idleWorkers = _workers.Values
                .Where(w => w.State == WorkerState.Idle)
                .ToList();

            if (idleWorkers.Count == 0)
                return;

            // 3. 按优先级分配任务
            while (idleWorkers.Count > 0 && _taskQueue.Count > 0)
            {
                // 取出最高优先级任务
                if (!_taskQueue.TryDequeue(out var task, out var priority))
                    break;

                // 找到最合适的员工
                var bestWorker = FindBestWorker(task, idleWorkers);
                if (bestWorker == null)
                {
                    // 无合适员工，任务重新入队（延后处理
                    // _taskQueue.Enqueue(task, priority); 可以降低优先级
                    break;
                }

                // 分配任务
                AssignTask(bestWorker, task);
                idleWorkers.Remove(bestWorker);
            }
        }

        /// <summary>
        /// 评分算法：找到最适合做此任务的员工
        /// </summary>
        private Worker? FindBestWorker(ProductionTask task, List<Worker> idleWorkers)
        {
            if (!_buildings.TryGetValue(task.BuildingId, out var building))
                return null;

            float bestScore = float.MinValue;
            Worker? bestWorker = null;

            foreach (var worker in idleWorkers)
            {
                // ===== 评分规则（可配置权重
                float score = 0;

                // 1. 距离分（越近越好）
                float distance = Distance(worker.X, worker.Y, building.X, building.Y);
                score -= distance * 1f;  // 距离权重

                // 2. 技能匹配分（员工技能与任务匹配度
                if (worker.Skills.TryGetValue(task.RequiredSkill, out var skillLevel))
                    score += skillLevel * 5f;

                // 3. 工作量分（越闲优先级越高
                score -= worker.TaskCountToday * 0.1f;

                // 4. 任务类型偏好
                if (worker.PreferredTaskType == task.Type)
                    score += 10f;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestWorker = worker;
                }
            }

            return bestWorker;
        }

        private void AssignTask(Worker worker, ProductionTask task)
        {
            worker.State = WorkerState.Working;
            worker.CurrentTaskId = task.Id;

            var assignment = new TaskAssignment
            {
                TaskId = task.Id,
                WorkerId = worker.Id,
                BuildingId = task.BuildingId,
                Progress = 0f,
                TotalDuration = task.Duration
            };

            _activeAssignments[worker.Id] = assignment;

            // 任务开始回调
            task.OnStart?.Invoke();
        }

        private void UpdateActiveTasks(float deltaTime)
        {
            var completed = new List<int>();

            foreach (var (workerId, assignment) in _activeAssignments)
            {
                if (assignment.IsCancelled)
                {
                    completed.Add(workerId);
                    continue;
                }

                assignment.Progress += deltaTime;

                if (assignment.Progress >= assignment.TotalDuration)
                {
                    // 任务完成
                    if (_workers.TryGetValue(workerId, out var worker))
                    {
                        worker.State = WorkerState.Idle;
                        worker.CurrentTaskId = 0;
                        worker.TaskCountToday++;
                    }

                    // 完成回调
                    // task.OnComplete?.Invoke();

                    completed.Add(workerId);
                }
            }

            // 清理已完成的任务
            foreach (var workerId in completed)
            {
                _activeAssignments.Remove(workerId);
            }
        }

        #endregion

        #region 查询

        /// <summary>
        /// 获取员工当前任务
        /// </summary>
        public TaskAssignment? GetWorkerAssignment(int workerId)
        {
            return _activeAssignments.TryGetValue(workerId, out var a) ? a : null;
        }

        /// <summary>
        /// 获取排队任务数
        /// </summary>
        public int GetQueueLength() => _taskQueue.Count;

        /// <summary>
        /// 获取进行中的任务数
        /// </summary>
        public int GetActiveTaskCount() => _activeAssignments.Count;

        /// <summary>
        /// 员工利用率统计
        /// </summary>
        public (int working, int idle) GetWorkerStats()
        {
            int working = _workers.Values.Count(w => w.State == WorkerState.Working);
            return (working, _workers.Count - working);
        }

        #endregion

        private static float Distance(int x1, int y1, int x2, int y2)
        {
            float dx = x1 - x2;
            float dy = y1 - y2;
            return MathF.Sqrt(dx * dx + dy * dy);
        }
    }

    #region 数据类型

    public class ProductionTask
    {
        public int Id;
        public int BuildingId;
        public string Type = string.Empty;  // 任务类型：Harvest/Craft/Serve等
        public string RequiredSkill = string.Empty; // 需要的技能
        public int Priority;              // 优先级（越高越先执行
        public float Duration;             // 所需时间
        public Action? OnStart;
        public Action? OnComplete;
        public DateTime SubmitTime;
    }

    public class Worker
    {
        public int Id;
        public string Name = string.Empty;
        public int X, Y;                  // 当前位置
        public WorkerState State;
        public int CurrentTaskId;
        public int TaskCountToday;        // 今日已完成任务数
        public Dictionary<string, float> Skills = new(); // 技能等级
        public string? PreferredTaskType; // 偏好任务类型
    }

    public class Building
    {
        public int Id;
        public string Name = string.Empty;
        public int X, Y;
        public string BuildingType = string.Empty;
    }

    public class TaskAssignment
    {
        public int TaskId;
        public int WorkerId;
        public int BuildingId;
        public float Progress;
        public float TotalDuration;
        public bool IsCancelled;
    }

    public enum WorkerState
    {
        Idle,       // 空闲
        Moving,     // 移动中
        Working,    // 工作中
        Resting     // 休息中
    }

    #endregion
}
