using System.Collections.Generic;
using GPGems.ManorSimulation.Map;

namespace GPGems.ManorSimulation.Building;

/// <summary>
/// 建筑单元接口
/// 核心基础 Unit，持有建筑基础数据 + 行为字典
/// </summary>
public interface IBuildingUnit
{
    /// <summary>建筑唯一ID</summary>
    int Id { get; }

    /// <summary>配置ID</summary>
    string ConfigId { get; }

    /// <summary>网格坐标</summary>
    (int x, int y) GridPosition { get; set; }

    /// <summary>楼层索引</summary>
    int FloorIndex { get; set; }

    /// <summary>占位尺寸</summary>
    (int width, int height) Size { get; }

    /// <summary>旋转角度�?/90/180/270�?/summary>
    int Rotation { get; set; }

    /// <summary>占位定义</summary>
    IFootprint? Footprint { get; set; }

    /// <summary>是否已放�?/summary>
    bool IsPlaced { get; set; }

    /// <summary>是否可移�?/summary>
    bool CanMove { get; }

    /// <summary>是否可旋�?/summary>
    bool CanRotate { get; }

    /// <summary>是否可收�?/summary>
    bool CanStore { get; }

    /// <summary>是否可出�?/summary>
    bool CanSell { get; }

    /// <summary>行为字典（享元，所有建筑共享行为实例）</summary>
    Dictionary<string, IBehavior> Behaviors { get; }

    /// <summary>行为数据字典（每个建筑独有，存储状态）</summary>
    Dictionary<string, BehaviorData> BehaviorData { get; }

    /// <summary>自定义数据（上层业务使用�?/summary>
    object? UserData { get; set; }

    /// <summary>
    /// 添加行为
    /// </summary>
    void AddBehavior(IBehavior behavior);

    /// <summary>
    /// 获取行为
    /// </summary>
    T? GetBehavior<T>() where T : class, IBehavior;

    /// <summary>
    /// 移除行为
    /// </summary>
    bool RemoveBehavior(string behaviorName);

    /// <summary>
    /// 初始化所有行�?    /// </summary>
    void InitializeBehaviors();

    /// <summary>
    /// 销毁建筑和所有行�?    /// </summary>
    void Destroy();
}
