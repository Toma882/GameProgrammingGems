/*
 * 分块位图 Chunked Bit Map
 * 时间复杂度: O(1) 单格访问, O(chunkSize) 块加载
 * 空间复杂度: O(k * chunkSize), k=已加载区块数（远小于全量 O(n*m)）
 *
 * 经营游戏核心用途:
 *   - 超大地图: 10000x10000 开放式地图
 *   - 无限生成: 程序化无限地图探索
 *   - 区块懒加载: 只加载玩家附近的区域
 *   - 内存优化: 百万格子只占几十 MB
 */

using System;
using System.Collections.Generic;
using System.Numerics;

namespace GPGems.Core.DataStructures;

/// <summary>
/// 单个地图块数据
/// </summary>
public class MapChunk
{
    /// <summary>块 X 坐标（块坐标，不是格子坐标）</summary>
    public int ChunkX { get; }

    /// <summary>块 Y 坐标</summary>
    public int ChunkY { get; }

    /// <summary>块内数据（64x64 = 4096 位 = 512 字节）</summary>
    private readonly ulong[] _data;

    /// <summary>最后访问时间（用于 LRU 淘汰）</summary>
    public long LastAccessTime { get; set; }

    /// <summary>是否有修改（需要保存）</summary>
    public bool IsDirty { get; set; }

    /// <summary>块大小</summary>
    public const int Size = 64;

    /// <summary>每个维度所需的 ulong 数（64 / 64 = 1）</summary>
    private const int UlongsPerDim = Size / 64;

    public MapChunk(int chunkX, int chunkY)
    {
        ChunkX = chunkX;
        ChunkY = chunkY;
        _data = new ulong[Size * UlongsPerDim];  // 64 * 1 = 64 ulongs = 512 bytes
        LastAccessTime = DateTime.UtcNow.Ticks;
        IsDirty = false;
    }

    /// <summary>
    /// 获取块内格子值
    /// </summary>
    public bool Get(int localX, int localY)
    {
        int index = localY * UlongsPerDim + (localX / 64);
        int bit = localX % 64;
        return (_data[index] & (1UL << bit)) != 0;
    }

    /// <summary>
    /// 设置块内格子值
    /// </summary>
    public void Set(int localX, int localY, bool value)
    {
        int index = localY * UlongsPerDim + (localX / 64);
        int bit = localX % 64;

        if (value)
            _data[index] |= 1UL << bit;
        else
            _data[index] &= ~(1UL << bit);

        IsDirty = true;
    }

    /// <summary>
    /// 检查块是否全空
    /// </summary>
    public bool IsEmpty()
    {
        for (int i = 0; i < _data.Length; i++)
        {
            if (_data[i] != 0)
                return false;
        }
        return true;
    }

    /// <summary>
    /// 清空块
    /// </summary>
    public void Clear()
    {
        Array.Clear(_data, 0, _data.Length);
        IsDirty = true;
    }

    /// <summary>
    /// 统计块内已设置的位数
    /// </summary>
    public int CountSetBits()
    {
        int count = 0;
        for (int i = 0; i < _data.Length; i++)
        {
            count += BitOperations.PopCount(_data[i]);
        }
        return count;
    }
}

/// <summary>
/// 分块位图 - 超大地图专用
/// 支持懒加载、LRU 淘汰、无限坐标
/// </summary>
public class ChunkedBitMap
{
    #region 字段与属性

    /// <summary>块缓存 (chunkKey -> MapChunk)</summary>
    private readonly Dictionary<long, MapChunk> _chunks;

    /// <summary>LRU 淘汰缓存</summary>
    private readonly LRUCache<long, MapChunk> _lruCache;

    /// <summary>最大缓存块数</summary>
    private readonly int _maxChunksInMemory;

    /// <summary>块大小（每个维度的格子数）</summary>
    public const int ChunkSize = MapChunk.Size;

    /// <summary>已加载块数</summary>
    public int LoadedChunkCount => _chunks.Count;

    /// <summary>LRU 缓存中的块数</summary>
    public int CachedChunkCount => _lruCache.Count;

    /// <summary>块被卸载事件</summary>
    public event Action<MapChunk>? OnChunkUnloaded;

    /// <summary>块被加载事件</summary>
    public event Action<MapChunk>? OnChunkLoaded;

    #endregion

    #region 构造函数

