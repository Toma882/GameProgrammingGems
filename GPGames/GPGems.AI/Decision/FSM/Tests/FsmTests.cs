/*
 * GPGems.AI - FSM Tests
 * 状态机 + 消息路由 单元测试
 */

using GPGems.AI.Decision.Blackboards;
using GPGems.AI.Decision.FSM.Examples;
using GPGems.Core.Messages;

namespace GPGems.AI.Decision.FSM.Tests;

public static class FsmTests
{
    public static bool RunAllTests()
    {
        Console.WriteLine("=== Running FSM Tests ===");
        Console.WriteLine();

        var passed = 0;
        var total = 0;

        RunTest("Message_Basic", TestMessageBasics, ref passed, ref total);
        RunTest("MessageRouter_Register", TestRouterRegister, ref passed, ref total);
        RunTest("MessageRouter_Send_Broadcast", TestRouterSendAndBroadcast, ref passed, ref total);
        RunTest("StateMachine_Basic_Lifecycle", TestStateMachineLifecycle, ref passed, ref total);
        RunTest("StateMachine_Automatic_Transition", TestAutomaticTransition, ref passed, ref total);
        RunTest("StateMachine_Message_Transition", TestMessageTransition, ref passed, ref total);
        RunTest("StateMachine_Delegate_State", TestDelegateState, ref passed, ref total);
        RunTest("DroneFSM_Full_Scenario", TestDroneFullScenario, ref passed, ref total);

        Console.WriteLine();
        Console.WriteLine($"=== Result: {passed}/{total} PASSED ===");

        return passed == total;
    }

    private static void RunTest(string name, Func<bool> test, ref int passed, ref int total)
    {
        total++;
        try
        {
            // 重置时间提供器
            Message.CurrentTimeProvider = () => 0f;

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

    #region 测试用例

    private static bool TestMessageBasics()
    {
        var msg = new Message("Test.Type");
        msg.SenderId = "sender1";
        msg.ReceiverId = "receiver1";
        msg.Priority = MessagePriority.High;

        return msg.Type == "Test.Type"
            && msg.SenderId == "sender1"
            && msg.ReceiverId == "receiver1"
            && msg.Priority == MessagePriority.High;
    }

    private static bool TestRouterRegister()
    {
        var router = new MessageRouter("Test");
        var receiver = new TestReceiver("test1");
        router.RegisterReceiver(receiver);

        return router.ReceiverCount == 1
            && router.GetReceiver("test1") == receiver;
    }

    private static bool TestRouterSendAndBroadcast()
    {
        var router = new MessageRouter("Test");
        var receiver1 = new TestReceiver("r1");
        var receiver2 = new TestReceiver("r2");
        router.RegisterReceiver(receiver1);
        router.RegisterReceiver(receiver2);

        // 单播
        var msg1 = new Message("Test.Msg");
        router.Send(msg1, "r1");
        router.Dispatch();

        if (receiver1.ReceivedCount != 1 || receiver2.ReceivedCount != 0)
            return false;

        // 广播
        var msg2 = new Message("Test.Broadcast");
        router.Broadcast(msg2);
        router.Dispatch();

        return receiver1.ReceivedCount == 2 && receiver2.ReceivedCount == 1;
    }

    private static bool TestStateMachineLifecycle()
    {
        var fsm = new StateMachine("TestFSM");
        var stateA = new DelegateState("A");
        fsm.SetInitialState(stateA);

        int enterCount = 0;
        int exitCount = 0;
        stateA.EnterAction = (_, _) => enterCount++;
        stateA.ExitAction = (_, _) => exitCount++;

        if (fsm.IsStarted) return false;

        fsm.Start();
        if (!fsm.IsStarted || fsm.CurrentState != stateA) return false;
        if (enterCount != 1) return false;

        fsm.Stop();
        if (fsm.IsStarted) return false;
        if (exitCount != 1) return false;

        return true;
    }

    private static bool TestAutomaticTransition()
    {
        var fsm = new StateMachine("Test");
        var stateA = new DelegateState("A");
        var stateB = new DelegateState("B");

        fsm.SetInitialState(stateA);
        fsm.AddTransitions(fsm.From(stateA)
            .To(stateB, ctx => ctx.GetOrDefault("go_b", false)));

        fsm.Start();

        // 条件不满足，不转换
        fsm.Update();
        if (fsm.CurrentState != stateA) return false;

        // 设置条件
        fsm.Context.Set("go_b", true);
        fsm.Update();

        return fsm.CurrentState == stateB;
    }

    private static bool TestMessageTransition()
    {
        var router = new MessageRouter("Test");
        var fsm = new StateMachine("TestFSM");
        var stateA = new DelegateState("A");
        var stateB = new DelegateState("B");

        fsm.SetInitialState(stateA);
        fsm.Router = router;
        router.RegisterReceiver(fsm);

        fsm.AddTransitions(fsm.From(stateA)
            .OnMessage(stateB, "Trigger"));

        fsm.Start();
        if (fsm.CurrentState != stateA) return false;

        // 发送消息触发转换
        var msg = new Message("Trigger");
        router.Send(msg, "TestFSM");
        router.Dispatch();
        fsm.Update();

        return fsm.CurrentState == stateB;
    }

    private static bool TestDelegateState()
    {
        var state = new DelegateState("Test");
        int updateCount = 0;
        state.UpdateAction = _ => updateCount++;

        var bb = new Blackboard("Test");
        state.OnEnter(bb, null);
        state.OnUpdate(bb);
        state.OnUpdate(bb);
        state.OnExit(bb, null);

        return updateCount == 2;
    }

    private static bool TestDroneFullScenario()
    {
        var router = new MessageRouter("DroneTest");
        var drone = DroneFSMBuilder.Build("Drone1", router);

        drone.Start();
        if (drone.CurrentState?.Name != "Patrol") return false;

        // 模拟发现敌人
        for (var i = 0; i < 10; i++)
        {
            drone.Update();
            router.Dispatch();
        }

        return true;
    }

    #endregion

    #region 测试辅助类

    private class TestReceiver : IMessageReceiver
    {
        public string ReceiverId { get; }
        public int ReceivedCount { get; private set; }

        public TestReceiver(string id)
        {
            ReceiverId = id;
        }

        public bool ReceiveMessage(Message message)
        {
            ReceivedCount++;
            return true;
        }
    }

    #endregion
}
