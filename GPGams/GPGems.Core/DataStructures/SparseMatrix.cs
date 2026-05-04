/*
 * 稀疏矩阵 Sparse Matrix
 * 时间复杂度: O(1) 单格读写（平均）, O(k) 遍历, k=非零元素数
 * 空间复杂度: O(k), k=非零元素数（远小于 O(nm) 稠密矩阵）
 *
 * 经营游戏核心用途:
 *   - 超大地图建筑密度: 10000×10000 地图只存已建筑位置
 *   - 玩家探索度记录: 百万级格子只记录已探索位置
 *   - 资源分布数据: 矿物/植物等稀有资源位置
 *   - 影响叠加矩阵: 光环/噪音等稀疏分布效果
 */

using System;
using System.Collections;
using System.Collections.Generic;

namespace GPGems.Core.DataStructures;

/// <summary>
/// 稀疏矩阵 - 基于字典的坐标到值的映射
// * 仅存储非默认值元素，极大节省超大矩阵内存
/// </summary>
/// <typeparam name="T">值类型</typeparam>
public class SparseMatrix<T> : IEnumerable<(int x, int y, T value)>
{
    #region 内部结构

    /// <summary>
    /// 坐标编码：将 (x, y) 编码为 64 位整数
    /// </summary>
    private static long EncodeKey(int x, int y)
    {
        return ((long)x << 32) | (uint)y;
    }

    /// <summary>
    /// 解码坐标
    /// </summary>
    private static (int x, int y) DecodeKey(long key)
    {
        return ((int)(key >> 32), (int)(key & 0xFFFFFFFF));
    }

    #endregion

    #region 字段与属性

    private readonly Dictionary<long, T> _data;
    private readonly T _defaultValue;

    /// <summary>非默认值元素数量</summary>
    public int Count => _data.Count;

    /// <summary>矩阵是否为空（全为默认值）</summary>
    public bool IsEmpty => _data.Count == 0;

    /// <summary>默认值</summary>
    public T DefaultValue => _defaultValue;

    #endregion

    #region 构造函数

    public SparseMatrix()
    {
        _data = new Dictionary<long, T>();
        _defaultValue = default!;
    }

    public SparseMatrix(T defaultValue)
    {
        _data = new Dictionary<long, T>();
        _defaultValue = defaultValue;
    }

    public SparseMatrix(int initialCapacity)
    {
        _data = new Dictionary<long, T>(initialCapacity);
        _defaultValue = default!;
    }

    #endregion

    #region 核心操作

    /// <summary>
    /// 获取或设置指定位置的值
    /// </summary>
    public T this[int x, int y]
    {
        get
        {
            long key = EncodeKey(x, y);
            return _data.TryGetValue(key, out var value) ? value : _defaultValue;
        }
        set
        {
            long key = EncodeKey(x, y);
            if (EqualityComparer<T>.Default.Equals(value, _defaultValue))
            {
                _data.Remove(key);
            }
            else
            {
                _data[key] = value;
            }
        }
    }

    /// <summary>
    /// 检查指定位置是否有非默认值
    /// </summary>
    public bool HasValue(int x, int y)
    {
        return _data.ContainsKey(EncodeKey(x, y));
    }

    /// <summary>
    /// 尝试获取值
    /// </summary>
    public bool TryGetValue(int x, int y, out T value)
    {
        bool result = _data.TryGetValue(EncodeKey(x, y), out value!);
        if (!result)
            value = _defaultValue;
        return result;
    }

    /// <summary>
    /// 清除指定位置（设为默认值）
    /// </summary>
    public bool ClearAt(int x, int y)
    {
        return _data.Remove(EncodeKey(x, y));
    }

    /// <summary>
    /// 清空整个矩阵
    /// </summary>
    public void Clear()
    {
        _data.Clear();
    }

    #endregion

    #region 区域操作

