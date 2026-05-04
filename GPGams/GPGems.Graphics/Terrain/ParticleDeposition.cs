namespace GPGems.Graphics.Terrain;

/// <summary>
/// 粒子沉积侵蚀算法
/// 基于 Game Programming Gems 1 - 19 Shankel
///
/// 原理：
/// 1. 模拟大量雨滴粒子落在地形上
/// 2. 粒子沿坡度方向流动
/// 3. 在运动过程中侵蚀（搬运）地形
/// 4. 当粒子速度变慢或到达边界时沉积（释放）携带的物质
///
/// 特点：生成真实的河流、峡谷侵蚀效果
/// </summary>
public static class ParticleDeposition
{
    /// <summary>
    /// 应用侵蚀效果到现有地形
    /// </summary>
    /// <param name="heightfield">要侵蚀的地形</param>
    /// <param name="particleCount">粒子数量</param>
    /// <param name="erosionRate">侵蚀率（每次搬运的量）</param>
    /// <param name="depositionRate">沉积率（每次释放的量）</param>
    /// <param name="inertia">惯性系数（0-1），控制粒子改变方向的难易</param>
    /// <param name="minSlope">最小坡度阈值</param>
    /// <param name="capacity">粒子最大携带量</param>
    /// <param name="evaporation">蒸发率（每步粒子缩小的比例）</param>
    /// <param name="seed">随机种子</param>
    public static void Erode(
        Heightfield heightfield,
        int particleCount,
        float erosionRate = 0.3f,
        float depositionRate = 0.3f,
        float inertia = 0.05f,
        float minSlope = 0.01f,
        float capacity = 4.0f,
        float evaporation = 0.02f,
        int? seed = null)
    {
        var random = seed.HasValue ? new Random(seed.Value) : new Random();
        int width = heightfield.Width;
        int height = heightfield.Height;

        for (int i = 0; i < particleCount; i++)
        {
            // 随机生成粒子位置
            float posX = (float)random.NextDouble() * (width - 1);
            float posY = (float)random.NextDouble() * (height - 1);
            float dirX = 0;
            float dirY = 0;
            float speed = 1.0f;
            float water = 1.0f;
            float sediment = 0.0f;

            // 粒子最多移动步数
            int maxSteps = width * 4;
            for (int step = 0; step < maxSteps; step++)
            {
                int gridX = (int)posX;
                int gridY = (int)posY;

                // 检查边界
                if (gridX < 0 || gridX >= width - 1 || gridY < 0 || gridY >= height - 1)
                    break;

                // 获取双线性插值的高度和梯度
                float u = posX - gridX;
                float v = posY - gridY;

                float h = heightfield[gridX, gridY];
                float hRight = heightfield[gridX + 1, gridY];
                float hDown = heightfield[gridX, gridY + 1];
                float hRightDown = heightfield[gridX + 1, gridY + 1];

                // 双线性插值计算当前高度
                float currentHeight = h * (1 - u) * (1 - v) +
                                      hRight * u * (1 - v) +
                                      hDown * (1 - u) * v +
                                      hRightDown * u * v;

                // 计算梯度（坡度方向）
                float gradX = (hRight - h) * (1 - v) + (hRightDown - hDown) * v;
                float gradY = (hDown - h) * (1 - u) + (hRightDown - hRight) * u;

                // 更新运动方向（结合惯性）
                dirX = dirX * inertia - gradX * (1 - inertia);
                dirY = dirY * inertia - gradY * (1 - inertia);

                // 归一化方向
                float dirLen = MathF.Sqrt(dirX * dirX + dirY * dirY);
                if (dirLen > 1e-6f)
                {
                    dirX /= dirLen;
                    dirY /= dirLen;
                }
                else
                {
                    // 随机方向
                    float angle = (float)random.NextDouble() * MathF.PI * 2;
                    dirX = MathF.Cos(angle);
                    dirY = MathF.Sin(angle);
                }

                // 移动粒子
                float newX = posX + dirX;
                float newY = posY + dirY;

                // 检查新位置是否越界
                if (newX < 0 || newX >= width - 1 || newY < 0 || newY >= height - 1)
                    break;

                // 计算新位置高度
                int newGridX = (int)newX;
                int newGridY = (int)newY;
                float nu = newX - newGridX;
                float nv = newY - newGridY;

                float nh = heightfield[newGridX, newGridY];
                float nhRight = heightfield[newGridX + 1, newGridY];
                float nhDown = heightfield[newGridX, newGridY + 1];
                float nhRightDown = heightfield[newGridX + 1, newGridY + 1];

                float newHeight = nh * (1 - nu) * (1 - nv) +
                                  nhRight * nu * (1 - nv) +
                                  nhDown * (1 - nu) * nv +
                                  nhRightDown * nu * nv;

                // 高度差
                float deltaH = newHeight - currentHeight;

                // 计算粒子能携带的最大沉积物
                float maxSediment = MathF.Max(-deltaH, minSlope) * speed * water * capacity;

                if (deltaH > 0 || sediment > maxSediment)
                {
                    // 上坡或携带量超过上限：沉积
                    float amountToDeposit = (deltaH > 0)
                        ? Math.Min(deltaH, sediment)
                        : (sediment - maxSediment) * depositionRate;

                    // 在四个格子上双线性沉积
                    Deposit(heightfield, gridX, gridY, u, v, amountToDeposit);
                    sediment -= amountToDeposit;
                    speed = MathF.Max(0.1f, speed - deltaH * 2);
                }
                else
                {
                    // 下坡：侵蚀
                    float amountToErode = MathF.Min(
                        MathF.Min(maxSediment - sediment, -deltaH),
                        erosionRate);

                    // 从四个格子侵蚀
                    Erode(heightfield, gridX, gridY, u, v, amountToErode);
                    sediment += amountToErode;
                    speed = MathF.Min(speed + deltaH * 2, 10);
                }

                // 蒸发
                water *= (1 - evaporation);
                if (water < 0.01f) break;

                posX = newX;
                posY = newY;
            }
        }
    }

