/*
 * 哈希网格 Hash Grid / Spatial Hash
 * 时间复杂度: O(1) 插入/删除/查询（平均）
 * 空间复杂度: O(n), 但空间利用率极高
 *
 * 经营游戏核心用途:
 *   - 极端稀疏大地图: 百万级格子但只有千级单位
 *   - 玩家分布追踪: 千人在线服务器位置索引
 *   - 投射物碰撞: 子弹/技能快速范围检测
 *   - 动态物体追踪: 单位频繁移动时的索引更新
 */

using System;
using System.Collections.Generic;

namespace GPGems.MathPhysics.Spatial;

/// <summary>
/// 哈希网格 - 空间哈希索引
// * 将 2D 空间划分为格子，每个格子用哈希表存储元素
/// 适合极稀疏、元素频繁移动的场景
/// </summary>
/// <typeparam name="T">元素类型</typeparam>
public class HashGrid<T> where T : class
{
    #region 字段与属性

    private readonly Dictionary<long, List<T>> _grid;
    private readonly Dictionary<T, List<long>> _elementToCells;
    private readonly float _cellSize;
    private readonly float _invCellSize;  // 1 / cellSize（优化除法）

    /// <summary>网格中元素总数</summary>
    public int ElementCount => _elementToCells.Count;

    /// <summary>非空格子数量</summary>
    public int UsedCells => _grid.Count;

    /// <summary>格子大小</summary>
    public float CellSize => _cellSize;

    #endregion

    #region 构造函数

    public HashGrid(float cellSize)
    {
        if (cellSize <= 0)
            throw new ArgumentException("Cell size must be positive", nameof(cellSize));

        _cellSize = cellSize;
        _invCellSize = 1.0f / cellSize;
        _grid = new Dictionary<long, List<T>>();
        _elementToCells = new Dictionary<T, List<long>>();
    }

    #endregion

    #region 坐标编码

    /// <summary>
    /// 将世界坐标转换为格子坐标
    /// </summary>
    private (int cx, int cy) WorldToCell(float x, float y)
    {
        return ((int)Math.Floor(x * _invCellSize), (int)Math.Floor(y * _invCellSize));
    }

    /// <summary>
    /// 将格子坐标编码为 64 位键
    /// </summary>
    private static long EncodeCell(int cx, int cy)
    {
        return ((long)cx << 32) | (uint)cy;
    }

    /// <summary>
    /// 解码格子坐标
    /// </summary>
    private static (int cx, int cy) DecodeCell(long key)
    {
        return ((int)(key >> 32), (int)(key & 0xFFFFFFFF));
    }

    #endregion

    #region 核心操作

    /// <summary>
    /// 插入元素（点元素）
    /// </summary>
    public void Insert(T element, float x, float y)
    {
        if (element == null)
            throw new ArgumentNullException(nameof(element));

        // 先移除旧位置
        Remove(element);

        var (cx, cy) = WorldToCell(x, y);
        long key = EncodeCell(cx, cy);

        // 添加到网格
        if (!_grid.TryGetValue(key, out var cell))
        {
            cell = new List<T>();
            _grid[key] = cell;
        }
        cell.Add(element);

        // 记录元素所在格子
        _elementToCells[element] = new List<long> { key };
    }

    /// <summary>
    /// 插入元素（有边界的元素，可能跨多个格子）
    /// </summary>
    public void Insert(T element, float x, float y, float width, float height)
    {
        if (element == null)
            throw new ArgumentNullException(nameof(element));

        // 先移除旧位置
        Remove(element);

        // 计算元素覆盖的格子范围
        var (minCx, minCy) = WorldToCell(x, y);
        var (maxCx, maxCy) = WorldToCell(x + width, y + height);

        var cells = new List<long>();

        for (int cy = minCy; cy <= maxCy; cy++)
        {
            for (int cx = minCx; cx <= maxCx; cx++)
            {
                long key = EncodeCell(cx, cy);
                cells.Add(key);

                if (!_grid.TryGetValue(key, out var cell))
                {
                    cell = new List<T>();
                    _grid[key] = cell;
                }
                cell.Add(element);
            }
        }

        _elementToCells[element] = cells;
    }

    /// <summary>
    /// 移除元素
    /// </summary>
    public bool Remove(T element)
    {
        if (element == null)
            throw new ArgumentNullException(nameof(element));

        if (!_elementToCells.TryGetValue(element, out var cells))
            return false;

        // 从每个格子中移除
        foreach (var key in cells)
        {
            if (_grid.TryGetValue(key, out var cell))
            {
                cell.Remove(element);
                if (cell.Count == 0)
                {
                    _grid.Remove(key);
                }
            }
        }

        _elementToCells.Remove(element);
        return true;
    }

    /// <summary>
    /// 更新元素位置
    /// </summary>
    public void Update(T element, float newX, float newY)
    {
        Insert(element, newX, newY);
    }

    /// <summary>
    /// 更新带边界的元素位置
    /// </summary>
    public void Update(T element, float newX, float newY, float width, float height)
    {
        Insert(element, newX, newY, width, height);
    }

