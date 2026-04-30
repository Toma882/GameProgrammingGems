/*
 * 时间轮调度器 HashedTimeWheel
 * 时间复杂度: O(1) 添加/取消，O(k) 触发k个到期事件
 *
 * 经营游戏核心用途:
 *   - 作物成熟计时: 5分钟 → 1小时 → 8小时
 *   - 建筑生产: 工厂/作坊生产完成
 *   - 技能/建筑冷却
 *   - 顾客生成间隔
 *   - 全局每日/每周任务重置
 *
 * 设计: 分层多级时间轮，支持从毫秒 → 小时级别的精度
 */

using System;
using System.Collections.Generic;

namespace GPGems.Core.DataStructures
{
    /// <summary>
    /// 分层时间轮调度器
    /// </summary>
    public class HierarchicalTimeWheel
    {
        // 5层时间轮: 毫秒 → 秒 → 分 → 时 → 天
        private readonly TimeWheelLayer[] _layers;
        private long _currentTime; // 虚拟时间（毫秒）
        private long _startTime;   // 启动时的真实时间
        private readonly Queue<TimerTask> _pendingAdd = new();

        private const int MS_SLOTS = 1000;   // 毫秒轮: 1000格
        private const int SEC_SLOTS = 60;    // 秒轮: 60格
        private const int MIN_SLOTS = 60;    // 分轮: 60格
        private const int HOUR_SLOTS = 24;   // 时轮: 24格
        private const int DAY_SLOTS = 365;   // 天轮: 365格

        public HierarchicalTimeWheel()
        {
            _layers = new TimeWheelLayer[5];
            _layers[0] = new TimeWheelLayer(MS_SLOTS, 1);           // 1ms/格
            _layers[1] = new TimeWheelLayer(SEC_SLOTS, 1000);        // 1s/格
            _layers[2] = new TimeWheelLayer(MIN_SLOTS, 60 * 1000);   // 1min/格
            _layers[3] = new TimeWheelLayer(HOUR_SLOTS, 3600 * 1000); // 1h/格
            _layers[4] = new TimeWheelLayer(DAY_SLOTS, 86400 * 1000); // 1d/格

            _startTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            _currentTime = 0;
        }

        #region 对外API

        /// <summary>
        /// 添加定时任务
        /// </summary>
        /// <param name="delayMs">延迟毫秒数</param>
        /// <param name="callback">回调</param>
        /// <returns>任务ID，用于取消</returns>
        public long AddTimer(long delayMs, Action callback)
        {
            if (delayMs < 0) delayMs = 0;

            var task = new TimerTask
            {
                Id = GenerateId(),
                DelayMs = delayMs,
                DueTime = _currentTime + delayMs,
                Callback = callback,
                Period = 0
            };

            _pendingAdd.Enqueue(task);
            return task.Id;
        }

        /// <summary>
        /// 添加重复任务
        /// </summary>
        public long AddPeriodic(long periodMs, Action callback)
        {
            var task = new TimerTask
            {
                Id = GenerateId(),
                DelayMs = periodMs,
                DueTime = _currentTime + periodMs,
                Callback = callback,
                Period = periodMs
            };

            _pendingAdd.Enqueue(task);
            return task.Id;
        }

