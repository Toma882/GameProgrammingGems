namespace GPGems.AI.ManorSimulation.Placement;

/// <summary>
/// 地图图层类型
/// </summary>
public enum MapLayer
{
    /// <summary>地形层（地面、道路、水域）</summary>
    Terrain,
    /// <summary>建筑层（建筑物、设施）</summary>
    Building,
    /// <summary>装饰层（植物、摆件）</summary>
    Decoration,
    /// <summary>路径层（行人路径、道路）</summary>
    Path
}

/// <summary>
/// 建筑物类型
/// </summary>
public enum BuildingType
{
    /// <summary>道路</summary>
    Road,
    /// <summary>入口</summary>
    Entrance,
    /// <summary>出口</summary>
    Exit,
    /// <summary>围栏/围墙</summary>
    Fence,
    /// <summary>房屋/建筑</summary>
    House,
    /// <summary>商店</summary>
    Shop,
    /// <summary>餐厅</summary>
    Restaurant,
    /// <summary>景点/娱乐设施</summary>
    Attraction,
    /// <summary>动物栖息地</summary>
    Habitat,
    /// <summary>员工设施</summary>
    StaffFacility,
    /// <summary>植物/树木</summary>
    Plant,
    /// <summary>装饰物</summary>
    Decoration
}

/// <summary>
/// 占位形状类型
/// </summary>
public enum FootprintShape
{
    /// <summary>矩形（默认）</summary>
    Rectangle,
    /// <summary>圆形</summary>
    Circle,
    /// <summary>自定义形状</summary>
    Custom
}

/// <summary>
/// 放置验证结果
/// </summary>
public enum PlacementResult
{
    /// <summary>可以放置</summary>
    Valid,
    /// <summary>超出地图边界</summary>
    OutOfBounds,
    /// <summary>与其他物体碰撞</summary>
    Collision,
    /// <summary>与地形冲突</summary>
    TerrainConflict,
    /// <summary>图层不允许</summary>
    LayerNotAllowed
}

/// <summary>
/// 格子占用状态
/// </summary>
public enum CellOccupancy
{
    /// <summary>空闲</summary>
    Free = 0,
    /// <summary>完全占用（不可通行）</summary>
    Blocked = 1,
    /// <summary>部分占用（可绕行）</summary>
    Partial = 2,
    /// <summary>路径专用</summary>
    PathOnly = 3
}