    /// <summary>
    /// 清空网格
    /// </summary>
    public void Clear()
    {
        _grid.Clear();
        _elementToCells.Clear();
    }

    #endregion

    #region 查询操作

    /// <summary>
    /// 查询指定点所在格子的所有元素
    /// </summary>
    public List<T> QueryPoint(float x, float y)
    {
        var (cx, cy) = WorldToCell(x, y);
        long key = EncodeCell(cx, cy);

        return _grid.TryGetValue(key, out var cell)
            ? new List<T>(cell)
            : new List<T>();
    }

    /// <summary>
    /// 查询矩形范围内的所有元素
    /// </summary>
    public List<T> QueryRect(float minX, float minY, float maxX, float maxY)
    {
        var result = new HashSet<T>();

        var (minCx, minCy) = WorldToCell(minX, minY);
        var (maxCx, maxCy) = WorldToCell(maxX, maxY);

        for (int cy = minCy; cy <= maxCy; cy++)
        {
            for (int cx = minCx; cx <= maxCx; cx++)
            {
                long key = EncodeCell(cx, cy);
                if (_grid.TryGetValue(key, out var cell))
                {
                    foreach (var element in cell)
                    {
                        result.Add(element);
                    }
                }
            }
        }

        return new List<T>(result);
    }

    /// <summary>
    /// 查询圆形范围内的所有元素
    /// </summary>
    public List<T> QueryCircle(float cx, float cy, float radius)
    {
        // 先查矩形范围
        var candidates = QueryRect(cx - radius, cy - radius, cx + radius, cy + radius);

        // 然后精确过滤
        var result = new List<T>();
        float radiusSq = radius * radius;

        foreach (var element in candidates)
        {
            // 假设元素有 X/Y 属性（通过动态分派，实际项目中应该用接口）
            if (element is IQuadTreeElement spatial)
            {
                float dx = spatial.X - cx;
                float dy = spatial.Y - cy;
                if (dx * dx + dy * dy <= radiusSq)
                {
                    result.Add(element);
                }
            }
            else
            {
                result.Add(element);  // 非空间元素直接返回
            }
        }

        return result;
    }

    /// <summary>
    /// 查询目标位置周围 N 圈格子的所有元素
    /// </summary>
    public List<T> QueryNeighbors(float x, float y, int radiusCells = 1)
    {
        var result = new HashSet<T>();
        var (cx, cy) = WorldToCell(x, y);

        for (int dy = -radiusCells; dy <= radiusCells; dy++)
        {
            for (int dx = -radiusCells; dx <= radiusCells; dx++)
            {
                long key = EncodeCell(cx + dx, cy + dy);
                if (_grid.TryGetValue(key, out var cell))
                {
                    foreach (var element in cell)
                    {
                        result.Add(element);
                    }
                }
            }
        }

        return new List<T>(result);
    }

    #endregion

    #region 碰撞检测

    /// <summary>
    /// 查找所有可能与指定元素碰撞的候选元素
    /// </summary>
    public List<T> FindCollisionCandidates(T element)
    {
        if (element == null)
            throw new ArgumentNullException(nameof(element));

        var result = new HashSet<T>();

        if (_elementToCells.TryGetValue(element, out var cells))
        {
            foreach (var key in cells)
            {
                if (_grid.TryGetValue(key, out var cell))
                {
                    foreach (var other in cell)
                    {
                        if (!ReferenceEquals(other, element))
                        {
                            result.Add(other);
                        }
                    }
                }
            }
        }

        return new List<T>(result);
    }

    /// <summary>
    /// 查找所有元素对（可能碰撞的对）
    /// </summary>
    public List<(T, T)> FindAllPairs()
    {
        var pairs = new List<(T, T)>();
        var processed = new HashSet<long>();

        foreach (var kvp in _grid)
        {
            var cell = kvp.Value;
            for (int i = 0; i < cell.Count; i++)
            {
                for (int j = i + 1; j < cell.Count; j++)
                {
                    pairs.Add((cell[i], cell[j]));
                }
            }
        }

        return pairs;
    }

    #endregion

    #region 遍历与统计

    /// <summary>
    /// 获取所有元素
    /// </summary>
    public List<T> GetAllElements()
    {
        return new List<T>(_elementToCells.Keys);
    }

    /// <summary>
    /// 获取所有使用中的格子坐标
    /// </summary>
    public List<(int cx, int cy)> GetUsedCells()
    {
        var result = new List<(int, int)>(_grid.Count);
        foreach (var key in _grid.Keys)
        {
            result.Add(DecodeCell(key));
        }
        return result;
    }

    /// <summary>
    /// 获取统计信息
    /// </summary>
    public (int elementCount, int usedCells, float avgElementsPerCell) GetStats()
    {
        int totalElements = 0;
        foreach (var cell in _grid.Values)
        {
            totalElements += cell.Count;
        }

        float avg = _grid.Count > 0 ? (float)totalElements / _grid.Count : 0;
        return (ElementCount, UsedCells, avg);
    }

    #endregion

}
