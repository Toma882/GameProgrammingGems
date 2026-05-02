using System;
using System.Collections.Generic;

namespace GPGems.AI.ManorSimulation.Placement;

/// <summary>
/// 建筑物占位定义
/// 描述一个建筑物在网格地图上的形状和占用范围
/// </summary>
public class BuildingFootprint
{
    /// <summary>建筑物类型</summary>
    public BuildingType Type { get; }

    /// <summary>占位宽度（格子数）</summary>
    public int Width { get; }

    /// <summary>占位高度（格子数）</summary>
    public int Height { get; }

    /// <summary>占位形状</summary>
    public FootprintShape Shape { get; set; }

    /// <summary>锚点X偏移（相对于占位左上角，0~1）</summary>
    public float AnchorX { get; set; } = 0.5f;

    /// <summary>锚点Y偏移（相对于占位左上角，0~1）</summary>
    public float AnchorY { get; set; } = 0.5f;

    /// <summary>是否阻挡通行</summary>
    public bool BlocksMovement { get; set; } = true;

    /// <summary>所属图层</summary>
    public MapLayer Layer { get; set; } = MapLayer.Building;

    /// <summary>自定义形状掩码（true=占用，Width x Height大小）</summary>
    public bool[,]? CustomMask { get; set; }

    /// <summary>
    /// 创建矩形占位
    /// </summary>
    public BuildingFootprint(BuildingType type, int width, int height)
    {
        Type = type;
        Width = width;
        Height = height;
        Shape = FootprintShape.Rectangle;
    }

    /// <summary>
    /// 创建圆形占位
    /// </summary>
    public static BuildingFootprint CreateCircle(BuildingType type, int radius)
    {
        int size = radius * 2 + 1;
        var footprint = new BuildingFootprint(type, size, size)
        {
            Shape = FootprintShape.Circle
        };

        // 生成圆形掩码
        footprint.CustomMask = new bool[size, size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            int dx = x - radius;
            int dy = y - radius;
            footprint.CustomMask[x, y] = dx * dx + dy * dy <= radius * radius;
        }

        return footprint;
    }

    /// <summary>
    /// 创建自定义形状占位
    /// </summary>
    public static BuildingFootprint CreateCustom(BuildingType type, int width, int height, bool[,] mask)
    {
        if (mask.GetLength(0) != width || mask.GetLength(1) != height)
            throw new ArgumentException("掩码尺寸不匹配");

        return new BuildingFootprint(type, width, height)
        {
            Shape = FootprintShape.Custom,
            CustomMask = mask
        };
    }

    /// <summary>
    /// 获取锚点在世界坐标中的偏移（格子数）
    /// </summary>
    public (int offsetX, int offsetY) GetAnchorOffset()
    {
        return ((int)(Width * AnchorX), (int)(Height * AnchorY));
    }

    /// <summary>
    /// 检查指定相对位置是否被占用
    /// </summary>
    /// <param name="localX">占位内的相对X坐标</param>
    /// <param name="localY">占位内的相对Y坐标</param>
    public bool IsOccupied(int localX, int localY)
    {
        if (localX < 0 || localX >= Width || localY < 0 || localY >= Height)
            return false;

        return Shape switch
        {
            FootprintShape.Rectangle => true,
            FootprintShape.Circle => CustomMask?[localX, localY] ?? true,
            FootprintShape.Custom => CustomMask?[localX, localY] ?? true,
            _ => true
        };
    }

    /// <summary>
    /// 枚举所有被占用的格子坐标（相对于锚点）
    /// </summary>
    public IEnumerable<(int dx, int dy)> EnumerateOccupiedCells()
    {
        var (anchorOffsetX, anchorOffsetY) = GetAnchorOffset();

        for (int y = 0; y < Height; y++)
        for (int x = 0; x < Width; x++)
        {
            if (IsOccupied(x, y))
            {
                yield return (x - anchorOffsetX, y - anchorOffsetY);
            }
        }
    }

    /// <summary>
    /// 获取占位的边界范围（以锚点为原点的世界坐标）
    /// </summary>
    public (int minX, int minY, int maxX, int maxY) GetBounds()
    {
        var (anchorOffsetX, anchorOffsetY) = GetAnchorOffset();
        return (
            minX: -anchorOffsetX,
            minY: -anchorOffsetY,
            maxX: Width - anchorOffsetX - 1,
            maxY: Height - anchorOffsetY - 1
        );
    }
}
