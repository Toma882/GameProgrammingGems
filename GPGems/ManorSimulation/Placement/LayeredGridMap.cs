using System;
using System.Collections.Generic;
using GPGems.AI.Pathfinding;

namespace GPGems.ManorSimulation;

/// <summary>
/// 多层网格地图 - 三层楼实现
/// Floor 0 = 地面层 / 一楼
/// Floor 1 = 二楼
/// Floor 2 = 三楼
/// </summary>
public class LayeredGridMap
{
    public const int MaxFloors = 3;

    /// <summary>地图宽度（格子数）</summary>
    public int Width { get; }

    /// <summary>地图高度（格子数）</summary>
    public int Height { get; }

    /// <summary>基础寻路地图（每楼层独立）</summary>
    public GridMap[] FloorMaps { get; }

    /// <summary>
    /// 三维占位位图：[floor, x, y] = 建筑物ID，0=空地
    /// 每层独立的网格，各自管理自己的碰撞
    /// </summary>
    private readonly int[,,] _grid;

    // 所有已放置的建筑物
    private readonly Dictionary<int, PlacedObject> _objects;
    private int _nextObjectId = 1;

    public LayeredGridMap(int width, int height)
    {
        Width = width;
        Height = height;
        _grid = new int[MaxFloors, width, height];
        FloorMaps = new GridMap[MaxFloors];
        _objects = new Dictionary<int, PlacedObject>();

        for (int f = 0; f < MaxFloors; f++)
        {
            FloorMaps[f] = new GridMap(width, height);
        }
    }

    /// <summary>
    /// 检查坐标是否在有效范围内
    /// </summary>
    public bool IsInBounds(int x, int y, int floor = 0)
    {
        return floor >= 0 && floor < MaxFloors &&
               x >= 0 && x < Width &&
               y >= 0 && y < Height;
    }

    /// <summary>
    /// 获取指定位置的建筑物ID
    /// </summary>
    public int GetObjectIdAt(int x, int y, int floor = 0)
    {
        return IsInBounds(x, y, floor) ? _grid[floor, x, y] : 0;
    }

    /// <summary>
    /// 获取指定位置的建筑物
    /// </summary>
    public PlacedObject? GetObjectAt(int x, int y, int floor = 0)
    {
        int id = GetObjectIdAt(x, y, floor);
        return id > 0 ? _objects.GetValueOrDefault(id) : null;
    }

    /// <summary>
    /// 检查建筑物是否可以放置在指定位置
    /// </summary>
    public PlacementResult CanPlace(BuildingFootprint footprint, int anchorX, int anchorY, int floor = 0)
    {
        if (!IsInBounds(anchorX, anchorY, floor))
            return PlacementResult.OutOfBounds;

        var bounds = footprint.GetBounds();
        int worldMinX = anchorX + bounds.minX;
        int worldMinY = anchorY + bounds.minY;
        int worldMaxX = anchorX + bounds.maxX;
        int worldMaxY = anchorY + bounds.maxY;

        // 检查边界
        if (worldMinX < 0 || worldMinY < 0 || worldMaxX >= Width || worldMaxY >= Height)
            return PlacementResult.OutOfBounds;

        // 检查该楼层的碰撞
        foreach (var (dx, dy) in footprint.EnumerateOccupiedCells())
        {
            int worldX = anchorX + dx;
            int worldY = anchorY + dy;

            if (_grid[floor, worldX, worldY] != 0)
                return PlacementResult.Collision;
        }

        return PlacementResult.Valid;
    }

    /// <summary>
    /// 放置建筑物
    /// </summary>
    /// <returns>建筑物唯一ID，失败返回0</returns>
    public int PlaceObject(BuildingFootprint footprint, int anchorX, int anchorY, int floor = 0)
    {
        if (CanPlace(footprint, anchorX, anchorY, floor) != PlacementResult.Valid)
            return 0;

        int objectId = _nextObjectId++;
        var placed = new PlacedObject(objectId, footprint, anchorX, anchorY, floor);

        // 记录
        _objects[objectId] = placed;

        // 更新网格
        foreach (var (dx, dy) in footprint.EnumerateOccupiedCells())
        {
            int worldX = anchorX + dx;
            int worldY = anchorY + dy;
            _grid[floor, worldX, worldY] = objectId;

            // 如果阻挡通行，同步更新寻路地图
            if (footprint.BlocksMovement)
            {
                var node = FloorMaps[floor].GetNode(worldX, worldY);
                if (node != null)
                {
                    node.IsWalkable = false;
                }
            }
        }

        return objectId;
    }