    /// <summary>
    /// 创建分块位图
    /// </summary>
    /// <param name="maxChunksInMemory">最大内存块数（超过后 LRU 淘汰）</param>
    public ChunkedBitMap(int maxChunksInMemory = 1024)
    {
        _maxChunksInMemory = maxChunksInMemory;
        _chunks = new Dictionary<long, MapChunk>();
        _lruCache = new LRUCache<long, MapChunk>(maxChunksInMemory);

        // LRU 淘汰时触发事件
        _lruCache.OnEvicted += (key, chunk) =>
        {
            _chunks.Remove(key);
            OnChunkUnloaded?.Invoke(chunk);
        };
    }

    #endregion

    #region 坐标转换

    /// <summary>
    /// 将世界坐标转换为块坐标
    /// </summary>
    public static (int chunkX, int chunkY) WorldToChunk(int worldX, int worldY)
    {
        // 处理负数坐标（向下取整）
        int chunkX = worldX >= 0
            ? worldX / ChunkSize
            : (worldX - ChunkSize + 1) / ChunkSize;
        int chunkY = worldY >= 0
            ? worldY / ChunkSize
            : (worldY - ChunkSize + 1) / ChunkSize;

        return (chunkX, chunkY);
    }

    /// <summary>
    /// 将世界坐标转换为块内局部坐标
    /// </summary>
    public static (int localX, int localY) WorldToLocal(int worldX, int worldY)
    {
        int localX = worldX % ChunkSize;
        int localY = worldY % ChunkSize;
        if (localX < 0) localX += ChunkSize;
        if (localY < 0) localY += ChunkSize;
        return (localX, localY);
    }

    /// <summary>
    /// 块坐标编码为 64 位键
    /// </summary>
    private static long EncodeChunkKey(int chunkX, int chunkY)
    {
        // 偏移，使坐标可以是负数
        const long offset = 1 << 31;  // 2^31
        return ((long)(chunkY + offset) << 32) | (uint)(chunkX + offset);
    }

    #endregion

    #region 核心操作

    /// <summary>
    /// 获取指定位置的值
    /// </summary>
    public bool Get(int x, int y)
    {
        var (chunkX, chunkY) = WorldToChunk(x, y);
        var chunk = GetChunk(chunkX, chunkY, loadIfMissing: false);

        if (chunk == null)
            return false;  // 未加载的块视为空

        var (localX, localY) = WorldToLocal(x, y);
        chunk.LastAccessTime = DateTime.UtcNow.Ticks;

        return chunk.Get(localX, localY);
    }

    /// <summary>
    /// 设置指定位置的值
    /// </summary>
    public void Set(int x, int y, bool value)
    {
        var (chunkX, chunkY) = WorldToChunk(x, y);
        var chunk = GetChunk(chunkX, chunkY, loadIfMissing: true)!;

        var (localX, localY) = WorldToLocal(x, y);
        chunk.Set(localX, localY, value);
        chunk.LastAccessTime = DateTime.UtcNow.Ticks;
    }

    /// <summary>
    /// 切换指定位置的值
    /// </summary>
    public void Toggle(int x, int y)
    {
        Set(x, y, !Get(x, y));
    }

    #endregion

    #region 块管理

    /// <summary>
    /// 获取块（如果不存在且需要加载则创建）
    /// </summary>
    public MapChunk? GetChunk(int chunkX, int chunkY, bool loadIfMissing = true)
    {
        long key = EncodeChunkKey(chunkX, chunkY);

        // 先尝试从缓存获取
        if (_chunks.TryGetValue(key, out var chunk))
        {
            // 更新 LRU
            _lruCache.Put(key, chunk);
            return chunk;
        }

        if (!loadIfMissing)
            return null;

        // 创建新块
        chunk = new MapChunk(chunkX, chunkY);
        _chunks[key] = chunk;
        _lruCache.Put(key, chunk);

        OnChunkLoaded?.Invoke(chunk);
        return chunk;
    }

    /// <summary>
    /// 检查块是否已加载
    /// </summary>
    public bool IsChunkLoaded(int chunkX, int chunkY)
    {
        long key = EncodeChunkKey(chunkX, chunkY);
        return _chunks.ContainsKey(key);
    }

    /// <summary>
    /// 卸载块（从内存移除）
    /// </summary>
    public bool UnloadChunk(int chunkX, int chunkY)
    {
        long key = EncodeChunkKey(chunkX, chunkY);
        if (_chunks.TryGetValue(key, out var chunk))
        {
            _chunks.Remove(key);
            // LRU cache 会自动处理
            OnChunkUnloaded?.Invoke(chunk);
            return true;
        }
        return false;
    }

