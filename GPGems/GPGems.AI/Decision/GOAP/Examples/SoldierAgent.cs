/*
 * GPGems.AI - GOAP Example: Soldier Agent
 * 士兵特工 AI：经典《F.E.A.R.》风格的 GOAP 实现
 *
 * 目标（按优先级排序）：
 * 1. 保持存活（血量低时治疗）
 * 2. 保持弹药充足
 * 3. 杀死敌人
 *
 * 动作：
 * - 寻找掩体
 * - 治疗
 * - 装弹
 * - 移动到敌人
 * - 攻击敌人
 * - 巡逻
 */

namespace GPGems.AI.Decision.GOAP.Examples;

/// <summary>
/// 士兵特工 AI 示例
/// </summary>
public class SoldierAgent
{
    public GoapAgent Agent { get; }

    public SoldierAgent()
    {
        Agent = new GoapAgent("Soldier");
        SetupGoals();
        SetupActions();
        SetupInitialState();
    }

    /// <summary>
    /// 设置初始状态
    /// </summary>
    private void SetupInitialState()
    {
        Agent.UpdateState("is_alive", true);
        Agent.UpdateState("health", 100f);
        Agent.UpdateState("ammo", 50f);
        Agent.UpdateState("has_weapon", true);
        Agent.UpdateState("enemy_visible", true);
        Agent.UpdateState("enemy_alive", true);
        Agent.UpdateState("enemy_near", false);
        Agent.UpdateState("in_cover", false);
        Agent.UpdateState("was_hit", false);
    }

    /// <summary>
    /// 设置目标（按优先级从高到低）
    /// </summary>
    private void SetupGoals()
    {
        // 目标 1: 保持存活（血量 > 70）
        var stayAlive = new GoapGoal("StayAlive")
            .AddCondition("is_alive", true)
            .SetPriorityCalculator(state =>
            {
                var health = state.Get<float>("health");
                return 100f - health; // 血量越低优先级越高
            });
        Agent.AddGoal(stayAlive);

        // 目标 2: 保持弹药充足
        var keepAmmo = new GoapGoal("KeepAmmo")
            .AddCondition("ammo", 50f)
            .SetPriorityCalculator(state =>
            {
                var ammo = state.Get<float>("ammo");
                return (50f - ammo) * 1.5f; // 弹药越少优先级越高
            });
        Agent.AddGoal(keepAmmo);

        // 目标 3: 杀死敌人
        var killEnemy = new GoapGoal("KillEnemy")
            .AddCondition("enemy_alive", false)
            .SetPriority(30f); // 基础优先级
        Agent.AddGoal(killEnemy);

        // 目标 4: 在安全区（掩体后）
        var beSafe = new GoapGoal("BeSafe")
            .AddCondition("in_cover", true)
            .SetPriorityCalculator(state =>
            {
                var wasHit = state.Get<bool>("was_hit");
                var enemyNear = state.Get<bool>("enemy_near");
                var priority = 0f;
                if (wasHit) priority += 40f;
                if (enemyNear) priority += 20f;
                return priority;
            });
        Agent.AddGoal(beSafe);
    }

