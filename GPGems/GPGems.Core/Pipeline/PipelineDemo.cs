using System;
using System.Collections.Generic;

namespace GPGems.Core.PipelineHub;

/// <summary>
/// 管线框架使用演示
/// 展示如何定义节点、配置管线、执行管线
/// 展示节点如何与 CommunicationBus 集成
/// </summary>
public static class PipelineDemo
{
    #region 演示 1: 建筑放置管线（展示 PushChannel 积累-批量处理模式

    /// <summary>
    /// 演示：建筑放置管线
    /// 展示节点如何通过 PushChannel 积累副作用，执行完批量处理
    /// </summary>
    public static PipelineResult RunBuildingPlacementDemo()
    {
        var bus = CommunicationBus.Instance;

        // ===== 步骤 1: 注册处理器（模块初始化时做）
        // 注册资源消耗处理器
        bus.AddHandler(new ProgressHandlerContext
        {
            Subscriber = BuildingSystem.Instance,
            Handler = BuildingSystem.Instance,
            ProcessFunctionMap = new Dictionary<string, Action<object>>
            {
                ["consume_resource"] = data =>
                {
                    var (type, amount) = ((string type, int amount))data!;
                    Console.WriteLine($"[Push] 消耗资源: {type} x {amount}");
                }
            }
        });

        // 注册特效播放处理器
        bus.AddHandler(new ProgressHandlerContext
        {
            Subscriber = EffectSystem.Instance,
            Handler = EffectSystem.Instance,
            ProcessFunctionMap = new Dictionary<string, Action<object>>
            {
                ["play_effect"] = data =>
                {
                    var tuple = ((string, (int, int)))data!;
                    Console.WriteLine($"[Push] 播放特效: {tuple.Item1} at ({tuple.Item2.Item1}, {tuple.Item2.Item2})");
                }
            }
        });

        // ===== 步骤 2: 创建管线

        var pipeline = new Pipeline("BUILDING_PLACEMENT");

        pipeline.RegisterNodes(new IPipelineNode[]
        {
            new CheckPlacementPositionNode(),
            new CheckResourcesNode(),
            new CheckBuildingSpaceNode(),
            new PlaceBuildingNode(),
            new PlayBuildEffectNode(),
        });

        pipeline.AddNodes(new[]
        {
            "CheckPlacementPosition",
            "CheckResources",
            "CheckBuildingSpace",
            "PlaceBuilding",
            "PlayBuildEffect"
        });

        // ===== 步骤 3: 执行管线

        var initialData = new Dictionary<string, object>
        {
            ["unit"] = new { Id = 1, Name = "Player" },
            ["building_type"] = "Farm",
            ["grid_position"] = (x: 10, y: 20),
            ["resource_type"] = "Gold",
            ["resource_amount"] = 100,
            ["building_size"] = (width: 3, height: 3),
        };

        var result = pipeline.Execute(subject: null, initialData);

        // ===== 步骤 4: 批量处理所有积累的副作用

        Console.WriteLine("\n--- 批量处理副作用 ---");
        bus.ProcessData(BuildingSystem.Instance);
        bus.ProcessData(EffectSystem.Instance);

        // ===== 输出结果

        Console.WriteLine($"\n=== 建筑放置管线执行结果 ===");
        Console.WriteLine($"成功: {result.Success}");
        Console.WriteLine($"耗时: {result.DurationMs}ms");
        foreach (var log in result.ExecutedNodes)
        {
            var status = log.Skipped ? "跳过" : (log.Success ? "成功" : "失败");
            Console.WriteLine($"  [{status}] {log.NodeName} {log.Reason}");
        }

        return result;
    }

    #endregion

    #region 演示 2: 动作管线（展示互斥分支 + EventChannel

