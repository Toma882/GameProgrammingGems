/*
 * 庄园游戏预设配置 - 享元模式实现
 * 所有 BoidSettings 和 BuildingFootprint 作为不可变享元被缓存共享，避免重复创建
 */

using GPGems.AI.Boids;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace GPGems.ManorSimulation;

/// <summary>
/// 庄园游戏中各种群体行为和建筑的预设配置（享元模式）
/// </summary>
public static class ManorGamePresets
{
    // 享元缓存：静态字段只在首次访问时初始化一次
    private static readonly BoidSettings _fishSchool = CreateFishSchool();
    private static readonly BoidSettings _grazingAnimal = CreateGrazingAnimal();
    private static readonly BoidSettings _butterfly = CreateButterfly();
    private static readonly BoidSettings _visitor = CreateVisitor();
    private static readonly BoidSettings _employee = CreateEmployee();

    // 建筑预设
    private static readonly BuildingFootprint _entrance;
    private static readonly BuildingFootprint _exit;
    private static readonly BuildingFootprint _attraction;
    private static readonly BuildingFootprint _shop;
    private static readonly BuildingFootprint _restaurant;
    private static readonly BuildingFootprint _road;
    // 员工设施预设
    private static readonly BuildingFootprint _staffDorm;
    private static readonly BuildingFootprint _harvestPoint;
    private static readonly BuildingFootprint _feedPoint;
    private static readonly BuildingFootprint _servePoint;

    // 自定义参数享元池 - 根据关键参数组合缓存
    private static readonly Dictionary<(float cohesion, float alignment, float separation), BoidSettings>
        _customSettingsCache = new();

    /// <summary>鱼群配置 - 快速、紧密聚集、有一定漫游</summary>
    public static BoidSettings FishSchool => _fishSchool;

    /// <summary>放牧动物配置 - 缓慢、松散聚集、高漫游倾向</summary>
    public static BoidSettings GrazingAnimal => _grazingAnimal;

    /// <summary>蝴蝶配置 - 非常快速、分散、高随机运动</summary>
    public static BoidSettings Butterfly => _butterfly;

    /// <summary>游客配置 - 中等速度、保持距离、目标导向</summary>
    public static BoidSettings Visitor => _visitor;

    /// <summary>员工配置 - 快速、目标导向、低聚集</summary>
    public static BoidSettings Employee => _employee;

    #region 建筑预设

    /// <summary>入口建筑预设</summary>
    public static BuildingFootprint Entrance => _entrance;

    /// <summary>出口建筑预设</summary>
    public static BuildingFootprint Exit => _exit;

    /// <summary>景点建筑预设</summary>
    public static BuildingFootprint Attraction => _attraction;

    /// <summary>商店建筑预设</summary>
    public static BuildingFootprint Shop => _shop;

    /// <summary>餐厅建筑预设</summary>
    public static BuildingFootprint Restaurant => _restaurant;

    /// <summary>道路/楼梯建筑预设</summary>
    public static BuildingFootprint Road => _road;

    /// <summary>整合场景可用的建筑预设列表</summary>
    public static ReadOnlyCollection<BuildingFootprint> IntegratedSceneBuildings { get; }

    #endregion

    /// <summary>静态构造函数 - 确保所有享元按正确顺序初始化</summary>
    static ManorGamePresets()
    {
        // 建筑预设必须在 IntegratedSceneBuildings 之前初始化
        _entrance = CreateEntrance();
        _exit = CreateExit();
        _attraction = CreateAttraction();
        _shop = CreateShop();
        _restaurant = CreateRestaurant();
        _road = CreateRoad();
        _staffDorm = CreateStaffDorm();
        _harvestPoint = CreateHarvestPoint();
        _feedPoint = CreateFeedPoint();
        _servePoint = CreateServePoint();

        IntegratedSceneBuildings = new List<BuildingFootprint>
            { _entrance, _exit, _attraction, _shop, _restaurant, _road,
              _staffDorm, _harvestPoint, _feedPoint, _servePoint }
            .AsReadOnly();
    }

    /// <summary>
    /// 获取或创建自定义 Boids 参数享元
    /// 相同参数组合返回同一实例，避免重复分配
    /// </summary>
    public static BoidSettings GetOrCreateCustom(float cohesion, float alignment, float separation)
    {
        var key = (cohesion, alignment, separation);

        if (_customSettingsCache.TryGetValue(key, out var cached))
            return cached;

        var settings = FishSchool with
        {
            CohesionWeight = cohesion,
            AlignmentWeight = alignment,
            SeparationWeight = separation
        };

        _customSettingsCache[key] = settings;
        return settings;
    }

