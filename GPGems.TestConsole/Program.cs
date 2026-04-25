using GPGems.AI.Decision.Integration;

Console.WriteLine("=== 融合决策系统快速测试 ===\n");

var npc = new SmartNpc("小明");

Console.WriteLine($"NPC: {npc.Name}");
Console.WriteLine($"初始 FSM 状态: {npc.CurrentState}");
Console.WriteLine($"行为树: {npc.CurrentBehaviorTree?.Name ?? "None"}");
Console.WriteLine($"效用动作数: {npc.UtilityReasoner.Actions.Count}");
Console.WriteLine($"GOAP 动作数: {npc.GoapAgent.Actions.Count}");
Console.WriteLine($"GOAP 目标数: {npc.GoapAgent.Goals.Count}");
Console.WriteLine();

Console.WriteLine("运行 20 帧模拟...\n");

for (var i = 0; i < 20; i++)
{
    npc.Update(1f);

    var hour = npc.Blackboard.GetOrDefault("hour_of_day", 0f);
    var energy = npc.Blackboard.GetOrDefault("energy", 0f);
    var hunger = npc.Blackboard.GetOrDefault("hunger", 0f);
    var stress = npc.Blackboard.GetOrDefault("stress", 0f);
    var action = npc.UtilityReasoner.CurrentAction?.Name ?? "None";

    Console.WriteLine($"Frame {i,2}: Time={hour:F1}, State={npc.CurrentState,-15}, " +
                      $"Energy={energy:F0}, Hunger={hunger:F0}, Stress={stress:F0}, Action={action}");
}

Console.WriteLine("\n=== 效用系统最终得分 ===");
Console.WriteLine(npc.UtilityReasoner.GetDebugInfo());

Console.WriteLine("\n=== 测试成功完成 ===");