    /// <summary>
    /// 演示：带互斥分支的动作管线
    /// 展示节点如何发布事件
    /// </summary>
    public static PipelineResult RunMutexBranchDemo()
    {
        var bus = CommunicationBus.Instance;

        // 订阅动作完成事件
        bus.Subscribe("unit.action.completed", args =>
        {
            Console.WriteLine($"[Event] 收到动作完成事件: {args}");
        });

        var pipeline = new Pipeline("UNIT_ACTION");

        pipeline.RegisterNodes(new IPipelineNode[]
        {
            new CheckCanActNode(),
            new JumpActionNode(),
            new MoveActionNode(),
            new AttackActionNode(),
            new ShowTipNode(),
        });

        pipeline.AddNode("CheckCanAct");

        var branch = new MutexBranch(name: "ActionSelect", fallback: "ShowTip")
            .Add("JumpAction", ctx => ctx.Get<string>("action_type") == "jump")
            .Add("MoveAction", ctx => ctx.Get<string>("action_type") == "move")
            .Add("AttackAction", ctx => ctx.Get<string>("action_type") == "attack");

        pipeline.AddBranch(branch);

        var initialData = new Dictionary<string, object>
        {
            ["unit"] = new { Id = 1, Name = "Hero" },
            ["action_type"] = "attack",
        };

        var result = pipeline.Execute(subject: null, initialData);

        Console.WriteLine($"\n=== 单元动作管线（攻击） ===");
        Console.WriteLine($"成功: {result.Success}");
        foreach (var log in result.ExecutedNodes)
        {
            var status = log.Skipped ? "跳过" : (log.Success ? "成功" : "失败");
            Console.WriteLine($"  [{status}] {log.NodeName}");
        }

        return result;
    }

    #endregion

    #region 演示 3: 子管线（节点原生能力）

    /// <summary>
    /// 演示：子管线 - 节点的原生能力
    /// 节点在 Execute 里可以做任何事，包括启动新管线
    /// </summary>
    public static PipelineResult RunSubPipelineDemo()
    {
        Console.WriteLine("\n=== 子管线演示（节点原生能力） ===");

        var mainPipeline = new Pipeline("CAST_SKILL");

        mainPipeline.RegisterNodes(new IPipelineNode[]
        {
            new CastSkillNode(),
            new ApplyDamageNode(),
        });

        mainPipeline.AddNodes(new[] { "CastSkill", "ApplyDamage" });

        var initialData = new Dictionary<string, object>
        {
            ["unit"] = new { Id = 1, Name = "Hero" },
            ["skill_id"] = "fireball",
            ["target_id"] = 1001,
        };

        var result = mainPipeline.Execute(subject: null, initialData);

        Console.WriteLine($"主管线成功: {result.Success}");
        return result;
    }

    #endregion
}

#region 示例系统单例

/// <summary>
/// 建筑系统单例（作为 PushChannel 的 subscriber）
/// </summary>
public class BuildingSystem
{
    public static BuildingSystem Instance { get; } = new();
    private BuildingSystem() { }
}

/// <summary>
/// 特效系统单例（作为 PushChannel 的 subscriber）
/// </summary>
public class EffectSystem
{
    public static EffectSystem Instance { get; } = new();
    private EffectSystem() { }
}

#endregion

#region 示例节点实现

/// <summary>
/// 检查放置位置节点
/// </summary>
public class CheckPlacementPositionNode : PipelineNodeBase
{
    public override string Name => "CheckPlacementPosition";
    public override IReadOnlyList<string> Requires => new[] { "grid_position" };
    public override IReadOnlyList<string> Provides => new[] { "position_valid" };

    public override Dictionary<string, object> Execute(PipelineContext context)
    {
        var pos = context.Get<(int x, int y)>("grid_position");
        bool valid = pos.x >= 0 && pos.y >= 0;
        return Output(("position_valid", valid));
    }
}

/// <summary>
/// 检查资源节点
/// </summary>
public class CheckResourcesNode : PipelineNodeBase
{
    public override string Name => "CheckResources";
    public override IReadOnlyList<string> Requires => new[] { "resource_type", "resource_amount" };
    public override IReadOnlyList<string> Provides => new[] { "has_enough_resource" };

    public override Dictionary<string, object> Execute(PipelineContext context)
    {
        var amount = context.Get<int>("resource_amount");
        return Output(("has_enough_resource", amount >= 0));
    }
}

/// <summary>
/// 检查建筑空间节点
/// </summary>
public class CheckBuildingSpaceNode : PipelineNodeBase
{
    public override string Name => "CheckBuildingSpace";
    public override IReadOnlyList<string> Requires => new[] { "grid_position", "building_size" };
    public override IReadOnlyList<string> Provides => new[] { "space_available" };

    public override bool When(PipelineContext context)
    {
        return context.Get<bool>("position_valid") && context.Get<bool>("has_enough_resource");
    }

    public override Dictionary<string, object> Execute(PipelineContext context)
    {
        var size = context.Get<(int width, int height)>("building_size");
        bool available = size.width > 0 && size.height > 0;
        return Output(("space_available", available));
    }
}

