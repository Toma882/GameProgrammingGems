namespace GPGems.AI.CollisionAvoidance;

/// <summary>
/// ORCA（Optimal Reciprocal Collision Avoidance）
/// 最优互惠碰撞避免算法
///
/// 核心特性：
/// 1. 互惠性：碰撞责任由双方平摊，避免"你让我，我也让你"的死锁
/// 2. 速度空间：在速度空间中求解可行域，而非位置空间
/// 3. 多智能体：支持N个智能体同时避障，O(n)复杂度
///
/// 适用场景：RTS游戏单位移动、人群模拟、自动驾驶车队
/// </summary>
public class ORCAAgent
{
    public Vector2 Position;       // 当前位置
    public Vector2 Velocity;       // 当前速度
    public Vector2 PreferredVel;   // 期望速度（向目标的移动方向）
    public float Radius;           // 碰撞半径
    public float MaxSpeed;         // 最大速度
    public float TimeHorizon;      // 避障预测时间（越大越保守）
    public int AgentId;

    public ORCAAgent(int id, Vector2 position, float radius, float maxSpeed)
    {
        AgentId = id;
        Position = position;
        Radius = radius;
        MaxSpeed = maxSpeed;
        TimeHorizon = 2f;  // 默认预测2秒内的碰撞
        Velocity = Vector2.Zero;
        PreferredVel = Vector2.Zero;
    }

    /// <summary>
    /// 计算新速度（核心ORCA算法）
    /// </summary>
    public Vector2 ComputeNewVelocity(List<ORCAAgent> neighbors, float timeStep)
    {
        // 步骤1：收集所有ORCA半平面约束
        var orcaLines = new List<ORCALine>();

        foreach (var other in neighbors)
        {
            if (other.AgentId == AgentId) continue;

            Vector2 relativePos = other.Position - Position;
            Vector2 relativeVel = Velocity - other.Velocity;
            float combinedRadius = Radius + other.Radius;
            float distSq = relativePos.LengthSquared();

            // 无碰撞风险，跳过
            if (distSq > combinedRadius * combinedRadius + MathUtil.EPS)
            {
                // 速度障碍VO（Velocity Obstacle）
                // VO = { v | ∃ t ∈ [0, tau]: (v - v_other) * t 在 (p_other - p) 的圆盘内 }
                Vector2 w = relativePos;
                float wLenSq = w.LengthSquared();
                float wLen = (float)Math.Sqrt(wLenSq);

                Vector2 unitW = w / wLen;
                Vector2 unitPerpW = new Vector2(-unitW.Y, unitW.X);

                // 计算VO圆锥的两边角度
                float sinTheta = combinedRadius / wLen;
                sinTheta = Math.Clamp(sinTheta, 0, 1);
                float cosTheta = (float)Math.Sqrt(1 - sinTheta * sinTheta);

                // VO的两个边界方向
                Vector2 left = new Vector2(
                    unitW.X * cosTheta - unitPerpW.X * sinTheta,
                    unitW.Y * cosTheta - unitPerpW.Y * sinTheta
                );
                Vector2 right = new Vector2(
                    unitW.X * cosTheta + unitPerpW.X * sinTheta,
                    unitW.Y * cosTheta + unitPerpW.Y * sinTheta
                );

                // 互惠碰撞避免：双方各承担50%的避让责任
                float invTimeHorizon = 1.0f / TimeHorizon;
                Vector2 u = left * (Vector2.Dot(relativeVel, left) - invTimeHorizon * (wLen - combinedRadius));
                Vector2 direction = (u + other.Velocity - Velocity) * 0.5f;

                orcaLines.Add(new ORCALine
                {
                    Point = Velocity + direction,
                    Normal = new Vector2(direction.Y, -direction.X).Normalized()
                });
            }
            else
            {
                // 已经碰撞或即将碰撞，立即推开
                float dist = (float)Math.Sqrt(distSq);
                Vector2 direction = (dist > MathUtil.EPS) ? (relativePos / dist) : new Vector2(0, 1);

                orcaLines.Add(new ORCALine
                {
                    Point = Velocity + direction * (MaxSpeed - Vector2.Dot(Velocity, direction)),
                    Normal = new Vector2(direction.Y, -direction.X).Normalized()
                });
            }
        }

        // 步骤2：线性规划求解最优速度
        Vector2 newVel = LinearProgram2(orcaLines, MaxSpeed, PreferredVel);

        return newVel;
    }

