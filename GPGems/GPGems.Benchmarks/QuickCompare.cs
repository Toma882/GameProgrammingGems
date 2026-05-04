using System.Diagnostics;
using GPGems.AI.Pathfinding;

namespace GPGems.Benchmarks;

/// <summary>
/// 快速对比工具 - 不需要 BenchmarkDotNet，直接运行即可
/// </summary>
public static class QuickCompare
{
    public static void Run(int gridSize = 50, int iterations = 100)
    {
        Console.WriteLine($"=== 寻路算法快速对比 ({gridSize}x{gridSize}, 迭代 {iterations} 次) ===");
        Console.WriteLine();

        // 准备地图
        var map = new GridMap(gridSize, gridSize);
        var start = map.Nodes[0, gridSize / 2];
        var goal = map.Nodes[gridSize - 1, gridSize / 2];

        // 所有算法
        var algorithms = new List<(string Name, IPathfinder Instance)>()
        {
            ("A*", new AStarPathfinder()),
            ("A* 优化版", new AStarOptimizedPathfinder()),
            ("Dijkstra", new DijkstraPathfinder()),
            ("BFS", new BFSPathfinder()),
            ("DFS", new DFSPathfinder()),
            ("Best-First", new BestFirstPathfinder()),
            ("双向 A*", new BidirectionalAStarPathfinder()),
            ("并行双向 A*", new ParallelBidirectionalAStarPathfinder()),
        };

        // 预热（排除 JIT 编译影响）
        Console.WriteLine("预热中...");
        foreach (var (_, algo) in algorithms)
        {
            algo.FindPath(map, start, goal);
        }

        // 正式测试
        Console.WriteLine("{0,-15} {1,12} {2,12} {3,12}", "算法", "总耗时(ms)", "平均(μs)", "路径长度");
        Console.WriteLine(new string('-', 55));

        var results = new List<(string Name, double TotalMs, double AvgUs, int PathLen)>();

        foreach (var (name, algo) in algorithms)
        {
            GC.Collect();  // 测试前强制 GC，减少互相干扰
            GC.WaitForPendingFinalizers();

            var sw = Stopwatch.StartNew();
            List<GridNode>? path = null;

            for (int i = 0; i < iterations; i++)
            {
                path = algo.FindPath(map, start, goal);
            }

            sw.Stop();

            double totalMs = sw.Elapsed.TotalMilliseconds;
            double avgUs = (totalMs * 1000) / iterations;
            int pathLen = path?.Count ?? 0;

            results.Add((name, totalMs, avgUs, pathLen));

            Console.WriteLine("{0,-15} {1,12:N2} {2,12:N2} {3,12}",
                name, totalMs, avgUs, pathLen);
        }

        // 排名
        Console.WriteLine();
        Console.WriteLine("=== 性能排名 (按平均耗时) ===");
        int rank = 1;
        foreach (var r in results.OrderBy(x => x.AvgUs))
        {
            double ratio = r.AvgUs / results[0].AvgUs;
            Console.WriteLine($"{rank++}. {r.Name,-15} {r.AvgUs,8:N2} μs  (x{ratio:F2})");
        }
    }
}
