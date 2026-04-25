using BenchmarkDotNet.Running;
using GPGems.Benchmarks;

Console.WriteLine("=== 寻路算法性能测试 ===");
Console.WriteLine();
Console.WriteLine("1: 完整基准测试 (8 种算法, 需 15-30 分钟)");
Console.WriteLine("2: A* 对比专项 (经典 vs 优化版, 需 3-5 分钟)");
Console.WriteLine("3: 8 种算法快速对比");
Console.WriteLine("4: A* 经典 vs 优化版 详细分析 (默认)");
Console.WriteLine();
Console.Write("请选择 [1/2/3/4]: ");

var key = Console.ReadLine()?.Trim() ?? "3";

Console.WriteLine();

switch (key)
{
    case "1":
        Console.WriteLine("开始完整基准测试...");
        BenchmarkRunner.Run<PathfindingBenchmarks>();
        break;
    case "2":
        Console.WriteLine("开始 A* 专项对比...");
        BenchmarkRunner.Run<AStarComparison>();
        break;
    case "3":
        QuickCompare.Run(gridSize: 50, iterations: 100);
        break;
    case "4":
    default:
        DetailedCompare.Run();
        break;
}

Console.WriteLine();
if (!Console.IsInputRedirected)
{
    Console.WriteLine("按任意键退出...");
    Console.ReadKey();
}
