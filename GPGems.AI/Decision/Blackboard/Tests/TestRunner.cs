/*
 * GPGems.AI - Blackboard Test Runner
 * 简单的测试运行入口
 */

using GPGems.AI.Decision.Blackboards.Tests;

namespace GPGems.AI.Decision.Blackboards.Tests;

public static class TestRunner
{
    /// <summary>运行黑板系统所有测试</summary>
    /// <returns>是否全部通过</returns>
    public static bool RunBlackboardTests()
    {
        return BlackboardTests.RunAllTests();
    }

    /// <summary>快速演示黑板系统的主要功能</summary>
    public static void DemoBlackboardFeatures()
    {
        Console.WriteLine();
        Console.WriteLine("=== Blackboard Feature Demo ===");
        Console.WriteLine();

        var bb = new Blackboard("Demo");

        // 1. 基本读写
        Console.WriteLine("1. Basic Read/Write:");
        bb.Set("player_health", 100, writer: "GameEngine");
        bb.Set("player_name", "Hero123");
        bb.Set("is_alive", true);
        Console.WriteLine($"   Health: {bb.Get<int>("player_health")}");
        Console.WriteLine($"   Name: {bb.Get<string>("player_name")}");
        Console.WriteLine($"   IsAlive: {bb.Get<bool>("is_alive")}");
        Console.WriteLine();

        // 2. 观察者模式
        Console.WriteLine("2. Observer Pattern:");
        using var subscription = bb.Subscribe<int>("player_health", args =>
        {
            Console.WriteLine($"   NOTIFIED: Health changed from {args.OldValue} to {args.NewValue}");
        });
        bb.Set("player_health", 80);
        bb.Set("player_health", 50);
        Console.WriteLine();

        // 3. TTL 过期
        Console.WriteLine("3. TTL Expiration (simulating time):");
        var testTime = 0f;
        BlackboardEntry.CurrentTimeProvider = () => testTime;

        bb.Set("temp_buff", "active", ttl: 5f);
        Console.WriteLine($"   Time=0: Has 'temp_buff' = {bb.HasKey("temp_buff")}");

        testTime = 3f;
        Console.WriteLine($"   Time=3: Has 'temp_buff' = {bb.HasKey("temp_buff")}");

        testTime = 6f;
        Console.WriteLine($"   Time=6: Has 'temp_buff' = {bb.HasKey("temp_buff")} (expired!)");

        // 恢复默认时间
        BlackboardEntry.CurrentTimeProvider = () =>
            (float)(DateTime.UtcNow - DateTime.UnixEpoch).TotalSeconds;
        Console.WriteLine();

        // 4. 原子更新
        Console.WriteLine("4. Atomic Update:");
        bb.Set("score", 0);
        bb.Update<int>("score", old => old + 100);
        bb.Update<int>("score", old => old + 250);
        Console.WriteLine($"   Final Score: {bb.Get<int>("score")}");
        Console.WriteLine();

        // 5. 调试输出
        Console.WriteLine("5. Debug Dump:");
        Console.WriteLine(bb.Dump());
        Console.WriteLine();

        Console.WriteLine("=== Demo Complete ===");
    }
}
