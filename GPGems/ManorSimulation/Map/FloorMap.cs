using System;
using System.Collections.Generic;
using GPGems.Core.DataStructures;

namespace GPGems.ManorSimulation.Map;

/// <summary>
/// 单楼层地图管理层
/// 负责：单个楼层的占位管理、碰撞检测、对象查�?/// 支持附件扩展，遵循核心对�?+ 附件扩展微内核架�?/// </summary>
public class FloorMap
{
    #region 基础属�?
    /// <summary>楼层索引</summary>
    public int FloorIndex { get; }

    /// <summary>地图宽度（格子数�?/summary>
    public int Width { get; }

    /// <summary>地图高度（格子数�?/summary>
    public int Height { get; }

    #endregion

    #region 核心数据

    /// <summary>占位位图 - true=已占�?/summary>
    private readonly BitMap2D _occupancy;

    /// <summary>稀疏对象ID字典 - key=(x,y), value=对象ID�?=空（未存储）</summary>
    /// <remarks>稀疏优化：100x100 空地图从 40KB 降至 ~1KB�?0% 格子为空时节�?70%+ 内存</remarks>
    private readonly Dictionary<(int x, int y), int> _objectGrid;

    /// <summary>已放置对象字�?/summary>
    private readonly Dictionary<int, PlacedMapObject> _objects;

    private int _nextObjectId = 1;

    #endregion

    #region 附件系统

    private readonly Dictionary<Type, object> _attachments = new();

    /// <summary>
    /// 挂载附件
    /// </summary>
    public void Attach<T>(T attachment) where T : class
    {
        _attachments[typeof(T)] = attachment;
    }

    /// <summary>
    /// 获取附件
    /// </summary>
    public T? GetAttachment<T>() where T : class
    {
        return _attachments.TryGetValue(typeof(T), out var a) ? (T)a : null;
    }

    /// <summary>
    /// 移除附件
    /// </summary>
    public bool Detach<T>() where T : class
    {
        return _attachments.Remove(typeof(T));
    }

    #endregion

    #region 构造函�?
    public FloorMap(int floorIndex, int width, int height)
    {
        if (width <= 0) throw new ArgumentException("Width must be positive", nameof(width));
        if (height <= 0) throw new ArgumentException("Height must be positive", nameof(height));

        FloorIndex = floorIndex;
        Width = width;
        Height = height;

        _occupancy = new BitMap2D(width, height);
        _objectGrid = new Dictionary<(int x, int y), int>();
        _objects = new Dictionary<int, PlacedMapObject>();
    }

    #endregion

    #region 基础查询

    /// <summary>
    /// 检查坐标是否在有效范围�?    /// </summary>
    public bool IsInBounds(int x, int y)
    {
        return x >= 0 && x < Width && y >= 0 && y < Height;
    }

    /// <summary>
    /// 检查位置是否已占用
    /// </summary>
    public bool IsOccupied(int x, int y)
    {
        return IsInBounds(x, y) && _occupancy[x, y];
    }

    /// <summary>
    /// 获取指定位置的对象ID
    /// </summary>
    public int GetObjectIdAt(int x, int y)
    {
        if (!IsInBounds(x, y)) return 0;
        return _objectGrid.TryGetValue((x, y), out var id) ? id : 0;
    }

    /// <summary>
    /// 获取指定位置的对�?    /// </summary>
    public PlacedMapObject? GetObjectAt(int x, int y)
    {
        int id = GetObjectIdAt(x, y);
        return id > 0 ? _objects.GetValueOrDefault(id) : null;
    }

    /// <summary>
    /// 通过ID获取对象
    /// </summary>
    public PlacedMapObject? GetObjectById(int objectId)
    {
        return _objects.GetValueOrDefault(objectId);
    }

    #endregion

    #region 放置检�?
    /// <summary>
    /// 检查Footprint是否可以放置在指定位�?    /// </summary>
    public PlacementResult CanPlace(IFootprint footprint, int anchorX, int anchorY)
    {
        if (!IsInBounds(anchorX, anchorY))
            return PlacementResult.OutOfBounds;

        var bounds = footprint.GetBounds();
        int worldMinX = anchorX + bounds.minX;
        int worldMinY = anchorY + bounds.minY;
        int worldMaxX = anchorX + bounds.maxX;
        int worldMaxY = anchorY + bounds.maxY;

        // 检查边界
        if (worldMinX < 0 || worldMinY < 0 || worldMaxX >= Width || worldMaxY >= Height)
            return PlacementResult.OutOfBounds;

        // 检查碰撞
        foreach (var (dx, dy) in footprint.EnumerateOccupiedCells())
        {
            int worldX = anchorX + dx;
            int worldY = anchorY + dy;

            if (_occupancy[worldX, worldY])
                return PlacementResult.Collision;
        }

        return PlacementResult.Valid;
    }

