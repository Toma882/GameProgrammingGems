namespace GPGems.Graphics.Terrain;

/// <summary>
/// 中点位移分形地形生成算法
/// 基于 Game Programming Gems 1 - 18 Shankel
///
/// 原理（钻石-方块算法）：
/// 1. 初始化四个角点的高度
/// 2. 方块步骤（Square）：计算正方形中心点高度（四个角的平均值 + 随机偏移）
/// 3. 钻石步骤（Diamond）：计算菱形中心点高度（四个边的平均值 + 随机偏移）
/// 4. 减少随机偏移的范围，重复步骤直到填满
///
/// 特点：生成自然的分形地形，有很好的细节层次感
/// </summary>
public static class MidpointDisplacement
{
    /// <summary>
    /// 生成分形地形
    /// </summary>
    /// <param name="level">细分级别，实际大小 = 2^level + 1</param>
    /// <param name="roughness">粗糙度（0-1），值越大地形越崎岖</param>
    /// <param name="heightScale">高度缩放系数</param>
    /// <param name="seed">随机种子</param>
    /// <returns>生成的高度场</returns>
    public static Heightfield Generate(
        int level,
        float roughness = 0.5f,
        float heightScale = 1.0f,
        int? seed = null)
    {
        int size = (1 << level) + 1;  // 2^level + 1
        var random = seed.HasValue ? new Random(seed.Value) : new Random();
        var heights = new float[size, size];

        // 初始化四个角
        heights[0, 0] = (float)random.NextDouble() * heightScale;
        heights[0, size - 1] = (float)random.NextDouble() * heightScale;
        heights[size - 1, 0] = (float)random.NextDouble() * heightScale;
        heights[size - 1, size - 1] = (float)random.NextDouble() * heightScale;

        int step = size - 1;
        float currentRange = heightScale;

        while (step > 1)
        {
            int halfStep = step / 2;

            // 方块步骤（Square）：计算正方形中心
            for (int y = halfStep; y < size; y += step)
            {
                for (int x = halfStep; x < size; x += step)
                {
                    float avg = (
                        heights[y - halfStep, x - halfStep] +  // 左上
                        heights[y - halfStep, x + halfStep] +  // 右上
                        heights[y + halfStep, x - halfStep] +  // 左下
                        heights[y + halfStep, x + halfStep]    // 右下
                    ) / 4.0f;

                    heights[y, x] = avg + RandomOffset(random, currentRange);
                }
            }

            // 钻石步骤（Diamond）：计算菱形中心
            bool evenRow = true;
            for (int y = 0; y < size; y += halfStep)
            {
                int xStart = evenRow ? halfStep : 0;
                for (int x = xStart; x < size; x += step)
                {
                    float avg = DiamondAverage(heights, size, x, y, halfStep);
                    heights[y, x] = avg + RandomOffset(random, currentRange);
                }
                evenRow = !evenRow;
            }

            step = halfStep;
            currentRange *= roughness;
        }

        return new Heightfield(heights);
    }

    /// <summary>计算钻石步骤的平均值（四个方向的邻居）</summary>
    private static float DiamondAverage(float[,] heights, int size, int x, int y, int halfStep)
    {
        float sum = 0;
        int count = 0;

        // 上
        if (y - halfStep >= 0)
        {
            sum += heights[y - halfStep, x];
            count++;
        }
        // 下
        if (y + halfStep < size)
        {
            sum += heights[y + halfStep, x];
            count++;
        }
        // 左
        if (x - halfStep >= 0)
        {
            sum += heights[y, x - halfStep];
            count++;
        }
        // 右
        if (x + halfStep < size)
        {
            sum += heights[y, x + halfStep];
            count++;
        }

        return sum / count;
    }

    /// <summary>生成 [-range, range] 的随机偏移</summary>
    private static float RandomOffset(Random random, float range)
    {
        return (float)(random.NextDouble() * 2 - 1) * range;
    }

    /// <summary>
    /// 生成更平滑的分形地形（使用改进的随机分布）
    /// </summary>
    public static Heightfield GenerateSmooth(
        int level,
        float roughness = 0.6f,
        float heightScale = 1.0f,
        int smoothIterations = 2,
        int? seed = null)
    {
        var hf = Generate(level, roughness, heightScale, seed);
        hf.Smooth(smoothIterations);
        return hf;
    }
}
