using GPGems.Core;

namespace GPGems.TestConsole;

/// <summary>
/// CommunicationBus 全面测试
/// 测试所有频道类型和功能（参考 Lua 版本的测试设计）
/// </summary>
public static class CommunicationBusTests
{
    private static int _totalTests = 0;
    private static int _passedTests = 0;
    private static int _failedTests = 0;
    private static readonly List<string> _errors = new();

    public static void RunAllTests()
    {
        Console.WriteLine("\n" + new string('=', 70));
        Console.WriteLine("  CommunicationBus 通讯总线全面测试");
        Console.WriteLine(new string('=', 70));

        _totalTests = 0;
        _passedTests = 0;
        _failedTests = 0;
        _errors.Clear();

        // 每次测试前清空
        CommunicationBus.Instance.Clear();

        RunEventChannelTests();
        RunMessageChannelTests();
        RunPushChannelTests();
        RunQueryChannelTests();
        RunComprehensiveTests();
        RunBoundaryTests();

        PrintSummary();
    }

    #region 测试辅助方法

    private static void Assert(bool condition, string message)
    {
        _totalTests++;
        if (condition)
        {
            _passedTests++;
            Console.WriteLine($"  ✅ PASS: {message}");
        }
        else
        {
            _failedTests++;
            _errors.Add(message);
            Console.WriteLine($"  ❌ FAIL: {message}");
        }
    }

    private static void AssertEquals<T>(T actual, T expected, string message)
    {
        var condition = EqualityComparer<T>.Default.Equals(actual, expected);
        Assert(condition, $"{message} (expected: {expected}, actual: {actual})");
    }

    private static void PrintSection(string title)
    {
        Console.WriteLine($"\n--- {title} ---");
    }

    #endregion

    #region 1. EventChannel 测试 - 发布-订阅模式

    private static void RunEventChannelTests()
    {
        PrintSection("EventChannel 测试 - 发布-订阅模式");
        CommunicationBus.Instance.Clear();

        // 1.1 基本事件订阅和发布
        {
            bool eventReceived = false;
            object? receivedData = null;

            CommunicationBus.Instance.Subscribe("TestEvent1", args =>
            {
                eventReceived = true;
                receivedData = args;
            });

            CommunicationBus.Instance.Publish("TestEvent1", "Hello Event");

            Assert(eventReceived, "EventChannel: 事件应该被接收");
            AssertEquals(receivedData, "Hello Event", "EventChannel: 接收的数据应该正确");
        }

        // 1.2 多个订阅者
        {
            int count1 = 0;
            int count2 = 0;

            CommunicationBus.Instance.Subscribe("TestEvent2", _ => count1++);
            CommunicationBus.Instance.Subscribe("TestEvent2", _ => count2++);

            CommunicationBus.Instance.Publish("TestEvent2", "Multi Subscribe");

            AssertEquals(count1, 1, "EventChannel: 订阅者1应该收到事件");
            AssertEquals(count2, 1, "EventChannel: 订阅者2应该收到事件");
        }

        // 1.3 取消订阅
        {
            int count = 0;
            Action<object?> handler = _ => count++;

            CommunicationBus.Instance.Subscribe("TestEvent3", handler);
            CommunicationBus.Instance.Publish("TestEvent3", "Before");
            AssertEquals(count, 1, "EventChannel: 取消订阅前应该收到事件");

            CommunicationBus.Instance.Unsubscribe("TestEvent3", handler);
            CommunicationBus.Instance.Publish("TestEvent3", "After");
            AssertEquals(count, 1, "EventChannel: 取消订阅后不应该收到事件");
        }

        // 1.4 异常处理（一个订阅者异常不影响其他）
        {
            int normalCount = 0;

            CommunicationBus.Instance.Subscribe("TestEvent4", _ =>
            {
                throw new InvalidOperationException("Test Exception");
            });
            CommunicationBus.Instance.Subscribe("TestEvent4", _ => normalCount++);

            CommunicationBus.Instance.Publish("TestEvent4", "Data");

            AssertEquals(normalCount, 1, "EventChannel: 一个订阅者异常不应该影响其他订阅者");
        }
    }

