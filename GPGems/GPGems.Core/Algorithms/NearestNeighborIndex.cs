using SysMath = System.Math;

namespace GPGems.Core.Algorithms;

/// <summary>
/// 最近邻索引的统一接口
/// 用于不同算法之间的性能对比和切换
/// </summary>
/// <typeparam name="T">值类型</typeparam>
public interface INearestNeighborIndex<T>
    where T : notnull
{
    /// <summary>维度</summary>
    int Dimensions { get; }

    /// <summary>构建索引</summary>
    void Build(List<(float[] Point, T Value)> points);

    /// <summary>k-近邻搜索</summary>
    List<(float[] Point, T Value, float Distance)> FindKNearest(float[] query, int k);

    /// <summary>范围查询</summary>
    List<(float[] Point, T Value, float Distance)> RangeQuery(float[] query, float radius);

    /// <summary>索引统计信息</summary>
    string GetStats();
}

/// <summary>
/// 暴力搜索基准实现（用于对比性能）
/// </summary>
public class BruteForceSearch<T> : INearestNeighborIndex<T>
    where T : notnull
{
    private List<(float[] Point, T Value)>? _points;

    public int Dimensions { get; }

    public BruteForceSearch(int dimensions)
    {
        Dimensions = dimensions;
    }

    public void Build(List<(float[] Point, T Value)> points)
    {
        _points = points.ToList();
    }

    public List<(float[] Point, T Value, float Distance)> FindKNearest(float[] query, int k)
    {
        if (_points == null) return new();

        var results = new List<(float[], T, float)>(_points.Count);
        foreach (var (point, value) in _points)
        {
            float dist = DistanceMetrics.Euclidean(query, point);
            results.Add((point, value, dist));
        }

        return results
            .OrderBy(r => r.Item3)
            .Take(k)
            .ToList();
    }

    public List<(float[] Point, T Value, float Distance)> RangeQuery(float[] query, float radius)
    {
        if (_points == null) return new();

        float radiusSq = radius * radius;
        var results = new List<(float[], T, float)>();

        foreach (var (point, value) in _points)
        {
            float dist = DistanceMetrics.Euclidean(query, point);
            if (dist * dist <= radiusSq)
            {
                results.Add((point, value, dist));
            }
        }

        return results.OrderBy(r => r.Item3).ToList();
    }

    public string GetStats()
    {
        return $"暴力搜索, 点数: {_points?.Count ?? 0}";
    }
}

/// <summary>
/// KD树索引适配器
/// </summary>
public class KDTreeIndex<T> : INearestNeighborIndex<T>
    where T : notnull
{
    private BallTree<T>? _tree; // 复用BallTree的实现（高维下更稳健）

    public int Dimensions { get; }

    public KDTreeIndex(int dimensions)
    {
        Dimensions = dimensions;
        _tree = new BallTree<T>(dimensions, leafCapacity: 1);
    }

    public void Build(List<(float[] Point, T Value)> points)
    {
        _tree?.Build(points);
    }

    public List<(float[] Point, T Value, float Distance)> FindKNearest(float[] query, int k)
    {
        return _tree?.FindKNearest(query, k) ?? new();
    }

    public List<(float[] Point, T Value, float Distance)> RangeQuery(float[] query, float radius)
    {
        return _tree?.RangeQuery(query, radius) ?? new();
    }

    public string GetStats()
    {
        var stats = _tree?.GetStats() ?? (0, 0, 0);
        return $"KD树适配, 节点数: {stats.NodeCount}, 叶子数: {stats.LeafCount}, 深度: {stats.MaxDepth}";
    }
}

/// <summary>
/// Ball树索引
/// </summary>
public class BallTreeIndex<T> : INearestNeighborIndex<T>
    where T : notnull
{
    private BallTree<T>? _tree;

    public int Dimensions { get; }

    public BallTreeIndex(int dimensions, int leafCapacity = 20)
    {
        Dimensions = dimensions;
        _tree = new BallTree<T>(dimensions, leafCapacity);
    }

    public void Build(List<(float[] Point, T Value)> points)
    {
        _tree?.Build(points);
    }

    public List<(float[] Point, T Value, float Distance)> FindKNearest(float[] query, int k)
    {
        return _tree?.FindKNearest(query, k) ?? new();
    }

    public List<(float[] Point, T Value, float Distance)> RangeQuery(float[] query, float radius)
    {
        return _tree?.RangeQuery(query, radius) ?? new();
    }

    public string GetStats()
    {
        var stats = _tree?.GetStats() ?? (0, 0, 0);
        return $"Ball树, 节点数: {stats.NodeCount}, 叶子数: {stats.LeafCount}, 深度: {stats.MaxDepth}";
    }
}

