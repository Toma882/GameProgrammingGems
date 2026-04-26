namespace GPGems.Core.Math;

/// <summary>
/// 四元数 - 用于3D旋转表示
/// 避免万向节锁，支持平滑插值
/// </summary>
public readonly struct Quaternion
{
    public readonly float X, Y, Z, W;

    public static Quaternion Identity => new(0, 0, 0, 1);

    public Quaternion(float x, float y, float z, float w)
    {
        X = x;
        Y = y;
        Z = z;
        W = w;
    }

    /// <summary>从欧拉角创建四元数（弧度）</summary>
    public static Quaternion FromEuler(float pitch, float yaw, float roll)
    {
        float cy = MathF.Cos(roll * 0.5f);
        float sy = MathF.Sin(roll * 0.5f);
        float cp = MathF.Cos(yaw * 0.5f);
        float sp = MathF.Sin(yaw * 0.5f);
        float cx = MathF.Cos(pitch * 0.5f);
        float sx = MathF.Sin(pitch * 0.5f);

        return new Quaternion(
            sx * cp * cy - cx * sp * sy,
            cx * sp * cy + sx * cp * sy,
            cx * cp * sy - sx * sp * cy,
            cx * cp * cy + sx * sp * sy
        );
    }

    /// <summary>从轴角创建四元数</summary>
    public static Quaternion FromAxisAngle(Vector3 axis, float angle)
    {
        float halfAngle = angle * 0.5f;
        float s = MathF.Sin(halfAngle);
        return new Quaternion(
            axis.X * s,
            axis.Y * s,
            axis.Z * s,
            MathF.Cos(halfAngle)
        );
    }

    /// <summary>四元数长度</summary>
    public float Length() => MathF.Sqrt(X * X + Y * Y + Z * Z + W * W);

    /// <summary>单位化四元数</summary>
    public Quaternion Normalize()
    {
        float len = Length();
        return len > 1e-6f ? new Quaternion(X / len, Y / len, Z / len, W / len) : Identity;
    }

    /// <summary>共轭四元数（逆旋转）</summary>
    public Quaternion Conjugate() => new(-X, -Y, -Z, W);

    /// <summary>用四元数旋转向量</summary>
    public Vector3 Rotate(Vector3 v)
    {
        Vector3 q = new(X, Y, Z);
        Vector3 t = 2 * Vector3.Cross(q, v);
        return v + W * t + Vector3.Cross(q, t);
    }

    /// <summary>四元数点积（用于测量旋转相似度）</summary>
    public static float Dot(Quaternion a, Quaternion b)
    {
        return a.X * b.X + a.Y * b.Y + a.Z * b.Z + a.W * b.W;
    }

    public static Quaternion operator *(Quaternion a, Quaternion b)
    {
        return new Quaternion(
            a.W * b.X + a.X * b.W + a.Y * b.Z - a.Z * b.Y,
            a.W * b.Y + a.Y * b.W + a.Z * b.X - a.X * b.Z,
            a.W * b.Z + a.Z * b.W + a.X * b.Y - a.Y * b.X,
            a.W * b.W - a.X * b.X - a.Y * b.Y - a.Z * b.Z
        );
    }
}
