using System.Numerics;
namespace GPGems.Core.Math;

/// <summary>
/// Simplex 噪声
/// 比柏林噪声更快，各向同性更好，无明显方向性
/// 适合地形、粒子、程序化生成
/// </summary>
public class SimplexNoise
{
    private static readonly float F2 = 0.5f * (MathF.Sqrt(3) - 1);
    private static readonly float G2 = (3 - MathF.Sqrt(3)) / 6;
    private static readonly float F3 = 1f / 3f;
    private static readonly float G3 = 1f / 6f;

    private readonly int[] _perm;

    private static readonly sbyte[] _grad3 = {
        1,1,0, -1,1,0, 1,-1,0, -1,-1,0,
        1,0,1, -1,0,1, 1,0,-1, -1,0,-1,
        0,1,1, 0,-1,1, 0,1,-1, 0,-1,-1
    };

    public SimplexNoise(int seed = 0)
    {
        _perm = GeneratePermutation(seed);
    }

    /// <summary>2D Simplex 噪声</summary>
    public float Noise2D(float x, float y)
    {
        float s = (x + y) * F2;
        int i = (int)MathF.Floor(x + s);
        int j = (int)MathF.Floor(y + s);

        float t = (i + j) * G2;
        float X0 = i - t;
        float Y0 = j - t;
        float x0 = x - X0;
        float y0 = y - Y0;

        int i1, j1;
        if (x0 > y0) { i1 = 1; j1 = 0; }
        else { i1 = 0; j1 = 1; }

        float x1 = x0 - i1 + G2;
        float y1 = y0 - j1 + G2;
        float x2 = x0 - 1 + 2 * G2;
        float y2 = y0 - 1 + 2 * G2;

        int ii = i & 255;
        int jj = j & 255;

        float n0 = 0, n1 = 0, n2 = 0;

        float t0 = 0.5f - x0 * x0 - y0 * y0;
        if (t0 >= 0)
        {
            t0 *= t0;
            int gi0 = _perm[ii + _perm[jj]] % 12;
            n0 = t0 * t0 * (_grad3[gi0 * 3] * x0 + _grad3[gi0 * 3 + 1] * y0);
        }

        float t1 = 0.5f - x1 * x1 - y1 * y1;
        if (t1 >= 0)
        {
            t1 *= t1;
            int gi1 = _perm[ii + i1 + _perm[jj + j1]] % 12;
            n1 = t1 * t1 * (_grad3[gi1 * 3] * x1 + _grad3[gi1 * 3 + 1] * y1);
        }

        float t2 = 0.5f - x2 * x2 - y2 * y2;
        if (t2 >= 0)
        {
            t2 *= t2;
            int gi2 = _perm[ii + 1 + _perm[jj + 1]] % 12;
            n2 = t2 * t2 * (_grad3[gi2 * 3] * x2 + _grad3[gi2 * 3 + 1] * y2);
        }

        return 70 * (n0 + n1 + n2);
    }

    /// <summary>3D Simplex 噪声</summary>
    public float Noise3D(float x, float y, float z)
    {
        float s = (x + y + z) * F3;
        int i = (int)MathF.Floor(x + s);
        int j = (int)MathF.Floor(y + s);
        int k = (int)MathF.Floor(z + s);

        float t = (i + j + k) * G3;
        float X0 = i - t;
        float Y0 = j - t;
        float Z0 = k - t;
        float x0 = x - X0;
        float y0 = y - Y0;
        float z0 = z - Z0;

        int i1, j1, k1, i2, j2, k2;

        if (x0 >= y0)
        {
            if (y0 >= z0) { i1 = 1; j1 = 0; k1 = 0; i2 = 1; j2 = 1; k2 = 0; }
            else if (x0 >= z0) { i1 = 1; j1 = 0; k1 = 0; i2 = 1; j2 = 0; k2 = 1; }
            else { i1 = 0; j1 = 0; k1 = 1; i2 = 1; j2 = 0; k2 = 1; }
        }
        else
        {
            if (y0 < z0) { i1 = 0; j1 = 0; k1 = 1; i2 = 0; j2 = 1; k2 = 1; }
            else if (x0 < z0) { i1 = 0; j1 = 1; k1 = 0; i2 = 0; j2 = 1; k2 = 1; }
            else { i1 = 0; j1 = 1; k1 = 0; i2 = 1; j2 = 1; k2 = 0; }
        }

        float x1 = x0 - i1 + G3;
        float y1 = y0 - j1 + G3;
        float z1 = z0 - k1 + G3;
        float x2 = x0 - i2 + 2 * G3;
        float y2 = y0 - j2 + 2 * G3;
        float z2 = z0 - k2 + 2 * G3;
        float x3 = x0 - 1 + 3 * G3;
        float y3 = y0 - 1 + 3 * G3;
        float z3 = z0 - 1 + 3 * G3;

        int ii = i & 255;
        int jj = j & 255;
        int kk = k & 255;

        float n0 = 0, n1 = 0, n2 = 0, n3 = 0;

        float t0 = 0.6f - x0 * x0 - y0 * y0 - z0 * z0;
        if (t0 >= 0)
        {
            t0 *= t0;
            int gi0 = _perm[ii + _perm[jj + _perm[kk]]] % 12;
            n0 = t0 * t0 * (_grad3[gi0 * 3] * x0 + _grad3[gi0 * 3 + 1] * y0 + _grad3[gi0 * 3 + 2] * z0);
        }

        float t1 = 0.6f - x1 * x1 - y1 * y1 - z1 * z1;
        if (t1 >= 0)
        {
            t1 *= t1;
            int gi1 = _perm[ii + i1 + _perm[jj + j1 + _perm[kk + k1]]] % 12;
            n1 = t1 * t1 * (_grad3[gi1 * 3] * x1 + _grad3[gi1 * 3 + 1] * y1 + _grad3[gi1 * 3 + 2] * z1);
        }

        float t2 = 0.6f - x2 * x2 - y2 * y2 - z2 * z2;
        if (t2 >= 0)
        {
            t2 *= t2;
            int gi2 = _perm[ii + i2 + _perm[jj + j2 + _perm[kk + k2]]] % 12;
            n2 = t2 * t2 * (_grad3[gi2 * 3] * x2 + _grad3[gi2 * 3 + 1] * y2 + _grad3[gi2 * 3 + 2] * z2);
        }

        float t3 = 0.6f - x3 * x3 - y3 * y3 - z3 * z3;
        if (t3 >= 0)
        {
            t3 *= t3;
            int gi3 = _perm[ii + 1 + _perm[jj + 1 + _perm[kk + 1]]] % 12;
            n3 = t3 * t3 * (_grad3[gi3 * 3] * x3 + _grad3[gi3 * 3 + 1] * y3 + _grad3[gi3 * 3 + 2] * z3);
        }

        return 32 * (n0 + n1 + n2 + n3);
    }

    /// <summary>分形 Simplex 噪声</summary>
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