    /// <summary>
    /// 设置矩形区域内的值
    /// </summary>
    public void FillRect(int minX, int minY, int maxX, int maxY, T value)
    {
        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                this[x, y] = value;
            }
        }
    }

    /// <summary>
    /// 清除矩形区域
    /// </summary>
    public void ClearRect(int minX, int minY, int maxX, int maxY)
    {
        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                ClearAt(x, y);
            }
        }
    }

    /// <summary>
    /// 获取指定区域内的所有非默认值元素
    /// </summary>
    public List<(int x, int y, T value)> GetRegion(int minX, int minY, int maxX, int maxY)
    {
        var result = new List<(int, int, T)>();
        foreach (var kvp in _data)
        {
            var (x, y) = DecodeKey(kvp.Key);
            if (x >= minX && x <= maxX && y >= minY && y <= maxY)
            {
                result.Add((x, y, kvp.Value));
            }
        }
        return result;
    }

    /// <summary>
    /// 统计指定区域内的非默认值元素数量
    /// </summary>
    public int CountInRegion(int minX, int minY, int maxX, int maxY)
    {
        int count = 0;
        foreach (var kvp in _data)
        {
            var (x, y) = DecodeKey(kvp.Key);
            if (x >= minX && x <= maxX && y >= minY && y <= maxY)
            {
                count++;
            }
        }
        return count;
    }

    #endregion

    #region 邻近查询

    /// <summary>
    /// 获取 4 邻域的非默认值元素
    /// </summary>
    public List<(int x, int y, T value)> Get4Neighbors(int x, int y)
    {
        var result = new List<(int, int, T)>(4);

        TryAddNeighbor(x, y - 1, result);  // 上
        TryAddNeighbor(x + 1, y, result);  // 右
        TryAddNeighbor(x, y + 1, result);  // 下
        TryAddNeighbor(x - 1, y, result);  // 左

        return result;
    }

    /// <summary>
    /// 获取 8 邻域的非默认值元素
    /// </summary>
    public List<(int x, int y, T value)> Get8Neighbors(int x, int y)
    {
        var result = new List<(int, int, T)>(8);

        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0) continue;
                TryAddNeighbor(x + dx, y + dy, result);
            }
        }

        return result;
    }

    private void TryAddNeighbor(int x, int y, List<(int x, int y, T value)> result)
    {
        if (TryGetValue(x, y, out var value) &&
            !EqualityComparer<T>.Default.Equals(value, _defaultValue))
        {
            result.Add((x, y, value));
        }
    }

    /// <summary>
    /// 获取指定半径范围内的所有非默认值元素
    /// </summary>
    public List<(int x, int y, T value)> GetInRadius(int centerX, int centerY, int radius)
    {
        var result = new List<(int, int, T)>();
        int radiusSq = radius * radius;

        foreach (var kvp in _data)
        {
            var (x, y) = DecodeKey(kvp.Key);
            int dx = x - centerX;
            int dy = y - centerY;
            if (dx * dx + dy * dy <= radiusSq)
            {
                result.Add((x, y, kvp.Value));
            }
        }

        return result;
    }

    #endregion

    #region 边界与统计

    /// <summary>
    /// 获取当前矩阵的边界范围
    /// </summary>
    /// <returns>(minX, minY, maxX, maxY)，如无元素则返回 (0,0,0,0)</returns>
    public (int minX, int minY, int maxX, int maxY) GetBounds()
    {
        if (_data.Count == 0)
            return (0, 0, 0, 0);

        int minX = int.MaxValue, minY = int.MaxValue;
        int maxX = int.MinValue, maxY = int.MinValue;

        foreach (var key in _data.Keys)
        {
            var (x, y) = DecodeKey(key);
            minX = global::System.Math.Min(minX, x);
            minY = global::System.Math.Min(minY, y);
            maxX = global::System.Math.Max(maxX, x);
            maxY = global::System.Math.Max(maxY, y);
        }

        return (minX, minY, maxX, maxY);
    }

    /// <summary>
    /// 获取所有元素坐标列表
    /// </summary>
    public List<(int x, int y)> GetAllPositions()
    {
        var result = new List<(int, int)>(_data.Count);
        foreach (var key in _data.Keys)
        {
            result.Add(DecodeKey(key));
        }
        return result;
    }

    /// <summary>
    /// 获取所有值列表
    /// </summary>
    public List<T> GetAllValues()
    {
        return new List<T>(_data.Values);
    }

    #endregion

    #region 合并操作

    /// <summary>
    /// 合并另一个稀疏矩阵（值相加）
    /// </summary>
    public void MergeAdd(SparseMatrix<T> other)
    {
        if (typeof(T) == typeof(int) || typeof(T) == typeof(float) ||
            typeof(T) == typeof(double) || typeof(T) == typeof(long))
        {
            foreach (var kvp in other._data)
            {
                var (x, y) = DecodeKey(kvp.Key);
                dynamic current = this[x, y];
                dynamic add = kvp.Value;
                this[x, y] = current + add;
            }
        }
        else
        {
            throw new InvalidOperationException("MergeAdd requires numeric type T");
        }
    }

    /// <summary>
    /// 与另一个稀疏矩阵求交集
    /// </summary>
    public void Intersect(SparseMatrix<T> other)
    {
        var keysToRemove = new List<long>();
        foreach (var key in _data.Keys)
        {
            if (!other._data.ContainsKey(key))
            {
                keysToRemove.Add(key);
            }
        }
        foreach (var key in keysToRemove)
        {
            _data.Remove(key);
        }
    }

    #endregion

    #region IEnumerable 实现

    public IEnumerator<(int x, int y, T value)> GetEnumerator()
    {
        foreach (var kvp in _data)
        {
            var (x, y) = DecodeKey(kvp.Key);
            yield return (x, y, kvp.Value);
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    #endregion
}
