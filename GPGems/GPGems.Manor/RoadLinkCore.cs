/*
 * 道路自动连接核心 RoadLinkCore
 * 时间复杂度: O(1) 单格更新, O(k) k=受影响邻居数
 *
 * 经营游戏核心用途:
 *   - 放置道路时自动选择正确的Sprite（直道/弯道/十字路口）
 *   - 地形过渡自动连接（沙地/草地/石路）
 *   - 4邻接级联更新（影响周围4格重新计算连接状态）
 *   - 支持半格/斜向道路（可选）
 *
 * 16种道路组合:
 *   0000 = 孤立（无连接）
 *   0001 = 上
 *   0010 = 右
 *   0100 = 下
 *   1000 = 左
 *   ... 以此类推 ...
 *   1111 = 四向全通（十字路口）
 */

using System;
using System.Collections.Generic;

namespace GPGems.Manor;

/// <summary>
/// 道路连接方向位掩码
/// </summary>
[Flags]
public enum RoadDirection : byte
{
    None = 0,
    Up = 1 << 0,      // 0001
    Right = 1 << 1,   // 0010
    Down = 1 << 2,    // 0100
    Left = 1 << 3,    // 1000
    All = Up | Right | Down | Left  // 1111
}

/// <summary>
/// 道路类型枚举
/// </summary>
public enum RoadType : byte
{
    Dirt = 0,      // 土路
    Stone = 1,     // 石路
    Brick = 2,     // 砖路
    Wood = 3,      // 木板路
    Water = 4,     // 水渠/桥
    Rail = 5,      // 铁轨
}

/// <summary>
/// 道路瓦片数据
/// </summary>
public readonly struct RoadTile
{
    public RoadType Type { get; init; }
    public RoadDirection Connections { get; init; }
    public byte Variant { get; init; }  // 变体（随机装饰用）

    public RoadTile(RoadType type, RoadDirection connections, byte variant = 0)
    {
        Type = type;
        Connections = connections;
        Variant = variant;
    }

    public override string ToString() => $"{Type} [{Connections}] v{Variant}";
}

/// <summary>
/// 道路自动连接核心
/// 管理道路网格的连接状态与Sprite选择
/// </summary>
public class RoadLinkCore
{
    #region 字段与属性

    private readonly int _width;
    private readonly int _height;
    private readonly RoadTile?[,] _grid;
    private readonly Random _random = new();

    public int Width => _width;
    public int Height => _height;

    /// <summary>道路放置事件回调</summary>
    public event Action<int, int, RoadTile>? OnRoadPlaced;

    /// <summary>道路移除事件回调</summary>
    public event Action<int, int>? OnRoadRemoved;

    #endregion

    #region 构造函数

    public RoadLinkCore(int width, int height)
    {
        _width = width;
        _height = height;
        _grid = new RoadTile?[height, width];
    }

    #endregion

    #region 放置/移除道路

    /// <summary>
    /// 放置道路并自动更新连接状态
    /// </summary>
    public void PlaceRoad(int x, int y, RoadType type = RoadType.Stone)
    {
        ValidateBounds(x, y);

        // 设置新道路（先无连接，后面统一计算）
        _grid[y, x] = new RoadTile(type, RoadDirection.None, (byte)_random.Next(4));

        // 级联更新：自己 + 4邻接
        UpdateTileAndNeighbors(x, y);
    }

    /// <summary>
    /// 移除道路
    /// </summary>
    public void RemoveRoad(int x, int y)
    {
        ValidateBounds(x, y);

        if (!_grid[y, x].HasValue)
            return;

        _grid[y, x] = null;

        // 级联更新邻居
        UpdateTileAndNeighbors(x, y);

        OnRoadRemoved?.Invoke(x, y);
    }

    /// <summary>
    /// 批量放置矩形道路区域
    /// </summary>
    public void PlaceRoadRect(int minX, int minY, int maxX, int maxY, RoadType type = RoadType.Stone)
    {
        // 先全量设置
        for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
                _grid[y, x] = new RoadTile(type, RoadDirection.None, (byte)_random.Next(4));

        // 然后全量更新连接状态
        for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
                UpdateTileConnections(x, y);

        // 最后更新外围邻居
        for (int y = minY - 1; y <= maxY + 1; y++)
            for (int x = minX - 1; x <= maxX + 1; x++)
                if (x >= 0 && x < _width && y >= 0 && y < _height)
                    if ((x == minX - 1 || x == maxX + 1 || y == minY - 1 || y == maxY + 1))
                        UpdateTileConnections(x, y);
    }

    #endregion