    #endregion

    #region 2. MessageChannel 测试 - 点对点模式

    private static void RunMessageChannelTests()
    {
        PrintSection("MessageChannel 测试 - 点对点模式");
        CommunicationBus.Instance.Clear();

        // 2.1 基本消息发送和接收
        {
            bool messageReceived = false;
            object? receivedData = null;

            CommunicationBus.Instance.RegisterMessageHandler("TestMessage1", args =>
            {
                messageReceived = true;
                receivedData = args;
            });

            CommunicationBus.Instance.SendMessage("TestMessage1", "Hello Message");

            Assert(messageReceived, "MessageChannel: 消息应该被接收");
            AssertEquals(receivedData, "Hello Message", "MessageChannel: 接收的数据应该正确");
        }

        // 2.2 点对点特性（后订阅覆盖前订阅）
        {
            int count1 = 0;
            int count2 = 0;

            CommunicationBus.Instance.RegisterMessageHandler("TestMessage2", _ => count1++);
            CommunicationBus.Instance.SendMessage("TestMessage2", "First");
            AssertEquals(count1, 1, "MessageChannel: 第一个订阅者应该收到消息");

            CommunicationBus.Instance.RegisterMessageHandler("TestMessage2", _ => count2++); // 覆盖第一个
            CommunicationBus.Instance.SendMessage("TestMessage2", "Second");
            AssertEquals(count1, 1, "MessageChannel: 第一个订阅者不应该再收到消息");
            AssertEquals(count2, 1, "MessageChannel: 第二个订阅者应该收到消息");
        }
    }

    #endregion

    #region 3. PushChannel 测试 - 数据推送模式（注入式处理）

