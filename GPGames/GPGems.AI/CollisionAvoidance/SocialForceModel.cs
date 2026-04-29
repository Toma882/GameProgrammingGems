namespace GPGems.AI.CollisionAvoidance;

/// <summary>
/// 社会力模型（Social Force Model）
/// 基于物理学的人群行为模拟算法
///
/// 核心力：
/// 1. 驱动力：向目标移动的力
/// 2. 排斥力：与其他Agent/障碍物保持距离的力
/// 3. 吸引力：与社交伙伴保持接近的力
/// 4. 边界力：墙壁/障碍物的排斥
///
/// 适用场景：顾客排队、人群疏散、社交团体行为、拥挤避让
/// </summary>
public class SocialForceAgent
{
    public Vector2 Position;
    public Vector2 Velocity;
    public Vector2 Target;
    public float Mass;              // 质量
    public float Radius;            // 个人空间半径
    public float DesiredSpeed;      // 期望速度
    public float MaxSpeed;          // 最大速度
    public int AgentId;

    // 社会力参数
    public float A = 2.0f;         // 排斥力强度
    public float B = 0.3f;         // 排斥力作用范围
    public float Tau = 0.5f;       // 反应时间

    public SocialForceAgent(int id, Vector2 position, float radius, float desiredSpeed)
    {
        AgentId = id;
        Position = position;
        Radius = radius;
        DesiredSpeed = desiredSpeed;
        MaxSpeed = desiredSpeed * 1.5f;
        Mass = 80f;  // 人均体重约80kg
        Velocity = Vector2.Zero;
    }

    /// <summary>
    /// 计算合力并更新
    /// </summary>
    public void Update(List<SocialForceAgent> neighbors, List<Obstacle> obstacles, float timeStep)
    {
        // F_total = F_drive + F_agent + F_obstacle + F_social
        Vector2 totalForce = Vector2.Zero;

        // 1. 驱动力：向目标加速
        totalForce += ComputeDrivingForce();

        // 2. Agent之间的排斥力
        foreach (var other in neighbors)
        {
            if (other.AgentId == AgentId) continue;
            totalForce += ComputeAgentRepulsion(other);
        }

        // 3. 障碍物排斥力
        foreach (var obstacle in obstacles)
        {
            totalForce += ComputeObstacleRepulsion(obstacle);
        }

        // 4. 社交吸引力（可选，如朋友、家人团体）
        // totalForce += ComputeSocialAttraction(neighbors);

        // F = ma → a = F/m → v += a*dt
        Vector2 acceleration = totalForce / Mass;
        Velocity += acceleration * timeStep;

        // 限速
        if (Velocity.LengthSquared() > MaxSpeed * MaxSpeed)
        {
            Velocity = Velocity.Normalized() * MaxSpeed;
        }

        // 更新位置
        Position += Velocity * timeStep;
    }

    /// <summary>
    /// 驱动力：向目标加速的力
    /// F_drive = m * (v_desired - v_current) / tau
    /// </summary>
    private Vector2 ComputeDrivingForce()
    {
        Vector2 toTarget = Target - Position;
        float dist = toTarget.Length();

        // 已到达目标，减速
        if (dist < 0.1f)
            return -Velocity * Mass / Tau;

        // 期望速度方向
        Vector2 desiredVel = toTarget / dist * DesiredSpeed;

        return Mass * (desiredVel - Velocity) / Tau;
    }

    /// <summary>
    /// Agent间排斥力（指数衰减模型）
    /// F_rep = A * exp(-d / B) * 法向量
    /// </summary>
    private Vector2 ComputeAgentRepulsion(SocialForceAgent other)
    {
        Vector2 diff = Position - other.Position;
        float dist = diff.Length();

        if (dist < MathUtil.EPS)
            return new Vector2(Random.Shared.NextSingle() - 0.5f, Random.Shared.NextSingle() - 0.5f).Normalized() * A;

        // 心理距离 = 实际距离 - 双方半径之和
        float psychologicalDist = dist - Radius - other.Radius;

        // 指数衰减排斥力
        float forceMagnitude = A * (float)Math.Exp(-psychologicalDist / B);

        return diff / dist * forceMagnitude;
    }