/// <summary>
/// 放置建筑节点 - 展示 PushChannel 积累模式
/// </summary>
public class PlaceBuildingNode : PipelineNodeBase
{
    public override string Name => "PlaceBuilding";
    public override IReadOnlyList<string> Requires => new[] { "unit", "building_type", "grid_position" };
    public override IReadOnlyList<string> Provides => new[] { "building_placed", "building_id" };

    public override bool When(PipelineContext context)
    {
        return context.Get<bool>("space_available");
    }

    public override Dictionary<string, object> Execute(PipelineContext context)
    {
        var unit = context.Get<object>("unit");
        var buildingType = context.Get<string>("building_type");
        var pos = context.Get<(int x, int y)>("grid_position");

        Console.WriteLine($"放置建筑 {buildingType} 在 ({pos.x}, {pos.y})");

        // ===== 推送副作用（只积累，不立即处理 =====
        var resourceType = context.Get<string>("resource_type");
        var amount = context.Get<int>("resource_amount");

        // 通过 CommunicationBus 推送，积累到 BuildingSystem 的队列
        CommunicationBus.Instance.PushData(
            subscriber: BuildingSystem.Instance,
            dataType: "consume_resource",
            data: (resourceType, amount)
        );

        return Output(
            ("building_placed", true),
            ("building_id", Guid.NewGuid().GetHashCode())
        );
    }
}

/// <summary>
/// 播放建造特效节点
/// </summary>
public class PlayBuildEffectNode : PipelineNodeBase
{
    public override string Name => "PlayBuildEffect";
    public override IReadOnlyList<string> Requires => new[] { "building_id", "grid_position" };
    public override IReadOnlyList<string> Provides => new[] { "effect_played" };

    public override bool When(PipelineContext context)
    {
        return context.Get<bool>("building_placed");
    }

    public override Dictionary<string, object> Execute(PipelineContext context)
    {
        var buildingId = context.Get<int>("building_id");
        var pos = context.Get<(int x, int y)>("grid_position");

        // ===== 推送副作用 =====
        CommunicationBus.Instance.PushData(
            subscriber: EffectSystem.Instance,
            dataType: "play_effect",
            data: ("build_effect", pos)
        );

        return Output(("effect_played", true));
    }
}

/// <summary>
/// 检查是否能行动
/// </summary>
public class CheckCanActNode : PipelineNodeBase
{
    public override string Name => "CheckCanAct";
    public override IReadOnlyList<string> Requires => new[] { "unit" };
    public override IReadOnlyList<string> Provides => new[] { "can_act" };

    public override Dictionary<string, object> Execute(PipelineContext context)
    {
        var unit = context.Get<object>("unit");
        bool canAct = unit != null;
        return Output(("can_act", canAct));
    }
}

/// <summary>
/// 攻击动作节点 - 展示发布事件
/// </summary>
public class AttackActionNode : PipelineNodeBase
{
    public override string Name => "AttackAction";
    public override IReadOnlyList<string> Requires => new[] { "unit" };
    public override IReadOnlyList<string> Provides => new[] { "action_executed" };

    public override Dictionary<string, object> Execute(PipelineContext context)
    {
        Console.WriteLine("执行攻击动作");

        // ===== 发布事件（立即通知所有订阅者）
        CommunicationBus.Instance.Publish("unit.action.completed", "attack");

        return Output(("action_executed", "attack"));
    }
}

/// <summary>
/// 跳跃动作节点
/// </summary>
public class JumpActionNode : PipelineNodeBase
{
    public override string Name => "JumpAction";
    public override IReadOnlyList<string> Requires => new[] { "unit" };
    public override IReadOnlyList<string> Provides => new[] { "action_executed" };

    public override Dictionary<string, object> Execute(PipelineContext context)
    {
        Console.WriteLine("执行跳跃动作");
        CommunicationBus.Instance.Publish("unit.action.completed", "jump");
        return Output(("action_executed", "jump"));
    }
}

/// <summary>
/// 移动动作节点
/// </summary>
public class MoveActionNode : PipelineNodeBase
{
    public override string Name => "MoveAction";
    public override IReadOnlyList<string> Requires => new[] { "unit" };
    public override IReadOnlyList<string> Provides => new[] { "action_executed" };

    public override Dictionary<string, object> Execute(PipelineContext context)
    {
        Console.WriteLine("执行移动动作");
        CommunicationBus.Instance.Publish("unit.action.completed", "move");
        return Output(("action_executed", "move"));
    }
}

