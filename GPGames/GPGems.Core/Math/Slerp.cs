using System.Numerics;
namespace GPGems.Core.Math;

/// <summary>
/// 球面线性插值（Slerp）
/// 用于四元数和方向向量的平滑过渡
/// </summary>
public static class Slerp
{
    /// <summary>四元数球面插值</summary>
    /// <param name="a">起始四元数</param>
    /// <param name="b">目标四元数</param>
    /// <param name="t">插值参数 [0, 1]</param>
    public static Quaternion Interpolate(Quaternion a, Quaternion b, float t)
    {
        float dot = Quaternion.Dot(a, b);

        if (dot < 0)
        {
            b = new Quaternion(-b.X, -b.Y, -b.Z, -b.W);
            dot = -dot;
        }

        const float Epsilon = 1e-6f;
        if (dot > 1 - Epsilon)
        {
            return new Quaternion(
                a.X + t * (b.X - a.X),
                a.Y + t * (b.Y - a.Y),
                a.Z + t * (b.Z - a.Z),
                a.W + t * (b.W - a.W)
            ).Normalize();
        }

        dot = MathUtil.Clamp(dot, -1f, 1f);
        float theta0 = MathF.Acos(dot);
        float theta = theta0 * t;

        float sinTheta = MathF.Sin(theta);
        float sinTheta0 = MathF.Sin(theta0);

        float s1 = sinTheta / sinTheta0;
        float s0 = MathF.Cos(theta) - dot * s1;

        return new Quaternion(
            s0 * a.X + s1 * b.X,
            s0 * a.Y + s1 * b.Y,
            s0 * a.Z + s1 * b.Z,
            s0 * a.W + s1 * b.W
        );
    }

    /// <summary>向量球面插值（保持长度不变）</summary>
    public static Vector3 Interpolate(Vector3 a, Vector3 b, float t)
    {
        a = a.Normalize();
        b = b.Normalize();

        float dot = Vector3.Dot(a, b);
        dot = MathUtil.Clamp(dot, -1f, 1f);

        float theta = MathF.Acos(dot) * t;
        Vector3 relative = (b - a * dot).Normalize();

        return a * MathF.Cos(theta) + relative * MathF.Sin(theta);
    }
}