    /// <summary>在双线性插值位置沉积</summary>
    private static void Deposit(Heightfield hf, int x, int y, float u, float v, float amount)
    {
        // 权重分配
        hf[x, y] += amount * (1 - u) * (1 - v);
        hf[x + 1, y] += amount * u * (1 - v);
        hf[x, y + 1] += amount * (1 - u) * v;
        hf[x + 1, y + 1] += amount * u * v;
    }

    /// <summary>在双线性插值位置侵蚀</summary>
    private static void Erode(Heightfield hf, int x, int y, float u, float v, float amount)
    {
        // 确保不会侵蚀到负数
        float w00 = (1 - u) * (1 - v);
        float w10 = u * (1 - v);
        float w01 = (1 - u) * v;
        float w11 = u * v;

        // 实际能侵蚀的量（不能小于0）
        float maxE00 = hf[x, y];
        float maxE10 = hf[x + 1, y];
        float maxE01 = hf[x, y + 1];
        float maxE11 = hf[x + 1, y + 1];

        float actualAmount = Math.Min(amount,
            maxE00 / w00 + maxE10 / w10 + maxE01 / w01 + maxE11 / w11);

        hf[x, y] -= actualAmount * w00;
        hf[x + 1, y] -= actualAmount * w10;
        hf[x, y + 1] -= actualAmount * w01;
        hf[x + 1, y + 1] -= actualAmount * w11;
    }

    /// <summary>
    /// 快速生成基础地形并应用侵蚀
    /// </summary>
    public static Heightfield GenerateAndErode(
        int size,
        int faultCount = 100,
        int particleCount = 50000,
        float erosionStrength = 0.3f,
        int? seed = null)
    {
        // 先用断层生成基础地形
        var hf = FaultFormation.Generate(size, faultCount, 1.0f, 0.3f, seed);
        hf.Normalize();

        // 应用侵蚀
        Erode(hf, particleCount, erosionStrength, erosionStrength, 0.05f, 0.01f, 6f, 0.02f, seed);
        hf.Normalize();

        return hf;
    }
}
