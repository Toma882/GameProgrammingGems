using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using GPGems.AI.Pathfinding;

namespace GPGems.Benchmarks;

[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
public class AStarComparison
{
    private GridMap _map = null!;
    private GridNode _start = null!;
    private GridNode _goal = null!;

    [Params(25, 50, 100, 200)]
    public int GridSize { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _map = new GridMap(GridSize, GridSize);
        _start = _map.Nodes[0, GridSize / 2];
        _goal = _map.Nodes[GridSize - 1, GridSize / 2];
    }

    [Benchmark(Baseline = true, Description = "经典 A*")]
    public List<GridNode> AStar()
        => new AStarPathfinder().FindPath(_map, _start, _goal);

    [Benchmark(Description = "优化版 A*")]
    public List<GridNode> AStarOptimized()
        => new AStarOptimizedPathfinder().FindPath(_map, _start, _goal);
}
