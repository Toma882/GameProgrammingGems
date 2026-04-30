/*
 * 2D 位图 BitMap 2D
 * 时间复杂度: O(1) 单格读写, O(w/64) 行扫描
 * 空间复杂度: O(w*h/64) - 每 64 格用一个 ulong
 *
 * 经营游戏核心用途:
 *   - 建筑放置碰撞
 *   - 道路占用标记
 *   - 农田可种植区域
 *   - 地图探索记录
 */

using System;
using System.Numerics;

namespace GPGems.Core.DataStructures;

/// <summary>
/// 2D 位图 - 网格布尔值存储的最优方案
/// 每 64 格打包为一个 ulong，空间效率提升 64 倍
/// </summary>
public class BitMap2D
{
    #region 字段与属性

    private readonly ulong[] _data;
    private readonly int _width;
    private readonly int _height;
    private readonly int _ulongsPerRow;  // 每行需要多少个 ulong

    public int Width => _width;
    public int Height => _height;

    /// <summary>已设置为 true 的位数（延迟计算，调用 CountSetBits 才更新）</summary>
    private int _cachedCount = -1;

    #endregion

    #region 构造函数

    public BitMap2D(int width, int height)
    {
        if (width <= 0) throw new ArgumentException("Width must be positive", nameof(width));
        if (height <= 0) throw new ArgumentException("Height must be positive", nameof(height));

        _width = width;
        _height = height;
        _ulongsPerRow = (width + 63) / 64;  // 向上取整
        _data = new ulong[height * _ulongsPerRow];
    }

    #endregion

    #region 基础位操作

    /// <summary>
    /// 设置指定位置的值
    /// </summary>
    public void Set(int x, int y, bool value)
    {
        ValidateBounds(x, y);

        int index = y * _ulongsPerRow + (x / 64);
        int bit = x % 64;

        if (value)
            _data[index] |= 1UL << bit;
        else
            _data[index] &= ~(1UL << bit);

        _cachedCount = -1;  // 缓存失效
    }

    /// <summary>
    /// 获取指定位置的值
    /// </summary>
    public bool Get(int x, int y)
    {
        ValidateBounds(x, y);

        int index = y * _ulongsPerRow + (x / 64);
        int bit = x % 64;
        return (_data[index] & (1UL << bit)) != 0;
    }

    /// <summary>
    /// 切换指定位置的值（取反）
    /// </summary>
    public void Toggle(int x, int y)
    {
        Set(x, y, !Get(x, y));
    }

    /// <summary>
    /// 索引器快捷访问
    /// </summary>
    public bool this[int x, int y]
    {
        get => Get(x, y);
        set => Set(x, y, value);
    }

    #endregion

    #region 矩形区域操作

    /// <summary>
    /// 设置矩形区域
    /// </summary>
    public void SetRect(int minX, int minY, int maxX, int maxY, bool value)
    {
        ValidateBounds(minX, minY);
        ValidateBounds(maxX, maxY);

        for (int y = minY; y <= maxY; y++)
        {
            int rowBase = y * _ulongsPerRow;
            int x = minX;

            // 处理开头不满一个 ulong 的部分
            while (x <= maxX && x % 64 != 0)
            {
                int bit = x % 64;
                if (value)
                    _data[rowBase + (x / 64)] |= 1UL << bit;
                else
                    _data[rowBase + (x / 64)] &= ~(1UL << bit);
                x++;
            }

            // 处理完整的 ulong
            while (x + 63 <= maxX)
            {
                int idx = rowBase + (x / 64);
                _data[idx] = value ? ulong.MaxValue : 0UL;
                x += 64;
            }

            // 处理末尾不满一个 ulong 的部分
            while (x <= maxX)
            {
                int bit = x % 64;
                if (value)
                    _data[rowBase + (x / 64)] |= 1UL << bit;
                else
                    _data[rowBase + (x / 64)] &= ~(1UL << bit);
                x++;
            }
        }

        _cachedCount = -1;
    }

    /// <summary>
    /// 检查矩形区域是否全空（全为 false）
    /// </summary>
    public bool IsRectEmpty(int minX, int minY, int maxX, int maxY)
    {
        ValidateBounds(minX, minY);
        ValidateBounds(maxX, maxY);

        for (int y = minY; y <= maxY; y++)
        {
            int rowBase = y * _ulongsPerRow;
            int x = minX;

            // 检查开头不满一个 ulong 的部分
            while (x <= maxX && x % 64 != 0)
            {
                if (Get(x, y))
                    return false;
                x++;
            }

            // 检查完整的 ulong
            while (x + 63 <= maxX)
            {
                if (_data[rowBase + (x / 64)] != 0)
                    return false;
                x += 64;
            }

            // 检查末尾不满一个 ulong 的部分
            while (x <= maxX)
            {
                if (Get(x, y))
                    return false;
                x++;
            }
        }

        return true;
    }