/// <summary>
/// 显示提示节点
/// </summary>
public class ShowTipNode : PipelineNodeBase
{
    public override string Name => "ShowTip";
    public override IReadOnlyList<string> Requires => Array.Empty<string>();
    public override IReadOnlyList<string> Provides => new[] { "tip_shown" };

    public override Dictionary<string, object> Execute(PipelineContext context)
    {
        Console.WriteLine("没有匹配的动作，显示提示");
        return Output(("tip_shown", true));
    }
}

/// <summary>
/// 释放技能节点 - 展示子管线（节点原生能力）
/// </summary>
public class CastSkillNode : PipelineNodeBase
{
    public override string Name => "CastSkill";
    public override IReadOnlyList<string> Requires => new[] { "unit", "skill_id", "target_id" };
    public override IReadOnlyList<string> Provides => new[] { "skill_cast" };

    public override Dictionary<string, object> Execute(PipelineContext context)
    {
        var unit = context.Get<object>("unit");
        var skillId = context.Get<string>("skill_id");
        var targetId = context.Get<int>("target_id");

        Console.WriteLine($"[CastSkill] {unit} 释放技能 {skillId} 到目标 {targetId}");

        // ===== 节点原生能力：启动子管线
        // 框架完全不需要知道这件事，节点想做什么就做什么

        Console.WriteLine("  → 启动子管线: PLAY_ANIMATION");

        var subPipeline = new Pipeline("PLAY_ANIMATION");
        subPipeline.RegisterNodes(new IPipelineNode[]
        {
            new PlaySoundEffectNode(),
            new PlayAnimationEffectNode(),
            new WaitAnimationFinishNode(),
        });
        subPipeline.AddNodes(new[] { "PlaySoundEffect", "PlayAnimationEffect", "WaitAnimationFinish" });

        var subResult = subPipeline.Execute(subject: unit, initialData: null);
        Console.WriteLine($"  ← 子管线完成: {subResult.Success}, 执行了 {subResult.ExecutedNodes.Count} 个节点");

        return Output(("skill_cast", true));
    }
}

/// <summary>
/// 应用伤害节点 - 展示 QueryChannel 查询数据
/// </summary>
public class ApplyDamageNode : PipelineNodeBase
{
    public override string Name => "ApplyDamage";
    public override IReadOnlyList<string> Requires => new[] { "target_id" };
    public override IReadOnlyList<string> Provides => new[] { "damage_applied" };

    public override Dictionary<string, object> Execute(PipelineContext context)
    {
        var targetId = context.Get<int>("target_id");

        // ===== 通过 QueryChannel 查询目标数据
        var hp = CommunicationBus.Instance.QueryData<int>(
            subscriber: "EnemySystem",
            dataType: "get_hp",
            args: targetId
        );

        Console.WriteLine($"[ApplyDamage] 对目标 {targetId} 应用伤害 (当前HP: {hp})");

        return Output(("damage_applied", true));
    }
}

/// <summary>
/// 播放音效节点
/// </summary>
public class PlaySoundEffectNode : PipelineNodeBase
{
    public override string Name => "PlaySoundEffect";
    public override IReadOnlyList<string> Provides => new[] { "sound_played" };

    public override Dictionary<string, object> Execute(PipelineContext context)
    {
        Console.WriteLine("    [子管线] 播放音效: skill_fireball.wav");
        return Output(("sound_played", true));
    }
}

/// <summary>
/// 播放动画节点
/// </summary>
public class PlayAnimationEffectNode : PipelineNodeBase
{
    public override string Name => "PlayAnimationEffect";
    public override IReadOnlyList<string> Provides => new[] { "anim_started" };

    public override Dictionary<string, object> Execute(PipelineContext context)
    {
        Console.WriteLine("    [子管线] 播放动画: anim_fireball");
        return Output(("anim_started", true));
    }
}

/// <summary>
/// 等待动画完成节点
/// </summary>
public class WaitAnimationFinishNode : PipelineNodeBase
{
    public override string Name => "WaitAnimationFinish";
    public override IReadOnlyList<string> Provides => new[] { "anim_finished" };

    public override Dictionary<string, object> Execute(PipelineContext context)
    {
        Console.WriteLine("    [子管线] 等待动画结束...");
        return Output(("anim_finished", true));
    }
}

#endregion
