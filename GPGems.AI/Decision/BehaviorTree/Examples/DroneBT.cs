/*
 * GPGems.AI - Behavior Tree Example: Drone
 * 无人机行为树示例：巡逻 -> 追击 -> 攻击 -> 返回巡逻
 */
using GPGems.AI.Decision.Blackboards;

namespace GPGems.AI.Decision.BehaviorTree.Examples
{
/// <summary>
/// 无人机行为树构建器
/// </summary>
public static class DroneBTBuilder
{
    public static BehaviorTree BuildDroneAI(string droneName)
    {
        var bb = new Blackboard($"{droneName}_BB");
        bb.Set("ammo", 100);
        bb.Set("health", 100);
        bb.Set("enemy_distance", 100f);
        bb.Set("enemy_visible", false);
        bb.Set("attack_range", 30f);
        bb.Set("chase_speed", 80f);
        bb.Set("patrol_waypoint", 0);

        // 使用 Fluent API 构建行为树
        return BehaviorTreeBuilder.Create($"{droneName}_AI", bb)

            // 根节点：选择器（优先级从高到低）
            .Selector("Root")

                // 1. 高优先级：低血量撤退
                .Sequence("RetreatIfLowHealth")
                    .Condition("health", false, c => c.GetOrDefault("health", 100f) < 30f)
                    .Log("⚠️ Health critical! Retreating...")
                    .Action(Retreat, "RetreatAction")
                .End()

                // 2. 中优先级：战斗行为
                .Sequence("CombatBehavior")
                    .Condition("enemy_visible", true)

                    // 子选择器：追击或攻击
                    .Selector("EngageType")
                        // 2a. 在攻击范围内 -> 攻击
                        .Sequence("AttackIfInRange")
                            .Condition(InAttackRange, "InAttackRange?")
                            .Condition(HasAmmo, "HasAmmo?")
                            .Log("🎯 Enemy in range! Attacking...")
                            .Action(AttackEnemy, "Attack")
                        .End()

                        // 2b. 不在攻击范围内 -> 追击
                        .Sequence("ChaseEnemy")
                            .Log("🏃 Enemy detected! Chasing...")
                            .Action(ChaseEnemy, "Chase")
                        .End()
                    .End()
                .End()

                // 3. 低优先级：巡逻
                .Sequence("PatrolBehavior")
                    .Log("🚶 Patrolling...")
                    .Action(Patrol, "PatrolAction")
                    .Wait(2f, "PatrolPause")
                .End()

            .End()
            .Build();
    }

    private static NodeStatus Retreat(Blackboard bb)
    {
        var health = bb.GetOrDefault("health", 100f);
        bb.Set("drone_mode", "Retreating");

        health += 5f; // 撤退时回血
        bb.Set("health", Math.Min(health, 100f));

        if (health >= 80f)
        {
            bb.Set("enemy_visible", false);
            return NodeStatus.Success;
        }
        return NodeStatus.Running;
    }

    private static bool InAttackRange(Blackboard bb)
    {
        var dist = bb.GetOrDefault("enemy_distance", 100f);
        var range = bb.GetOrDefault("attack_range", 30f);
        return dist <= range;
    }

    private static bool HasAmmo(Blackboard bb)
    {
        return bb.GetOrDefault("ammo", 0) > 0;
    }

    private static NodeStatus AttackEnemy(Blackboard bb)
    {
        var ammo = bb.GetOrDefault("ammo", 0);
        bb.Set("drone_mode", "Attacking");

        if (ammo <= 0)
            return NodeStatus.Failure;

        bb.Set("ammo", ammo - 10);

        // 模拟敌人距离随机变化
        if (Random.Shared.NextDouble() < 0.1)
        {
            var dist = bb.GetOrDefault("enemy_distance", 100f);
            bb.Set("enemy_distance", dist + 20f);
        }

        Console.WriteLine($"  ⚔️ Firing! Ammo left: {ammo - 10}");
        return NodeStatus.Running;
    }

    private static NodeStatus ChaseEnemy(Blackboard bb)
    {
        var dist = bb.GetOrDefault("enemy_distance", 100f);
        var speed = bb.GetOrDefault("chase_speed", 80f);
        bb.Set("drone_mode", "Chasing");

        dist = Math.Max(0, dist - speed * 0.1f);
        bb.Set("enemy_distance", dist);

        Console.WriteLine($"  🏃 Distance to enemy: {dist:F1}m");

        if (dist <= bb.GetOrDefault("attack_range", 30f))
            return NodeStatus.Success;

        return NodeStatus.Running;
    }

    private static NodeStatus Patrol(Blackboard bb)
    {
        var wp = bb.GetOrDefault("patrol_waypoint", 0);
        bb.Set("drone_mode", "Patrolling");

        wp = (wp + 1) % 5;
        bb.Set("patrol_waypoint", wp);
        Console.WriteLine($"  🚶 Moving to waypoint {wp}");

        // 随机发现敌人
        if (Random.Shared.NextDouble() < 0.15)
        {
            bb.Set("enemy_visible", true);
            bb.Set("enemy_distance", 100f);
            Console.WriteLine("  👁️ Enemy spotted!");
        }

        return NodeStatus.Success;
    }
}
}