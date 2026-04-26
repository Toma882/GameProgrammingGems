namespace GPGems.Graphics.Terrain;

/// <summary>
/// 高度场数据结构
/// 存储地形高度数据，提供通用的地形操作
/// </summary>
public class Heightfield
{
    /// <summary>高度值数组 [y, x] 格式</summary>
    private readonly float[,] _heights;

    /// <summary>网格宽度（X轴方向的点数）</summary>
    public int Width => _heights.GetLength(1);

    /// <summary>网格高度（Y轴方向的点数）</summary>
    public int Height => _heights.GetLength(0);

    /// <summary>索引访问器</summary>
    public float this[int x, int y]
    {
        get => _heights[y, x];
        set => _heights[y, x] = value;
    }

    /// <summary>创建指定大小的高度场</summary>
    public Heightfield(int width, int height)
    {
        if (width < 2) throw new ArgumentException("宽度必须至少为2", nameof(width));
        if (height < 2) throw new ArgumentException("高度必须至少为2", nameof(height));

        _heights = new float[height, width];
    }

    /// <summary>从现有数据创建高度场</summary>
    public Heightfield(float[,] heights)
    {
        _heights = heights ?? throw new ArgumentNullException(nameof(heights));
    }

    /// <summary>重置所有高度为0</summary>
    public void Clear()
    {
        Array.Clear(_heights);
    }

    /// <summary>将所有高度归一化到 [0, 1] 范围</summary>
    public void Normalize()
    {
        float min = float.MaxValue;
        float max = float.MinValue;

        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                float h = _heights[y, x];
                if (h < min) min = h;
                if (h > max) max = h;
            }
        }

        float range = max - min;
        if (range < 1e-6f) return;

        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                _heights[y, x] = (_heights[y, x] - min) / range;
            }
        }
    }

    /// <summary>将所有高度缩放到指定范围</summary>
    public void ScaleToRange(float minHeight, float maxHeight)
    {
        Normalize();
        float range = maxHeight - minHeight;

        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                _heights[y, x] = minHeight + _heights[y, x] * range;
            }
        }
    }

    /// <summary>添加高斯模糊平滑地形</summary>
    public void Smooth(int iterations = 1)
    {
        float[,] temp = (float[,])_heights.Clone();

        for (int iter = 0; iter < iterations; iter++)
        {
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    float sum = 0;
                    int count = 0;

                    // 3x3 平均
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = x + dx;
                            int ny = y + dy;
                            if (nx >= 0 && nx < Width && ny >= 0 && ny < Height)
                            {
                                sum += _heights[ny, nx];
                                count++;
                            }
                        }
                    }

                    temp[y, x] = sum / count;
                }
            }

            // 复制回原数组
            Array.Copy(temp, _heights, _heights.Length);
        }
    }

    /// <summary>计算平均高度</summary>
    public float GetAverageHeight()
    {
        float sum = 0;
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                sum += _heights[y, x];
            }
        }
        return sum / (Width * Height);
    }

    /// <summary>获取最小值和最大值</summary>
    public (float Min, float Max) GetMinMax()
    {
        float min = float.MaxValue;
        float max = float.MinValue;

        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                float h = _heights[y, x];
                if (h < min) min = h;
                if (h > max) max = h;
            }
        }

        return (min, max);
    }

    /// <summary>克隆高度场</summary>
    public Heightfield Clone()
    {
        return new Heightfield((float[,])_heights.Clone());
    }

    /// <summary>获取原始高度数组（用于可视化）</summary>
    public float[,] GetRawData() => _heights;
}
