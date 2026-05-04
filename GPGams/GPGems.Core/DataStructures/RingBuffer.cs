/*
 * 环形缓冲区 Ring Buffer / Circular Buffer
 * 时间复杂度: O(1) 入队/出队, 无内存重新分配
 * 空间复杂度: O(n) 固定容量
 *
 * 经营游戏核心用途:
 *   - 日志缓存: 游戏运行日志循环记录
 *   - 帧同步队列: 网络数据包缓冲
 *   - 玩家操作历史: 撤销/重做功能
 *   - 性能采样: FPS/内存占用最近 N 帧统计
 */

using System;
using System.Collections;
using System.Collections.Generic;

namespace GPGems.Core.DataStructures;

/// <summary>
/// 环形缓冲区 - 固定容量的循环队列
/// 新元素覆盖旧元素, 适合流式数据处理
/// </summary>
/// <typeparam name="T">元素类型</typeparam>
public class RingBuffer<T> : IEnumerable<T>
{
    #region 字段与属性

    private readonly T[] _buffer;
    private int _head;      // 队头索引（下一个读取位置）
    private int _tail;      // 队尾索引（下一个写入位置）
    private int _count;     // 当前元素数量

    public int Capacity => _buffer.Length;
    public int Count => _count;
    public bool IsEmpty => _count == 0;
    public bool IsFull => _count == _buffer.Length;

    /// <summary>元素入队事件</summary>
    public event Action<T>? OnEnqueue;

    /// <summary>元素被覆盖事件</summary>
    public event Action<T>? OnOverwrite;

    #endregion

    #region 构造函数

    public RingBuffer(int capacity)
    {
        if (capacity <= 0)
            throw new ArgumentException("Capacity must be positive", nameof(capacity));

        _buffer = new T[capacity];
        _head = 0;
        _tail = 0;
        _count = 0;
    }

    #endregion

    #region 核心操作

    /// <summary>
    /// 元素入队（队尾添加）
    /// 如果缓冲区已满, 自动覆盖最旧元素
    /// </summary>
    public void Enqueue(T item)
    {
        if (IsFull)
        {
            // 覆盖队头元素
            OnOverwrite?.Invoke(_buffer[_head]);
            _buffer[_tail] = item;
            _head = (_head + 1) % Capacity;
            _tail = (_tail + 1) % Capacity;
        }
        else
        {
            _buffer[_tail] = item;
            _tail = (_tail + 1) % Capacity;
            _count++;
        }

        OnEnqueue?.Invoke(item);
    }

    /// <summary>
    /// 元素出队（队头移除）
    /// </summary>
    /// <exception cref="InvalidOperationException">缓冲区为空</exception>
    public T Dequeue()
    {
        if (IsEmpty)
            throw new InvalidOperationException("Ring buffer is empty");

        var item = _buffer[_head];
        _buffer[_head] = default!;  // 帮助 GC
        _head = (_head + 1) % Capacity;
        _count--;
        return item;
    }

    /// <summary>
    /// 尝试出队
    /// </summary>
    public bool TryDequeue(out T item)
    {
        if (IsEmpty)
        {
            item = default!;
            return false;
        }

        item = Dequeue();
        return true;
    }

    /// <summary>
    /// 查看队头元素（不移除）
    /// </summary>
    /// <exception cref="InvalidOperationException">缓冲区为空</exception>
    public T Peek()
    {
        if (IsEmpty)
            throw new InvalidOperationException("Ring buffer is empty");

        return _buffer[_head];
    }

    /// <summary>
    /// 查看队尾元素（不移除）
    /// </summary>
    /// <exception cref="InvalidOperationException">缓冲区为空</exception>
    public T PeekLast()
    {
        if (IsEmpty)
            throw new InvalidOperationException("Ring buffer is empty");

        int lastIndex = (_tail - 1 + Capacity) % Capacity;
        return _buffer[lastIndex];
    }

    /// <summary>
    /// 清空缓冲区
    /// </summary>
    public void Clear()
    {
        Array.Clear(_buffer, 0, _buffer.Length);
        _head = 0;
        _tail = 0;
        _count = 0;
    }

    #endregion

    #region 索引访问

    /// <summary>
    /// 获取指定位置的元素（0=最旧, Count-1=最新）
    /// </summary>
    public T this[int index]
    {
        get
        {
            if (index < 0 || index >= _count)
                throw new ArgumentOutOfRangeException(nameof(index));

            int actualIndex = (_head + index) % Capacity;
            return _buffer[actualIndex];
        }
    }

    #endregion

    #region 批量操作

    /// <summary>
    /// 批量入队
    /// </summary>
    public void EnqueueRange(IEnumerable<T> items)
    {
        foreach (var item in items)
        {
            Enqueue(item);
        }
    }

    /// <summary>
    /// 取出所有元素
    /// </summary>
    public List<T> DequeueAll()
    {
        var result = new List<T>(_count);
        while (!IsEmpty)
        {
            result.Add(Dequeue());
        }
        return result;
    }

    /// <summary>
    /// 复制到数组（从旧到新）
    /// </summary>
    public void CopyTo(T[] array, int arrayIndex)
    {
        if (array == null)
            throw new ArgumentNullException(nameof(array));
        if (arrayIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(arrayIndex));
        if (array.Length - arrayIndex < _count)
            throw new ArgumentException("Destination array is too small");

        for (int i = 0; i < _count; i++)
        {
            array[arrayIndex + i] = this[i];
        }
    }

    /// <summary>
    /// 转换为数组（从旧到新）
    /// </summary>
    public T[] ToArray()
    {
        var result = new T[_count];
        CopyTo(result, 0);
        return result;
    }

    #endregion

    #region 统计计算（数值类型快捷方法）

    /// <summary>
    /// 计算平均值（浮点型）
    /// </summary>
    public double Average()
    {
        if (IsEmpty)
            return 0;

        if (typeof(T) == typeof(float) || typeof(T) == typeof(double) ||
            typeof(T) == typeof(int) || typeof(T) == typeof(long))
        {
            double sum = 0;
            for (int i = 0; i < _count; i++)
            {
                sum += Convert.ToDouble(this[i]);
            }
            return sum / _count;
        }

        throw new InvalidOperationException("Type T must be numeric for Average");
    }

    /// <summary>
    /// 计算最大值
    /// </summary>
    public T Max()
    {
        if (IsEmpty)
            throw new InvalidOperationException("Ring buffer is empty");

        var max = this[0];
        var comparer = Comparer<T>.Default;
        for (int i = 1; i < _count; i++)
        {
            if (comparer.Compare(this[i], max) > 0)
            {
                max = this[i];
            }
        }
        return max;
    }

    /// <summary>
    /// 计算最小值
    /// </summary>
    public T Min()
    {
        if (IsEmpty)
            throw new InvalidOperationException("Ring buffer is empty");

        var min = this[0];
        var comparer = Comparer<T>.Default;
        for (int i = 1; i < _count; i++)
        {
            if (comparer.Compare(this[i], min) < 0)
            {
                min = this[i];
            }
        }
        return min;
    }

    #endregion

    #region IEnumerable 实现

    public IEnumerator<T> GetEnumerator()
    {
        for (int i = 0; i < _count; i++)
        {
            yield return this[i];
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    #endregion
}
