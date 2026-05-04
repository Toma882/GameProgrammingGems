using System;
using System.Collections.Generic;
using System.Linq;

namespace GPGems.ManorSimulation.Map;

/// <summary>
/// 多层楼地图管�?/// 负责：楼层集合管理、垂直连接关系管理、跨楼层查询
/// 支持附件扩展，遵循核心对�?+ 附件扩展微内核架�?///
/// 设计原则�?/// - 楼梯/电梯本身是放置在 FloorMap 中的普通对�?/// - VerticalConnection 只存储配对关系，不重复存储建筑数�?/// - 跨楼层查询逻辑作为附件挂载
/// </summary>
public class MultiFloorMap
{
    #region 基础属�?
    /// <summary>单楼层宽度（格子数）</summary>
    public int FloorWidth { get; }

    /// <summary>单楼层高度（格子数）</summary>
    public int FloorHeight { get; }

    /// <summary>当前存在的楼层数</summary>
    public int FloorCount => _floors.Count;

    #endregion

    #region 核心数据

    /// <summary>楼层字典 - [floorIndex, FloorMap]</summary>
    private readonly Dictionary<int, FloorMap> _floors = new();

    /// <summary>垂直连接关系字典</summary>
    private readonly Dictionary<string, VerticalConnection> _connections = new();

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
    public MultiFloorMap(int floorWidth, int floorHeight)
    {
        if (floorWidth <= 0) throw new ArgumentException("Width must be positive", nameof(floorWidth));
        if (floorHeight <= 0) throw new ArgumentException("Height must be positive", nameof(floorHeight));

        FloorWidth = floorWidth;
        FloorHeight = floorHeight;
    }

    #endregion

    #region 楼层管理

    /// <summary>
    /// 检查楼层是否存�?    /// </summary>
    public bool HasFloor(int floorIndex) => _floors.ContainsKey(floorIndex);

    /// <summary>
    /// 获取或创建楼�?    /// </summary>
    public FloorMap GetOrCreateFloor(int floorIndex)
    {
        if (!_floors.TryGetValue(floorIndex, out var floor))
        {
            floor = new FloorMap(floorIndex, FloorWidth, FloorHeight);
            _floors[floorIndex] = floor;
        }
        return floor;
    }

    /// <summary>
    /// 获取楼层（不存在返回null�?    /// </summary>
    public FloorMap? GetFloor(int floorIndex)
    {
        return _floors.TryGetValue(floorIndex, out var floor) ? floor : null;
    }

    /// <summary>
    /// 获取所有楼�?    /// </summary>
    public IEnumerable<FloorMap> GetAllFloors() => _floors.Values;

    /// <summary>
    /// 移除楼层（注意：会移除该楼层所有对象和相关连接�?    /// </summary>
    public bool RemoveFloor(int floorIndex)
    {
        if (!_floors.Remove(floorIndex))
            return false;

        // 移除涉及该楼层的连接
        var toRemove = _connections.Values
            .Where(c => c.Stops.Any(s => s.Floor == floorIndex))
            .Select(c => c.Id)
            .ToList();

        foreach (var id in toRemove)
        {
            _connections.Remove(id);
        }

        return true;
    }

    #endregion

    #region 垂直连接管理

    /// <summary>
    /// 添加垂直连接
    /// </summary>
    public void AddConnection(VerticalConnection connection)
    {
        _connections[connection.Id] = connection;
    }

    /// <summary>
    /// 创建并添加楼梯连接（方便方法�?    /// </summary>
    public VerticalConnection AddStairConnection(string id,
        int floor1, int objectId1, (int x, int y) pos1,
        int floor2, int objectId2, (int x, int y) pos2)
    {
        var conn = new VerticalConnection(id, ConnectionType.Stair);
        conn.AddStop(floor1, objectId1, pos1);
        conn.AddStop(floor2, objectId2, pos2);
        AddConnection(conn);
        return conn;
    }

    /// <summary>
    /// 获取垂直连接
    /// </summary>
    public VerticalConnection? GetConnection(string id)
    {
        return _connections.GetValueOrDefault(id);
    }

    /// <summary>
    /// 获取指定楼层的所有连�?    /// </summary>
    public IEnumerable<VerticalConnection> GetConnectionsOnFloor(int floorIndex)
    {
        return _connections.Values.Where(c => c.GetStop(floorIndex) != null);
    }

    /// <summary>
    /// 查找指定位置的垂直连�?    /// </summary>
    public VerticalConnection? FindConnectionAt(int floorIndex, int x, int y)
    {
        var floor = GetFloor(floorIndex);
        if (floor == null) return null;

        var obj = floor.GetObjectAt(x, y);
        if (obj == null) return null;

        foreach (var conn in _connections.Values)
        {
            if (conn.Stops.Any(s => s.ObjectId == obj.Id))
                return conn;
        }

        return null;
    }

    /// <summary>
    /// 移除垂直连接
    /// </summary>
    public bool RemoveConnection(string id)
    {
        return _connections.Remove(id);
    }

    /// <summary>
    /// 获取所有连�?    /// </summary>
    public IEnumerable<VerticalConnection> GetAllConnections() => _connections.Values;

    #endregion

    #region 跨楼层查�?
    /// <summary>
    /// 跨楼层查找对�?    /// </summary>
    public PlacedMapObject? FindObjectById(int objectId)
    {
        foreach (var floor in _floors.Values)
        {
            var obj = floor.GetObjectById(objectId);
            if (obj != null) return obj;
        }
        return null;
    }

    /// <summary>
    /// 跨楼层统计所有对象数�?    /// </summary>
    public int CountAllObjects()
    {
        return _floors.Values.Sum(f => f.CountObjects());
    }

    #endregion

    #region 清空

    /// <summary>
    /// 清空所有楼层和连接
    /// </summary>
    public void ClearAll()
    {
        _floors.Clear();
        _connections.Clear();
    }

    #endregion
}
