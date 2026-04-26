namespace GPGems.Core.Algorithms;

/// <summary>
/// k-近邻算法通用实现
/// 支持分类、回归，多种距离度量
///
/// 用途：
/// 1. AI：k-NN回归模型、分类模型
/// 2. Graphics：空间查询、最近邻搜索
/// 3. General：推荐系统、异常检测
/// </summary>
/// <typeparam name="TData">数据点类型</typeparam>
/// <typeparam name="TLabel">标签/输出类型（分类=类别，回归=float）</typeparam>
public class KNearestNeighbors<TData, TLabel>
    where TLabel : notnull
{
    /// <summary>距离度量函数</summary>
    public Func<TData, TData, float> DistanceMetric { get; set; }

    /// <summary>邻居数量 k</summary>
    public int K { get; set; }

    private readonly List<(TData Point, TLabel Label)> _trainingData = [];

    public KNearestNeighbors(int k, Func<TData, TData, float> distanceMetric)
    {
        K = k;
        DistanceMetric = distanceMetric;
    }

    /// <summary>添加训练数据</summary>
    public void AddTrainingData(TData point, TLabel label)
    {
        _trainingData.Add((point, label));
    }

    /// <summary>批量添加训练数据</summary>
    public void AddTrainingDataRange(IEnumerable<(TData Point, TLabel Label)> data)
    {
        _trainingData.AddRange(data);
    }

    /// <summary>清空训练数据</summary>
    public void Clear() => _trainingData.Clear();

    /// <summary>查找k个最近邻（暴力搜索）</summary>
    public List<(TData Point, TLabel Label, float Distance)> FindNearest(TData query)
    {
        var neighbors = new List<(TData, TLabel, float)>(_trainingData.Count);

        foreach (var (point, label) in _trainingData)
        {
            float dist = DistanceMetric(query, point);
            neighbors.Add((point, label, dist));
        }

        return neighbors
            .OrderBy(n => n.Item3)
            .Take(K)
            .ToList();
    }

    /// <summary>k-NN分类：多数投票</summary>
    public TLabel? Classify(TData query)
    {
        var neighbors = FindNearest(query);
        if (neighbors.Count == 0) return default;

        // 多数投票
        var votes = neighbors
            .GroupBy(n => n.Label)
            .OrderByDescending(g => g.Count())
            .First();

        return votes.Key;
    }

    /// <summary>k-NN分类：带权重的投票（距离越近权重越高）</summary>
    public TLabel? ClassifyWeighted(TData query)
    {
        var neighbors = FindNearest(query);
        if (neighbors.Count == 0) return default;

        // 加权投票：权重 = 1/(距离 + epsilon)
        var weights = new Dictionary<TLabel, float>();
        const float epsilon = 1e-6f;

        foreach (var (_, label, dist) in neighbors)
        {
            float weight = 1.0f / (dist + epsilon);
            if (weights.ContainsKey(label))
                weights[label] += weight;
            else
                weights[label] = weight;
        }

        return weights.OrderByDescending(kv => kv.Value).First().Key;
    }
}

/// <summary>
/// k-NN回归专用实现（输出为float）
/// 用于AI回归模型、数值预测
/// </summary>
/// <typeparam name="TData">数据点类型</typeparam>
public class KNearestNeighborsRegressor<TData> : KNearestNeighbors<TData, float>
{
    public KNearestNeighborsRegressor(int k, Func<TData, TData, float> distanceMetric)
        : base(k, distanceMetric)
    {
    }

    /// <summary>k-NN回归：平均邻居值</summary>
    public float Regress(TData query)
    {
        var neighbors = FindNearest(query);
        if (neighbors.Count == 0) return 0f;
        return neighbors.Average(n => n.Label);
    }

    /// <summary>k-NN回归：加权平均（距离越近权重越高）</summary>
    public float RegressWeighted(TData query)
    {
        var neighbors = FindNearest(query);
        if (neighbors.Count == 0) return 0f;

        const float epsilon = 1e-6f;
        float totalWeight = 0f;
        float weightedSum = 0f;

        foreach (var (_, value, dist) in neighbors)
        {
            float weight = 1.0f / (dist + epsilon);
            weightedSum += value * weight;
            totalWeight += weight;
        }

        return totalWeight > 0 ? weightedSum / totalWeight : 0f;
    }
}

/// <summary>
/// 预定义距离度量函数
/// </summary>
public static class DistanceMetrics
{
    /// <summary>欧氏距离（L2）</summary>
    public static float Euclidean(float[] a, float[] b)
    {
        float sum = 0f;
        for (int i = 0; i < a.Length; i++)
        {
            float d = a[i] - b[i];
            sum += d * d;
        }
        return MathF.Sqrt(sum);
    }

    /// <summary>曼哈顿距离（L1）</summary>
    public static float Manhattan(float[] a, float[] b)
    {
        float sum = 0f;
        for (int i = 0; i < a.Length; i++)
        {
            sum += MathF.Abs(a[i] - b[i]);
        }
        return sum;
    }

    /// <summary>切比雪夫距离（L∞）</summary>
    public static float Chebyshev(float[] a, float[] b)
    {
        float max = 0f;
        for (int i = 0; i < a.Length; i++)
        {
            max = MathF.Max(max, MathF.Abs(a[i] - b[i]));
        }
        return max;
    }

    /// <summary>闵可夫斯基距离</summary>
    public static float Minkowski(float[] a, float[] b, float p)
    {
        float sum = 0f;
        for (int i = 0; i < a.Length; i++)
        {
            sum += MathF.Pow(MathF.Abs(a[i] - b[i]), p);
        }
        return MathF.Pow(sum, 1.0f / p);
    }
}
