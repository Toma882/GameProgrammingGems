/*
 * 传播扩散算法 SpreadAlgorithm
 * 时间复杂度: O(r²) r=传播半径, BFS 分层扩散
 *
 * 经营游戏核心用途:
 *   - 灌溉范围: 水井/水渠覆盖范围计算，带衰减
 *   - 瘟疫传播: 疾病从污染源向外扩散，可设置传染率
 *   - 影响力辐射: 建筑光环/美观度影响叠加
 *   - 噪音污染: 工厂噪音衰减计算
 *   - 服务范围: 市场/学校/医院覆盖区域
 *   - 火/水蔓延: 火势/洪水流向模拟
 *
 * 支持特性:
 *   - 多源叠加: 多个来源影响叠加
 *   - 距离衰减: 线性/平方/指数/自定义衰减函数
 *   - 障碍物阻挡: 地形/建筑可阻挡传播
 *   - 传播方向限制: 单向传播
 */

using System;
using System.Collections.Generic;

namespace GPGems.Manor;

/// <summary>
/// 衰减函数类型
/// </summary>
public enum FalloffType
{
    /// <summary>无衰减，范围内全量</summary>
    Constant,

    /// <summary>线性衰减</summary>
    Linear,

    /// <summary>平方衰减（近处远，边缘快）</summary>
    Quadratic,

    /// <summary>平滑阶梯</summary>
    Smoothstep,

    /// <summary>指数衰减</summary>
    Exponential,

    /// <summary>反距离衰减（越近越强）</summary>
    Inverse,
}

/// <summary>
/// 传播类型
/// </summary>
public enum SpreadType
{
    /// <summary>灌溉 - 水系扩散</summary>
    Irrigation,

    /// <summary>瘟疫 - 疾病传染</summary>
    Plague,

    /// <summary>噪音 - 音频扩散</summary>
    Noise,

    /// <summary>美观度 - 装饰/雕像影响</summary>
    Beauty,

    /// <summary>服务范围 - 市场/公共设施</summary>
    Service,

    /// <summary>火灾蔓延</summary>
    Fire,

    /// <summary>洪水淹没</summary>
    Flood,
}

/// <summary>
/// 传播源定义
/// </summary>
public class SpreadSource
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Radius { get; set; }
    public float Strength { get; set; }
    public FalloffType Falloff { get; set; }
    public SpreadType Type { get; set; }
    public bool IgnoreObstacles { get; set; }
}

/// <summary>
/// 传播扩散算法
/// BFS 分层扩散，支持距离衰减与障碍物阻挡
/// </summary>
public class SpreadAlgorithm
{
    #region 字段与属性

    private readonly int _width;
    private readonly int _height;
    private readonly float[,] _field;
    private readonly List<SpreadSource> _sources = new();
    private readonly bool[,] _obstacles;

    public int Width => _width;
    public int Height => _height;

    /// <summary>传播完成事件</summary>
    public event Action<SpreadSource, int, int, float>? OnCellReached;

    #endregion

    #region 构造函数

    public SpreadAlgorithm(int width, int height)
    {
        _width = width;
        _height = height;
        _field = new float[height, width];
        _obstacles = new bool[height, width];
    }

    #endregion

    #region 传播源管理

    /// <summary>
    /// 添加传播源
    /// </summary>
    public void AddSource(int x, int y, int radius, float strength = 1.0f,
                       FalloffType falloff = FalloffType.Linear,
                       SpreadType type = SpreadType.Irrigation,
                       bool ignoreObstacles = false)
    {
        _sources.Add(new SpreadSource
        {
            X = x,
            Y = y,
            Radius = radius,
            Strength = strength,
            Falloff = falloff,
            Type = type,
            IgnoreObstacles = ignoreObstacles
        });
    }

    /// <summary>
    /// 清除所有传播源
    /// </summary>
    public void ClearSources() => _sources.Clear();

    /// <summary>
    /// 移除指定位置的传播源
    /// </summary>
    public void RemoveSourceAt(int x, int y) => _sources.RemoveAll(s => s.X == x && s.Y == y);

    #endregion

    #region 障碍物管理

    /// <summary>
    /// 设置障碍物
    /// </summary>
    public void SetObstacle(int x, int y, bool isObstacle = true)
    {
        if (x >= 0 && x < _width && y >= 0 && y < _height)
            _obstacles[y, x] = isObstacle;
    }