    #region 连接状态计算

    /// <summary>
    /// 更新单个格子及其4邻接的连接状态
    /// </summary>
    private void UpdateTileAndNeighbors(int x, int y)
    {
        // 更新自己
        UpdateTileConnections(x, y);

        // 更新4邻接
        if (y > 0) UpdateTileConnections(x, y - 1);
        if (x < _width - 1) UpdateTileConnections(x + 1, y);
        if (y < _height - 1) UpdateTileConnections(x, y + 1);
        if (x > 0) UpdateTileConnections(x - 1, y);
    }

    /// <summary>
    /// 更新单个格子的连接状态
    /// </summary>
    private void UpdateTileConnections(int x, int y)
    {
        var tile = _grid[y, x];
        if (!tile.HasValue)
            return;

        RoadDirection connections = RoadDirection.None;

        // 检查4邻接道路是否存在（同类型才能连接）
        if (y > 0 && CanConnect(x, y, x, y - 1))
            connections |= RoadDirection.Up;
        if (x < _width - 1 && CanConnect(x, y, x + 1, y))
            connections |= RoadDirection.Right;
        if (y < _height - 1 && CanConnect(x, y, x, y + 1))
            connections |= RoadDirection.Down;
        if (x > 0 && CanConnect(x, y, x - 1, y))
            connections |= RoadDirection.Left;

        // 更新连接状态
        _grid[y, x] = new RoadTile(tile.Value.Type, connections, tile.Value.Variant);

        // 触发回调
        OnRoadPlaced?.Invoke(x, y, _grid[y, x]!.Value);
    }

    /// <summary>
    /// 检查两个格子能否连接
    /// 规则：都存在，且类型相同（或都允许跨类型连接）
    /// </summary>
    private bool CanConnect(int x1, int y1, int x2, int y2)
    {
        var t1 = _grid[y1, x1];
        var t2 = _grid[y2, x2];

        if (!t1.HasValue || !t2.HasValue)
            return false;

        // 同类型可连接
        if (t1.Value.Type == t2.Value.Type)
            return true;

        // 水渠可以和桥连接
        if ((t1.Value.Type == RoadType.Water && t2.Value.Type == RoadType.Wood) ||
            (t1.Value.Type == RoadType.Wood && t2.Value.Type == RoadType.Water))
            return true;

        // TODO: 可配置连接规则，比如铁路只能连铁路
        return false;
    }

    #endregion

    #region 查询接口

    /// <summary>
    /// 获取指定位置的道路瓦片
    /// </summary>
    public RoadTile? GetTile(int x, int y)
    {
        if (x < 0 || x >= _width || y < 0 || y >= _height)
            return null;
        return _grid[y, x];
    }

    /// <summary>
    /// 获取指定位置的连接掩码（0-15）
    /// 用于直接索引到Sprite数组
    /// </summary>
    public byte GetConnectionMask(int x, int y)
    {
        var tile = GetTile(x, y);
        return tile.HasValue ? (byte)tile.Value.Connections : (byte)0;
    }

    /// <summary>
    /// 检查道路是否连接到指定方向
    /// </summary>
    public bool HasConnection(int x, int y, RoadDirection dir)
    {
        var tile = GetTile(x, y);
        return tile.HasValue && tile.Value.Connections.HasFlag(dir);
    }

    /// <summary>
    /// 检查两个位置是否通过道路连通
    /// BFS 寻路检测
    /// </summary>
    public bool IsConnected(int x1, int y1, int x2, int y2)
    {
        if (!GetTile(x1, y1).HasValue || !GetTile(x2, y2).HasValue)
            return false;

        if (x1 == x2 && y1 == y2)
            return true;

        var visited = new HashSet<(int, int)>();
        var queue = new Queue<(int, int)>();
        queue.Enqueue((x1, y1));

        while (queue.Count > 0)
        {
            var (x, y) = queue.Dequeue();
            if (x == x2 && y == y2)
                return true;

            if (!visited.Add((x, y)))
                continue;

            var tile = GetTile(x, y);
            if (!tile.HasValue)
                continue;

            var conn = tile.Value.Connections;

            if (conn.HasFlag(RoadDirection.Up) && y > 0)
                queue.Enqueue((x, y - 1));
            if (conn.HasFlag(RoadDirection.Right) && x < _width - 1)
                queue.Enqueue((x + 1, y));
            if (conn.HasFlag(RoadDirection.Down) && y < _height - 1)
                queue.Enqueue((x, y + 1));
            if (conn.HasFlag(RoadDirection.Left) && x > 0)
                queue.Enqueue((x - 1, y));
        }

        return false;
    }