    /// <summary>
    /// 移除建筑物
    /// </summary>
    public bool RemoveObject(int objectId)
    {
        if (!_objects.TryGetValue(objectId, out var placed))
            return false;

        int floor = placed.Floor;

        // 清除网格
        foreach (var (dx, dy) in placed.Footprint.EnumerateOccupiedCells())
        {
            int worldX = placed.AnchorX + dx;
            int worldY = placed.AnchorY + dy;
            _grid[floor, worldX, worldY] = 0;

            // 恢复寻路地图
            var node = FloorMaps[floor].GetNode(worldX, worldY);
            if (node != null)
            {
                node.IsWalkable = true;
            }
        }

        _objects.Remove(objectId);
        return true;
    }

    /// <summary>
    /// 获取指定楼层的所有建筑物
    /// </summary>
    public List<PlacedObject> GetAllObjectsOnFloor(int floor)
    {
        var result = new List<PlacedObject>();
        if (floor < 0 || floor >= MaxFloors)
            return result;

        var visited = new HashSet<int>();
        for (int x = 0; x < Width; x++)
        for (int y = 0; y < Height; y++)
        {
            int id = _grid[floor, x, y];
            if (id > 0 && visited.Add(id))
                result.Add(_objects[id]);
        }

        return result;
    }

    /// <summary>
    /// 获取所有建筑物
    /// </summary>
    public IReadOnlyCollection<PlacedObject> GetAllObjects() => _objects.Values;

    /// <summary>
    /// 清空所有楼层
    /// </summary>
    public void ClearAll()
    {
        Array.Clear(_grid);
        _objects.Clear();
        for (int f = 0; f < MaxFloors; f++)
        {
            FloorMaps[f].ClearAll();
        }
    }

    /// <summary>
    /// 统计指定楼层的建筑数量
    /// </summary>
    public int CountObjectsOnFloor(int floor)
    {
        int count = 0;
        if (floor < 0 || floor >= MaxFloors)
            return 0;

        var visited = new HashSet<int>();
        for (int x = 0; x < Width; x++)
        for (int y = 0; y < Height; y++)
        {
            int id = _grid[floor, x, y];
            if (id > 0 && visited.Add(id))
                count++;
        }
        return count;
    }
}

/// <summary>
/// 已放置的建筑物
/// </summary>
public class PlacedObject
{
    /// <summary>唯一ID</summary>
    public int Id { get; }

    /// <summary>占位定义</summary>
    public BuildingFootprint Footprint { get; }

    /// <summary>锚点X坐标</summary>
    public int AnchorX { get; }

    /// <summary>锚点Y坐标</summary>
    public int AnchorY { get; }

    /// <summary>所在楼层（0=地面，1=二楼，2=三楼）</summary>
    public int Floor { get; }

    /// <summary>自定义数据（用于上层业务）</summary>
    public object? UserData { get; set; }

    public PlacedObject(int id, BuildingFootprint footprint, int anchorX, int anchorY, int floor)
    {
        Id = id;
        Footprint = footprint;
        AnchorX = anchorX;
        AnchorY = anchorY;
        Floor = floor;
    }

    /// <summary>
    /// 获取世界坐标边界
    /// </summary>
    public (int minX, int minY, int maxX, int maxY) GetWorldBounds()
    {
        var bounds = Footprint.GetBounds();
        return (
            minX: AnchorX + bounds.minX,
            minY: AnchorY + bounds.minY,
            maxX: AnchorX + bounds.maxX,
            maxY: AnchorY + bounds.maxY
        );
    }

    /// <summary>
    /// 检查点是否在建筑物范围内
    /// </summary>
    public bool ContainsPoint(int worldX, int worldY)
    {
        var (offsetX, offsetY) = Footprint.GetAnchorOffset();
        int localX = worldX - AnchorX + offsetX;
        int localY = worldY - AnchorY + offsetY;
        return Footprint.IsOccupied(localX, localY);
    }
}