    #region Factory Methods - 创建不可变享元实例

    private static BuildingFootprint CreateEntrance() =>
        new BuildingFootprint(BuildingType.Entrance, 3, 2) { BlocksMovement = false, FloorCount = 1 };

    private static BuildingFootprint CreateExit() =>
        new BuildingFootprint(BuildingType.Exit, 3, 2) { BlocksMovement = false, FloorCount = 1 };

    private static BuildingFootprint CreateAttraction() =>
        new BuildingFootprint(BuildingType.Attraction, 4, 4) { BlocksMovement = true, FloorCount = 2 };

    private static BuildingFootprint CreateShop() =>
        new BuildingFootprint(BuildingType.Shop, 2, 2) { BlocksMovement = true, FloorCount = 1 };

    private static BuildingFootprint CreateRestaurant() =>
        new BuildingFootprint(BuildingType.Restaurant, 4, 3) { BlocksMovement = true, FloorCount = 1 };

    private static BuildingFootprint CreateRoad() =>
        new BuildingFootprint(BuildingType.Road, 2, 1) { BlocksMovement = false, FloorCount = 0 };

    private static BuildingFootprint CreateStaffDorm() =>
        new BuildingFootprint(BuildingType.StaffFacility, 4, 4) { BlocksMovement = true, FloorCount = 2 };

    private static BuildingFootprint CreateHarvestPoint() =>
        new BuildingFootprint(BuildingType.StaffFacility, 2, 2) { BlocksMovement = false, FloorCount = 1 };

    private static BuildingFootprint CreateFeedPoint() =>
        new BuildingFootprint(BuildingType.StaffFacility, 2, 2) { BlocksMovement = false, FloorCount = 1 };

    private static BuildingFootprint CreateServePoint() =>
        new BuildingFootprint(BuildingType.StaffFacility, 2, 2) { BlocksMovement = false, FloorCount = 1 };

    private static BoidSettings CreateFishSchool() => new()
    {
        PerceptionRange = 15.0f,
        SeparationDist = 3.0f,
        DesiredSpeed = 2.0f,
        MaxSpeed = 4.0f,
        MaxAcceleration = 8.0f,
        SeparationWeight = 1.8f,
        AlignmentWeight = 1.2f,
        CohesionWeight = 1.0f,
        WanderWeight = 0.8f,
        SeekTargetWeight = 1.5f,
        VerticalDamping = 1.0f
    };

    private static BoidSettings CreateGrazingAnimal() => new()
    {
        PerceptionRange = 20.0f,
        SeparationDist = 5.0f,
        DesiredSpeed = 0.5f,
        MaxSpeed = 1.5f,
        MaxAcceleration = 3.0f,
        SeparationWeight = 1.0f,
        AlignmentWeight = 0.5f,
        CohesionWeight = 0.3f,
        WanderWeight = 2.5f,
        SeekTargetWeight = 0f,
        VerticalDamping = 1.0f
    };

    private static BoidSettings CreateButterfly() => new()
    {
        PerceptionRange = 8.0f,
        SeparationDist = 2.0f,
        DesiredSpeed = 1.5f,
        MaxSpeed = 3.0f,
        MaxAcceleration = 6.0f,
        SeparationWeight = 0.8f,
        AlignmentWeight = 0.2f,
        CohesionWeight = 0.3f,
        WanderWeight = 3.0f,
        SeekTargetWeight = 0f,
        VerticalDamping = 0.95f
    };

    private static BoidSettings CreateVisitor() => new()
    {
        PerceptionRange = 10.0f,
        SeparationDist = 2.5f,
        DesiredSpeed = 1.2f,
        MaxSpeed = 2.5f,
        MaxAcceleration = 4.0f,
        SeparationWeight = 1.5f,
        AlignmentWeight = 0.3f,
        CohesionWeight = 0.1f,
        WanderWeight = 0.2f,
        SeekTargetWeight = 2.0f,
        VerticalDamping = 1.0f
    };

    private static BoidSettings CreateEmployee() => new()
    {
        PerceptionRange = 8.0f,
        SeparationDist = 1.5f,
        DesiredSpeed = 2.0f,
        MaxSpeed = 4.0f,
        MaxAcceleration = 6.0f,
        SeparationWeight = 1.2f,
        AlignmentWeight = 0.1f,
        CohesionWeight = 0.1f,
        WanderWeight = 0.1f,
        SeekTargetWeight = 3.0f,
        VerticalDamping = 1.0f
    };
    #endregion
}
