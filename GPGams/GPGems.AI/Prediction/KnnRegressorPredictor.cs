using GPGems.Core.Algorithms;

namespace GPGems.AI.Prediction;

/// <summary>
/// 基于k-NN回归的玩家行为预测器
///
/// 应用场景：
/// 1. 预测玩家在不同状态下的移动速度
/// 2. 预测NPC受到的伤害值
/// 3. 预测技能冷却时间的最佳选择
///
/// 原理：找到历史中最相似的k个样本，加权平均输出
/// </summary>
public class KnnRegressorPredictor
{
    private readonly KNearestNeighborsRegressor<float[]> _regressor;
    private readonly Dictionary<string, int> _featureIndices = [];
    private int _nextFeatureIndex = 0;

    /// <summary>
    /// 创建预测器
    /// </summary>
    /// <param name="k">邻居数量，建议3-7</param>
    public KnnRegressorPredictor(int k = 5)
    {
        _regressor = new KNearestNeighborsRegressor<float[]>(k, (a, b) => DistanceMetrics.Euclidean(a, b));
    }

    /// <summary>注册特征列（用于名称映射）</summary>
    public void RegisterFeature(string name)
    {
        if (!_featureIndices.ContainsKey(name))
            _featureIndices[name] = _nextFeatureIndex++;
    }

    /// <summary>添加训练样本</summary>
    /// <param name="features">特征向量（如：距离、血量、等级、护甲...）</param>
    /// <param name="targetValue">目标预测值（如：实际伤害、实际速度）</param>
    public void AddSample(float[] features, float targetValue)
    {
        _regressor.AddTrainingData(features, targetValue);
    }

    /// <summary>预测（加权平均）</summary>
    public float Predict(float[] features)
    {
        return _regressor.RegressWeighted(features);
    }

    /// <summary>获取k个最近邻的详细信息（用于AI决策解释）</summary>
    public List<(float[] Features, float ActualValue, float Distance)>
        GetNearestNeighbors(float[] features)
    {
        return _regressor.FindNearest(features)
            .Select(n => (n.Point, n.Label, n.Distance))
            .ToList();
    }

    /// <summary>训练样本数量</summary>
    public int TrainingSampleCount => _regressor.FindNearest(Array.Empty<float>()).Count;
}

/// <summary>
/// 距离度量的扩展方法
/// </summary>
public static class DistanceMetrics
{
    /// <summary>标准欧氏距离</summary>
    public static float Euclidean(float[] a, float[] b)
    {
        float sum = 0f;
        for (int i = 0; i < a.Length && i < b.Length; i++)
        {
            float d = a[i] - b[i];
            sum += d * d;
        }
        return MathF.Sqrt(sum);
    }

    /// <summary>加权欧氏距离（可给重要特征更高权重）</summary>
    public static float WeightedEuclidean(float[] a, float[] b, float[]? weights = null)
    {
        if (weights == null) return Euclidean(a, b);

        float sum = 0f;
        for (int i = 0; i < a.Length && i < b.Length && i < weights.Length; i++)
        {
            float d = (a[i] - b[i]) * weights[i];
            sum += d * d;
        }
        return MathF.Sqrt(sum);
    }

    /// <summary>归一化欧氏距离（每个特征缩放到[0,1]）</summary>
    public static float NormalizedEuclidean(float[] a, float[] b, float[] minValues, float[] maxValues)
    {
        float sum = 0f;
        for (int i = 0; i < a.Length && i < b.Length; i++)
        {
            float range = maxValues[i] - minValues[i];
            if (range <= 0) continue;

            float normA = (a[i] - minValues[i]) / range;
            float normB = (b[i] - minValues[i]) / range;
            float d = normA - normB;
            sum += d * d;
        }
        return MathF.Sqrt(sum);
    }

    /// <summary>曼哈顿距离（L1）</summary>
    public static float Manhattan(float[] a, float[] b)
    {
        float sum = 0f;
        for (int i = 0; i < a.Length && i < b.Length; i++)
        {
            sum += MathF.Abs(a[i] - b[i]);
        }
        return sum;
    }
}

/// <summary>
/// 示例：伤害预测器
/// 输入特征：[攻击强度, 目标护甲, 距离, 暴击率]
/// 输出预测：实际伤害值
/// </summary>
public class DamagePredictor
{
    private readonly KnnRegressorPredictor _predictor = new(k: 5);

    public DamagePredictor()
    {
        // 预填充一些训练数据（实际游戏中应从战斗日志中学习）
        AddSample(100f, 20f, 5f, 0.1f, 85f);
        AddSample(100f, 50f, 5f, 0.1f, 60f);
        AddSample(150f, 20f, 10f, 0.3f, 140f);
        AddSample(80f, 30f, 20f, 0.05f, 50f);
        AddSample(200f, 100f, 5f, 0.5f, 150f);
    }

    private void AddSample(float attack, float armor, float dist, float critRate, float actualDamage)
    {
        _predictor.AddSample(new[] { attack, armor, dist, critRate }, actualDamage);
    }

    /// <summary>预测伤害</summary>
    public float Predict(float attackPower, float targetArmor, float distance, float critChance)
    {
        return _predictor.Predict(new[] { attackPower, targetArmor, distance, critChance });
    }

    /// <summary>获取预测依据（用于调试/解释AI决策）</summary>
    public string GetPredictionExplanation(float attackPower, float targetArmor,
        float distance, float critChance)
    {
        var neighbors = _predictor.GetNearestNeighbors(new[] { attackPower, targetArmor, distance, critChance });
        var lines = neighbors.Select((n, i) =>
            $"  邻居#{i + 1}: 攻击={n.Features[0]:F0} 护甲={n.Features[1]:F0} " +
            $"距离={n.Features[2]:F0} 暴击={n.Features[3]:P0} → 实际伤害={n.ActualValue:F0} (距离={n.Distance:F2})");

        return "预测依据（最近5个历史样本）：\n" + string.Join("\n", lines);
    }
}
