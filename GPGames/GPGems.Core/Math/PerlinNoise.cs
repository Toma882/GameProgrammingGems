using System.Numerics;
namespace GPGems.Core.Math;

/// <summary>
/// 柏林噪声
/// 用于地形生成、纹理、自然运动等
/// </summary>
public class PerlinNoise
{
    private readonly int[] _permutation;

    public PerlinNoise(int seed = 0)
    {
        _permutation = GeneratePermutation(seed);
    }

    /// <summary>2D 柏林噪声</summary>
    public float Noise2D(float x, float y)
    {
        int xi = (int)MathF.Floor(x) & 255;
        int yi = (int)MathF.Floor(y) & 255;

        float xf = x - MathF.Floor(x);
        float yf = y - MathF.Floor(y);

        float u = Fade(xf);
        float v = Fade(yf);

        int aa = _permutation[_permutation[xi] + yi] & 255;
        int ab = _permutation[_permutation[xi] + yi + 1] & 255;
        int ba = _permutation[_permutation[xi + 1] + yi] & 255;
        int bb = _permutation[_permutation[xi + 1] + yi + 1] & 255;

        float x1 = Lerp(Grad(aa, xf, yf), Grad(ba, xf - 1, yf), u);
        float x2 = Lerp(Grad(ab, xf, yf - 1), Grad(bb, xf - 1, yf - 1), u);

        return Lerp(x1, x2, v);
    }

    /// <summary>3D 柏林噪声</summary>
    public float Noise3D(float x, float y, float z)
    {
        int xi = (int)MathF.Floor(x) & 255;
        int yi = (int)MathF.Floor(y) & 255;
        int zi = (int)MathF.Floor(z) & 255;

        float xf = x - MathF.Floor(x);
        float yf = y - MathF.Floor(y);
        float zf = z - MathF.Floor(z);

        float u = Fade(xf);
        float v = Fade(yf);
        float w = Fade(zf);

        int a = _permutation[xi] + yi;
        int aa = _permutation[a] + zi;
        int ab = _permutation[a + 1] + zi;
        int b = _permutation[xi + 1] + yi;
        int ba = _permutation[b] + zi;
        int bb = _permutation[b + 1] + zi;

        float x1 = Lerp(Grad(aa, xf, yf, zf), Grad(ba, xf - 1, yf, zf), u);
        float x2 = Lerp(Grad(ab, xf, yf - 1, zf), Grad(bb, xf - 1, yf - 1, zf), u);
        float y1 = Lerp(x1, x2, v);

        x1 = Lerp(Grad(aa + 1, xf, yf, zf - 1), Grad(ba + 1, xf - 1, yf, zf - 1), u);
        x2 = Lerp(Grad(ab + 1, xf, yf - 1, zf - 1), Grad(bb + 1, xf - 1, yf - 1, zf - 1), u);
        float y2 = Lerp(x1, x2, v);

        return Lerp(y1, y2, w);
    }

    /// <summary>分形噪声（多重叠加）</summary>
    public float FractalNoise(float x, float y, int octaves = 4, float persistence = 0.5f)
    {
        float total = 0;
        float frequency = 1;
        float amplitude = 1;
        float maxValue = 0;

        for (int i = 0; i < octaves; i++)
        {
            total += Noise2D(x * frequency, y * frequency) * amplitude;
            maxValue += amplitude;
            amplitude *= persistence;
            frequency *= 2;
        }

        return total / maxValue;
    }

    private static float Fade(float t) => t * t * t * (t * (t * 6 - 15) + 10);

    private static float Lerp(float a, float b, float t) => a + t * (b - a);

    private static float Grad(int hash, float x, float y)
    {
        return ((hash & 1) == 0 ? x : -x) + ((hash & 2) == 0 ? y : -y);
    }

    private static float Grad(int hash, float x, float y, float z)
    {
        int h = hash & 15;
        float u = h < 8 ? x : y;
        float v = h < 4 ? y : h == 12 || h == 14 ? x : z;
        return ((h & 1) == 0 ? u : -u) + ((h & 2) == 0 ? v : -v);
    }

    private static int[] GeneratePermutation(int seed)
    {
        var random = new Random(seed);
        var p = new int[256];
        for (int i = 0; i < 256; i++)
            p[i] = i;

        for (int i = 255; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (p[i], p[j]) = (p[j], p[i]);
        }

        var perm = new int[512];
        for (int i = 0; i < 512; i++)
            perm[i] = p[i & 255];

        return perm;
    }
}