    #endregion

    #region 放置/移除

    /// <summary>
    /// 放置对象
    /// </summary>
    /// <returns>对象唯一ID，失败返�?</returns>
    public int PlaceObject(IFootprint footprint, int anchorX, int anchorY, object? userData = null)
    {
        if (CanPlace(footprint, anchorX, anchorY) != PlacementResult.Valid)
            return 0;

        int objectId = _nextObjectId++;
        var placed = new PlacedMapObject(objectId, FloorIndex, footprint, anchorX, anchorY, userData);

        _objects[objectId] = placed;

        // 更新网格
        foreach (var (dx, dy) in footprint.EnumerateOccupiedCells())
        {
            int worldX = anchorX + dx;
            int worldY = anchorY + dy;
            _occupancy.Set(worldX, worldY, true);
            _objectGrid[(worldX, worldY)] = objectId;
        }

        return objectId;
    }

    /// <summary>
    /// 移除对象
    /// </summary>
    public bool RemoveObject(int objectId)
    {
        if (!_objects.TryGetValue(objectId, out var placed))
            return false;

        // 清除网格
        foreach (var (dx, dy) in placed.Footprint.EnumerateOccupiedCells())
        {
            int worldX = placed.AnchorX + dx;
            int worldY = placed.AnchorY + dy;
            _occupancy.Set(worldX, worldY, false);
            _objectGrid.Remove((worldX, worldY));
        }

        _objects.Remove(objectId);
        return true;
    }

    #endregion

    #region 对象查询

    /// <summary>
    /// 获取本楼层所有对�?    /// </summary>
    public IReadOnlyCollection<PlacedMapObject> GetAllObjects() => _objects.Values;

    /// <summary>
    /// 统计本楼层对象数�?    /// </summary>
    public int CountObjects() => _objects.Count;

    #endregion

    #region 空位查找

    /// <summary>
    /// 查找 w × h 的空矩形区域（位运算优化版）
    /// </summary>
    public (int x, int y) FindEmptyRect(int rectWidth, int rectHeight)
    {
        return _occupancy.FindEmptyRect(rectWidth, rectHeight);
    }

    /// <summary>
    /// 围绕参考点查找最近的连续空位（BFS 搜索�?    /// 保证找到的空位与参考点距离最近，自然形成连续成片的布局
    /// </summary>
    public (int x, int y) FindContinuousEmptyRect(int refX, int refY, int rectWidth, int rectHeight, int maxSearchRadius = 0)
    {
        return _occupancy.FindContinuousEmptyRect(refX, refY, rectWidth, rectHeight, maxSearchRadius);
    }

    /// <summary>
    /// 在指定行中查找第一个连�?width 个空位的起始位置
    /// </summary>
    public int FindContinuousEmptyInRow(int y, int width)
    {
        return _occupancy.FindContinuousEmptyInRow(y, width);
    }

    #endregion

    #region 清空

    /// <summary>
    /// 清空本楼�?    /// </summary>
    public void Clear()
    {
        _occupancy.Clear();
        _objectGrid.Clear();
        _objects.Clear();
    }

    #endregion
}

/// <summary>
/// 已放置在地图上的对象
/// </summary>
public class PlacedMapObject
{
    /// <summary>唯一ID</summary>
    public int Id { get; }

    /// <summary>所在楼�?/summary>
    public int FloorIndex { get; }

    /// <summary>占位定义</summary>
    public IFootprint Footprint { get; }

    /// <summary>锚点X坐标</summary>
    public int AnchorX { get; }

    /// <summary>锚点Y坐标</summary>
    public int AnchorY { get; }

    /// <summary>自定义数据（上层业务使用�?/summary>
    public object? UserData { get; set; }

    public PlacedMapObject(int id, int floorIndex, IFootprint footprint, int anchorX, int anchorY, object? userData = null)
    {
        Id = id;
        FloorIndex = floorIndex;
        Footprint = footprint;
        AnchorX = anchorX;
        AnchorY = anchorY;
        UserData = userData;
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
    /// 检查点是否在对象范围内
    /// </summary>
    public bool ContainsPoint(int worldX, int worldY)
    {
        var bounds = GetWorldBounds();
        if (worldX < bounds.minX || worldX > bounds.maxX ||
            worldY < bounds.minY || worldY > bounds.maxY)
            return false;

        var (offsetX, offsetY) = Footprint.GetAnchorOffset();
        int localX = worldX - AnchorX + offsetX;
        int localY = worldY - AnchorY + offsetY;
        return Footprint.IsOccupied(localX, localY);
    }
}

/// <summary>
/// 放置结果
/// </summary>
public enum PlacementResult
{
    Valid = 0,
    OutOfBounds = 1,
    Collision = 2,
}