    /// <summary>
    /// 清除所有障碍物
    /// </summary>
    public void ClearObstacles() => Array.Clear(_obstacles);

    #endregion

    #region 核心传播算法

    /// <summary>
    /// 执行所有传播源的扩散计算
    /// </summary>
    public void ComputeAll()
    {
        Array.Clear(_field);

        foreach (var source in _sources)
        {
            ComputeSource(source);
        }
    }

    /// <summary>
    /// 单个传播源 BFS 扩散
    /// </summary>
    private void ComputeSource(SpreadSource source)
    {
        var queue = new Queue<(int x, int y, int dist)>();
        var visited = new bool[_height, _width];

        queue.Enqueue((source.X, source.Y, 0));
        visited[source.Y, source.X] = true;

        while (queue.Count > 0)
        {
            var (x, y, dist) = queue.Dequeue();

            // 计算衰减值
            float value = source.Strength * GetFalloff(dist, source.Radius, source.Falloff);
            _field[y, x] += value;

            OnCellReached?.Invoke(source, x, y, value);

            if (dist >= source.Radius)
                continue;

            // 4邻接扩散
            Span<(int dx, int dy)> dirs = stackalloc (int, int)[]
            {
                (0, -1), (1, 0), (0, 1), (-1, 0)
            };

            foreach (var (dx, dy) in dirs)
            {
                int nx = x + dx;
                int ny = y + dy;

                if (nx < 0 || nx >= _width || ny < 0 || ny >= _height)
                    continue;
                if (visited[ny, nx])
                    continue;
                if (!source.IgnoreObstacles && _obstacles[ny, nx])
                    continue;

                visited[ny, nx] = true;
                queue.Enqueue((nx, ny, dist + 1));
            }
        }
    }

