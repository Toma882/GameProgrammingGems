/*
 * GPGems.AI - Utility System Example: RPG Character
 * RPG角色决策系统：根据血量、弹药、敌人距离等因素选择行为
 */

using GPGems.AI.Decision.Blackboards;

namespace GPGems.AI.Decision.Utility.Examples;

/// <summary>
/// RPG角色效用AI构建器
/// </summary>
public static class RPGCharacterBuilder
{
    public static UtilityReasoner Build(string characterName)
    {
        var bb = new Blackboard($"{characterName}_BB");
        bb.Set("health", 100f);      // 血量 0-100
        bb.Set("ammo", 50f);         // 弹药 0-100
        bb.Set("enemy_distance", 50f); // 敌人距离 0-100
        bb.Set("enemy_count", 1f);     // 敌人数量
        bb.Set("cover_available", true); // 是否有掩体
        bb.Set("time_since_damage", 10f); // 受伤后经过的时间

        var reasoner = new UtilityReasoner(characterName, bb)
        {
            Strategy = SelectionStrategy.HighestScore,
            NoiseAmount = 0.02f
        };

        // 1. 逃跑：血量极低，敌人多或距离近时
        reasoner.AddAction(CreateFleeAction());

        // 2. 治疗：血量低，安全时
        reasoner.AddAction(CreateHealAction());

        // 3. 找掩体：受伤不久，有掩体可用
        reasoner.AddAction(CreateTakeCoverAction());

        // 4. 攻击：有弹药，敌人在范围内
        reasoner.AddAction(CreateAttackAction());

        // 5. 装弹：弹药少，相对安全
        reasoner.AddAction(CreateReloadAction());

        // 6. 巡逻：默认行为
        reasoner.AddAction(CreatePatrolAction());

        return reasoner;
    }

    private static UtilityAction CreateFleeAction()
    {
        var action = new UtilityAction("🏃 FLEE", bb =>
        {
            var dist = bb.GetOrDefault("enemy_distance", 50f);
            bb.Set("enemy_distance", dist + 10f);
            Console.WriteLine("  🏃 Running away!");
            return 1f;
        })
        {
            BaseScore = 0.95f // 高优先级
        };

        // 血量越低越想逃（Sigmoid 曲线，血量 < 30 时急剧上升）
        action.AddConsideration("LowHealth", "health",
            new SigmoidCurve { Midpoint = 0.3f, Steepness = -10f, Inverted = true },
            weight: 2.0f);

        // 敌人越多越想逃
        action.AddConsideration("EnemyCount", "enemy_count",
            new LinearCurve { MinX = 0, MaxX = 5, Slope = 0.2f },
            weight: 1.5f);

        // 敌人越近越想逃
        action.AddConsideration("EnemyClose", "enemy_distance",
            new LinearCurve { MinX = 0, MaxX = 50, Inverted = true },
            weight: 1.2f);

        return action;
    }

    private static UtilityAction CreateHealAction()
    {
        var action = new UtilityAction("💊 HEAL", bb =>
        {
            var health = bb.GetOrDefault("health", 50f);
            bb.Set("health", Math.Min(health + 15f, 100f));
            Console.WriteLine($"  💊 Healing! Health: {health + 15:F0}");
            return 1f;
        })
        {
            BaseScore = 0.85f
        };

        // 血量低于 70 时想治疗，低于 40 时强烈
        action.AddConsideration("NeedHeal", "health",
            new QuadraticCurve { MinX = 0, MaxX = 70, Inverted = true });

        // 敌人越远越敢治疗
        action.AddConsideration("SafeDistance", "enemy_distance",
            new LinearCurve { MinX = 0, MaxX = 60 });

        return action;
    }

    private static UtilityAction CreateTakeCoverAction()
    {
        var action = new UtilityAction("🛡️ TAKE COVER", bb =>
        {
            var time = bb.GetOrDefault("time_since_damage", 0f);
            bb.Set("time_since_damage", time + 1f);
            Console.WriteLine("  🛡️ Taking cover!");
            return 1f;
        })
        {
            BaseScore = 0.75f
        };

        // 必须有掩体可用
        action.AddConsideration(new BoolConsideration("HasCover", "cover_available"));

        // 刚受伤时更想找掩体
        action.AddConsideration("RecentlyDamaged", "time_since_damage",
            new ExponentialDecayCurve { HalfLife = 5f, MinX = 0, MaxX = 20 });

        return action;
    }

    private static UtilityAction CreateAttackAction()
    {
        var action = new UtilityAction("⚔️ ATTACK", bb =>
        {
            var ammo = bb.GetOrDefault("ammo", 50f);
            bb.Set("ammo", ammo - 5f);
            Console.WriteLine($"  ⚔️ Attacking! Ammo: {ammo - 5:F0}");
            return 1f;
        })
        {
            BaseScore = 0.7f
        };

        // 有弹药才攻击
        action.AddConsideration("HasAmmo", "ammo",
            new StepCurve { Threshold = 0.1f, MinX = 0, MaxX = 100 });

        // 敌人在攻击范围内
        action.AddConsideration("InRange", "enemy_distance",
            new GaussianCurve { Center = 30f, Width = 20f, MinX = 0, MaxX = 100 });

        // 有弹药更愿意攻击
        action.AddConsideration("EnoughAmmo", "ammo",
            new SquareRootCurve { MinX = 0, MaxX = 100 });

        return action;
    }

    private static UtilityAction CreateReloadAction()
    {
        var action = new UtilityAction("🔄 RELOAD", bb =>
        {
            bb.Set("ammo", 100f);
            Console.WriteLine("  🔄 Reloaded!");
            return 1f;
        })
        {
            BaseScore = 0.6f
        };

        // 弹药越少越想装弹
        action.AddConsideration("LowAmmo", "ammo",
            new CubicCurve { MinX = 0, MaxX = 50, Inverted = true });

        // 敌人远时才敢装弹
        action.AddConsideration("SafeToReload", "enemy_distance",
            new LinearCurve { MinX = 20, MaxX = 80 });

        return action;
    }

    private static UtilityAction CreatePatrolAction()
    {
        var action = new UtilityAction("🚶 PATROL", bb =>
        {
            var wp = bb.GetOrDefault("patrol_wp", 0);
            bb.Set("patrol_wp", (wp + 1) % 5);
            Console.WriteLine($"  🚶 Patrolling to WP{(wp + 1) % 5}");
            return 1f;
        })
        {
            BaseScore = 0.3f // 低优先级，兜底行为
        };

        // 血量高时更愿意巡逻
        action.AddConsideration("HealthOK", "health",
            new LinearCurve { MinX = 50, MaxX = 100 });

        // 敌人远时巡逻
        action.AddConsideration("NoThreat", "enemy_distance",
            new LinearCurve { MinX = 30, MaxX = 100 });

        return action;
    }
}

/// <summary>
/// 指数衰减曲线
/// </summary>
public class ExponentialDecayCurve : UtilityCurve
{
    public float HalfLife { get; set; } = 1f;

    protected override float CalculateCurve(float x)
    {
        return MathF.Exp(-x * MathF.Log(2f) / HalfLife);
    }
}
