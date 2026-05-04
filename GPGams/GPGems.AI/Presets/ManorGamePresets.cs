/*
 * 经营游戏算法参数预设库
 * 用途：所有参数在GPGems中已调优，直接抄到Lua配置表使用
 * 工作流：在GPGems验证 → 复制预设 → Lua重写
 */

using System;
using GPGems.AI.Boids;

namespace GPGems.AI.Presets
{
    /// <summary>
    /// 庄园/农场/经营类游戏参数预设
    /// </summary>
    public static class ManorGamePresets
    {
        #region ===== Boids 动物预设 =====

        /// <summary>
        /// 🦅 飞鸟群
        /// 特点：松散、快速、自由飞行
        /// </summary>
        public static BoidSettings BirdFlock => new()
        {
            PerceptionRange = 30f,
            SeparationDist = 8f,
            DesiredSpeed = 5f,
            MaxSpeed = 8f,
            SeparationWeight = 1.2f,
            AlignmentWeight = 1.0f,
            CohesionWeight = 0.8f,
            WanderWeight = 0.5f,  // 没有目标时随机漫游
            MaxAcceleration = 8f,
        };

        /// <summary>
        /// 🐟 池塘鱼群
        /// 特点：密集、同步转向、慢速度
        /// </summary>
        public static BoidSettings FishSchool => new()
        {
            PerceptionRange = 20f,
            SeparationDist = 4f,  // 鱼可以很密集
            DesiredSpeed = 2.5f,
            MaxSpeed = 4f,
            SeparationWeight = 1.8f,
            AlignmentWeight = 1.2f,
            CohesionWeight = 1.2f,
            WanderWeight = 0.3f,
            MaxAcceleration = 5f,
            VerticalDamping = 0.95f,  // 接近2D，减少上下浮动
        };

        /// <summary>
        /// 🐄 放牧动物群（牛羊）
        /// 特点：缓慢移动、松散聚集、偶尔停下
        /// </summary>
        public static BoidSettings GrazingAnimal => new()
        {
            PerceptionRange = 25f,
            SeparationDist = 6f,
            DesiredSpeed = 1f,  // 走得很慢
            MaxSpeed = 2f,
            SeparationWeight = 1.5f,
            AlignmentWeight = 0.5f,  // 不怎么对齐方向
            CohesionWeight = 0.6f,   // 不怎么凝聚
            WanderWeight = 0.8f,      // 大部分时候在漫游
            MaxAcceleration = 2f,
        };

        /// <summary>
        /// 🦋 蝴蝶群
        /// 特点：快速转向、飘忽不定、小范围
        /// </summary>
        public static BoidSettings Butterfly => new()
        {
            PerceptionRange = 10f,
            SeparationDist = 2f,
            DesiredSpeed = 1.5f,
            MaxSpeed = 3f,
            SeparationWeight = 2.0f,
            AlignmentWeight = 0.3f,  // 方向很乱
            CohesionWeight = 0.4f,   // 不太凝聚
            WanderWeight = 1.0f,      // 高度随机
            MaxAcceleration = 10f,    // 转向极快
        };

        #endregion

        #region ===== ORCA 人群预设 =====

        /// <summary>
        /// 👨‍👩‍👧‍👦 普通游客
        /// 特点：保持礼貌距离、不着急、避障意愿强
        /// </summary>
        public static ORCASettings CasualVisitor => new()
        {
            Radius = 0.5f,         // 人均占地0.5m半径
            MaxSpeed = 1.8f,       // 休闲散步速度
            TimeHorizon = 2.5f,    // 提前2.5秒预测碰撞
            NeighborSearchRange = 6f,
            SeparationBias = 1.2f,
        };

        /// <summary>
        /// 🏃 赶时间的游客
        /// 特点：速度快、可接受较近的距离
        /// </summary>
        public static ORCASettings HurriedVisitor => new()
        {
            Radius = 0.45f,
            MaxSpeed = 3.5f,       // 快走/小跑
            TimeHorizon = 1.5f,    // 反应时间短，更"猛"
            NeighborSearchRange = 8f,
            SeparationBias = 0.9f,
        };

        /// <summary>
        /// 👪 小团体（3-5人）
        /// 特点：内部吸引力强、移动较慢
        /// </summary>
        public static ORCASettings GroupVisitor => new()
        {
            Radius = 0.5f,
            MaxSpeed = 1.5f,       // 一起走更慢
            TimeHorizon = 3.0f,    // 更小心
            NeighborSearchRange = 7f,
            SeparationBias = 1.5f, // 与其他团体保持更远
            // + 社会力的同伴吸引力
        };

        #endregion

        #region ===== 社会力 排队预设 =====