    /// <summary>
    /// 8方向扩散（用于火灾/洪水等更自然的传播）
    /// </summary>
    public void ComputeSource8Way(SpreadSource source)
    {
        var queue = new Queue<(int x, int y, int dist)>();
        var visited = new bool[_height, _width];

        queue.Enqueue((source.X, source.Y, 0));
        visited[source.Y, source.X] = true;

        while (queue.Count > 0)
        {
            var (x, y, dist) = queue.Dequeue();

            float value = source.Strength * GetFalloff(dist, source.Radius, source.Falloff);
            _field[y, x] += value;

            OnCellReached?.Invoke(source, x, y, value);

            if (dist >= source.Radius)
                continue;

            // 8邻接扩散
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;

                    int nx = x + dx;
                    int ny = y + dy;
                    int moveDist = (dx != 0 && dy != 0) ? dist + 1 : dist + 1; // 简化，斜向计为1步

                    if (nx < 0 || nx >= _width || ny < 0 || ny >= _height)
                        continue;
                    if (visited[ny, nx])
                        continue;
                    if (!source.IgnoreObstacles && _obstacles[ny, nx])
                        continue;

                    visited[ny, nx] = true;
                    queue.Enqueue((nx, ny, moveDist));
                }
            }
        }
    }

    #endregion

    #region 衰减函数

    /// <summary>
    /// 根据距离计算衰减系数
    /// </summary>
    public static float GetFalloff(int distance, int radius, FalloffType type)
    {
        if (radius <= 0) return 0f;
        if (distance == 0) return 1f;

        float t = (float)distance / radius;
        if (t >= 1f) return 0f;

        return type switch
        {
            FalloffType.Constant => 1f,
            FalloffType.Linear => 1f - t,
            FalloffType.Quadratic => 1f - t * t,
            FalloffType.Smoothstep => t * t * (3f - 2f * t),
            FalloffType.Exponential => MathF.Exp(-t * 2f),
            FalloffType.Inverse => 1f / (1f + distance),
            _ => 1f - t,
        };
    }

    #endregion

    #region 查询接口

    /// <summary>
    /// 获取指定位置的传播值
    /// </summary>
    public float GetValue(int x, int y)
    {
        if (x < 0 || x >= _width || y < 0 || y >= _height)
            return 0f;
        return _field[y, x];
    }

    /// <summary>
    /// 获取区域平均值
    /// </summary>
    public float GetAverage(int minX, int minY, int maxX, int maxY)
    {
        float sum = 0f;
        int count = 0;
        for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
            {
                sum += GetValue(x, y);
                count++;
            }
        return count > 0 ? sum / count : 0f;
    }

    /// <summary>
    /// 获取全场统计
    /// </summary>
    public (float min, float max, float avg, float sum) GetStatistics()
    {
        float min = float.MaxValue;
        float max = float.MinValue;
        float sum = 0f;
        int count = 0;

        for (int y = 0; y < _height; y++)
        {
            for (int x = 0; x < _width; x++)
            {
                float v = _field[y, x];
                min = Math.Min(min, v);
                max = Math.Max(max, v);
                sum += v;
                count++;
            }
        }

        return (min, max, sum / count, sum);
    }

    /// <summary>
    /// 查找满足阈值的格子数
    /// </summary>
    public int CountAboveThreshold(float threshold)
    {
        int count = 0;
        for (int y = 0; y < _height; y++)
            for (int x = 0; x < _width; x++)
                if (_field[y, x] >= threshold)
                    count++;
        return count;
    }

    /// <summary>
    /// 查找最大值位置
    /// </summary>
    public (int x, int y) FindMaxPosition()
    {
        float max = float.MinValue;
        int maxX = 0, maxY = 0;
        for (int y = 0; y < _height; y++)
            for (int x = 0; x < _width; x++)
                if (_field[y, x] > max)
                {
                    max = _field[y, x];
                    maxX = x;
                    maxY = y;
                }
        return (maxX, maxY);
    }

    #endregion


    #region 高级传播模拟（动态模拟）

    /// <summary>
    /// 动态传播模拟（逐帧传播）
    /// </summary>
    public class SpreadSimulation
    {
        private readonly SpreadAlgorithm _parent;
        private readonly Queue<(int x, int y, float strength)> _wavefront;
        private readonly bool[,] _visited;
        private int _currentStep = 0;
        private readonly int _maxSteps;

        public SpreadSimulation(SpreadAlgorithm parent, int maxSteps = 100)
        {
            _parent = parent;
            _maxSteps = maxSteps;
            _visited = new bool[parent._height, parent._width];
            _wavefront = new Queue<(int, int, float)>();

            // 初始化所有源点
            foreach (var source in parent._sources)
            {
                _wavefront.Enqueue((source.X, source.Y, source.Strength));
                _visited[source.Y, source.X] = true;
            }
        }

        /// <summary>
        /// 推进一帧传播
        /// </summary>
        public bool Step()
        {
            if (_currentStep >= _maxSteps || _wavefront.Count == 0)
                return false;

            int count = _wavefront.Count;
            for (int i = 0; i < count; i++)
            {
                var (x, y, strength) = _wavefront.Dequeue();
                _parent._field[y, x] += strength;

                // 扩散到邻居（强度自然衰减）
                float nextStrength = strength * 0.9f;
                if (nextStrength < 0.01f)
                    continue;

                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        int nx = x + dx, ny = y + dy;
                        if (nx >= 0 && nx < _parent._width && ny >= 0 && ny < _parent._height)
                            if (!_visited[ny, nx] && !_parent._obstacles[ny, nx])
                            {
                                _visited[ny, nx] = true;
                                _wavefront.Enqueue((nx, ny, nextStrength));
                            }
                    }
            }

            _currentStep++;
            return true;
        }
    }

    /// <summary>
    /// 创建动态传播模拟
    /// </summary>
    public SpreadSimulation CreateSimulation(int maxSteps = 100)
        => new(this, maxSteps);

    #endregion

    #region 可视化（调试）

    /// <summary>
    /// ASCII 热力图可视化
    /// </summary>
    public string VisualizeHeatmap(int cellWidth = 2)
    {
        var sb = new System.Text.StringBuilder();
        char[] levels = { ' ', '·', '▪', '▫', '◦', '●', '○', '◆' };

        sb.AppendLine($"传播场 {_width}x{_height}:");

        for (int y = 0; y < _height; y++)
        {
            for (int x = 0; x < _width; x++)
            {
                float v = _field[y, x];
                int level = Math.Clamp((int)(v * 8), 0, 7);
                sb.Append(levels[level]);
                if (cellWidth > 1)
                    sb.Append(' ');
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }

    #endregion
}
