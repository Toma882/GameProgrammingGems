/*
 * 树状数组 Fenwick Tree / Binary Indexed Tree
 * 时间复杂度: O(log n) 单点更新/前缀和, O(n) 初始化
 *
 * 经营游戏核心用途:
 *   - 玩家成就/统计点数累积查询
 *   - 服务器在线人数分时统计
 *   - 经济系统流水求和
 *   - 动态频率统计: 实时玩家分布/经济指数
 */

using System;
using System.Numerics;

namespace GPGems.Core.DataStructures;

/// <summary>
/// 树状数组 (Fenwick Tree) - 支持前缀和与范围查询
/// </summary>
/// <typeparam name="T">数值类型</typeparam>
public class FenwickTree<T>
    where T : struct, INumber<T>
{
    #region 字段与属性

    private readonly T[] _tree;
    public int Size => _tree.Length - 1;

    #endregion

    #region 构造函数

    public FenwickTree(int size)
    {
        if (size <= 0)
            throw new ArgumentException("Size must be positive", nameof(size));

        _tree = new T[size + 1];  // 1-based indexing
    }

    public FenwickTree(T[] data) : this(data.Length)
    {
        for (int i = 0; i < data.Length; i++)
            Update(i + 1, data[i]);
    }

    #endregion

    #region 核心操作

    /// <summary>
    /// 单点更新: 给 index 位置加 delta
    /// 注意: index 是 1-based
    /// </summary>
    public void Update(int index, T delta)
    {
        ValidateIndex(index);

        while (index < _tree.Length)
        {
            _tree[index] += delta;
            index += Lsb(index);
        }
    }

    /// <summary>
    /// 前缀和查询: [1, index] 的和
    /// 注意: index 是 1-based
    /// </summary>
    public T Query(int index)
    {
        ValidateIndex(index);

        T sum = T.Zero;
        while (index > 0)
        {
            sum += _tree[index];
            index -= Lsb(index);
        }
        return sum;
    }

    /// <summary>
    /// 区间求和: [l, r] 的和
    /// 注意: l, r 是 1-based
    /// </summary>
    public T RangeQuery(int l, int r)
    {
        if (l > r) (l, r) = (r, l);
        if (l < 1) l = 1;
        if (r > Size) r = Size;

        return Query(r) - Query(l - 1);
    }

    /// <summary>
    /// 获取单点值
    /// 注意: index 是 1-based
    /// </summary>
    public T GetAt(int index) => RangeQuery(index, index);

    /// <summary>
    /// 设置单点值
    /// 注意: index 是 1-based
    /// </summary>
    public void SetAt(int index, T value)
    {
        T current = GetAt(index);
        Update(index, value - current);
    }

    #endregion

    #region 扩展操作

    /// <summary>
    /// 二分查找第一个前缀和 >= target 的位置
    /// 用于: 概率分布采样（成就解锁条件判断）
    /// </summary>
    public int FindFirstGreaterOrEqual(T target)
    {
        int index = 0;
        int mask = 1 << BitOperations.Log2((uint)Size);
        T sum = T.Zero;

        while (mask > 0)
        {
            int testIndex = index + mask;
            if (testIndex <= Size && sum + _tree[testIndex] < target)
            {
                sum += _tree[testIndex];
                index = testIndex;
            }
            mask >>= 1;
        }

        return index + 1;
    }

    /// <summary>
    /// 0-based 索引器方便访问
    /// </summary>
    public T this[int index]
    {
        get => GetAt(index + 1);
        set => SetAt(index + 1, value);
    }

    #endregion

    #region 辅助方法

    /// <summary>获取最低有效位 (Least Significant Bit)</summary>
    private static int Lsb(int x) => x & -x;

    private void ValidateIndex(int index)
    {
        if (index < 1 || index > Size)
            throw new ArgumentOutOfRangeException(nameof(index),
                $"Index must be between 1 and {Size}");
    }

    #endregion
}

/// <summary>
/// 二维树状数组 - 支持矩形区域求和
/// </summary>
/// <typeparam name="T">数值类型</typeparam>
public class FenwickTree2D<T>
    where T : struct, INumber<T>
{
    private readonly T[,] _tree;
    public int Width { get; }
    public int Height { get; }

    public FenwickTree2D(int width, int height)
    {
        Width = width;
        Height = height;
        _tree = new T[height + 1, width + 1];
    }

    /// <summary>
    /// 单点更新 (1-based)
    /// </summary>
    public void Update(int x, int y, T delta)
    {
        for (int i = y; i <= Height; i += Lsb(i))
            for (int j = x; j <= Width; j += Lsb(j))
                _tree[i, j] += delta;
    }

    /// <summary>
    /// 前缀和查询: (1,1) 到 (x,y) 的和
    /// </summary>
    public T Query(int x, int y)
    {
        T sum = T.Zero;
        for (int i = y; i > 0; i -= Lsb(i))
            for (int j = x; j > 0; j -= Lsb(j))
                sum += _tree[i, j];
        return sum;
    }

    /// <summary>
    /// 矩形区域求和: (x1,y1) 到 (x2,y2)
    /// </summary>
    public T RangeQuery(int x1, int y1, int x2, int y2)
    {
        if (x1 > x2) (x1, x2) = (x2, x1);
        if (y1 > y2) (y1, y2) = (y2, y1);

        return Query(x2, y2)
             - Query(x1 - 1, y2)
             - Query(x2, y1 - 1)
             + Query(x1 - 1, y1 - 1);
    }

    private static int Lsb(int x) => x & -x;
}