    /// <summary>
    /// 预加载指定范围的块
    /// </summary>
    public void PreloadChunks(int minChunkX, int minChunkY, int maxChunkX, int maxChunkY)
    {
        for (int cy = minChunkY; cy <= maxChunkY; cy++)
        {
            for (int cx = minChunkX; cx <= maxChunkX; cx++)
            {
                GetChunk(cx, cy, loadIfMissing: true);
            }
        }
    }

    /// <summary>
    /// 预加载中心点周围的块
    /// </summary>
    public void PreloadAround(int centerX, int centerY, int radiusChunks)
    {
        var (cx, cy) = WorldToChunk(centerX, centerY);
        PreloadChunks(
            cx - radiusChunks, cy - radiusChunks,
            cx + radiusChunks, cy + radiusChunks);
    }

    /// <summary>
    /// 卸载指定范围外的块
    /// </summary>
    public void UnloadOutside(int minChunkX, int minChunkY, int maxChunkX, int maxChunkY)
    {
        var toRemove = new List<long>();

        foreach (var kvp in _chunks)
        {
            var chunk = kvp.Value;
            if (chunk.ChunkX < minChunkX || chunk.ChunkX > maxChunkX ||
                chunk.ChunkY < minChunkY || chunk.ChunkY > maxChunkY)
            {
                toRemove.Add(kvp.Key);
            }
        }

        foreach (var key in toRemove)
        {
            var chunk = _chunks[key];
            _chunks.Remove(key);
            OnChunkUnloaded?.Invoke(chunk);
        }
    }

    /// <summary>
    /// 获取所有已加载的块
    /// </summary>
    public IEnumerable<MapChunk> GetLoadedChunks()
    {
        return _chunks.Values;
    }

    #endregion

    #region 区域操作

    /// <summary>
    /// 设置矩形区域
    /// </summary>
    public void SetRect(int minX, int minY, int maxX, int maxY, bool value)
    {
        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                Set(x, y, value);
            }
        }
    }

    /// <summary>
    /// 检查矩形区域是否全空
    /// </summary>
    public bool IsRectEmpty(int minX, int minY, int maxX, int maxY)
    {
        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                if (Get(x, y))
                    return false;
            }
        }
        return true;
    }

    /// <summary>
    /// 计算指定范围内的设置位数
    /// </summary>
    public int CountInRect(int minX, int minY, int maxX, int maxY)
    {
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

    #region 连通性检测

    /// <summary>
    /// 4 邻域连通性检测
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

            if (x == x2 && y == y2)
                return true;

            // 4 邻域
            TryEnqueue(x, y - 1);
            TryEnqueue(x + 1, y);
            TryEnqueue(x, y + 1);
            TryEnqueue(x - 1, y);
        }

        void TryEnqueue(int nx, int ny)
        {
            if (Get(nx, ny) && visited.Add((nx, ny)))
                queue.Enqueue((nx, ny));
        }

        return false;
    }

    #endregion

    #region 内存统计

    /// <summary>
    /// 获取内存使用估计（字节）
    /// </summary>
    public long GetMemoryUsage()
    {
        // 每个块约 512 字节数据 + Dictionary 开销
        return _chunks.Count * 600L;
    }

    /// <summary>
    /// 获取统计信息
    /// </summary>
    public (int loadedChunks, int totalSetBits, long memoryBytes) GetStats()
    {
        int setBits = 0;
        foreach (var chunk in _chunks.Values)
        {
            setBits += chunk.CountSetBits();
        }
        return (_chunks.Count, setBits, GetMemoryUsage());
    }

    #endregion

    /// <summary>
    /// 更新视野区域（预加载周围块，卸载远处块）
    /// 这是分块位图的核心功能，不是业务方法
    /// </summary>
    public void UpdateViewArea(int centerX, int centerY, int viewRadiusChunks = 3)
    {
        var (cx, cy) = WorldToChunk(centerX, centerY);

        PreloadAround(centerX, centerY, viewRadiusChunks);

        int unloadRadius = viewRadiusChunks + 2;
        UnloadOutside(
            cx - unloadRadius, cy - unloadRadius,
            cx + unloadRadius, cy + unloadRadius);
    }
}