    /// <summary>
    /// 检查矩形区域是否全部已占用（全为 true）
    /// </summary>
    public bool IsRectFull(int minX, int minY, int maxX, int maxY)
    {
        ValidateBounds(minX, minY);
        ValidateBounds(maxX, maxY);

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                if (!Get(x, y))
                    return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 统计矩形区域内已设置的位数
    /// </summary>
    public int CountInRect(int minX, int minY, int maxX, int maxY)
    {
        ValidateBounds(minX, minY);
        ValidateBounds(maxX, maxY);

        int count = 0;
        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                if (Get(x, y))
                    count++;
            }
        }

        return count;
    }

    #endregion

    #region 行优化扫描

    /// <summary>
    /// 在指定行中查找第一个连续 width 个空位的起始位置
    /// </summary>
    public int FindContinuousEmptyInRow(int y, int width)
    {
        if (width <= 0 || width > _width) return -1;
        if (y < 0 || y >= _height) return -1;

        int consecutive = 0;
        for (int x = 0; x < _width; x++)
        {
            if (!Get(x, y))
            {
                consecutive++;
                if (consecutive == width)
                    return x - width + 1;
            }
            else
            {
                consecutive = 0;
            }
        }

        return -1;
    }

    /// <summary>
    /// 查找 w × h 的空矩形区域
    /// </summary>
    public (int x, int y) FindEmptyRect(int rectWidth, int rectHeight)
    {
        if (rectWidth <= 0 || rectHeight <= 0) return (-1, -1);
        if (rectWidth > _width || rectHeight > _height) return (-1, -1);

        // 逐行查找起始点，然后验证下方几行
        for (int y = 0; y <= _height - rectHeight; y++)
        {
            int startX = 0;
            while (startX <= _width - rectWidth)
            {
                startX = FindContinuousEmptyInRow(y, startX, rectWidth);
                if (startX == -1) break;

                // 验证下方 rectHeight - 1 行
                bool valid = true;
                for (int dy = 1; dy < rectHeight; dy++)
                {
                    if (!CheckContinuous(y + dy, startX, rectWidth))
                    {
                        valid = false;
                        startX++;
                        break;
                    }
                }

                if (valid)
                    return (startX, y);
            }
        }

        return (-1, -1);
    }

    /// <summary>
    /// 从指定位置开始查找连续空位
    /// </summary>
    private int FindContinuousEmptyInRow(int y, int fromX, int width)
    {
        int consecutive = 0;
        for (int x = fromX; x < _width; x++)
        {
            if (!Get(x, y))
            {
                consecutive++;
                if (consecutive == width)
                    return x - width + 1;
            }
            else
            {
                consecutive = 0;
            }
        }
        return -1;
    }

    /// <summary>
    /// 检查某行从 startX 开始是否连续 width 个空位
    /// </summary>
    private bool CheckContinuous(int y, int startX, int width)
    {
        for (int x = startX; x < startX + width; x++)
        {
            if (Get(x, y))
                return false;
        }
        return true;
    }

    #endregion

    #region 连通性检测

    /// <summary>
    /// 4 邻接连通性检测（BFS）
    /// </summary>
    public bool Is4Connected(int x1, int y1, int x2, int y2)
    {
        if (!Get(x1, y1) || !Get(x2, y2))
            return false;

        if (x1 == x2 && y1 == y2)
            return true;

        var visited = new HashSet<(int, int)>();
        var queue = new Queue<(int, int)>();
        queue.Enqueue((x1, y1));
        visited.Add((x1, y1));

        while (queue.Count > 0)
        {
            var (x, y) = queue.Dequeue();

            // 检查四个方向
            TryVisit(x, y - 1, visited, queue);  // 上
            TryVisit(x + 1, y, visited, queue);  // 右
            TryVisit(x, y + 1, visited, queue);  // 下
            TryVisit(x - 1, y, visited, queue);  // 左

            // 检查是否到达目标
            if (visited.Contains((x2, y2)))
                return true;
        }

        return false;
    }

    private void TryVisit(int x, int y, HashSet<(int, int)> visited, Queue<(int, int)> queue)
    {
        if (x >= 0 && x < _width && y >= 0 && y < _height &&
            Get(x, y) && visited.Add((x, y)))
        {
            queue.Enqueue((x, y));
        }
    }

    /// <summary>
    /// 8 邻接连通性检测
    /// </summary>
    public bool Is8Connected(int x1, int y1, int x2, int y2)
    {
        if (!Get(x1, y1) || !Get(x2, y2))
            return false;

        if (x1 == x2 && y1 == y2)
            return true;

        var visited = new HashSet<(int, int)>();
        var queue = new Queue<(int, int)>();
        queue.Enqueue((x1, y1));
        visited.Add((x1, y1));

        while (queue.Count > 0)
        {
            var (x, y) = queue.Dequeue();

            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int nx = x + dx, ny = y + dy;
                    if (nx >= 0 && nx < _width && ny >= 0 && ny < _height &&
                        Get(nx, ny) && visited.Add((nx, ny)))
                    {
                        if (nx == x2 && ny == y2)
                            return true;
                        queue.Enqueue((nx, ny));
                    }
                }
            }
        }

        return false;
    }

    /// <summary>
    /// 获取连通分量大小
    /// </summary>
    public int GetConnectedComponentSize(int startX, int startY)
    {
        if (!Get(startX, startY))
            return 0;

        int size = 0;
        var visited = new HashSet<(int, int)>();
        var queue = new Queue<(int, int)>();
        queue.Enqueue((startX, startY));
        visited.Add((startX, startY));

        while (queue.Count > 0)
        {
            var (x, y) = queue.Dequeue();
            size++;

            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int nx = x + dx, ny = y + dy;
                    if (nx >= 0 && nx < _width && ny >= 0 && ny < _height &&
                        Get(nx, ny) && visited.Add((nx, ny)))
                    {
                        queue.Enqueue((nx, ny));
                    }
                }
            }
        }

        return size;
    }

    #endregion

    #region 位运算批量操作

    /// <summary>
    /// 与另一个位图做 AND 运算
    /// </summary>
    public void And(BitMap2D other)
    {
        if (other._width != _width || other._height != _height)
            throw new ArgumentException("Bitmaps must have same dimensions");

        for (int i = 0; i < _data.Length; i++)
            _data[i] &= other._data[i];

        _cachedCount = -1;
    }

    /// <summary>
    /// 与另一个位图做 OR 运算
    /// </summary>
    public void Or(BitMap2D other)
    {
        if (other._width != _width || other._height != _height)
            throw new ArgumentException("Bitmaps must have same dimensions");

        for (int i = 0; i < _data.Length; i++)
            _data[i] |= other._data[i];

        _cachedCount = -1;
    }

    /// <summary>
    /// 与另一个位图做 XOR 运算
    /// </summary>
    public void Xor(BitMap2D other)
    {
        if (other._width != _width || other._height != _height)
            throw new ArgumentException("Bitmaps must have same dimensions");

        for (int i = 0; i < _data.Length; i++)
            _data[i] ^= other._data[i];

        _cachedCount = -1;
    }

    /// <summary>
    /// 全局取反
    /// </summary>
    public void Not()
    {
        for (int i = 0; i < _data.Length; i++)
            _data[i] = ~_data[i];

        // 处理最后一个 ulong 的无效位
        int tailBits = _width % 64;
        if (tailBits > 0)
        {
            ulong tailMask = (1UL << tailBits) - 1;
            for (int y = 0; y < _height; y++)
            {
                int idx = y * _ulongsPerRow + _ulongsPerRow - 1;
                _data[idx] &= tailMask;
            }
        }

        _cachedCount = -1;
    }

    /// <summary>
    /// 清空全部
    /// </summary>
    public void Clear()
    {
        Array.Clear(_data, 0, _data.Length);
        _cachedCount = 0;
    }

    /// <summary>
    /// 全部设为 true
    /// </summary>
    public void Fill()
    {
        for (int i = 0; i < _data.Length; i++)
            _data[i] = ulong.MaxValue;

        // 处理最后一个 ulong 的无效位
        int tailBits = _width % 64;
        if (tailBits > 0)
        {
            ulong tailMask = (1UL << tailBits) - 1;
            for (int y = 0; y < _height; y++)
            {
                int idx = y * _ulongsPerRow + _ulongsPerRow - 1;
                _data[idx] &= tailMask;
            }
        }

        _cachedCount = _width * _height;
    }

    #endregion

    #region 统计

    /// <summary>
    /// 统计已设置的位数
    /// </summary>
    public int CountSetBits()
    {
        if (_cachedCount >= 0)
            return _cachedCount;

        int count = 0;
        for (int i = 0; i < _data.Length; i++)
            count += BitOperations.PopCount(_data[i]);

        _cachedCount = count;
        return count;
    }

    /// <summary>
    /// 统计某行已设置的位数
    /// </summary>
    public int CountSetBitsInRow(int y)
    {
        int count = 0;
        int rowBase = y * _ulongsPerRow;
        for (int i = 0; i < _ulongsPerRow; i++)
            count += BitOperations.PopCount(_data[rowBase + i]);
        return count;
    }

    /// <summary>
    /// 检查是否全空
    /// </summary>
    public bool IsAllEmpty() => CountSetBits() == 0;

    /// <summary>
    /// 检查是否全满
    /// </summary>
    public bool IsAllFull() => CountSetBits() == _width * _height;

    #endregion

    #region 辅助方法

    private void ValidateBounds(int x, int y)
    {
        if (x < 0 || x >= _width)
            throw new ArgumentOutOfRangeException(nameof(x), $"X must be 0-{_width - 1}");
        if (y < 0 || y >= _height)
            throw new ArgumentOutOfRangeException(nameof(y), $"Y must be 0-{_height - 1}");
    }

    #endregion
}