    /// <summary>
    /// 设置动作
    /// </summary>
    private void SetupActions()
    {
        // 动作：寻找掩体
        var takeCover = new GoapAction("TakeCover")
            .AddPrecondition("enemy_visible", true)
            .AddEffect("in_cover", true)
            .SetCost(2f)
            .SetExecute(state =>
            {
                Console.WriteLine("  🛡️  移动到掩体后...");
                return new WorldState(state)
                    .Set("in_cover", true)
                    .Set("was_hit", false);
            });
        Agent.AddAction(takeCover);

        // 动作：治疗（需要在掩体后）
        var heal = new GoapAction("Heal")
            .AddPrecondition("in_cover", true)
            .AddEffect("health", 100f)
            .SetCost(5f)
            .SetAvailable(state => state.Get<float>("health") < 70f)
            .SetExecute(state =>
            {
                Console.WriteLine("  💊  使用治疗包...");
                return new WorldState(state)
                    .Set("health", 100f);
            });
        Agent.AddAction(heal);

        // 动作：装弹
        var reload = new GoapAction("Reload")
            .AddPrecondition("in_cover", true)
            .AddEffect("ammo", 100f)
            .SetCost(3f)
            .SetAvailable(state => state.Get<float>("ammo") < 20f)
            .SetExecute(state =>
            {
                Console.WriteLine("  🔄  装填弹药...");
                return new WorldState(state)
                    .Set("ammo", 100f);
            });
        Agent.AddAction(reload);

        // 动作：移动到敌人
        var moveToEnemy = new GoapAction("MoveToEnemy")
            .AddPrecondition("enemy_visible", true)
            .AddPrecondition("has_weapon", true)
            .AddEffect("enemy_near", true)
            .SetCost(4f)
            .SetExecute(state =>
            {
                Console.WriteLine("  🏃  接近敌人...");
                return new WorldState(state)
                    .Set("enemy_near", true);
            });
        Agent.AddAction(moveToEnemy);

        // 动作：攻击敌人（需要近距离且有弹药）
        var attack = new GoapAction("AttackEnemy")
            .AddPrecondition("enemy_near", true)
            .AddPrecondition("enemy_alive", true)
            .AddPrecondition("has_weapon", true)
            .AddEffect("enemy_alive", false)
            .SetCost(1f)
            .SetAvailable(state => state.Get<float>("ammo") > 10f)
            .SetExecute(state =>
            {
                var ammo = state.Get<float>("ammo");
                Console.WriteLine($"  ⚔️  开火攻击！剩余弹药: {ammo - 10}");
                return new WorldState(state)
                    .Set("enemy_alive", false)
                    .Set("ammo", ammo - 10f);
            });
        Agent.AddAction(attack);

        // 动作：巡逻（默认兜底行为）
        var patrol = new GoapAction("Patrol")
            .AddEffect("patrolling", true)
            .SetCost(10f)
            .SetExecute(state =>
            {
                Console.WriteLine("  🚶  巡逻中...");
                return state;
            });
        Agent.AddAction(patrol);
    }

    /// <summary>
    /// 模拟受到伤害
    /// </summary>
    public void TakeDamage(float amount)
    {
        var currentHealth = Agent.CurrentState.Get<float>("health");
        var newHealth = Math.Max(0f, currentHealth - amount);
        Agent.UpdateState("health", newHealth);
        Agent.UpdateState("was_hit", true);

        if (newHealth <= 0f)
        {
            Agent.UpdateState("is_alive", false);
            Console.WriteLine("  💀  阵亡！");
        }
        else
        {
            Console.WriteLine($"  💥  受到 {amount} 点伤害！剩余血量: {newHealth}");
        }
    }

    /// <summary>
    /// 模拟弹药消耗
    /// </summary>
    public void ConsumeAmmo(float amount)
    {
        var currentAmmo = Agent.CurrentState.Get<float>("ammo");
        Agent.UpdateState("ammo", Math.Max(0f, currentAmmo - amount));
    }

    /// <summary>
    /// 更新并执行一帧
    /// </summary>
    public void Update()
    {
        Agent.Update();
    }

    /// <summary>
    /// 运行完整模拟
    /// </summary>
    public void RunSimulation(int steps = 10)
    {
        Console.WriteLine("=== GOAP 士兵特工模拟 ===");
        Console.WriteLine();

        for (var i = 0; i < steps; i++)
        {
            Console.WriteLine($"--- Step {i + 1} ---");
            Update();

            // 随机事件模拟
            if (i == 2)
            {
                Console.WriteLine();
                TakeDamage(40f); // 第 3 步受到伤害
            }
            if (i == 5)
            {
                Console.WriteLine();
                ConsumeAmmo(35f); // 第 6 步消耗大量弹药
                Agent.AbortPlan(); // 强制重新规划
            }

            Console.WriteLine();
            Thread.Sleep(200);
        }

        Console.WriteLine();
        Console.WriteLine(Agent.GetDebugInfo());
    }
}
