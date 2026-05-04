namespace GPGems.Core.PipelineHub;

/// <summary>
/// 节点外观层 - 统一创建入口，按职责分类
/// 团队写管线时只需要记住 4 个大类，不需要记 100 个节点名
///
/// 分类原则与边界：
/// | 节点类型 | when职责 | Execute职责 | 能否修改Context | 能否异步
/// |---------|---------|------------|----------------|---------|
/// | 输入节点 | 检查输入源是否就绪 | 收集输入 → 写入Context | ✅ 只能写自己provides的数据 | ✅ 支持（玩家输入需要等待）
/// | 条件节点 | 永远为true | 只做判断 → 产出布尔标记 | ✅ 只能写判断结果字段 | ❌ 必须同步
/// | 执行节点 | 检查 requires + 业务前置 | 执行业务逻辑 → 推送变更 | ✅ 可读写 + PushChannel | ✅ 支持（复杂计算）
/// | 可视化节点 | 检查表现开关/配置 | 播动画/特效 → 纯表现 | ❌ 绝对不能改逻辑数据 | ✅ 必须异步（需要等待表现）
/// </summary>
public static class Node
{
    /// <summary>
    /// 1. 输入节点：负责收集玩家/AI输入，产出决策数据
    /// </summary>
    public static class Input
    {
        // 可在各模块中扩展：如 Node.Input.BuildingPlacement
    }

    /// <summary>
    /// 2. 条件节点：只做判断，不修改数据，产出布尔标记
    /// </summary>
    public static class Condition
    {
        // 可在各模块中扩展：如 Node.Condition.CanPlaceBuilding
    }

    /// <summary>
    /// 3. 执行节点：纯逻辑计算，修改状态，推送变更
    /// </summary>
    public static class Execute
    {
        // 可在各模块中扩展：如 Node.Execute.PlaceBuilding
    }

    /// <summary>
    /// 4. 可视化节点：播动画/特效，纯表现，不影响逻辑结果
    /// </summary>
    public static class Visual
    {
        // 可在各模块中扩展：如 Node.Visual.PlayBuildAnimation
    }
}