    private static void RunPushChannelTests()
    {
        PrintSection("PushChannel 测试 - 数据推送模式");
        CommunicationBus.Instance.Clear();

        // 3.1 基本数据推送和处理
        {
            var processedData = new List<object>();
            var subscriber = new { Name = "TestSubscriber1" };

            // 创建处理器（无 IsDirty，默认总是处理）
            var handler = new { };

            // 定义处理函数映射
            var processFunctionMap = new Dictionary<string, Action<object>>
            {
                ["AttributeChange"] = data => processedData.Add(data)
            };

            // 注册处理器
            CommunicationBus.Instance.AddHandler(new ProgressHandlerContext
            {
                Subscriber = subscriber,
                Handler = handler,
                ProcessFunctionMap = processFunctionMap
            });

            // 推送数据（只积累，不立即处理）
            CommunicationBus.Instance.PushData(subscriber, "AttributeChange", new { AttrId = 1, Value = 100 });

            // 此时还未处理
            AssertEquals(processedData.Count, 0, "PushChannel: ProcessData 之前不应该处理数据");

            // 处理数据（批量处理）
            CommunicationBus.Instance.ProcessData(subscriber);

            AssertEquals(processedData.Count, 1, "PushChannel: ProcessData 之后应该处理数据");
        }

        // 3.2 多个数据类型处理
        {
            var attributeData = new List<object>();
            var skillData = new List<object>();
            var subscriber = new { Name = "TestSubscriber2" };

            var handler = new { };

            var processFunctionMap = new Dictionary<string, Action<object>>
            {
                ["AttributeChange"] = data => attributeData.Add(data),
                ["SkillUpgrade"] = data => skillData.Add(data)
            };

            CommunicationBus.Instance.AddHandler(new ProgressHandlerContext
            {
                Subscriber = subscriber,
                Handler = handler,
                ProcessFunctionMap = processFunctionMap
            });

            // 推送不同类型的数据
            CommunicationBus.Instance.PushData(subscriber, "AttributeChange", new { AttrId = 1 });
            CommunicationBus.Instance.PushData(subscriber, "SkillUpgrade", new { SkillId = 100 });
            CommunicationBus.Instance.PushData(subscriber, "AttributeChange", new { AttrId = 2 });

            // 处理数据
            CommunicationBus.Instance.ProcessData(subscriber);

            AssertEquals(attributeData.Count, 2, "PushChannel: 属性数据应该被处理2次");
            AssertEquals(skillData.Count, 1, "PushChannel: 技能数据应该被处理1次");
        }

        // 3.3 IsDirty 机制
        {
            int processedCount = 0;
            var subscriber = new { Name = "TestSubscriber3" };
            bool isDirtyFlag = false;

            // 带有 IsDirty 方法的处理器
            var handler = new
            {
                IsDirty = (Func<bool>)(() => isDirtyFlag),
                MarkDirty = (Action)(() => { })
            };

            var processFunctionMap = new Dictionary<string, Action<object>>
            {
                ["AttributeChange"] = _ => processedCount++
            };

            CommunicationBus.Instance.AddHandler(new ProgressHandlerContext
            {
                Subscriber = subscriber,
                Handler = handler,
                ProcessFunctionMap = processFunctionMap
            });

            // 推送数据但 isDirty 为 false
            CommunicationBus.Instance.PushData(subscriber, "AttributeChange", new { });
            CommunicationBus.Instance.ProcessData(subscriber);
            AssertEquals(processedCount, 0, "PushChannel: IsDirty=false 时不应该处理数据");

            // 设置 isDirty 为 true
            isDirtyFlag = true;
            CommunicationBus.Instance.ProcessData(subscriber);
            AssertEquals(processedCount, 1, "PushChannel: IsDirty=true 时应该处理数据");
        }

        // 3.4 多个订阅者独立数据队列
        {
            var subscriber1Data = new List<object>();
            var subscriber2Data = new List<object>();

            var subscriber1 = new { Name = "Subscriber1" };
            var subscriber2 = new { Name = "Subscriber2" };

            var handler1 = new { };
            var handler2 = new { };

            var processFunctionMap1 = new Dictionary<string, Action<object>>
            {
                ["AttributeChange"] = data => subscriber1Data.Add(data)
            };

            var processFunctionMap2 = new Dictionary<string, Action<object>>
            {
                ["AttributeChange"] = data => subscriber2Data.Add(data)
            };

            CommunicationBus.Instance.AddHandler(new ProgressHandlerContext
            {
                Subscriber = subscriber1,
                Handler = handler1,
                ProcessFunctionMap = processFunctionMap1
            });

            CommunicationBus.Instance.AddHandler(new ProgressHandlerContext
            {
                Subscriber = subscriber2,
                Handler = handler2,
                ProcessFunctionMap = processFunctionMap2
            });

            // 给不同订阅者推送数据
            CommunicationBus.Instance.PushData(subscriber1, "AttributeChange", new { Id = 1 });
            CommunicationBus.Instance.PushData(subscriber2, "AttributeChange", new { Id = 2 });

            // 分别处理
            CommunicationBus.Instance.ProcessData(subscriber1);
            CommunicationBus.Instance.ProcessData(subscriber2);

            AssertEquals(subscriber1Data.Count, 1, "PushChannel: 订阅者1应该只处理自己的数据");
            AssertEquals(subscriber2Data.Count, 1, "PushChannel: 订阅者2应该只处理自己的数据");
        }

        // 3.5 推送链测试 - 链式数据流（四个独立Handler）
        {
            bool equipProcessed = false;
            bool featProcessed = false;
            bool skillProcessed = false;
            bool effectProcessed = false;

            var subscriber = new { Name = "ChainSubscriber" };

            // Handler 1: 处理装备安装，触发专长推送
            var equipHandler = new { };
            var equipProcessMap = new Dictionary<string, Action<object>>
            {
                ["EquipInstalled"] = _ =>
                {
                    equipProcessed = true;
                    // 处理过程中推送新数据
                    CommunicationBus.Instance.PushData(subscriber, "FeatActivated", new { FeatId = 101 });
                }
            };

            // Handler 2: 处理专长安装，触发技能推送
            var featHandler = new { };
            var featProcessMap = new Dictionary<string, Action<object>>
            {
                ["FeatActivated"] = _ =>
                {
                    featProcessed = true;
                    CommunicationBus.Instance.PushData(subscriber, "SkillLearned", new { SkillId = 201 });
                }
            };

            // Handler 3: 处理技能数据，触发效果推送
            var skillHandler = new { };
            var skillProcessMap = new Dictionary<string, Action<object>>
            {
                ["SkillLearned"] = _ =>
                {
                    skillProcessed = true;
                    CommunicationBus.Instance.PushData(subscriber, "EffectApplied", new { EffectId = 301 });
                }
            };

            // Handler 4: 处理效果数据（链的终点）
            var effectHandler = new { };
            var effectProcessMap = new Dictionary<string, Action<object>>
            {
                ["EffectApplied"] = _ => effectProcessed = true
            };

            // 注册所有 Handler
            CommunicationBus.Instance.AddHandler(new ProgressHandlerContext
            {
                Subscriber = subscriber,
                Handler = equipHandler,
                ProcessFunctionMap = equipProcessMap
            });

            CommunicationBus.Instance.AddHandler(new ProgressHandlerContext
            {
                Subscriber = subscriber,
                Handler = featHandler,
                ProcessFunctionMap = featProcessMap
            });

            CommunicationBus.Instance.AddHandler(new ProgressHandlerContext
            {
                Subscriber = subscriber,
                Handler = skillHandler,
                ProcessFunctionMap = skillProcessMap
            });

            CommunicationBus.Instance.AddHandler(new ProgressHandlerContext
            {
                Subscriber = subscriber,
                Handler = effectHandler,
                ProcessFunctionMap = effectProcessMap
            });

            // 初始触发：推送装备安装
            CommunicationBus.Instance.PushData(subscriber, "EquipInstalled", new { EquipId = 1 });

            // 一次 ProcessData 应该处理整个推送链
            CommunicationBus.Instance.ProcessData(subscriber);

            Assert(equipProcessed, "PushChannel推送链: EquipInstalled 应该被处理");
            Assert(featProcessed, "PushChannel推送链: FeatActivated 应该被处理");
            Assert(skillProcessed, "PushChannel推送链: SkillLearned 应该被处理");
            Assert(effectProcessed, "PushChannel推送链: EffectApplied 应该被处理");
        }

        // 3.6 批量处理：一次处理多条同类型数据
        {
            var processedItems = new List<int>();
            var subscriber = new { Name = "BatchSubscriber" };

            var handler = new { };
            var processMap = new Dictionary<string, Action<object>>
            {
                ["ItemAdded"] = data => processedItems.Add((int)data)
            };

            CommunicationBus.Instance.AddHandler(new ProgressHandlerContext
            {
                Subscriber = subscriber,
                Handler = handler,
                ProcessFunctionMap = processMap
            });

            // 推送多条数据
            for (int i = 0; i < 5; i++)
            {
                CommunicationBus.Instance.PushData(subscriber, "ItemAdded", i);
            }

            // 一次处理所有
            CommunicationBus.Instance.ProcessData(subscriber);

            AssertEquals(processedItems.Count, 5, "PushChannel: 批量处理应该处理所有数据");
            AssertEquals(processedItems[0], 0, "PushChannel: 处理顺序应该正确");
            AssertEquals(processedItems[4], 4, "PushChannel: 处理顺序应该正确");
        }
    }

