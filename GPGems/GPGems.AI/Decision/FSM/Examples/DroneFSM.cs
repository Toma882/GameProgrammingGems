/*
 * GPGems.AI - FSM Example: Drone
 * 无人机状态机：巡逻 -> 追击 -> 攻击 -> 返回巡逻
 * 移植自 Game Programming Gems 1 - Section 00 Rabin
 */

using GPGems.AI.Decision.Blackboards;
using GPGems.Core.Messages;

namespace GPGems.AI.Decision.FSM.Examples;

/// <summary>
/// 无人机自定义消息类型
/// </summary>
public static class DroneMessages
{
    public const string EnemySpotted = "Drone.EnemySpotted";
    public const string EnemyLost = "Drone.EnemyLost";
    public const string InAttackRange = "Drone.InAttackRange";
    public const string OutOfAttackRange = "Drone.OutOfAttackRange";
    public const string LowAmmo = "Drone.LowAmmo";
}

/// <summary>
/// 无人机：巡逻状态
/// </summary>
public class DronePatrolState : StateBase
{
    public DronePatrolState() : base("Patrol")
    {
    }

    public override void OnEnter(Blackboard context, IState? previousState)
    {
        base.OnEnter(context, previousState);
        context.Set("drone_mode", "Patrolling");
        context.Set("patrol_waypoint", 0);
    }

    public override void OnUpdate(Blackboard context)
    {
        base.OnUpdate(context);

        // 模拟巡逻移动
        var wp = context.GetOrDefault("patrol_waypoint", 0);
        context.Set("patrol_waypoint", (wp + 1) % 5);

        // 随机探测敌人（10%概率）
        if (Random.Shared.NextDouble() < 0.1)
        {
            var router = context.GetOrDefault<MessageRouter?>("message_router", null);
            router?.Broadcast(new Message(DroneMessages.EnemySpotted, "EnemyUnit1"));
        }
    }

    public override MessageResult HandleMessage(Message message, Blackboard context)
    {
        if (message.Type == DroneMessages.EnemySpotted)
        {
            context.Set("target_enemy", message.GetData<string>());
            return MessageResult.Handled;
        }
        return base.HandleMessage(message, context);
    }
}

/// <summary>
/// 无人机：追击状态
/// </summary>
public class DroneChaseState : StateBase
{
    public DroneChaseState() : base("Chase")
    {
    }

    public override void OnEnter(Blackboard context, IState? previousState)
    {
        base.OnEnter(context, previousState);
        context.Set("drone_mode", "Chasing");
        context.Set("chase_speed", 100f);
    }

    public override void OnUpdate(Blackboard context)
    {
        base.OnUpdate(context);

        var distance = context.GetOrDefault("enemy_distance", 100f);

        // 模拟追击靠近敌人
        distance = Math.Max(0, distance - 10);
        context.Set("enemy_distance", distance);

        // 进入攻击范围
        if (distance < 30f)
        {
            var router = context.GetOrDefault<MessageRouter?>("message_router", null);
            router?.Broadcast(new Message(DroneMessages.InAttackRange));
        }
    }
}

/// <summary>
/// 无人机：攻击状态
/// </summary>
public class DroneAttackState : StateBase
{
    public DroneAttackState() : base("Attack")
    {
    }

    public override void OnEnter(Blackboard context, IState? previousState)
    {
        base.OnEnter(context, previousState);
        context.Set("drone_mode", "Attacking");
        context.Set("attack_cooldown", 0f);
    }

    public override void OnUpdate(Blackboard context)
    {
        base.OnUpdate(context);

        var ammo = context.GetOrDefault("ammo", 100);
        if (ammo <= 0)
        {
            var router = context.GetOrDefault<MessageRouter?>("message_router", null);
            router?.Broadcast(new Message(DroneMessages.LowAmmo));
            return;
        }

        // 模拟射击
        context.Set("ammo", ammo - 5);
        context.Set("attack_cooldown", 0.5f);

        // 随机敌人逃跑
        if (Random.Shared.NextDouble() < 0.05)
        {
            context.Set("enemy_distance", 100f);
            var router = context.GetOrDefault<MessageRouter?>("message_router", null);
            router?.Broadcast(new Message(DroneMessages.OutOfAttackRange));
        }
    }
}

/// <summary>
/// 无人机状态机构建器
/// </summary>
public static class DroneFSMBuilder
{
    public static StateMachine Build(string droneName, MessageRouter router)
    {
        // 创建状态
        var patrol = new DronePatrolState();
        var chase = new DroneChaseState();
        var attack = new DroneAttackState();

        // 创建状态机
        var fsm = new StateMachine(droneName);

        // 设置初始状态
        fsm.SetInitialState(patrol);

        // 设置上下文
        fsm.Context.Set("ammo", 100);
        fsm.Context.Set("enemy_distance", 100f);
        fsm.Context.Set("message_router", router);

        // 注册到路由器
        router.RegisterReceiver(fsm);

        // 定义转换
        // 发现敌人 -> 追击
        fsm.AddTransitions(fsm.From(patrol)
            .OnMessage(chase, DroneMessages.EnemySpotted));

        // 进入攻击范围 -> 攻击
        fsm.AddTransitions(fsm.From(chase)
            .OnMessage(attack, DroneMessages.InAttackRange));

        // 攻击中丢失目标 -> 追击
        fsm.AddTransitions(fsm.From(attack)
            .OnMessage(chase, DroneMessages.OutOfAttackRange));

        // 弹药耗尽 -> 返回巡逻
        fsm.AddTransitions(fsm.From(attack)
            .OnMessage(patrol, DroneMessages.LowAmmo));

        return fsm;
    }
}
