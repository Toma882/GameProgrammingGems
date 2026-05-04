using System.Numerics;
using GPGems.Core.Math;

namespace GPGems.AI.Pathfinding;

/// <summary>
/// 流场寻路算法（Flow Field Pathfinding）
/// 适用于大量单位同时向同一目标移动的场景
/// 一次计算，所有单位共享路径，CPU开销与单位数量无关
///
/// 核心思想：
/// 1. 成本场（Cost Field）：每个格子的通行成本
/// 2. 积分场（Integration Field）：到目标的最小代价
/// 3. 流场（Flow Field）：每个格子指向邻居的方向向量
/// </summary>
public class FlowFieldPathfinder
{
    public string Name => "Flow Field";
    public string Description => "流场寻路 - 百级NPC同时移动时性能远超A*，一次计算全图共享";

    private readonly GridMap _map;
    private byte[,] _costField;          // 成本场 0-255
    private float[,] _integrationField;  // 积分场（到目标的累计代价）
    private Vector2[,] _flowField;       // 流场（每个格子的移动方向）

    public byte[,] CostField => _costField;
    public float[,] IntegrationField => _integrationField;
    public Vector2[,] FlowField => _flowField;

    public FlowFieldPathfinder(GridMap map)
    {
        _map = map;
        _costField = new byte[map.Width, map.Height];
        _integrationField = new float[map.Width, map.Height];
        _flowField = new Vector2[map.Width, map.Height];

        // 初始化成本场（从地图障碍物生成）
        InitializeCostField();
    }

    /// <summary>
    /// 初始化成本场
    /// 可行走区域 = 1，障碍物 = 255（不可通行）
    /// </summary>
    private void InitializeCostField()
    {
        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var node = _map.GetNode(x, y);
                _costField[x, y] = node.IsWalkable ? (byte)1 : (byte)255;
            }
        }
    }

    /// <summary>
    /// 设置特定区域的成本（如：沼泽=10，道路=1，建筑=255）
    /// </summary>
    public void SetCost(int x, int y, byte cost)
    {
        if (x >= 0 && x < _map.Width && y >= 0 && y < _map.Height)
            _costField[x, y] = cost;
    }

    /// <summary>
    /// 计算完整流场（目标点为吸引点）
    /// </summary>
    public void CalculateFlowField(int targetX, int targetY)
    {
        // 步骤1：计算积分场（从目标向外扩散）
        CalculateIntegrationField(targetX, targetY);

        // 步骤2：生成流场（每个格子指向代价更低的邻居）
        GenerateFlowField();
    }

    /// <summary>
    /// 计算积分场 - 使用Dijkstra算法从目标点向外扩散
    /// </summary>
    private void CalculateIntegrationField(int targetX, int targetY)
    {
        // 初始化积分场为无穷大
        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                _integrationField[x, y] = float.MaxValue;
            }
        }

        // 目标点代价为0
        _integrationField[targetX, targetY] = 0;

        // 优先队列按代价从小到大处理（Dijkstra）
        var openSet = new PriorityQueue<(int x, int y), float>();
        openSet.Enqueue((targetX, targetY), 0);

        // 8方向邻居偏移
        int[] dx = { -1, 0, 1, -1, 1, -1, 0, 1 };
        int[] dy = { -1, -1, -1, 0, 0, 1, 1, 1 };
        float[] moveCost = { 1.414f, 1f, 1.414f, 1f, 1f, 1.414f, 1f, 1.414f };

        while (openSet.Count > 0)
        {
            var (currentX, currentY) = openSet.Dequeue();
            float currentCost = _integrationField[currentX, currentY];

            for (int i = 0; i < 8; i++)
            {
                int nx = currentX + dx[i];
                int ny = currentY + dy[i];

                if (nx < 0 || nx >= _map.Width || ny < 0 || ny >= _map.Height)
                    continue;

                byte cellCost = _costField[nx, ny];
                if (cellCost == 255) continue; // 障碍物跳过

                float newCost = currentCost + cellCost * moveCost[i];

                if (newCost < _integrationField[nx, ny])
                {
                    _integrationField[nx, ny] = newCost;
                    openSet.Enqueue((nx, ny), newCost);
                }
            }
        }
    }

    /// <summary>
    /// 生成流场 - 每个格子选择代价最低的邻居作为方向
    /// </summary>
    private void GenerateFlowField()
    {
        int[] dx = { -1, 0, 1, -1, 1, -1, 0, 1 };
        int[] dy = { -1, -1, -1, 0, 0, 1, 1, 1 };

        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                // 障碍物无方向
                if (_costField[x, y] == 255)
                {
                    _flowField[x, y] = Vector2.Zero;
                    continue;
                }

                float lowestCost = _integrationField[x, y];
                int bestDx = 0, bestDy = 0;

                // 找到代价最低的邻居
                for (int i = 0; i < 8; i++)
                {
                    int nx = x + dx[i];
                    int ny = y + dy[i];

                    if (nx < 0 || nx >= _map.Width || ny < 0 || ny >= _map.Height)
                        continue;

                    if (_integrationField[nx, ny] < lowestCost)
                    {
                        lowestCost = _integrationField[nx, ny];
                        bestDx = dx[i];
                        bestDy = dy[i];
                    }
                }

                // 归一化方向向量
                _flowField[x, y] = new Vector2(bestDx, bestDy).Normalized();
            }
        }
    }

    /// <summary>
    /// 获取指定位置的移动方向
    /// </summary>
    public Vector2 GetDirection(float worldX, float worldY, float cellSize = 1f)
    {
        int gridX = (int)(worldX / cellSize);
        int gridY = (int)(worldY / cellSize);

        // 边界检查
        if (gridX < 0 || gridX >= _map.Width || gridY < 0 || gridY >= _map.Height)
            return Vector2.Zero;

        return _flowField[gridX, gridY];
    }

    /// <summary>
    /// 添加排斥场（用于动态障碍物避让）
    /// </summary>
    public void AddRepulsionField(int obstacleX, int obstacleY, float radius, float strength)
    {
        int startX = Math.Max(0, (int)(obstacleX - radius));
        int endX = Math.Min(_map.Width - 1, (int)(obstacleX + radius));
        int startY = Math.Max(0, (int)(obstacleY - radius));
        int endY = Math.Min(_map.Height - 1, (int)(obstacleY + radius));

        for (int x = startX; x <= endX; x++)
        {
            for (int y = startY; y <= endY; y++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(obstacleX, obstacleY));
                if (dist < radius)
                {
                    // 距离越近，排斥力越大
                    float repulsion = (1 - dist / radius) * strength;

                    // 修改流场方向：远离障碍物
                    Vector2 away = new Vector2(x - obstacleX, y - obstacleY).Normalized();
                    _flowField[x, y] = (_flowField[x, y] + away * repulsion).Normalized();
                }
            }
        }
    }
}
