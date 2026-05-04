/*
 * 布隆过滤器 Bloom Filter
 * 时间复杂度: O(k) 插入/查询, k=哈希函数数量
 * 空间复杂度: 极低, 无假阴性, 可控假阳性率
 *
 * 经营游戏核心用途:
 *   - 玩家成就快速查询: 百级成就 O(1) 判断
 *   - 物品黑名单: 禁用道具/聊天关键词快速过滤
 *   - 地图探索记录: 千万级格子探索状态
 *   - 重复操作检测: 签到/奖励领取防刷
 */

using System;
using System.Collections;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using Math = global::System.Math;

namespace GPGems.Core.DataStructures;

/// <summary>
/// 布隆过滤器 - 概率型数据结构
/// 特性: 无假阴性（返回 false 一定不存在）, 有假阳性（返回 true 可能不存在）
/// </summary>
public class BloomFilter
{
    #region 字段与属性

    private readonly BitArray _bits;
    private readonly int _hashCount;
    private readonly int _size;

    public int Size => _size;
    public int HashCount => _hashCount;

    /// <summary>已插入元素估计值</summary>
    public int EstimatedCount { get; private set; }

    #endregion

    #region 构造函数

    /// <summary>
    /// 根据期望元素数量和假阳性率创建布隆过滤器
    /// </summary>
    /// <param name="expectedItems">期望插入的元素数量</param>
    /// <param name="falsePositiveRate">期望的假阳性率 (0.0-1.0), 如 0.01 = 1%</param>
    public BloomFilter(int expectedItems, double falsePositiveRate = 0.01)
    {
        if (expectedItems <= 0)
            throw new ArgumentException("Expected items must be positive", nameof(expectedItems));
        if (falsePositiveRate <= 0 || falsePositiveRate >= 1)
            throw new ArgumentException("False positive rate must be between 0 and 1", nameof(falsePositiveRate));

        // 计算最佳位数: m = -n * ln(p) / (ln(2))^2
        _size = (int)global::System.Math.Ceiling(-expectedItems * global::System.Math.Log(falsePositiveRate) / (global::System.Math.Log(2) * global::System.Math.Log(2)));

        // 计算最佳哈希函数数量: k = m/n * ln(2)
        _hashCount = global::System.Math.Max(1, (int)global::System.Math.Round((double)_size / expectedItems * global::System.Math.Log(2)));

        _bits = new BitArray(_size, false);
        EstimatedCount = 0;
    }

    /// <summary>
    /// 直接指定位数和哈希函数数量
    /// </summary>
    public BloomFilter(int size, int hashCount)
    {
        if (size <= 0)
            throw new ArgumentException("Size must be positive", nameof(size));
        if (hashCount <= 0)
            throw new ArgumentException("Hash count must be positive", nameof(hashCount));

        _size = size;
        _hashCount = hashCount;
        _bits = new BitArray(_size, false);
        EstimatedCount = 0;
    }

    #endregion

    #region 核心操作

    /// <summary>
    /// 添加字符串元素
    /// </summary>
    public void Add(string value)
    {
        if (value == null)
            throw new ArgumentNullException(nameof(value));

        var hashes = ComputeHashes(Encoding.UTF8.GetBytes(value));
        foreach (var hash in hashes)
        {
            _bits[hash] = true;
        }
        EstimatedCount++;
    }

    /// <summary>
    /// 添加整数元素
    /// </summary>
    public void Add(int value)
    {
        var hashes = ComputeHashes(BitConverter.GetBytes(value));
        foreach (var hash in hashes)
        {
            _bits[hash] = true;
        }
        EstimatedCount++;
    }

    /// <summary>
    /// 添加长整数元素
    /// </summary>
    public void Add(long value)
    {
        var hashes = ComputeHashes(BitConverter.GetBytes(value));
        foreach (var hash in hashes)
        {
            _bits[hash] = true;
        }
        EstimatedCount++;
    }

