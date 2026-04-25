namespace GPGems.AI.Pathfinding;

/// <summary>
/// 寻路算法策略接口
/// 所有寻路算法都实现此接口，支持在同一场景下切换对比
/// </summary>
public interface IPathfinder
{
    /// <summary>算法名称</summary>
    string Name { get; }

    /// <summary>算法描述</summary>
    string Description { get; }

    /// <summary>
    /// 在指定地图上寻路
    /// </summary>
    /// <param name="map">网格地图</param>
    /// <param name="start">起点</param>
    /// <param name="goal">终点</param>
    /// <returns>路径节点列表，从起点到终点。找不到路径返回空列表。</returns>
    List<GridNode> FindPath(GridMap map, GridNode start, GridNode goal);

    /// <summary>搜索过程中开放集的节点数</summary>
    int OpenSetCount { get; }

    /// <summary>搜索过程中关闭集的节点数</summary>
    int ClosedSetCount { get; }
}