/// <summary>
/// LSH 局部敏感哈希（近似最近邻）
/// 适用于超大规模高维数据（百万级以上），牺牲精度换取速度
/// </summary>
public class LocalitySensitiveHashing<T> : INearestNeighborIndex<T>
    where T : notnull
{
    private readonly int _dimensions;
    private readonly int _hashTables;
    private readonly int _hashFunctions;
    private readonly float _bucketWidth;
    private List<HashTable>? _tables;
    private List<(float[] Point, T Value)>? _allPoints;

    private class HashTable
    {
        public float[] RandomVector { get; }
        public Dictionary<long, List<int>> Buckets { get; } = new();

        public HashTable(int dimensions)
        {
            // 随机投影向量
            var rand = new Random();
            RandomVector = new float[dimensions];
            for (int i = 0; i < dimensions; i++)
            {
                // 标准正态分布采样
                double u1 = 1.0 - rand.NextDouble();
                double u2 = 1.0 - rand.NextDouble();
                double z = global::System.Math.Sqrt(-2.0 * global::System.Math.Log(u1)) * global::System.Math.Cos(2.0 * global::System.Math.PI * u2);
                RandomVector[i] = (float)z;
            }
        }

        public long Hash(float[] point, float bucketWidth)
        {
            double dot = 0;
            for (int i = 0; i < point.Length; i++)
            {
                dot += point[i] * RandomVector[i];
            }
            return (long)global::System.Math.Floor(dot / bucketWidth);
        }
    }

    public LocalitySensitiveHashing(int dimensions, int hashTables = 4, int hashFunctionsPerTable = 8, float bucketWidth = 5.0f)
    {
        _dimensions = dimensions;
        _hashTables = hashTables;
        _hashFunctions = hashFunctionsPerTable;
        _bucketWidth = bucketWidth;
    }

    public int Dimensions => _dimensions;

    public void Build(List<(float[] Point, T Value)> points)
    {
        _allPoints = points.ToList();
        _tables = new List<HashTable>(_hashTables);

        for (int t = 0; t < _hashTables; t++)
        {
            _tables.Add(new HashTable(_dimensions));
        }

        // 索引所有点
        for (int i = 0; i < points.Count; i++)
        {
            foreach (var table in _tables)
            {
                long hash = table.Hash(points[i].Point, _bucketWidth);
                if (!table.Buckets.TryGetValue(hash, out var bucket))
                {
                    bucket = new List<int>();
                    table.Buckets[hash] = bucket;
                }
                bucket.Add(i);
            }
        }
    }

    public List<(float[] Point, T Value, float Distance)> FindKNearest(float[] query, int k)
    {
        if (_tables == null || _allPoints == null) return new();

        // 收集所有哈希桶中的候选点
        var candidates = new HashSet<int>();
        foreach (var table in _tables)
        {
            long hash = table.Hash(query, _bucketWidth);
            // 检查相邻桶
            for (long offset = -1; offset <= 1; offset++)
            {
                if (table.Buckets.TryGetValue(hash + offset, out var bucket))
                {
                    foreach (var idx in bucket)
                        candidates.Add(idx);
                }
            }
        }

        // 对候选点进行暴力搜索
        var results = new List<(float[], T, float)>(candidates.Count);
        foreach (int idx in candidates)
        {
            var (point, value) = _allPoints[idx];
            float dist = DistanceMetrics.Euclidean(query, point);
            results.Add((point, value, dist));
        }

        // 如果候选太少，回退到全部搜索
        if (candidates.Count < k * 5)
        {
            for (int i = 0; i < _allPoints.Count; i++)
            {
                if (!candidates.Contains(i))
                {
                    var (point, value) = _allPoints[i];
                    float dist = DistanceMetrics.Euclidean(query, point);
                    results.Add((point, value, dist));
                }
            }
        }

        return results.OrderBy(r => r.Item3).Take(k).ToList();
    }

    public List<(float[] Point, T Value, float Distance)> RangeQuery(float[] query, float radius)
    {
        // 先用k-NN找足够多的点，再过滤半径内的
        var knn = FindKNearest(query, 100);
        return knn.Where(r => r.Item3 <= radius).ToList();
    }

    public string GetStats()
    {
        int totalBuckets = _tables?.Sum(t => t.Buckets.Count) ?? 0;
        return $"LSH, 哈希表数: {_hashTables}, 桶总数: {totalBuckets}, 点数: {_allPoints?.Count ?? 0}";
    }
}

/// <summary>
/// 最近邻算法性能对比工具
/// </summary>
public class NearestNeighborBenchmark
{
    /// <summary>
    /// 对多种算法进行基准测试
    /// </summary>
    public static BenchmarkResult Run<T>(
        INearestNeighborIndex<T>[] algorithms,
        List<(float[] Point, T Value)> dataPoints,
        float[][] testQueries,
        int k)
        where T : notnull
    {
        var result = new BenchmarkResult();

        foreach (var algo in algorithms)
        {
            // 构建索引
            var buildTime = System.Diagnostics.Stopwatch.StartNew();
            algo.Build(dataPoints);
            buildTime.Stop();

            // 执行查询
            var queryTime = System.Diagnostics.Stopwatch.StartNew();
            int totalFound = 0;
            foreach (var query in testQueries)
            {
                var found = algo.FindKNearest(query, k);
                totalFound += found.Count;
            }
            queryTime.Stop();

            result.Results.Add(new AlgorithmResult
            {
                AlgorithmName = algo.GetType().Name,
                BuildTimeMs = buildTime.Elapsed.TotalMilliseconds,
                TotalQueryTimeMs = queryTime.Elapsed.TotalMilliseconds,
                AvgQueryTimeUs = queryTime.Elapsed.TotalMicroseconds / testQueries.Length,
                TotalPointsIndexed = dataPoints.Count,
                Stats = algo.GetStats()
            });
        }

        return result;
    }

    public class BenchmarkResult
    {
        public List<AlgorithmResult> Results { get; } = new();

        public override string ToString()
        {
            var lines = Results.Select(r =>
                $"{r.AlgorithmName,-20} 构建: {r.BuildTimeMs,8:F2}ms  " +
                $"平均查询: {r.AvgQueryTimeUs,7:F1}μs  {r.Stats}");
            return string.Join("\n", lines);
        }
    }

    public class AlgorithmResult
    {
        public string AlgorithmName { get; set; } = string.Empty;
        public double BuildTimeMs { get; set; }
        public double TotalQueryTimeMs { get; set; }
        public double AvgQueryTimeUs { get; set; }
        public int TotalPointsIndexed { get; set; }
        public string Stats { get; set; } = string.Empty;
    }
}