    #endregion

    #region 4. QueryChannel 测试 - 请求-响应模式

    private static void RunQueryChannelTests()
    {
        PrintSection("QueryChannel 测试 - 请求-响应模式");
        CommunicationBus.Instance.Clear();

        // 4.1 基本查询
        {
            var subscriber = new { Name = "QuerySubscriber1" };

            CommunicationBus.Instance.AddQueryDelegate(subscriber, "GetLevel", _ => 50);

            var result = CommunicationBus.Instance.QueryData<int>(subscriber, "GetLevel");

            AssertEquals(result, 50, "QueryChannel: 查询结果应该正确");
        }

        // 4.2 带参数的查询
        {
            var subscriber = new { Name = "QuerySubscriber2" };

            CommunicationBus.Instance.AddQueryDelegate(subscriber, "GetAttribute", args =>
            {
                int attrId = (int)args[0]!;
                var attributes = new Dictionary<int, int> { [1] = 100, [2] = 200 };
                return attributes.GetValueOrDefault(attrId, 0);
            });

            var result1 = CommunicationBus.Instance.QueryData<int>(subscriber, "GetAttribute", 1);
            var result2 = CommunicationBus.Instance.QueryData<int>(subscriber, "GetAttribute", 2);

            AssertEquals(result1, 100, "QueryChannel: 带参数的查询应该正确");
            AssertEquals(result2, 200, "QueryChannel: 带参数的查询应该正确");
        }

        // 4.3 查询不存在的委托
        {
            var subscriber = new { Name = "QuerySubscriber3" };
            var result = CommunicationBus.Instance.QueryData<int>(subscriber, "NonExistentQuery");
            AssertEquals(result, 0, "QueryChannel: 查询不存在的委托应该返回默认值");
        }
    }

