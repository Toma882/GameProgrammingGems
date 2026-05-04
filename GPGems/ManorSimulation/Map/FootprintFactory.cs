using System;
using System.Collections.Generic;

namespace GPGems.ManorSimulation.Map;

/// <summary>
/// 占位享元工厂
///
/// 享元模式：相同形状的建筑共享同一�?Footprint 实例
///
/// 设计目的�?/// 1. 节省内存�?000 �?2x2 农田只需�?1 �?Footprint 实例
/// 2. 配置驱动：从配置表读取占位形状，自动创建/复用
/// 3. 统一管理：所有占位形状集中管理，便于调试和优�?/// </summary>
public class FootprintFactory
{
    /// <summary>
    /// 享元缓存：key = 配置名称，value = Footprint 实例
    /// </summary>
    private readonly Dictionary<string, IFootprint> _footprintCache = new();

    /// <summary>
    /// 全局单例（推荐方式）
    /// </summary>
    public static FootprintFactory Instance { get; } = new();

    /// <summary>
    /// 从配置创建或复用 Footprint（享元模式核心方法）
    /// </summary>
    /// <param name="configName">配置名称（享�?key�?/param>
    /// <param name="configCreator">首次创建时的配置逻辑</param>
    public IFootprint GetOrCreate(string configName, Func<FootprintConfig> configCreator)
    {
        if (string.IsNullOrEmpty(configName))
            throw new ArgumentException("Config name cannot be empty", nameof(configName));

        // 享元核心：已存在则直接复用
        if (_footprintCache.TryGetValue(configName, out var existing))
            return existing;

        // 首次创建
        var config = configCreator();
        var footprint = CreateFromConfig(config);
        _footprintCache[configName] = footprint;
        return footprint;
    }

    /// <summary>
    /// 从配置创�?Footprint（支持多种预设形状）
    /// </summary>
    private IFootprint CreateFromConfig(FootprintConfig config)
    {
        return config.ShapeType switch
        {
            FootprintShapeType.Rectangle => new RectangleFootprint(config),
            FootprintShapeType.Cross => new CrossFootprint(config),
            FootprintShapeType.LShape => new LShapeFootprint(config),
            FootprintShapeType.Custom => new CustomFootprint(config),
            _ => throw new NotSupportedException($"Unknown shape type: {config.ShapeType}")
        };
    }

    /// <summary>
    /// 便捷方法：创建矩形占位（最常用�?    /// </summary>
    public IFootprint GetRectangle(string configName, int width, int height, bool blocksMovement = true)
    {
        return GetOrCreate(configName, () => new FootprintConfig
        {
            Name = configName,
            ShapeType = FootprintShapeType.Rectangle,
            Width = width,
            Height = height,
            BlocksMovement = blocksMovement
        });
    }

    /// <summary>
    /// 清除缓存（场景切换时使用�?    /// </summary>
    public void ClearCache()
    {
        _footprintCache.Clear();
    }

    /// <summary>
    /// 获取缓存统计（调试用�?    /// </summary>
    public (int count, int totalMemory) GetCacheStats()
    {
        return (_footprintCache.Count, 0);
    }
}

#region 配置和形状类�?
/// <summary>
/// 占位形状类型
/// </summary>
public enum FootprintShapeType
{
    /// <summary>矩形（默认）</summary>
    Rectangle,
    /// <summary>十字形（如装饰物�?/summary>
    Cross,
    /// <summary>L 形（如角落建筑）</summary>
    LShape,
    /// <summary>自定义形�?/summary>
    Custom
}

/// <summary>
/// 占位配置
/// 从配置表读取后传给工�?/// </summary>
public class FootprintConfig
{
    /// <summary>配置名称（享�?key�?/summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>对象类型标识</summary>
    public string ObjectType { get; set; } = "building";

    /// <summary>形状类型</summary>
    public FootprintShapeType ShapeType { get; set; }

    /// <summary>宽度（格子数�?/summary>
    public int Width { get; set; }

    /// <summary>高度（格子数�?/summary>
    public int Height { get; set; }

    /// <summary>是否阻挡通行</summary>
    public bool BlocksMovement { get; set; } = true;

    /// <summary>锚点偏移（相对于左上角）</summary>
    public (int x, int y) AnchorOffset { get; set; }

    /// <summary>自定义形状的掩码（ShapeType = Custom 时使用）</summary>
    public bool[,]? CustomMask { get; set; }
}

#endregion

#region 具体 Footprint 实现（享元对象）

/// <summary>
/// 矩形占位（享元对象，不可变）
/// </summary>
public class RectangleFootprint : IFootprint
{
    private readonly FootprintConfig _config;

    public string Name => _config.Name;
    public string ObjectType => _config.ObjectType;
    public int Width => _config.Width;
    public int Height => _config.Height;
    public bool BlocksMovement => _config.BlocksMovement;

    public RectangleFootprint(FootprintConfig config)
    {
        _config = config;
    }

    public (int minX, int minY, int maxX, int maxY) GetBounds()
    {
        var (ox, oy) = GetAnchorOffset();
        return (-ox, -oy, Width - ox - 1, Height - oy - 1);
    }