    /// <summary>
    /// 添加字节数组元素
    /// </summary>
    public void Add(byte[] value)
    {
        if (value == null)
            throw new ArgumentNullException(nameof(value));

        var hashes = ComputeHashes(value);
        foreach (var hash in hashes)
        {
            _bits[hash] = true;
        }
        EstimatedCount++;
    }

    /// <summary>
    /// 检查字符串是否可能存在
    /// </summary>
    /// <returns>false=一定不存在, true=可能存在</returns>
    public bool Contains(string value)
    {
        if (value == null)
            throw new ArgumentNullException(nameof(value));

        var hashes = ComputeHashes(Encoding.UTF8.GetBytes(value));
        foreach (var hash in hashes)
        {
            if (!_bits[hash])
                return false;
        }
        return true;
    }

    /// <summary>
    /// 检查整数是否可能存在
    /// </summary>
    public bool Contains(int value)
    {
        var hashes = ComputeHashes(BitConverter.GetBytes(value));
        foreach (var hash in hashes)
        {
            if (!_bits[hash])
                return false;
        }
        return true;
    }

    /// <summary>
    /// 检查长整数是否可能存在
    /// </summary>
    public bool Contains(long value)
    {
        var hashes = ComputeHashes(BitConverter.GetBytes(value));
        foreach (var hash in hashes)
        {
            if (!_bits[hash])
                return false;
        }
        return true;
    }

    /// <summary>
    /// 检查字节数组是否可能存在
    /// </summary>
    public bool Contains(byte[] value)
    {
        if (value == null)
            throw new ArgumentNullException(nameof(value));

        var hashes = ComputeHashes(value);
        foreach (var hash in hashes)
        {
            if (!_bits[hash])
                return false;
        }
        return true;
    }

    /// <summary>
    /// 清空过滤器
    /// </summary>
    public void Clear()
    {
        _bits.SetAll(false);
        EstimatedCount = 0;
    }

    #endregion

    #region 哈希函数

    /// <summary>
    /// 使用双重哈希技术生成 k 个哈希值
    /// hash_i = h1 + i * h2
    /// </summary>
    private int[] ComputeHashes(byte[] data)
    {
        // 使用 SHA256 生成两个独立的 64 位哈希值
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(data);

        ulong h1 = BitConverter.ToUInt64(hashBytes, 0);
        ulong h2 = BitConverter.ToUInt64(hashBytes, 8);

        var result = new int[_hashCount];
        for (int i = 0; i < _hashCount; i++)
        {
            // 双重哈希: h1 + i * h2
            ulong combined = h1 + (ulong)i * h2;
            result[i] = (int)(combined % (ulong)_size);
        }

        return result;
    }

    #endregion

    #region 统计与合并

    /// <summary>
    /// 获取当前假阳性率估计
    /// </summary>
    public double GetFalsePositiveRate()
    {
        // p = (1 - e^(-kn/m))^k
        double exponent = -_hashCount * EstimatedCount / (double)_size;
        return global::System.Math.Pow(1 - global::System.Math.Exp(exponent), _hashCount);
    }

    /// <summary>
    /// 获取填充率（已设置位的比例）
    /// </summary>
    public double GetFillRate()
    {
        int setBits = 0;
        for (int i = 0; i < _size; i++)
        {
            if (_bits[i]) setBits++;
        }
        return (double)setBits / _size;
    }

    /// <summary>
    /// 合并另一个布隆过滤器（必须是相同大小和哈希函数数量）
    /// </summary>
    public void Merge(BloomFilter other)
    {
        if (other._size != _size || other._hashCount != _hashCount)
            throw new ArgumentException("Bloom filters must have same size and hash count");

        _bits.Or(other._bits);
        EstimatedCount += other.EstimatedCount;
    }

    /// <summary>
    /// 与另一个布隆过滤器求交集
    /// </summary>
    public void Intersect(BloomFilter other)
    {
        if (other._size != _size || other._hashCount != _hashCount)
            throw new ArgumentException("Bloom filters must have same size and hash count");

        _bits.And(other._bits);
    }

    #endregion

}
