using GPGems.Core.Math;

namespace GPGems.Core.Geometry;

/// <summary>
/// 距离计算工具类
/// 包含各种几何图元之间的距离计算
/// </summary>
public static class Distance
{
    /// <summary>点到直线的距离</summary>
    public static float PointToLine(Vector3 point, Vector3 lineStart, Vector3 lineEnd)
    {
        Vector3 line = lineEnd - lineStart;
        Vector3 dir = line.Normalize();
        Vector3 toPoint = point - lineStart;
        Vector3 projection = toPoint - dir * Vector3.Dot(toPoint, dir);
        return projection.Length();
    }

    /// <summary>点到线段的距离</summary>
    public static float PointToSegment(Vector3 point, Vector3 segStart, Vector3 segEnd)
    {
        Vector3 seg = segEnd - segStart;
        float segLenSq = seg.LengthSquared();

        if (segLenSq < 1e-6f)
            return Vector3.Distance(point, segStart);

        float t = MathUtil.Clamp(Vector3.Dot(point - segStart, seg) / segLenSq, 0, 1);
        Vector3 closest = segStart + seg * t;
        return Vector3.Distance(point, closest);
    }

    /// <summary>点到平面的距离（有符号）</summary>
    public static float PointToPlane(Vector3 point, Plane plane) => plane.DistanceToPoint(point);

    /// <summary>两条线段之间的距离</summary>
    public static float SegmentToSegment(Vector3 a0, Vector3 a1, Vector3 b0, Vector3 b1)
    {
        Vector3 u = a1 - a0;
        Vector3 v = b1 - b0;
        Vector3 w = a0 - b0;

        float a = Vector3.Dot(u, u);
        float b = Vector3.Dot(u, v);
        float c = Vector3.Dot(v, v);
        float d = Vector3.Dot(u, w);
        float e = Vector3.Dot(v, w);
        float denom = a * c - b * b;

        float s, t;

        if (denom < 1e-6f)
        {
            s = 0;
            t = (b > c ? d / b : e / c);
        }
        else
        {
            s = (b * e - c * d) / denom;
            t = (a * e - b * d) / denom;
        }

        s = MathUtil.Clamp(s, 0, 1);
        t = MathUtil.Clamp(t, 0, 1);

        Vector3 pA = a0 + u * s;
        Vector3 pB = b0 + v * t;
        return Vector3.Distance(pA, pB);
    }
}
