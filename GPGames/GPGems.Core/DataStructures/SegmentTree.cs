/*
 * 线段树 Segment Tree
 * 时间复杂度: O(n) 构建, O(log n) 单点更新/区间查询, O(log n) 懒更新
 *
 * 经营游戏核心用途:
 *   - 地图区域伤害查询（范围事件触发）
 *   - 动态事件系统: 区域查询/更新
 *   - 排行榜动态更新
 *   - 区域统计: 各区域经济指数
 */

using System;
using System.Numerics;

namespace GPGems.Core.DataStructures;

/// <summary>
/// 线段树支持的操作类型
/// </summary>
public enum SegmentTreeOp
{
    /// <summary>区间求和</summary>
    Sum,

    /// <summary>区间最小值</summary>
    Min,

    /// <summary>区间最大值</summary>
    Max,

    /// <summary>区间最大公约数</summary>
    Gcd,

    /// <summary>区间逻辑与</summary>
    And,

    /// <summary>区间逻辑或</summary>
    Or,
}

/// <summary>
/// 线段树 - 支持区间查询与懒标记延迟传播
/// </summary>
/// <typeparam name="T">数值类型</typeparam>
public class SegmentTree<T>
    where T : struct, IBinaryInteger<T>
{
    #region 字段

    private readonly T[] _tree;
    private readonly T[] _lazy;  // 懒标记
    private readonly int _size;
    private readonly T _identity;  // 单位元 (Sum=0, Min=+inf, Max=-inf, etc.)
    private readonly Func<T, T, T> _merge;

    #endregion

    #region 构造函数

    public SegmentTree(T[] data, SegmentTreeOp op = SegmentTreeOp.Sum)
    {
        _size = data.Length;
        _tree = new T[_size * 4];
        _lazy = new T[_size * 4];

        _merge = op switch
        {
            SegmentTreeOp.Sum => (a, b) => a + b,
            SegmentTreeOp.Min => (a, b) => T.Min(a, b),
            SegmentTreeOp.Max => (a, b) => T.Max(a, b),
            SegmentTreeOp.Gcd => Gcd,
            SegmentTreeOp.And => (a, b) => a & b,
            SegmentTreeOp.Or => (a, b) => a | b,
            _ => (a, b) => a + b,
        };

        _identity = op switch
        {
            SegmentTreeOp.Sum => T.Zero,
            SegmentTreeOp.Min => T.CreateTruncating(double.PositiveInfinity),
            SegmentTreeOp.Max => T.CreateTruncating(double.NegativeInfinity),
            SegmentTreeOp.Gcd => T.Zero,
            SegmentTreeOp.And => T.CreateTruncating(0xFFFFFFFF),
            SegmentTreeOp.Or => T.Zero,
            _ => T.Zero,
        };

        Build(data, 1, 0, _size - 1);
    }

    #endregion

    #region 核心构建

    private void Build(T[] data, int node, int l, int r)
    {
        if (l == r)
        {
            _tree[node] = data[l];
            return;
        }

        int mid = (l + r) / 2;
        Build(data, node * 2, l, mid);
        Build(data, node * 2 + 1, mid + 1, r);
        _tree[node] = _merge(_tree[node * 2], _tree[node * 2 + 1]);
    }

    #endregion

    #region 区间查询

    /// <summary>
    /// 查询区间 [l, r] 的聚合结果
    /// </summary>
    public T Query(int l, int r)
    {
        if (l > r) return _identity;
        return Query(1, 0, _size - 1, l, r);
    }

    private T Query(int node, int nodeL, int nodeR, int l, int r)
    {
        if (nodeR < l || nodeL > r) return _identity;
        if (l <= nodeL && nodeR <= r) return _tree[node];

        PushDown(node, nodeL, nodeR);

        int mid = (nodeL + nodeR) / 2;
        return _merge(
            Query(node * 2, nodeL, mid, l, r),
            Query(node * 2 + 1, mid + 1, nodeR, l, r));
    }

    #endregion

    #region 单点更新

    /// <summary>
    /// 单点更新: 设置 index 位置的值
    /// </summary>
    public void Set(int index, T value)
    {
        Set(1, 0, _size - 1, index, value);
    }

    private void Set(int node, int nodeL, int nodeR, int idx, T value)
    {
        if (nodeL == nodeR)
        {
            _tree[node] = value;
            return;
        }

        PushDown(node, nodeL, nodeR);

        int mid = (nodeL + nodeR) / 2;
        if (idx <= mid)
            Set(node * 2, nodeL, mid, idx, value);
        else
            Set(node * 2 + 1, mid + 1, nodeR, idx, value);

        _tree[node] = _merge(_tree[node * 2], _tree[node * 2 + 1]);
    }

    #endregion

    #region 区间懒更新

    /// <summary>
    /// 区间加法: 给 [l, r] 的每个元素加 delta
    /// </summary>
    public void RangeAdd(int l, int r, T delta)
    {
        if (l > r) return;
        RangeAdd(1, 0, _size - 1, l, r, delta);
    }

    private void RangeAdd(int node, int nodeL, int nodeR, int l, int r, T delta)
    {
        if (nodeR < l || nodeL > r) return;

        if (l <= nodeL && nodeR <= r)
        {
            _tree[node] += delta * T.CreateTruncating(nodeR - nodeL + 1);
            _lazy[node] += delta;
            return;
        }

        PushDown(node, nodeL, nodeR);

        int mid = (nodeL + nodeR) / 2;
        RangeAdd(node * 2, nodeL, mid, l, r, delta);
        RangeAdd(node * 2 + 1, mid + 1, nodeR, l, r, delta);

        _tree[node] = _merge(_tree[node * 2], _tree[node * 2 + 1]);
    }

    /// <summary>
    /// 懒标记下传
    /// </summary>
    private void PushDown(int node, int nodeL, int nodeR)
    {
        if (nodeL == nodeR) return;
        if (_lazy[node] == T.Zero) return;

        int mid = (nodeL + nodeR) / 2;
        int left = node * 2;
        int right = node * 2 + 1;

        // 下传给左子树
        _tree[left] += _lazy[node] * T.CreateTruncating(mid - nodeL + 1);
        _lazy[left] += _lazy[node];

        // 下传给右子树
        _tree[right] += _lazy[node] * T.CreateTruncating(nodeR - mid);
        _lazy[right] += _lazy[node];

        // 清除当前节点懒标记
        _lazy[node] = T.Zero;
    }

    #endregion

    #region 快捷访问

    public T this[int index]
    {
        get => Query(index, index);
        set => Set(index, value);
    }

    public T this[int l, int r] => Query(l, r);

    #endregion

    #region 辅助方法

    private static T Gcd(T a, T b)
    {
        while (b != T.Zero)
        {
            T temp = b;
            b = a % b;
            a = temp;
        }
        return a;
    }

    #endregion
}

/// <summary>
/// 二维线段树 - 支持矩形区域查询
/// </summary>
public class SegmentTree2D<T>
    where T : struct, IBinaryInteger<T>
{
    private readonly SegmentTree<T>[] _rows;
    private readonly int _width, _height;

    public SegmentTree2D(T[,] data)
    {
        _height = data.GetLength(0);
        _width = data.GetLength(1);
        _rows = new SegmentTree<T>[_height];

        for (int y = 0; y < _height; y++)
        {
            var rowData = new T[_width];
            for (int x = 0; x < _width; x++)
                rowData[x] = data[y, x];
            _rows[y] = new SegmentTree<T>(rowData, SegmentTreeOp.Sum);
        }
    }

    /// <summary>
    /// 矩形区域求和
    /// </summary>
    public T Query(int x1, int y1, int x2, int y2)
    {
        T sum = T.Zero;
        for (int y = y1; y <= y2; y++)
            sum += _rows[y].Query(x1, x2);
        return sum;
    }

    /// <summary>
    /// 单点更新
    /// </summary>
    public void Set(int x, int y, T value)
    {
        _rows[y].Set(x, value);
    }
}