    #endregion

    #region 5. 综合场景测试

    private static void RunComprehensiveTests()
    {
        PrintSection("综合场景测试");
        CommunicationBus.Instance.Clear();

        // 5.1 多个频道同时使用
        {
            int eventCount = 0;
            int queryResult = 0;
            int pushCount = 0;

            // Event
            CommunicationBus.Instance.Subscribe("ComprehensiveEvent", _ => eventCount++);

            // Query
            var querySubscriber = new { Name = "CompQuery" };
            CommunicationBus.Instance.AddQueryDelegate(querySubscriber, "GetAnswer", _ => 42);

            // Push
            var pushSubscriber = new { Name = "CompPush" };
            var pushHandler = new { };
            var pushProcessMap = new Dictionary<string, Action<object>>
            {
                ["DataReceived"] = _ => pushCount++
            };
            CommunicationBus.Instance.AddHandler(new ProgressHandlerContext
            {
                Subscriber = pushSubscriber,
                Handler = pushHandler,
                ProcessFunctionMap = pushProcessMap
            });

            // 触发所有类型
            CommunicationBus.Instance.Publish("ComprehensiveEvent", "Event");
            queryResult = CommunicationBus.Instance.QueryData<int>(querySubscriber, "GetAnswer");
            CommunicationBus.Instance.PushData(pushSubscriber, "DataReceived", new { });
            CommunicationBus.Instance.ProcessData(pushSubscriber);

            AssertEquals(eventCount, 1, "综合测试: Event 应该工作");
            AssertEquals(queryResult, 42, "综合测试: Query 应该工作");
            AssertEquals(pushCount, 1, "综合测试: Push 应该工作");
        }

        // 5.2 玩家属性更新场景（典型游戏应用）
        {
            var player = new { Name = "Player1" };
            var attributeValues = new Dictionary<string, int>();

            // 场景：属性变化推送 → UI 批量更新
            var uiHandler = new { };
            var uiProcessMap = new Dictionary<string, Action<object>>
            {
                ["HpChanged"] = data =>
                {
                    var hp = (int)data;
                    attributeValues["Hp"] = hp;
                },
                ["MpChanged"] = data =>
                {
                    var mp = (int)data;
                    attributeValues["Mp"] = mp;
                }
            };

            CommunicationBus.Instance.AddHandler(new ProgressHandlerContext
            {
                Subscriber = player,
                Handler = uiHandler,
                ProcessFunctionMap = uiProcessMap
            });

            // 战斗中多次属性变化
            CommunicationBus.Instance.PushData(player, "HpChanged", 80);
            CommunicationBus.Instance.PushData(player, "MpChanged", 50);
            CommunicationBus.Instance.PushData(player, "HpChanged", 75);

            // 帧结束时一次更新 UI
            CommunicationBus.Instance.ProcessData(player);

            AssertEquals(attributeValues["Hp"], 75, "综合测试: Hp 应该正确更新");
            AssertEquals(attributeValues["Mp"], 50, "综合测试: Mp 应该正确更新");
        }
    }