    /// <summary>
    /// 障碍物排斥力
    /// </summary>
    private Vector2 ComputeObstacleRepulsion(Obstacle obstacle)
    {
        // 找到障碍物上距离当前Agent最近的点
        Vector2 closestPoint = obstacle.ClosestPoint(Position);
        Vector2 diff = Position - closestPoint;
        float dist = diff.Length();

        if (dist < MathUtil.EPS)
            return Vector2.Zero;

        // 同Agent排斥力公式
        float forceMagnitude = A * 1.5f * (float)Math.Exp(-dist / (B * 0.5f));

        return diff / dist * forceMagnitude;
    }

    /// <summary>
    /// 社交吸引力（团体成员间保持接近）
    /// </summary>
    private Vector2 ComputeSocialAttraction(List<SocialForceAgent> neighbors)
    {
        // 简化版：对同团体成员产生弱吸引力
        Vector2 force = Vector2.Zero;
        float attractionStrength = 0.5f;
        float optimalDistance = 1.0f;

        foreach (var other in neighbors)
        {
            // if (other.GroupId == GroupId)
            {
                Vector2 diff = other.Position - Position;
                float dist = diff.Length();

                if (dist > optimalDistance * 2)
                {
                    force += diff / dist * attractionStrength;
                }
            }
        }

        return force;
    }
}

/// <summary>
/// 障碍物（线段或矩形）
/// </summary>
public class Obstacle
{
    public Vector2 Start, End;  // 线段障碍
    public Rectangle? Rect;     // 矩形障碍

    public Obstacle(Vector2 start, Vector2 end)
    {
        Start = start;
        End = end;
    }

    public Obstacle(float x, float y, float width, float height)
    {
        Rect = new Rectangle(x, y, width, height);
    }

    /// <summary>
    /// 找到障碍上距离点最近的位置
    /// </summary>
    public Vector2 ClosestPoint(Vector2 point)
    {
        if (Rect.HasValue)
        {
            return Rect.Value.ClosestPoint(point);
        }

        // 线段最近点计算
        Vector2 line = End - Start;
        float lineLenSq = line.LengthSquared();

        if (lineLenSq < MathUtil.EPS)
            return Start;

        // 投影参数 t ∈ [0, 1]
        float t = Vector2.Dot(point - Start, line) / lineLenSq;
        t = MathUtil.Clamp(t, 0, 1);

        return Start + line * t;
    }
}

/// <summary>
/// 矩形障碍物
/// </summary>
public struct Rectangle
{
    public float X, Y, Width, Height;

    public Rectangle(float x, float y, float width, float height)
    {
        X = x; Y = y; Width = width; Height = height;
    }

    public Vector2 ClosestPoint(Vector2 point)
    {
        float cx = MathUtil.Clamp(point.X, X, X + Width);
        float cy = MathUtil.Clamp(point.Y, Y, Y + Height);
        return new Vector2(cx, cy);
    }
}

/// <summary>
/// 社会力群体模拟器
/// </summary>
public class SocialForceSimulation
{
    private List<SocialForceAgent> _agents = new List<SocialForceAgent>();
    private List<Obstacle> _obstacles = new List<Obstacle>();
    private float _neighborRadius = 5f;

    public List<SocialForceAgent> Agents => _agents;
    public List<Obstacle> Obstacles => _obstacles;

    public SocialForceAgent AddAgent(Vector2 position, float radius, float desiredSpeed)
    {
        var agent = new SocialForceAgent(_agents.Count, position, radius, desiredSpeed);
        _agents.Add(agent);
        return agent;
    }

    public void AddWall(float x1, float y1, float x2, float y2)
    {
        _obstacles.Add(new Obstacle(new Vector2(x1, y1), new Vector2(x2, y2)));
    }

    public void AddRectObstacle(float x, float y, float width, float height)
    {
        _obstacles.Add(new Obstacle(x, y, width, height));
    }

    public void Update(float timeStep)
    {
        // 查找邻居
        var neighborMap = new Dictionary<int, List<SocialForceAgent>>();
        foreach (var agent in _agents)
        {
            var neighbors = new List<SocialForceAgent>();
            foreach (var other in _agents)
            {
                if (other.AgentId != agent.AgentId &&
                    Vector2.Distance(agent.Position, other.Position) < _neighborRadius)
                {
                    neighbors.Add(other);
                }
            }
            neighborMap[agent.AgentId] = neighbors;
        }

        // 并行更新所有Agent
        foreach (var agent in _agents)
        {
            agent.Update(neighborMap[agent.AgentId], _obstacles, timeStep);
        }
    }
}