    /// <summary>
    /// 获取路网中道路的总数
    /// </summary>
    public int GetRoadCount()
    {
        int count = 0;
        for (int y = 0; y < _height; y++)
            for (int x = 0; x < _width; x++)
                if (_grid[y, x].HasValue)
                    count++;
        return count;
    }

    /// <summary>
    /// 获取指定类型的道路数量
    /// </summary>
    public int GetRoadCountByType(RoadType type)
    {
        int count = 0;
        for (int y = 0; y < _height; y++)
            for (int x = 0; x < _width; x++)
                if (_grid[y, x].HasValue && _grid[y, x]!.Value.Type == type)
                    count++;
        return count;
    }

    /// <summary>
    /// 获取道路连通块大小
    /// </summary>
    public int GetConnectedBlockSize(int startX, int startY)
    {
        if (!GetTile(startX, startY).HasValue)
            return 0;

        var visited = new HashSet<(int, int)>();
        var queue = new Queue<(int, int)>();
        queue.Enqueue((startX, startY));
        int size = 0;

        while (queue.Count > 0)
        {
            var (x, y) = queue.Dequeue();
            if (!visited.Add((x, y)))
                continue;

            size++;
            var tile = GetTile(x, y);
            if (!tile.HasValue)
                continue;

            var conn = tile.Value.Connections;

            if (conn.HasFlag(RoadDirection.Up) && y > 0)
                queue.Enqueue((x, y - 1));
            if (conn.HasFlag(RoadDirection.Right) && x < _width - 1)
                queue.Enqueue((x + 1, y));
            if (conn.HasFlag(RoadDirection.Down) && y < _height - 1)
                queue.Enqueue((x, y + 1));
            if (conn.HasFlag(RoadDirection.Left) && x > 0)
                queue.Enqueue((x - 1, y));
        }

        return size;
    }

    #endregion

    #region 辅助方法

    private void ValidateBounds(int x, int y)
    {
        if (x < 0 || x >= _width)
            throw new ArgumentOutOfRangeException(nameof(x), $"X must be 0-{_width - 1}");
        if (y < 0 || y >= _height)
            throw new ArgumentOutOfRangeException(nameof(y), $"Y must be 0-{_height - 1}");
    }

    /// <summary>
    /// 将连接掩码转换为可读字符串（调试用）
    /// </summary>
    public string MaskToString(byte mask)
    {
        var dirs = new List<string>();
        if ((mask & (int)RoadDirection.Up) != 0) dirs.Add("上");
        if ((mask & (int)RoadDirection.Right) != 0) dirs.Add("右");
        if ((mask & (int)RoadDirection.Down) != 0) dirs.Add("下");
        if ((mask & (int)RoadDirection.Left) != 0) dirs.Add("左");
        return dirs.Count > 0 ? string.Join("+", dirs) : "无连接";
    }

    #endregion

    #region 调试可视化

    /// <summary>
    /// ASCII 可视化路网（调试用）
    /// </summary>
    public string VisualizeASCII()
    {
        var sb = new System.Text.StringBuilder();

        // 标题行
        sb.AppendLine($"路网 {_width}x{_height}:");
        sb.AppendLine(" " + new string('-', _width * 2));

        for (int y = 0; y < _height; y++)
        {
            sb.Append("|");
            for (int x = 0; x < _width; x++)
            {
                var tile = _grid[y, x];
                if (!tile.HasValue)
                {
                    sb.Append("  ");
                    continue;
                }

                // 根据连接状态选择字符
                char c = (byte)tile.Value.Connections switch
                {
                    0b0000 => '○',  // 孤立
                    0b0001 => '║',  // 上
                    0b0010 => '═',  // 右
                    0b0011 => '╚',  // 上+右
                    0b0100 => '║',  // 下
                    0b0101 => '║',  // 上+下（竖向）
                    0b0110 => '╔',  // 右+下
                    0b0111 => '╠',  // 上+右+下
                    0b1000 => '═',  // 左
                    0b1001 => '╝',  // 左+上
                    0b1010 => '═',  // 左+右（横向）
                    0b1011 => '╩',  // 左+上+右
                    0b1100 => '╗',  // 左+下
                    0b1101 => '╣',  // 左+上+下
                    0b1110 => '╦',  // 左+右+下
                    0b1111 => '╬',  // 四向
                    _ => '?'
                };
                sb.Append(c + " ");
            }
            sb.AppendLine("|");
        }

        sb.AppendLine(" " + new string('-', _width * 2));
        return sb.ToString();
    }

    #endregion
}