        /// <summary>
        /// 🚶 排队人流
        /// 特点：同向吸引力强、横向排斥、保持前后距离
        /// </summary>
        public static SocialForceSettings QueueLine => new()
        {
            SeparationStrength = 2.0f,
            SeparationRange = 1.2f,    // 前后1.2米
            DesiredSpeed = 0.8f,        // 排队很慢走
            SameDirectionAttraction = 0.5f,  // 跟前面的人走
            CrossingPenalty = 1.5f,          // 横穿排队队伍的额外排斥
        };

        /// <summary>
        /// 🚪 出入口疏散
        /// 特点：速度快、竞争、但仍保持基本礼貌
        /// </summary>
        public static SocialForceSettings ExitEvacuation => new()
        {
            SeparationStrength = 1.5f,
            SeparationRange = 0.8f,     // 紧急时可以挤一点
            DesiredSpeed = 2.5f,         // 快走
            GoalStrength = 3.0f,         // 目标吸引力强
            WallRepulsionStrength = 2.5f, // 离墙远点
        };

        #endregion

        #region ===== 流场寻路预设 =====

        /// <summary>
        /// 🗺️ 园区人流流场
        /// </summary>
        public static FlowFieldSettings ParkCrowd => new()
        {
            CellSize = 1.0f,       // 1米网格
            CostWeight = 1.0f,
            Inertia = 0.3f,         // 方向保持惯性，防止频繁抖动
            AttractionPointWeight = 2.0f,  // 景点吸引强度
            RepulsionFromObstacles = 1.5f,  // 建筑排斥
        };

        #endregion

        #region ===== 寻路预设 =====

        /// <summary>
        /// 🧍 步行寻路参数
        /// </summary>
        public static AStarSettings WalkingPath => new()
        {
            HeuristicWeight = 1.0f,      // 标准A*，最优
            StraightCost = 1.0f,
            DiagonalCost = 1.4142f,      // 对角线真实成本
            SmoothingEnabled = true,     // 路径平滑，看起来更自然
        };

        /// <summary>
        /// 🏃 员工快速移动
        /// </summary>
        public static AStarSettings EmployeeFastPath => new()
        {
            HeuristicWeight = 1.2f,      // 稍微贪心一点，更快
            StraightCost = 1.0f,
            DiagonalCost = 1.4142f,
            SmoothingEnabled = true,
            TurnPenalty = 0.1f,          // 减少转弯，走更直的路
        };

        #endregion
    }

    #region ===== 配置类定义（Lua移植参考） =====

    public class ORCASettings
    {
        public float Radius;
        public float MaxSpeed;
        public float TimeHorizon;
        public float NeighborSearchRange;
        public float SeparationBias;
    }

    public class SocialForceSettings
    {
        public float SeparationStrength;
        public float SeparationRange;
        public float DesiredSpeed;
        public float SameDirectionAttraction;
        public float CrossingPenalty;
        public float GoalStrength;
        public float WallRepulsionStrength;
    }

    public class FlowFieldSettings
    {
        public float CellSize;
        public float CostWeight;
        public float Inertia;
        public float AttractionPointWeight;
        public float RepulsionFromObstacles;
    }

    public class AStarSettings
    {
        public float HeuristicWeight;
        public float StraightCost;
        public float DiagonalCost;
        public bool SmoothingEnabled;
        public float TurnPenalty;
    }

    #endregion
}

/*
 * Lua 配置表移植参考（直接复制到游戏）：
 * =====================================
 *
-- Boids 预设
BoidPresets = {
    bird = {
        perceptionRange = 30,
        separationDist = 8,
        desiredSpeed = 5,
        maxSpeed = 8,
        separationWeight = 1.2,
        alignmentWeight = 1.0,
        cohesionWeight = 0.8,
        wanderWeight = 0.5,
    },
    fish = {
        perceptionRange = 20,
        separationDist = 4,
        desiredSpeed = 2.5,
        separationWeight = 1.8,
        alignmentWeight = 1.2,
        cohesionWeight = 1.2,
    },
    grazing = {
        perceptionRange = 25,
        separationDist = 6,
        desiredSpeed = 1,
        maxSpeed = 2,
        separationWeight = 1.5,
        alignmentWeight = 0.5,
        cohesionWeight = 0.6,
        wanderWeight = 0.8,
    },
    butterfly = {
        perceptionRange = 10,
        separationDist = 2,
        desiredSpeed = 1.5,
        separationWeight = 2.0,
        alignmentWeight = 0.3,
        cohesionWeight = 0.4,
        wanderWeight = 1.0,
    },
}

-- ORCA 预设
ORCAPresets = {
    casual = {
        radius = 0.5,
        maxSpeed = 1.8,
        timeHorizon = 2.5,
        neighborRange = 6,
        separationBias = 1.2,
    },
    hurried = {
        radius = 0.45,
        maxSpeed = 3.5,
        timeHorizon = 1.5,
        neighborRange = 8,
        separationBias = 0.9,
    },
}

-- 使用示例：
-- local fishBoid = CreateBoid(BoidPresets.fish)
-- local visitor = CreateORCAAgent(ORCAPresets.casual)
 *
 */
