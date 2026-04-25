/*
 * 快速测试：融合决策系统
 */

using GPGems.AI.Decision.Integration;

Console.WriteLine("=== 融合决策系统测试 ===\n");

var npc = new SmartNpc("小明");

Console.WriteLine($"NPC: {npc.Name}");
Console.WriteLine($"初始状态: {npc.CurrentState}");
Console.WriteLine();

// 运行 10 帧
for (var i = 0; i < 10; i++)
{
    npc.Update(1f);

    var hour = npc.Blackboard.GetOrDefault("hour_of_day", 0f);
    var energy = npc.Blackboard.GetOrDefault("energy", 0f);
    var stress = npc.Blackboard.GetOrDefault("stress", 0f);
    var action = npc.UtilityReasoner.CurrentAction?.Name ?? "None";

    Console.WriteLine($"Frame {i}: Time={hour:F1}, State={npc.CurrentState}, " +
                      $"Energy={energy:F0}, Stress={stress:F0}, Action={action}");
}

Console.WriteLine("\n=== 测试完成 ===");
