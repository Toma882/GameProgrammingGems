/*
 * System.Numerics 扩展方法
 * 补充游戏开发常用的便利函数，同时保留 SIMD 硬件加速性能
 */

using System.Numerics;

namespace GPGems.Core.Math;

/// <summary>
/// System.Numerics.Vector2/3 的扩展方法
/// </summary>
public static class VectorExtensions
{
    #region Vector2

    /// <summary>两点距离</summary>
    public static float Distance(this Vector2 a, Vector2 b) => Vector2.Distance(a, b);

    /// <summary>两点距离的平方（避免开平方，更高效）</summary>
    public static float DistanceSquared(this Vector2 a, Vector2 b) => Vector2.DistanceSquared(a, b);

    /// <summary>叉积</summary>
    public static float Cross(this Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;

    /// <summary>设置向量的长度</summary>
    public static Vector2 SetMagnitude(this Vector2 v, float magnitude)
    {
        float len = v.Length();
        return len > 1e-6f ? v * (magnitude / len) : Vector2.Zero;
    }

    /// <summary>单位化向量（返回新向量，不修改原向量）</summary>
    public static Vector2 Normalized(this Vector2 v) => Vector2.Normalize(v);

    /// <summary>单位化向量（实例方法兼容）</summary>
    public static Vector2 Normalize(this Vector2 v) => Vector2.Normalize(v);

    #endregion

    #region Vector3

    /// <summary>两点距离</summary>
    public static float Distance(this Vector3 a, Vector3 b) => Vector3.Distance(a, b);

    /// <summary>两点距离的平方（避免开平方，更高效）</summary>
    public static float DistanceSquared(this Vector3 a, Vector3 b) => Vector3.DistanceSquared(a, b);

    /// <summary>按分量取最小值</summary>
    public static Vector3 Min(this Vector3 a, Vector3 b) => Vector3.Min(a, b);

    /// <summary>按分量取最大值</summary>
    public static Vector3 Max(this Vector3 a, Vector3 b) => Vector3.Max(a, b);

    /// <summary>设置向量的长度</summary>
    public static Vector3 SetMagnitude(this Vector3 v, float magnitude)
    {
        float len = v.Length();
        return len > 1e-6f ? v * (magnitude / len) : Vector3.Zero;
    }

    /// <summary>单位化向量（返回新向量，不修改原向量）</summary>
    public static Vector3 Normalized(this Vector3 v) => Vector3.Normalize(v);

    /// <summary>将向量各分量限制在指定范围内</summary>
    public static Vector3 Clamp(this Vector3 value, Vector3 min, Vector3 max) => Vector3.Clamp(value, min, max);

    /// <summary>单位化向量（实例方法兼容）</summary>
    public static Vector3 Normalize(this Vector3 v) => Vector3.Normalize(v);

    #endregion

    #region Quaternion

    /// <summary>用四元数旋转向量</summary>
    public static Vector3 Rotate(this Quaternion q, Vector3 v)
    {
        Vector3 qVec = new(q.X, q.Y, q.Z);
        Vector3 t = 2 * Vector3.Cross(qVec, v);
        return v + q.W * t + Vector3.Cross(qVec, t);
    }

    /// <summary>共轭四元数（逆旋转，实例方法兼容）</summary>
    public static Quaternion Conjugate(this Quaternion q) => Quaternion.Conjugate(q);

    /// <summary>单位化四元数（实例方法兼容）</summary>
    public static Quaternion Normalize(this Quaternion q) => Quaternion.Normalize(q);

    /// <summary>从欧拉角创建四元数</summary>
    public static Quaternion CreateFromEuler(float pitch, float yaw, float roll)
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

    #endregion

    #region Plane

    /// <summary>从三点创建平面（右手定则）</summary>
    public static Plane CreateFromPoints(Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 normal = Vector3.Normalize(Vector3.Cross(b - a, c - a));
        return new Plane(normal, -Vector3.Dot(normal, a));
    }

    /// <summary>计算点到平面的有符号距离</summary>
    /// <returns>正数在平面前方（法向量方向），负数在后方，0在平面上</returns>
    public static float DistanceToPoint(this Plane plane, Vector3 point)
    {
        return Vector3.Dot(plane.Normal, point) + plane.D;
    }

    /// <summary>判断点相对于平面的位置</summary>
    public static PlaneSide ClassifyPoint(this Plane plane, Vector3 point, float epsilon = 1e-6f)
    {
        float distance = DistanceToPoint(plane, point);
        if (distance > epsilon) return PlaneSide.Front;
        if (distance < -epsilon) return PlaneSide.Back;
        return PlaneSide.OnPlane;
    }

    /// <summary>翻转平面方向</summary>
    public static Plane Flipped(this Plane plane) => new(-plane.Normal, -plane.D);

    #endregion
}

/// <summary>点/多边形相对于平面的分类结果</summary>
public enum PlaneSide
{
    Front,    // 在平面前方（法向量方向）
    Back,     // 在平面后方
    OnPlane,  // 在平面上
    Spanning  // 跨平面（仅用于多边形）
}
