/*
 * GPGems.AI - 融合决策演示
 * 运行并展示 NPC 真实日常作息系统
 */

namespace GPGems.AI.Decision.Integration;

public static class IntegrationDemo
{
    public static void Run()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║          GPGems.AI - NPC 真实日常作息系统                       ║");
        Console.WriteLine("║  FSM 管状态 + 行为树管编排 + 专业算法管决策 + 黑板管数据        ║");
        Console.WriteLine("║  状态: 起床/洗漱/通勤/工作/午休/休闲/睡觉 + 周末模式            ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        // 创建两个不同性格的 NPC 做对比
        var npc1 = new SmartNpc("小明", new PersonalityTraits
        {
            Conscientiousness = 0.8f,  // 责任心强
            Extraversion = 0.3f,       // 内向
            Neuroticism = 0.2f,        // 情绪稳定
            Routineness = 0.9f         // 作息规律
        });

        var npc2 = new SmartNpc("小强", new PersonalityTraits
        {
            Conscientiousness = 0.3f,  // 爱摸鱼
            Extraversion = 0.8f,       // 外向
            Neuroticism = 0.7f,        // 容易焦虑
            Routineness = 0.2f         // 作息随意
        });

        Console.WriteLine($"创建 NPC: {npc1.Name} (责任心强/内向/情绪稳定)");
        Console.WriteLine($"创建 NPC: {npc2.Name} (爱摸鱼/外向/容易焦虑)");
        Console.WriteLine();
        Console.WriteLine("按任意键开始模拟，按 Q 退出");
        Console.WriteLine("按 1/2 切换观察对象，按 D 显示详细面板");
        Console.WriteLine();

        Console.ReadKey(true);

        var activeNpc = npc1;
        var step = 0;

        while (true)
        {
            step++;
            var hour = activeNpc.Blackboard.GetOrDefault("hour_of_day", 7f);
            var isWeekend = activeNpc.Blackboard.GetOrDefault("is_weekend", false);
            var dayOfWeek = activeNpc.Blackboard.GetOrDefault("day_of_week", 1f);

            Console.WriteLine($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            Console.WriteLine($"  第 {step} 步 | 星期{GetDayName(dayOfWeek)} " +
                              $"{hour:F1}:00 {(isWeekend ? "🎉 周末" : "📆 工作日")}");
            Console.WriteLine($"  观察对象: {activeNpc.Name}");
            Console.WriteLine($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

            // 更新 NPC
            activeNpc.Update(1f);

            // 摘要
            Console.WriteLine();
            Console.WriteLine($"  💬 {activeNpc.Blackboard.GetOrDefault("current_thought", "...")}");
            Console.WriteLine($"  精力: {activeNpc.Blackboard.GetOrDefault("energy", 50f):F0} | " +
                              $"饱腹: {activeNpc.Blackboard.GetOrDefault("hunger", 30f):F0} | " +
                              $"压力: {activeNpc.Blackboard.GetOrDefault("stress", 10f):F0}");
            Console.WriteLine($"  心情: {activeNpc.Blackboard.GetOrDefault("mood", 50f):F0} | " +
                              $"疲劳: {activeNpc.Blackboard.GetOrDefault("fatigue", 20f):F0} | " +
                              $"膀胱: {activeNpc.Blackboard.GetOrDefault("bladder", 20f):F0}");
            Console.WriteLine($"  社交: {activeNpc.Blackboard.GetOrDefault("social", 40f):F0} | " +
                              $"娱乐: {activeNpc.Blackboard.GetOrDefault("fun", 30f):F0} | " +
                              $"卫生: {activeNpc.Blackboard.GetOrDefault("hygiene", 70f):F0}");

            // 效用选择
            if (activeNpc.UtilityReasoner.CurrentAction != null)
            {
                Console.WriteLine($"  当前行为: {activeNpc.UtilityReasoner.CurrentAction.Name} " +
                                  $"(得分: {activeNpc.UtilityReasoner.CurrentAction.LastScore:F2})");
            }

            Console.WriteLine();

            // 键盘输入
            if (Console.KeyAvailable)
            {
                var key = Console.ReadKey(true).Key;
                switch (key)
                {
                    case ConsoleKey.Q:
                        Console.WriteLine("演示结束。");
                        return;

                    case ConsoleKey.D1:
                        activeNpc = npc1;
                        Console.WriteLine($"  切换到观察: {npc1.Name}");
                        Console.WriteLine($"  (责任心:{npc1.Personality.Conscientiousness:F1} " +
                                          $"外向:{npc1.Personality.Extraversion:F1} " +
                                          $"神经质:{npc1.Personality.Neuroticism:F1})");
                        Console.ReadKey(true);
                        break;

                    case ConsoleKey.D2:
                        activeNpc = npc2;
                        Console.WriteLine($"  切换到观察: {npc2.Name}");
                        Console.WriteLine($"  (责任心:{npc2.Personality.Conscientiousness:F1} " +
                                          $"外向:{npc2.Personality.Extraversion:F1} " +
                                          $"神经质:{npc2.Personality.Neuroticism:F1})");
                        Console.ReadKey(true);
                        break;

                    case ConsoleKey.D:
                        Console.WriteLine(activeNpc.GetDebugInfo());
                        Console.WriteLine();
                        Console.WriteLine("按任意键继续...");
                        Console.ReadKey(true);
                        break;
                }
            }

            // 每 15 步暂停
            if (step % 15 == 0)
            {
                Console.WriteLine("按任意键继续，Q 退出...");
                if (Console.ReadKey(true).Key == ConsoleKey.Q)
                    break;
            }
            else
            {
                Thread.Sleep(400);
            }
        }
    }

    public static void RunQuickTest()
    {
        Console.WriteLine("运行日常作息快速测试...");
        var npc = new SmartNpc("测试员");

        for (var i = 0; i < 100; i++)
        {
            npc.Update(1f);
            if (i % 10 == 0)
            {
                Console.WriteLine($"Step {i}: State={npc.CurrentState}, " +
                                  $"Time={npc.Blackboard.GetOrDefault("hour_of_day", 7f):F1}, " +
                                  $"Energy={npc.Blackboard.GetOrDefault("energy", 50f):F0}, " +
                                  $"Mood={npc.Blackboard.GetOrDefault("mood", 50f):F0}");
            }
        }
        Console.WriteLine("测试完成！");
    }

    private static string GetDayName(float day) => day switch
    {
        0f => "日",
        1f => "一",
        2f => "二",
        3f => "三",
        4f => "四",
        5f => "五",
        6f => "六",
        _ => "?"
    };
}
