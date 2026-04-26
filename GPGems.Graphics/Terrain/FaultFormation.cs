namespace GPGems.Graphics.Terrain;

/// <summary>
/// 断层地形生成算法
/// 基于 Game Programming Gems 1 - 17 Shankel
///
/// 原理：
/// 1. 随机生成一条穿过地形的断层线
/// 2. 将断层线一侧的所有点抬高一定高度
/// 3. 重复多次形成自然的山峦起伏
///
/// 特点：生成快速，效果类似地质断层形成的山脉
/// </summary>
public static class FaultFormation
{
    /// <summary>
    /// 生成断层地形
    /// </summary>
    /// <param name="size">地形大小（必须是正方形）</param>
    /// <param name="faultCount">断层数量</param>
    /// <param name="displacement">每次断层的抬升量</param>
    /// <param name="filterRadius">边界平滑半径</param>
    /// <param name="seed">随机种子</param>
    /// <returns>生成的高度场</returns>
    public static Heightfield Generate(
        int size,
        int faultCount,
        float displacement = 1.0f,
        float filterRadius = 0.5f,
        int? seed = null)
    {
        var random = seed.HasValue ? new Random(seed.Value) : new Random();
        var heightfield = new Heightfield(size, size);

        for (int i = 0; i < faultCount; i++)
        {
            ApplyFault(heightfield, random, displacement, filterRadius);
        }

        return heightfield;
    }

    /// <summary>应用单次断层</summary>
    private static void ApplyFault(Heightfield heightfield, Random random, float displacement, float filterRadius)
    {
        int size = heightfield.Width;

        // 随机生成一条穿过地形中心的线
        // 使用极坐标表示：从中心出发的方向和偏移
        double angle = random.NextDouble() * Math.PI * 2;
        double centerX = size / 2.0;
        double centerY = size / 2.0;

        // 线的方向向量（法向量）
        float nx = (float)Math.Cos(angle);
        float ny = (float)Math.Sin(angle);

        // 线的偏移：从中心随机偏移
        double offset = (random.NextDouble() - 0.5) * size * 0.8;

        // 平滑过渡区域大小
        float transition = filterRadius * size;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // 计算点到断层线的距离（带符号）
                // 点到线的距离公式：ax + by + c = 0
                // 这里：nx * x + ny * y + c = 0
                // c = - (nx * centerX + ny * centerY + offset)
                float dist = nx * (x - (float)centerX) + ny * (y - (float)centerY) - (float)offset;

                if (transition <= 0)
                {
                    // 硬边界：直接抬高一侧
                    if (dist > 0)
                    {
                        heightfield[x, y] += displacement;
                    }
                }
                else
                {
                    // 软边界：使用平滑函数过渡
                    // 使用 S 型曲线：smoothstep
                    float t = (dist + transition) / (2 * transition);
                    t = Math.Clamp(t, 0f, 1f);
                    float weight = t * t * (3 - 2 * t);  // smoothstep

                    heightfield[x, y] += displacement * weight;
                }
            }
        }
    }

    /// <summary>
    /// 生成具有衰减效果的断层地形（越后面的断层影响越小）
    /// </summary>
    public static Heightfield GenerateWithFalloff(
        int size,
        int faultCount,
        float maxDisplacement = 1.0f,
        float falloff = 0.95f,
        float filterRadius = 0.5f,
        int? seed = null)
    {
        var random = seed.HasValue ? new Random(seed.Value) : new Random();
        var heightfield = new Heightfield(size, size);

        float currentDisp = maxDisplacement;
        for (int i = 0; i < faultCount; i++)
        {
            ApplyFault(heightfield, random, currentDisp, filterRadius);
            currentDisp *= falloff;
        }

        return heightfield;
    }
}