    #endregion

    #region 6. 边界情况和错误处理测试

    private static void RunBoundaryTests()
    {
        PrintSection("边界情况和错误处理测试");
        CommunicationBus.Instance.Clear();

        // 6.1 Clear 功能测试
        {
            int count = 0;
            CommunicationBus.Instance.Subscribe("ClearTestEvent", _ => count++);
            CommunicationBus.Instance.Publish("ClearTestEvent", "Before");
            AssertEquals(count, 1, "边界测试: Clear 前应该能正常工作");

            CommunicationBus.Instance.Clear();
            CommunicationBus.Instance.Publish("ClearTestEvent", "After");
            AssertEquals(count, 1, "边界测试: Clear 后应该清空所有订阅");
        }

        // 6.2 空参数测试
        {
            bool noException = true;
            try
            {
                CommunicationBus.Instance.Publish("NullTest", null);
                CommunicationBus.Instance.Subscribe("NullTest", _ => { });
            }
            catch
            {
                noException = false;
            }
            Assert(noException, "边界测试: 空参数不应该崩溃");
        }

        // 6.3 处理不存在的订阅者
        {
            bool noException = true;
            try
            {
                var nonExistent = new { Name = "Nobody" };
                CommunicationBus.Instance.ProcessData(nonExistent);
            }
            catch
            {
                noException = false;
            }
            Assert(noException, "边界测试: 处理不存在的订阅者不应该崩溃");
        }

        // 6.4 推送链无限循环保护（最大迭代次数）
        {
            var subscriber = new { Name = "InfiniteLoop" };
            int processCount = 0;

            var handler = new { };
            var processMap = new Dictionary<string, Action<object>>
            {
                ["Loop"] = _ =>
                {
                    processCount++;
                    // 自循环：每次处理继续推送
                    CommunicationBus.Instance.PushData(subscriber, "Loop", new { });
                }
            };

            CommunicationBus.Instance.AddHandler(new ProgressHandlerContext
            {
                Subscriber = subscriber,
                Handler = handler,
                ProcessFunctionMap = processMap
            });

            CommunicationBus.Instance.PushData(subscriber, "Loop", new { });
            CommunicationBus.Instance.ProcessData(subscriber);

            // 应该在达到最大迭代次数后停止
            Assert(processCount <= 16, $"边界测试: 推送链应该被限制，实际处理次数: {processCount}");
        }
    }

    #endregion

    #region 测试总结

    private static void PrintSummary()
    {
        Console.WriteLine("\n" + new string('=', 70));
        Console.WriteLine("  测试结果汇总");
        Console.WriteLine(new string('=', 70));
        Console.WriteLine($"  总测试数: {_totalTests}");
        Console.WriteLine($"  通过:   {_passedTests}  ✅");
        Console.WriteLine($"  失败:   {_failedTests}  {( _failedTests == 0 ? "✅" : "❌" )}");

        if (_errors.Count > 0)
        {
            Console.WriteLine("\n  失败的测试:");
            foreach (var error in _errors)
            {
                Console.WriteLine($"    • {error}");
            }
        }

        if (_failedTests == 0)
        {
            Console.WriteLine("\n  🎉 所有测试通过！");
        }
        else
        {
            Console.WriteLine($"\n  ⚠️  有 {_failedTests} 个测试失败");
        }
        Console.WriteLine();
    }

    #endregion
}
