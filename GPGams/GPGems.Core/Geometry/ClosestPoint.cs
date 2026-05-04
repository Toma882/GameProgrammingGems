using System.Numerics;
using GPGems.Core.Math;

namespace GPGems.Core.Geometry;

/// <summary>
/// 最近点计算工具类
/// 用于碰撞检测、路径查找等
/// </summary>
public static class ClosestPoint
{
    /// <summary>线段上离给定点最近的点</summary>
    public static Vector3 OnSegment(Vector3 point, Vector3 segStart, Vector3 segEnd)
    {
        Vector3 seg = segEnd - segStart;
        float segLenSq = seg.LengthSquared();

        if (segLenSq < 1e-6f)
            return segStart;

        float t = Vector3.Dot(point - segStart, seg) / segLenSq;
        t = MathUtil.Clamp(t, 0, 1);
        return segStart + seg * t;
    }

    /// <summary>AABB 上离给定点最近的点</summary>
    public static Vector3 OnBounds(Vector3 point, Bounds bounds)
    {
        return Vector3.Clamp(point, bounds.Min, bounds.Max);
    }

    /// <summary>三角形上离给定点最近的点</summary>
    public static Vector3 OnTriangle(Vector3 point, Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 ab = b - a;
        Vector3 ac = c - a;
        Vector3 ap = point - a;

        float dotABAB = Vector3.Dot(ab, ab);
        float dotABAC = Vector3.Dot(ab, ac);
        float dotACAC = Vector3.Dot(ac, ac);
        float dotAPAB = Vector3.Dot(ap, ab);
        float dotAPAC = Vector3.Dot(ap, ac);

        float denom = dotABAB * dotACAC - dotABAC * dotABAC;
        float v = (dotACAC * dotAPAB - dotABAC * dotAPAC) / denom;
        float w = (dotABAB * dotAPAC - dotABAC * dotAPAB) / denom;
        float u = 1 - v - w;

        if (u >= 0 && v >= 0 && w >= 0)
            return a * u + b * v + c * w;

        Vector3 e0 = OnSegment(point, a, b);
        Vector3 e1 = OnSegment(point, b, c);
        Vector3 e2 = OnSegment(point, c, a);

        float d0 = (point - e0).LengthSquared();
        float d1 = (point - e1).LengthSquared();
        float d2 = (point - e2).LengthSquared();

        float minD = MathUtil.Min(d0, MathUtil.Min(d1, d2));
        if (minD == d0) return e0;
        if (minD == d1) return e1;
        return e2;
    }

    /// <summary>OBB 上离给定点最近的点</summary>
    public static Vector3 OnOrientedBox(Vector3 point, OrientedBox obb)
    {
        Vector3 local = point - obb.Center;
        float x = Vector3.Dot(local, obb.AxisX);
        float y = Vector3.Dot(local, obb.AxisY);
        float z = Vector3.Dot(local, obb.AxisZ);

        x = MathUtil.Clamp(x, -obb.Extents.X, obb.Extents.X);
        y = MathUtil.Clamp(y, -obb.Extents.Y, obb.Extents.Y);
        z = MathUtil.Clamp(z, -obb.Extents.Z, obb.Extents.Z);

        return obb.Center + obb.AxisX * x + obb.AxisY * y + obb.AxisZ * z;
    }

    /// <summary>球体上离给定点最近的点</summary>
    public static Vector3 OnSphere(Vector3 point, Vector3 center, float radius)
    {
        Vector3 dir = (point - center).Normalize();
        return center + dir * radius;
    }
}

/// <summary>
/// 方向包围盒（OBB）
/// 比 AABB 更精确的包围盒表示
/// </summary>
public readonly struct OrientedBox
{
    public Vector3 Center { get; }
    public Vector3 AxisX { get; }
    public Vector3 AxisY { get; }
    public Vector3 AxisZ { get; }
    public Vector3 Extents { get; }

    public OrientedBox(Vector3 center, Vector3 extents, Quaternion orientation)
    {
        Center = center;
        Extents = extents;
        AxisX = orientation.Rotate(Vector3.UnitX);
        AxisY = orientation.Rotate(Vector3.UnitY);
        AxisZ = orientation.Rotate(Vector3.UnitZ);
    }

    /// <summary>从 AABB 创建 OBB</summary>
    public static OrientedBox FromBounds(Bounds bounds, Quaternion orientation)
    {
        return new OrientedBox(bounds.Center, bounds.Extents, orientation);
    }
}
