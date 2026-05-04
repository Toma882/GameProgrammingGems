/*
 * GPGems.AI - Blackboard System Tests
 * 自包含测试，无需外部测试框架
 */

using System.Diagnostics;

namespace GPGems.AI.Decision.Blackboards.Tests;

/// <summary>
/// 黑板系统自测
/// </summary>
public static class BlackboardTests
{
    /// <summary>运行所有测试</summary>
    public static bool RunAllTests()
    {
        Console.WriteLine("=== Running Blackboard Tests ===");
        Console.WriteLine();

        var passed = 0;
        var total = 0;

        RunTest("Basic_Read_Write", TestBasicReadWrite, ref passed, ref total);
        RunTest("Type_Safety", TestTypeSafety, ref passed, ref total);
        RunTest("GetOrDefault", TestGetOrDefault, ref passed, ref total);
        RunTest("Observer_Notification", TestObserverNotification, ref passed, ref total);
        RunTest("TTL_Expiration", TestTTLExpiration, ref passed, ref total);
        RunTest("TrySetIfNotExists", TestTrySetIfNotExists, ref passed, ref total);
        RunTest("Atomic_Update", TestAtomicUpdate, ref passed, ref total);
        RunTest("Remove_Clear", TestRemoveClear, ref passed, ref total);
        RunTest("Unsubscribe", TestUnsubscribe, ref passed, ref total);
        RunTest("Multiple_Subscribers", TestMultipleSubscribers, ref passed, ref total);

        Console.WriteLine();
        Console.WriteLine($"=== Result: {passed}/{total} PASSED ===");

        return passed == total;
    }

    private static void RunTest(string name, Func<bool> test, ref int passed, ref int total)
    {
        total++;
        try
        {
            if (test())
            {
                Console.WriteLine($"✅ {name}");
                passed++;
            }
            else
            {
                Console.WriteLine($"❌ {name} - FAILED");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ {name} - EXCEPTION: {ex.Message}");
        }
    }

    #region Test Cases

    private static bool TestBasicReadWrite()
    {
        var bb = new Blackboard("Test", enableAutoCleanup: false);
        bb.Set("health", 100);
        bb.Set("name", "Player1");
        bb.Set("isAlive", true);

        return bb.Get<int>("health") == 100
            && bb.Get<string>("name") == "Player1"
            && bb.Get<bool>("isAlive") == true
            && bb.Count == 3;
    }

    private static bool TestTypeSafety()
    {
        var bb = new Blackboard("Test", enableAutoCleanup: false);
        bb.Set("value", 42);

        try
        {
            bb.Set("value", "string"); // 类型不匹配应该抛出
            return false;
        }
        catch (InvalidCastException)
        {
            return true;
        }
    }

    private static bool TestGetOrDefault()
    {
        var bb = new Blackboard("Test", enableAutoCleanup: false);

        return bb.GetOrDefault("nonexistent", 999) == 999
            && bb.GetOrDefault<string>("nonexistent") == null;
    }

    private static bool TestObserverNotification()
    {
        var bb = new Blackboard("Test", enableAutoCleanup: false);
        var notified = false;
        int newValue = 0;

        bb.Subscribe<int>("counter", args =>
        {
            notified = true;
            newValue = args.NewValue;
        });

        bb.Set("counter", 100);

        return notified && newValue == 100;
    }

    private static bool TestTTLExpiration()
    {
        // 使用可控制的时间提供器
        var testTime = 0f;
        BlackboardEntry.CurrentTimeProvider = () => testTime;

        try
        {
            var bb = new Blackboard("Test", enableAutoCleanup: false);
            bb.Set("temp", "value", ttl: 5f); // 5秒过期

            if (!bb.HasKey("temp")) return false;
            if (!bb.TryGetValue<string>("temp", out _)) return false;

            testTime = 3f; // 3秒后，还没过期
            if (!bb.HasKey("temp")) return false;

            testTime = 6f; // 6秒后，应该过期
            return !bb.HasKey("temp");
        }
        finally
        {
            // 恢复默认时间提供器
            BlackboardEntry.CurrentTimeProvider = () =>
                (float)(DateTime.UtcNow - DateTime.UnixEpoch).TotalSeconds;
        }
    }

    private static bool TestTrySetIfNotExists()
    {
        var bb = new Blackboard("Test", enableAutoCleanup: false);

        var result1 = bb.TrySetIfNotExists("key", "first");
        var result2 = bb.TrySetIfNotExists("key", "second");

        return result1 == true
            && result2 == false
            && bb.Get<string>("key") == "first";
    }

    private static bool TestAtomicUpdate()
    {
        var bb = new Blackboard("Test", enableAutoCleanup: false);
        bb.Set("counter", 0);

        bb.Update<int>("counter", old => old + 5);
        bb.Update<int>("counter", old => old + 3);

        return bb.Get<int>("counter") == 8;
    }

    private static bool TestRemoveClear()
    {
        var bb = new Blackboard("Test", enableAutoCleanup: false);
        bb.Set("a", 1);
        bb.Set("b", 2);
        bb.Set("c", 3);

        var removeResult = bb.Remove("b");
        if (!removeResult || bb.Count != 2) return false;

        bb.Clear();
        return bb.Count == 0;
    }

    private static bool TestUnsubscribe()
    {
        var bb = new Blackboard("Test", enableAutoCleanup: false);
        var notifyCount = 0;

        var token = bb.Subscribe<int>("key", args => notifyCount++);

        bb.Set("key", 1);
        if (notifyCount != 1) return false;

        token.Dispose();
        bb.Set("key", 2);

        return notifyCount == 1; // 取消订阅后不应该再收到通知
    }

    private static bool TestMultipleSubscribers()
    {
        var bb = new Blackboard("Test", enableAutoCleanup: false);
        var count1 = 0;
        var count2 = 0;

        bb.Subscribe<int>("key", args => count1++);
        bb.Subscribe<int>("key", args => count2++);

        bb.Set("key", 42);

        return count1 == 1 && count2 == 1;
    }

    #endregion
}
