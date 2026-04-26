using GPGems.Core.Math;

namespace GPGems.Core.Geometry;

/// <summary>
/// 3D 相交检测工具类
/// 用于 3D 碰撞、射线检测等
/// </summary>
public static class Intersection3D
{
    /// <summary>AABB 与 AABB 相交</summary>
    public static bool AABBIntersect(Bounds a, Bounds b)
    {
        return a.Min.X <= b.Max.X && a.Max.X >= b.Min.X &&
               a.Min.Y <= b.Max.Y && a.Max.Y >= b.Min.Y &&
               a.Min.Z <= b.Max.Z && a.Max.Z >= b.Min.Z;
    }

    /// <summary>点是否在 AABB 内</summary>
    public static bool PointInAABB(Vector3 point, Bounds bounds)
    {
        return point.X >= bounds.Min.X && point.X <= bounds.Max.X &&
               point.Y >= bounds.Min.Y && point.Y <= bounds.Max.Y &&
               point.Z >= bounds.Min.Z && point.Z <= bounds.Max.Z;
    }

    /// <summary>射线与平面相交</summary>
    public static bool RayPlane(Ray ray, Plane plane, out float t)
    {
        t = 0;
        float denom = Vector3.Dot(plane.Normal, ray.Direction);

        if (MathF.Abs(denom) < 1e-6f)
            return false;

        t = -plane.DistanceToPoint(ray.Origin) / denom;
        return t >= 0;
    }

    /// <summary>射线与三角形相交（Moller-Trumbore 算法）</summary>
    public static bool RayTriangle(Ray ray, Vector3 a, Vector3 b, Vector3 c, out float t)
    {
        t = 0;
        Vector3 e1 = b - a;
        Vector3 e2 = c - a;

        Vector3 h = Vector3.Cross(ray.Direction, e2);
        float det = Vector3.Dot(e1, h);

        if (det > -1e-6f && det < 1e-6f)
            return false;

        float invDet = 1 / det;
        Vector3 s = ray.Origin - a;
        float u = Vector3.Dot(s, h) * invDet;

        if (u < 0 || u > 1)
            return false;

        Vector3 q = Vector3.Cross(s, e1);
        float v = Vector3.Dot(ray.Direction, q) * invDet;

        if (v < 0 || u + v > 1)
            return false;

        t = Vector3.Dot(e2, q) * invDet;
        return t > 1e-6f;
    }

    /// <summary>射线与 AABB 相交（slabs 算法）</summary>
    public static bool RayAABB(Ray ray, Bounds bounds, out float tMin, out float tMax)
    {
        tMin = 0;
        tMax = float.MaxValue;

        float[] invDir = { 1 / ray.Direction.X, 1 / ray.Direction.Y, 1 / ray.Direction.Z };
        float[] min = { bounds.Min.X, bounds.Min.Y, bounds.Min.Z };
        float[] max = { bounds.Max.X, bounds.Max.Y, bounds.Max.Z };

        for (int i = 0; i < 3; i++)
        {
            float t1 = (min[i] - ray.Origin[i]) * invDir[i];
            float t2 = (max[i] - ray.Origin[i]) * invDir[i];

            if (t1 > t2)
                (t1, t2) = (t2, t1);

            tMin = MathF.Max(tMin, t1);
            tMax = MathF.Min(tMax, t2);

            if (tMin > tMax)
                return false;
        }

        return tMax >= 0;
    }

    /// <summary>射线与球体相交</summary>
    public static bool RaySphere(Ray ray, Vector3 center, float radius, out float t)
    {
        t = 0;
        Vector3 oc = ray.Origin - center;
        float a = ray.Direction.LengthSquared();
        float b = 2 * Vector3.Dot(oc, ray.Direction);
        float c = oc.LengthSquared() - radius * radius;
        float discriminant = b * b - 4 * a * c;

        if (discriminant < 0)
            return false;

        float sqrtD = MathF.Sqrt(discriminant);
        t = (-b - sqrtD) / (2 * a);

        if (t < 0)
            t = (-b + sqrtD) / (2 * a);

        return t >= 0;
    }

    /// <summary>球体与球体相交</summary>
    public static bool SphereIntersect(Vector3 centerA, float radiusA, Vector3 centerB, float radiusB)
    {
        float distSq = (centerA - centerB).LengthSquared();
        float radiusSum = radiusA + radiusB;
        return distSq <= radiusSum * radiusSum;
    }

    /// <summary>AABB 与球体相交</summary>
    public static bool AABBSphere(Bounds bounds, Vector3 center, float radius)
    {
        Vector3 closest = ClosestPoint.OnBounds(center, bounds);
        float distSq = (center - closest).LengthSquared();
        return distSq <= radius * radius;
    }

    /// <summary>OBB 与 OBB 相交（分离轴定理）</summary>
    public static bool OBBIntersect(OrientedBox a, OrientedBox b)
    {
        Vector3[] axes = {
            a.AxisX, a.AxisY, a.AxisZ,
            b.AxisX, b.AxisY, b.AxisZ,
            Vector3.Cross(a.AxisX, b.AxisX),
            Vector3.Cross(a.AxisX, b.AxisY),
            Vector3.Cross(a.AxisX, b.AxisZ),
            Vector3.Cross(a.AxisY, b.AxisX),
            Vector3.Cross(a.AxisY, b.AxisY),
            Vector3.Cross(a.AxisY, b.AxisZ),
            Vector3.Cross(a.AxisZ, b.AxisX),
            Vector3.Cross(a.AxisZ, b.AxisY),
            Vector3.Cross(a.AxisZ, b.AxisZ)
        };

        Vector3 delta = b.Center - a.Center;

        foreach (var axis in axes)
        {
            if (axis.LengthSquared() < 1e-6f)
                continue;

            float projA = ProjectOBB(a, axis);
            float projB = ProjectOBB(b, axis);
            float dist = MathF.Abs(Vector3.Dot(delta, axis));

            if (dist > projA + projB)
                return false;
        }

        return true;
    }

    private static float ProjectOBB(OrientedBox box, Vector3 axis)
    {
        return box.Extents.X * MathF.Abs(Vector3.Dot(axis, box.AxisX)) +
               box.Extents.Y * MathF.Abs(Vector3.Dot(axis, box.AxisY)) +
               box.Extents.Z * MathF.Abs(Vector3.Dot(axis, box.AxisZ));
    }
}

/// <summary>
/// 射线表示
/// </summary>
public readonly struct Ray
{
    public Vector3 Origin { get; }
    public Vector3 Direction { get; }

    public Ray(Vector3 origin, Vector3 direction)
    {
        Origin = origin;
        Direction = direction.Normalize();
    }

    public Vector3 GetPoint(float t) => Origin + Direction * t;
}
