using GPGems.Core.Math;

namespace GPGems.Core.Geometry;

/// <summary>
/// 2D 相交检测工具类
/// 用于 UI 碰撞、2D 游戏物理等
/// </summary>
public static class Intersection2D
{
    /// <summary>点是否在矩形内</summary>
    public static bool PointInRect(Vector2 point, Vector2 min, Vector2 max)
    {
        return point.X >= min.X && point.X <= max.X &&
               point.Y >= min.Y && point.Y <= max.Y;
    }

    /// <summary>点是否在三角形内（重心坐标法）</summary>
    public static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float v0x = c.X - a.X;
        float v0y = c.Y - a.Y;
        float v1x = b.X - a.X;
        float v1y = b.Y - a.Y;
        float v2x = p.X - a.X;
        float v2y = p.Y - a.Y;

        float dot00 = v0x * v0x + v0y * v0y;
        float dot01 = v0x * v1x + v0y * v1y;
        float dot02 = v0x * v2x + v0y * v2y;
        float dot11 = v1x * v1x + v1y * v1y;
        float dot12 = v1x * v2x + v1y * v2y;

        float denom = dot00 * dot11 - dot01 * dot01;
        if (MathF.Abs(denom) < 1e-6f)
            return false;

        float u = (dot11 * dot02 - dot01 * dot12) / denom;
        float v = (dot00 * dot12 - dot01 * dot02) / denom;

        return (u >= 0) && (v >= 0) && (u + v <= 1);
    }

    /// <summary>点是否在凸多边形内</summary>
    public static bool PointInConvexPolygon(Vector2 point, Vector2[] polygon)
    {
        if (polygon.Length < 3)
            return false;

        float sign = 0;
        for (int i = 0; i < polygon.Length; i++)
        {
            int j = (i + 1) % polygon.Length;
            float cross = Vector2.Cross(polygon[j] - polygon[i], point - polygon[i]);

            if (MathF.Abs(cross) < 1e-6f)
                continue;

            if (sign == 0)
                sign = cross;
            else if (cross * sign < 0)
                return false;
        }
        return true;
    }

    /// <summary>两个矩形是否相交</summary>
    public static bool RectIntersect(Vector2 minA, Vector2 maxA, Vector2 minB, Vector2 maxB)
    {
        return minA.X <= maxB.X && maxA.X >= minB.X &&
               minA.Y <= maxB.Y && maxA.Y >= minB.Y;
    }

    /// <summary>线段与线段相交检测</summary>
    public static bool SegmentIntersect(Vector2 a0, Vector2 a1, Vector2 b0, Vector2 b1)
    {
        float d1 = Direction(b0, b1, a0);
        float d2 = Direction(b0, b1, a1);
        float d3 = Direction(a0, a1, b0);
        float d4 = Direction(a0, a1, b1);

        if (((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) &&
            ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0)))
            return true;

        if (d1 == 0 && OnSegment(b0, b1, a0)) return true;
        if (d2 == 0 && OnSegment(b0, b1, a1)) return true;
        if (d3 == 0 && OnSegment(a0, a1, b0)) return true;
        if (d4 == 0 && OnSegment(a0, a1, b1)) return true;

        return false;
    }

    /// <summary>线段与线段求交点</summary>
    public static bool SegmentIntersect(Vector2 a0, Vector2 a1, Vector2 b0, Vector2 b1, out Vector2 intersection)
    {
        intersection = Vector2.Zero;

        Vector2 d1 = a1 - a0;
        Vector2 d2 = b1 - b0;

        float denom = d2.Y * d1.X - d2.X * d1.Y;
        if (MathF.Abs(denom) < 1e-6f)
            return false;

        float dx = b0.X - a0.X;
        float dy = b0.Y - a0.Y;

        float t = (d2.X * dy - d2.Y * dx) / denom;
        float u = (d1.X * dy - d1.Y * dx) / denom;

        if (t >= 0 && t <= 1 && u >= 0 && u <= 1)
        {
            intersection = a0 + d1 * t;
            return true;
        }

        return false;
    }

    private static float Direction(Vector2 p1, Vector2 p2, Vector2 p3)
    {
        return Vector2.Cross(p3 - p1, p2 - p1);
    }

    private static bool OnSegment(Vector2 p0, Vector2 p1, Vector2 p)
    {
        return p.X >= MathF.Min(p0.X, p1.X) && p.X <= MathF.Max(p0.X, p1.X) &&
               p.Y >= MathF.Min(p0.Y, p1.Y) && p.Y <= MathF.Max(p0.Y, p1.Y);
    }

    /// <summary>圆与圆相交</summary>
    public static bool CircleIntersect(Vector2 centerA, float radiusA, Vector2 centerB, float radiusB)
    {
        float distSq = (centerA - centerB).LengthSquared();
        float radiusSum = radiusA + radiusB;
        return distSq <= radiusSum * radiusSum;
    }

    /// <summary>线段与圆相交</summary>
    public static bool SegmentCircleIntersect(Vector2 start, Vector2 end, Vector2 center, float radius)
    {
        Vector2 seg = end - start;
        Vector2 toCenter = center - start;

        float t = Vector2.Dot(toCenter, seg) / seg.LengthSquared();
        t = MathUtil.Clamp(t, 0, 1);

        Vector2 closest = start + seg * t;
        float distSq = (center - closest).LengthSquared();

        return distSq <= radius * radius;
    }
}
