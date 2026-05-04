using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using GPGems.AI.Pathfinding;

namespace GPGems.Benchmarks;

[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
public class PathfindingBenchmarks
{
    private GridMap _map = null!;
    private GridNode _start = null!;
    private GridNode _goal = null!;

    [Params(25, 50, 100)]
    public int GridSize { get; set; }

    [Params(0.0, 0.2)]
    public double ObstacleDensity { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _map = new GridMap(GridSize, GridSize);

        // 随机生成障碍物
        if (ObstacleDensity > 0)
        {
            var random = new Random(42); // 固定种子，保证可重复性
            for (int x = 0; x < GridSize; x++)
            {
                for (int y = 0; y < GridSize; y++)
                {
                    if (random.NextDouble() < ObstacleDensity)
                    {
                        _map.Nodes[x, y].IsWalkable = false;
                    }
                }
            }
        }

        // 起点和终点保证可到达
        _start = _map.Nodes[0, GridSize / 2];
        _start.IsWalkable = true;
        _goal = _map.Nodes[GridSize - 1, GridSize / 2];
        _goal.IsWalkable = true;
    }

    [Benchmark(Baseline = true, Description = "经典 A* 算法")]
    public List<GridNode> AStar()
        => new AStarPathfinder().FindPath(_map, _start, _goal);

    [Benchmark(Description = "优化版 A* 算法")]
    public List<GridNode> AStarOptimized()
        => new AStarOptimizedPathfinder().FindPath(_map, _start, _goal);

    [Benchmark(Description = "Dijkstra 算法")]
    public List<GridNode> Dijkstra()
        => new DijkstraPathfinder().FindPath(_map, _start, _goal);

    [Benchmark(Description = "BFS 广度优先")]
    public List<GridNode> BFS()
        => new BFSPathfinder().FindPath(_map, _start, _goal);

    [Benchmark(Description = "DFS 深度优先")]
    public List<GridNode> DFS()
        => new DFSPathfinder().FindPath(_map, _start, _goal);

    [Benchmark(Description = "Best-First 贪心搜索")]
    public List<GridNode> BestFirst()
        => new BestFirstPathfinder().FindPath(_map, _start, _goal);

    [Benchmark(Description = "双向 A* 算法")]
    public List<GridNode> BidirectionalAStar()
        => new BidirectionalAStarPathfinder().FindPath(_map, _start, _goal);

    [Benchmark(Description = "并行双向 A* 算法")]
    public List<GridNode> ParallelBidirectionalAStar()
        => new ParallelBidirectionalAStarPathfinder().FindPath(_map, _start, _goal);
}