        /// <summary>
        /// 取消任务
        /// </summary>
        public bool CancelTask(long taskId)
        {
            // 懒删除：只标记，到期时跳过
            foreach (var layer in _layers)
            {
                foreach (var slot in layer.Slots)
                {
                    var task = slot.Find(t => t.Id == taskId);
                    if (task != null)
                    {
                        task.IsCancelled = true;
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// 推进时间（每帧调用）
        /// </summary>
        public void Tick()
        {
            // 先处理待添加的任务
            while (_pendingAdd.Count > 0)
                AddTaskInternal(_pendingAdd.Dequeue());

            // 推进第0层（毫秒层）
            int ticks = 1; // 默认每次Tick推进1ms，实际可根据真实delta调整
            for (int i = 0; i < ticks; i++)
            {
                AdvanceLayer(0);
                _currentTime++;
            }
        }

        /// <summary>
        /// 根据真实时间自动推进
        /// </summary>
        public void TickByRealTime()
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            long delta = now - _startTime - _currentTime;

            if (delta > 0)
            {
                for (int i = 0; i < delta; i++)
                {
                    AdvanceLayer(0);
                    _currentTime++;
                }
            }

            // 处理待添加
            while (_pendingAdd.Count > 0)
                AddTaskInternal(_pendingAdd.Dequeue());
        }

        #endregion

        #region 内部实现

        private void AdvanceLayer(int layerIdx)
        {
            var layer = _layers[layerIdx];
            int currentSlot = (int)(_currentTime / layer.SlotMs % layer.SlotCount);

            // 取出当前槽的所有任务
            var tasks = layer.Slots[currentSlot];
            layer.Slots[currentSlot] = new List<TimerTask>();

            foreach (var task in tasks)
            {
                if (task.IsCancelled)
                    continue;

                if (task.Rounds > 0)
                {
                    // 需要继续降级
                    task.Rounds--;
                    AddTaskInternal(task);
                }
                else
                {
                    // 到期触发
                    task.Callback?.Invoke();

                    // 重复任务重新入队
                    if (task.Period > 0 && !task.IsCancelled)
                    {
                        task.DueTime = _currentTime + task.Period;
                        AddTaskInternal(task);
                    }
                }
            }

            // 当前槽走满一圈，向上层进位
            if (currentSlot == 0 && layerIdx < _layers.Length - 1)
            {
                AdvanceLayer(layerIdx + 1);
            }
        }

        private void AddTaskInternal(TimerTask task)
        {
            long delay = task.DueTime - _currentTime;
            if (delay <= 0)
            {
                // 已到期，直接执行
                task.Callback?.Invoke();
                return;
            }

            // 选择合适的层级
            int layerIdx = SelectLayer(delay);
            var layer = _layers[layerIdx];

            // 计算槽位和剩余圈数
            long totalSlots = delay / layer.SlotMs;
            int slot = (int)(((_currentTime + delay) / layer.SlotMs) % layer.SlotCount);
            task.Rounds = (int)(totalSlots / layer.SlotCount);

            layer.Slots[slot].Add(task);
        }

        private int SelectLayer(long delayMs)
        {
                 if (delayMs < 1000)      return 0; // <1s → 毫秒轮
            else if (delayMs < 60 * 1000) return 1; // <1min → 秒轮
            else if (delayMs < 3600 * 1000) return 2; // <1h → 分轮
            else if (delayMs < 86400 * 1000) return 3; // <1d → 时轮
            else return 4; // ≥1d → 天轮
        }

        private long _nextId = 1;
        private long GenerateId() => _nextId++;

        #endregion

        #region 经营游戏便捷API

        /// <summary>
        /// 添加作物成熟计时器
        /// </summary>
        public long AddCropRipen(float minutes, Action onRipen)
        {
            return AddTimer((long)(minutes * 60 * 1000), onRipen);
        }

        /// <summary>
        /// 添加生产完成计时器
        /// </summary>
        public long AddProductionComplete(float seconds, Action onComplete)
        {
            return AddTimer((long)(seconds * 1000), onComplete);
        }

        /// <summary>
        /// 添加顾客生成间隔
        /// </summary>
        public long AddCustomerSpawn(float intervalMs, Action onSpawn)
        {
            return AddPeriodic((long)intervalMs, onSpawn);
        }

        /// <summary>
        /// 每日重置任务（固定时间点，如凌晨4点）
        /// </summary>
        public long AddDailyReset(int hour, int minute, Action onReset)
        {
            var now = DateTime.Now;
            var next = new DateTime(now.Year, now.Month, now.Day, hour, minute, 0);
            if (next <= now) next = next.AddDays(1);

            long delayMs = (long)(next - now).TotalMilliseconds;
            return AddPeriodic(24 * 3600 * 1000, onReset);
        }

        #endregion
    }

    #region 内部类型

    internal class TimeWheelLayer
    {
        public readonly List<TimerTask>[] Slots;
        public readonly int SlotCount;
        public readonly long SlotMs;

        public TimeWheelLayer(int slotCount, long slotMs)
        {
            SlotCount = slotCount;
            SlotMs = slotMs;
            Slots = new List<TimerTask>[slotCount];
            for (int i = 0; i < slotCount; i++)
                Slots[i] = new List<TimerTask>();
        }
    }

    internal class TimerTask
    {
        public long Id;
        public long DelayMs;
        public long DueTime;
        public long Period; // 0=不重复
        public int Rounds; // 剩余圈数
        public Action? Callback;
        public bool IsCancelled;
    }

    #endregion
}
