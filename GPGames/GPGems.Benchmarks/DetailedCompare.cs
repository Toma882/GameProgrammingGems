using System.Diagnostics;
using GPGems.AI.Pathfinding;

namespace GPGems.Benchmarks;

public static class DetailedCompare
{
    public static void Run()
    {
        Console.WriteLine("=== A* 经典 vs 优化版 详细对比 ===");
        Console.WriteLine();

        int[] sizes = { 25, 50, 100, 200 };
        int iterations = 100;

        Console.WriteLine("{0,8} {1,12} {2,12} {3,10} {4,10} {5,8}",
            "地图大小", "经典 A*(μs)", "优化版(μs)", "倍率", "经典路径", "优化路径");
        Console.WriteLine(new string('-', 70));

        foreach (var size in sizes)
        {
            var map = new GridMap(size, size);
            var start = map.Nodes[0, size / 2];
            var goal = map.Nodes[size - 1, size / 2];

            // 预热
            new AStarPathfinder().FindPath(map, start, goal);
            new AStarOptimizedPathfinder().FindPath(map, start, goal);

            // 测试经典 A*
            GC.Collect();
            GC.WaitForPendingFinalizers();
            var sw = Stopwatch.StartNew();
            List<GridNode>? p1 = null;
            for (int i = 0; i < iterations; i++)
                p1 = new AStarPathfinder().FindPath(map, start, goal);
            sw.Stop();
            double t1 = (sw.Elapsed.TotalMilliseconds * 1000) / iterations;

            // 测试优化版 A*
            GC.Collect();
            GC.WaitForPendingFinalizers();
            sw = Stopwatch.StartNew();
            List<GridNode>? p2 = null;
            for (int i = 0; i < iterations; i++)
                p2 = new AStarOptimizedPathfinder().FindPath(map, start, goal);
            sw.Stop();
            double t2 = (sw.Elapsed.TotalMilliseconds * 1000) / iterations;

            double ratio = t2 / t1;
            string status = ratio > 1.2 ? "❌ 反向优化" : (ratio < 0.8 ? "✅ 优化成功" : "⚖️  差不多");

            Console.WriteLine("{0,5}x{0,-3} {1,12:N2} {2,12:N2} {3,10:F2}x {4,10} {5,10} {6}",
                size, t1, t2, ratio, p1?.Count ?? 0, p2?.Count ?? 0, status);
        }

        Console.WriteLine();
        Console.WriteLine("=== 反向优化的原因分析 ===");
        Console.WriteLine();
        Console.WriteLine("1️⃣  邻居排序 .OrderBy()");
        Console.WriteLine("   开销: 每个节点 O(8 log 8) ≈ 24 次比较");
        Console.WriteLine("   收益: 在空旷地图上 ≈ 0（本来就是直走）");
        Console.WriteLine();
        Console.WriteLine("2️⃣  路径平滑 + Bresenham 视线检测");
        Console.WriteLine("   开销: O(n²) 复杂度，每条路径都要扫");
        Console.WriteLine("   收益: 空旷地图上把 50 个节点压缩到 2 个...");
        Console.WriteLine("          但计算开销比节省的路径点还大！");
        Console.WriteLine();
        Console.WriteLine("3️⃣  动态权重计算");
        Console.WriteLine("   开销: 额外的除法、乘法运算");
        Console.WriteLine("   收益: Octile 启发式已经很准了，没必要");
        Console.WriteLine();
        Console.WriteLine("💡 结论: 这些\"优化\"只适用于障碍物密集的复杂迷宫！");
    }
}