    public (int offsetX, int offsetY) GetAnchorOffset()
    {
        return _config.AnchorOffset;
    }

    public bool IsOccupied(int localX, int localY)
    {
        var (ox, oy) = GetAnchorOffset();
        var x = localX + ox;
        var y = localY + oy;
        return x >= 0 && x < Width && y >= 0 && y < Height;
    }

    public IEnumerable<(int dx, int dy)> EnumerateOccupiedCells()
    {
        var (ox, oy) = GetAnchorOffset();
        for (var y = 0; y < Height; y++)
        for (var x = 0; x < Width; x++)
            yield return (x - ox, y - oy);
    }
}

/// <summary>
/// 十字形占位（享元对象，不可变�?/// </summary>
public class CrossFootprint : IFootprint
{
    private readonly FootprintConfig _config;

    public string Name => _config.Name;
    public string ObjectType => _config.ObjectType;
    public int Width => _config.Width;
    public int Height => _config.Height;
    public bool BlocksMovement => _config.BlocksMovement;

    public CrossFootprint(FootprintConfig config)
    {
        _config = config;
    }

    public (int minX, int minY, int maxX, int maxY) GetBounds()
    {
        var (ox, oy) = GetAnchorOffset();
        return (-ox, -oy, Width - ox - 1, Height - oy - 1);
    }

    public (int offsetX, int offsetY) GetAnchorOffset()
    {
        return _config.AnchorOffset;
    }

    public bool IsOccupied(int localX, int localY)
    {
        var centerX = Width / 2;
        var centerY = Height / 2;
        var (ox, oy) = GetAnchorOffset();
        var x = localX + ox;
        var y = localY + oy;

        // 十字形：同一行或同一列
        return x == centerX || y == centerY;
    }

    public IEnumerable<(int dx, int dy)> EnumerateOccupiedCells()
    {
        var centerX = Width / 2;
        var centerY = Height / 2;
        var (ox, oy) = GetAnchorOffset();

        // 横线
        for (var x = 0; x < Width; x++)
            yield return (x - ox, centerY - oy);

        // 竖线（跳过中心避免重复）
        for (var y = 0; y < Height; y++)
            if (y != centerY)
                yield return (centerX - ox, y - oy);
    }
}

/// <summary>
/// L 形占位（享元对象，不可变�?/// </summary>
public class LShapeFootprint : IFootprint
{
    private readonly FootprintConfig _config;

    public string Name => _config.Name;
    public string ObjectType => _config.ObjectType;
    public int Width => _config.Width;
    public int Height => _config.Height;
    public bool BlocksMovement => _config.BlocksMovement;

    public LShapeFootprint(FootprintConfig config)
    {
        _config = config;
    }

    public (int minX, int minY, int maxX, int maxY) GetBounds()
    {
        var (ox, oy) = GetAnchorOffset();
        return (-ox, -oy, Width - ox - 1, Height - oy - 1);
    }

    public (int offsetX, int offsetY) GetAnchorOffset()
    {
        return _config.AnchorOffset;
    }

    public bool IsOccupied(int localX, int localY)
    {
        var (ox, oy) = GetAnchorOffset();
        var x = localX + ox;
        var y = localY + oy;

        // L 形：底边全部 + 右边全部
        return y == 0 || x == Width - 1;
    }

    public IEnumerable<(int dx, int dy)> EnumerateOccupiedCells()
    {
        var (ox, oy) = GetAnchorOffset();

        // 底边
        for (var x = 0; x < Width; x++)
            yield return (x - ox, 0 - oy);

        // 右边（跳过角落避免重复）
        for (var y = 1; y < Height; y++)
            yield return (Width - 1 - ox, y - oy);
    }
}

/// <summary>
/// 自定义占位（享元对象，不可变�?/// </summary>
public class CustomFootprint : IFootprint
{
    private readonly FootprintConfig _config;
    private readonly bool[,] _mask;

    public string Name => _config.Name;
    public string ObjectType => _config.ObjectType;
    public int Width => _config.Width;
    public int Height => _config.Height;
    public bool BlocksMovement => _config.BlocksMovement;

    public CustomFootprint(FootprintConfig config)
    {
        _config = config;
        _mask = config.CustomMask ?? new bool[config.Width, config.Height];
    }

    public (int minX, int minY, int maxX, int maxY) GetBounds()
    {
        var (ox, oy) = GetAnchorOffset();
        return (-ox, -oy, Width - ox - 1, Height - oy - 1);
    }

    public (int offsetX, int offsetY) GetAnchorOffset()
    {
        return _config.AnchorOffset;
    }

    public bool IsOccupied(int localX, int localY)
    {
        var (ox, oy) = GetAnchorOffset();
        var x = localX + ox;
        var y = localY + oy;

        if (x < 0 || x >= Width || y < 0 || y >= Height)
            return false;

        return _mask[x, y];
    }

    public IEnumerable<(int dx, int dy)> EnumerateOccupiedCells()
    {
        var (ox, oy) = GetAnchorOffset();
        for (var y = 0; y < Height; y++)
        for (var x = 0; x < Width; x++)
            if (_mask[x, y])
                yield return (x - ox, y - oy);
    }
}

#endregion
