using System;
using System.Collections.Generic;
using GPGems.ManorSimulation.Map;

namespace GPGems.ManorSimulation.Building;

/// <summary>
/// 建筑建造�?/// 建造者模式：根据配置动态注入行为，数据驱动创建
/// 享元模式：同类型建筑共享同一�?Footprint 实例
/// </summary>
public class BuildingBuilder
{
    private readonly BehaviorFactory _behaviorFactory;
    private readonly FootprintFactory _footprintFactory;
    private BuildingData? _config;
    private (int x, int y) _gridPosition;
    private int _floorIndex;
    private int _rotation;
    private readonly Dictionary<string, IBehavior> _customBehaviors = new();

    public BuildingBuilder(BehaviorFactory behaviorFactory)
        : this(behaviorFactory, FootprintFactory.Instance)
    {
    }

    public BuildingBuilder(BehaviorFactory behaviorFactory, FootprintFactory footprintFactory)
    {
        _behaviorFactory = behaviorFactory ?? throw new ArgumentNullException(nameof(behaviorFactory));
        _footprintFactory = footprintFactory ?? throw new ArgumentNullException(nameof(footprintFactory));
    }

    /// <summary>
    /// 设置建筑配置
    /// </summary>
    public BuildingBuilder WithConfig(BuildingData config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        return this;
    }

    /// <summary>
    /// 设置网格位置
    /// </summary>
    public BuildingBuilder AtPosition(int x, int y)
    {
        _gridPosition = (x, y);
        return this;
    }

    /// <summary>
    /// 设置楼层
    /// </summary>
    public BuildingBuilder OnFloor(int floorIndex)
    {
        _floorIndex = floorIndex;
        return this;
    }

    /// <summary>
    /// 设置旋转角度
    /// </summary>
    public BuildingBuilder WithRotation(int rotation)
    {
        _rotation = rotation;
        return this;
    }

    /// <summary>
    /// 添加自定义行为（覆盖配置中的同名行为�?    /// </summary>
    public BuildingBuilder AddCustomBehavior(IBehavior behavior)
    {
        if (behavior == null)
            throw new ArgumentNullException(nameof(behavior));
        _customBehaviors[behavior.Name] = behavior;
        return this;
    }

    /// <summary>
    /// 构建建筑单元
    /// </summary>
    public BuildingUnit Build()
    {
        if (_config == null)
            throw new InvalidOperationException("Building config is not set");

        // 享元模式：从工厂获取共享�?Footprint
        var footprint = _footprintFactory.GetOrCreate(
            configName: _config.ConfigId,
            configCreator: () => new FootprintConfig
            {
                Name = _config.ConfigId,
                ObjectType = "building",
                ShapeType = _config.FootprintShape,
                Width = _config.Size.width,
                Height = _config.Size.height,
                BlocksMovement = _config.BlocksMovement,
                AnchorOffset = _config.AnchorOffset,
                CustomMask = ParseCustomMask(_config.ShapeConfig)
            });

        // 创建核心单元
        var building = new BuildingUnit(_config)
        {
            GridPosition = _gridPosition,
            FloorIndex = _floorIndex,
            Rotation = _rotation,
            Footprint = footprint
        };

        // 注入配置定义的行为（享元模式：所有建筑共享行为实例）
        foreach (var behaviorName in _config.BehaviorList)
        {
            // 优先使用自定义行为，否则从工厂获取享元实例
            if (!_customBehaviors.TryGetValue(behaviorName, out var behavior))
            {
                behavior = _behaviorFactory.GetBehavior(behaviorName);
            }

            if (behavior != null)
            {
                building.AddBehavior(behavior);
            }
        }

        // 添加额外的自定义行为（不在配置列表中的）
        foreach (var kvp in _customBehaviors)
        {
            if (!_config.BehaviorList.Contains(kvp.Key))
            {
                building.AddBehavior(kvp.Value);
            }
        }

        // 初始化所有行�?        building.InitializeBehaviors();

        return building;
    }

    /// <summary>
    /// 重置建造者状�?    /// </summary>
    public void Reset()
    {
        _config = null;
        _gridPosition = (0, 0);
        _floorIndex = 0;
        _rotation = 0;
        _customBehaviors.Clear();
    }

    /// <summary>
    /// 解析自定义形状掩码（字符串数�?�?bool[,]�?    /// 字符串格式："X" 表示占用�?." 表示不占�?    /// </summary>
    private static bool[,]? ParseCustomMask(string[]? shapeConfig)
    {
        if (shapeConfig == null || shapeConfig.Length == 0)
            return null;

        var height = shapeConfig.Length;
        var width = shapeConfig[0].Length;
        var mask = new bool[width, height];

        for (var y = 0; y < height; y++)
        {
            var line = shapeConfig[y];
            for (var x = 0; x < line.Length && x < width; x++)
            {
                mask[x, y] = line[x] == 'X' || line[x] == 'x';
            }
        }

        return mask;
    }
}
