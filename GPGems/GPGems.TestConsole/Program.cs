// =============================================================
// 1. Using 指令（必须最前）
// =============================================================
using GPGems.Core;
using GPGems.Core.PipelineHub;
using GPGems.TestConsole;

// =============================================================
// 2. 顶级语句（主程序入口，必须在类型声明之前）
// =============================================================

Console.WriteLine("=== CommunicationBus 通讯总线测试 ===\n");

CommunicationBusTests.RunAllTests();

Console.WriteLine("\n=== Pipeline 管线框架测试 ===\n");

// 测试 1: 建筑放置管线（展示 PushChannel 积累-批量处理模式
PipelineDemo.RunBuildingPlacementDemo();

// 测试 2: 互斥分支管线（展示 EventChannel
PipelineDemo.RunMutexBranchDemo();

// 测试 3: 子管线（节点原生能力
PipelineDemo.RunSubPipelineDemo();

Console.WriteLine("\n=== 所有测试完成 ===");
