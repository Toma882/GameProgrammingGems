/* 有限状态机示例：NPC AI
 * 演示巡逻 -> 追击 -> 攻击 的经典FSM模式
 */

namespace GPGems.AI.FSM;

/// <summary>
/// NPC 实体 - 使用FSM驱动的游戏AI示例
/// </summary>
public class NpcEntity
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Health { get; set; } = 100;
    public double Speed { get; set; } = 2;
    public double AttackRange { get; set; } = 30;
    public double DetectionRange { get; set; } = 150;

    public bool IsAlive => Health > 0;

    private readonly Random _rand = new();

    public NpcEntity(double x, double y)
    {
        X = x;
        Y = y;
    }

    public void Patrol()
    {
        X += (_rand.NextDouble() - 0.5) * Speed;
        Y += (_rand.NextDouble() - 0.5) * Speed;
    }

    public void MoveToward(NpcEntity target)
    {
        double dx = target.X - X;
        double dy = target.Y - Y;
        double dist = Math.Sqrt(dx * dx + dy * dy);
        if (dist > 0)
        {
            X += dx / dist * Speed * 1.5;
            Y += dy / dist * Speed * 1.5;
        }
    }

    public void Attack(NpcEntity target)
    {
        target.Health -= 5;
    }

    public double DistanceTo(NpcEntity other)
    {
        double dx = other.X - X;
        double dy = other.Y - Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}

/// <summary>
/// 创建NPC FSM的工厂
/// 构建 巡逻(Patrol) -> 追击(Chase) -> 攻击(Attack) 的经典状态机
/// </summary>
public static class NpcFsmFactory
{
    public static FiniteStateMachine CreatePatrolFSM(NpcEntity npc, NpcEntity target)
    {
        var patrol = new FsmState("巡逻")
        {
            OnUpdate = () => npc.Patrol()
        };

        var chase = new FsmState("追击")
        {
            OnEnter = () => { /* 发现目标 */ },
            OnUpdate = () => npc.MoveToward(target)
        };

        var attack = new FsmState("攻击")
        {
            OnEnter = () => { /* 进入攻击 */ },
            OnUpdate = () => npc.Attack(target)
        };

        var dead = new FsmState("死亡")
        {
            OnEnter = () => { /* 播放死亡动画 */ }
        };

        var fsm = new FiniteStateMachine("NPC_AI", patrol);
        fsm.AddState(chase).AddState(attack).AddState(dead);

        // 巡逻 -> 追击：发现目标在感知范围内
        fsm.AddTransition(patrol, chase,
            () => npc.DistanceTo(target) < npc.DetectionRange,
            "发现目标");

        // 追击 -> 攻击：进入攻击范围
        fsm.AddTransition(chase, attack,
            () => npc.DistanceTo(target) < npc.AttackRange,
            "进入攻击");

        // 攻击 -> 追击：目标逃出攻击范围
        fsm.AddTransition(attack, chase,
            () => npc.DistanceTo(target) > npc.AttackRange,
            "目标逃跑");

        // 追击 -> 巡逻：目标超出感知范围
        fsm.AddTransition(chase, patrol,
            () => npc.DistanceTo(target) > npc.DetectionRange,
            "丢失目标");

        // 任意状态 -> 死亡：生命值为0
        fsm.AddTransition(patrol, dead, () => !npc.IsAlive, "死亡");
        fsm.AddTransition(chase, dead, () => !npc.IsAlive, "死亡");
        fsm.AddTransition(attack, dead, () => !npc.IsAlive, "死亡");

        return fsm;
    }
}