    /// <summary>
    /// 二维线性规划求解
    /// 在半平面约束内找到最接近期望速度的可行速度
    /// </summary>
    private Vector2 LinearProgram2(List<ORCALine> lines, float maxSpeed, Vector2 optVel)
    {
        Vector2 result = optVel;

        // 限制最大速度
        if (result.LengthSquared() > maxSpeed * maxSpeed)
        {
            result = result.Normalized() * maxSpeed;
        }

        // 逐个约束检查并修正
        for (int i = 0; i < lines.Count; i++)
        {
            float distance = Vector2.Dot(lines[i].Normal, result - lines[i].Point);

            // 如果在可行域内，继续
            if (distance <= MathUtil.EPS)
                continue;

            // 投影到半平面边界上
            result = result - lines[i].Normal * distance;

            // 保持在最大速度圆内
            if (result.LengthSquared() > maxSpeed * maxSpeed)
            {
                float len = result.Length();
                result = result / len * maxSpeed;
            }
        }

        return result;
    }

    /// <summary>
    /// 更新位置
    /// </summary>
    public void Update(float timeStep, List<ORCAAgent> neighbors)
    {
        Velocity = ComputeNewVelocity(neighbors, timeStep);
        Position += Velocity * timeStep;
    }
}

/// <summary>
/// ORCA半平面约束
/// </summary>
public struct ORCALine
{
    public Vector2 Point;   // 半平面上一点
    public Vector2 Normal;  // 法向量（指向可行域）
}

/// <summary>
/// ORCA群体管理器
/// </summary>
public class ORCASimulation
{
    private List<ORCAAgent> _agents = new List<ORCAAgent>();
    private float _neighborDist = 5f;  // 邻居搜索半径

    public List<ORCAAgent> Agents => _agents;

    public ORCAAgent AddAgent(Vector2 position, float radius, float maxSpeed)
    {
        var agent = new ORCAAgent(_agents.Count, position, radius, maxSpeed);
        _agents.Add(agent);
        return agent;
    }

    /// <summary>
    /// 更新所有Agent
    /// </summary>
    public void Update(float timeStep)
    {
        // 并行计算所有新速度（避免顺序依赖）
        var newVelocities = new Vector2[_agents.Count];

        for (int i = 0; i < _agents.Count; i++)
        {
            // 空间划分优化：只搜索附近的邻居
            var neighbors = FindNeighbors(_agents[i]);
            newVelocities[i] = _agents[i].ComputeNewVelocity(neighbors, timeStep);
        }

        // 统一应用速度和位置更新
        for (int i = 0; i < _agents.Count; i++)
        {
            _agents[i].Velocity = newVelocities[i];
            _agents[i].Position += newVelocities[i] * timeStep;
        }
    }

    /// <summary>
    /// 查找附近邻居（可替换为网格空间划分优化）
    /// </summary>
    private List<ORCAAgent> FindNeighbors(ORCAAgent agent)
    {
        var neighbors = new List<ORCAAgent>();

        foreach (var other in _agents)
        {
            if (other.AgentId == agent.AgentId) continue;

            float dist = Vector2.Distance(agent.Position, other.Position);
            if (dist < _neighborDist)
                neighbors.Add(other);
        }

        return neighbors;
    }

    /// <summary>
    /// 设置所有Agent的目标点
    /// </summary>
    public void SetTargetForAll(Vector2 target)
    {
        foreach (var agent in _agents)
        {
            Vector2 toTarget = target - agent.Position;
            float dist = toTarget.Length();

            if (dist > MathUtil.EPS)
            {
                agent.PreferredVel = toTarget / dist * Math.Min(dist, agent.MaxSpeed);
            }
            else
            {
                agent.PreferredVel = Vector2.Zero;
            }
        }
    }
}

/// <summary>
/// 数学工具类
/// </summary>
public static class MathUtil
{
    public const float EPS = 0.00001f;

    public static float Clamp(float value, float min, float max)
    {
        return value < min ? min : value > max ? max;
    }
}
