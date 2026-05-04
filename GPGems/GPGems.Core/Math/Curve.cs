using System.Numerics;
namespace GPGems.Core.Math;

/// <summary>
/// 贝塞尔曲线
/// 用于路径平滑、动画轨迹
/// </summary>
public static class BezierCurve
{
    /// <summary>二次贝塞尔曲线</summary>
    public static Vector3 Quadratic(Vector3 p0, Vector3 p1, Vector3 p2, float t)
    {
        float u = 1 - t;
        return u * u * p0 + 2 * u * t * p1 + t * t * p2;
    }

    /// <summary>三次贝塞尔曲线</summary>
    public static Vector3 Cubic(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float u = 1 - t;
        return u * u * u * p0 + 3 * u * u * t * p1 + 3 * u * t * t * p2 + t * t * t * p3;
    }

    /// <summary>二次贝塞尔曲线一阶导数（速度）</summary>
    public static Vector3 QuadraticDerivative(Vector3 p0, Vector3 p1, Vector3 p2, float t)
    {
        float u = 1 - t;
        return 2 * u * (p1 - p0) + 2 * t * (p2 - p1);
    }

    /// <summary>三次贝塞尔曲线一阶导数（速度）</summary>
    public static Vector3 CubicDerivative(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float u = 1 - t;
        return 3 * u * u * (p1 - p0) + 6 * u * t * (p2 - p1) + 3 * t * t * (p3 - p2);
    }
}

/// <summary>
/// Catmull-Rom 样条曲线
/// 经过所有控制点的平滑曲线，适合相机路径等
/// </summary>
public static class CatmullRomSpline
{
    /// <summary>Catmull-Rom 插值（需要4个控制点，插值在p1-p2之间）</summary>
    public static Vector3 Interpolate(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;

        return 0.5f * (
            (-t3 + 2 * t2 - t) * p0 +
            (3 * t3 - 5 * t2 + 2) * p1 +
            (-3 * t3 + 4 * t2 + t) * p2 +
            (t3 - t2) * p3
        );
    }

    /// <summary>从点数组计算曲线点</summary>
    public static Vector3[] ComputeSpline(Vector3[] controlPoints, int segmentsPerSpan = 10)
    {
        if (controlPoints.Length < 4)
            throw new ArgumentException("At least 4 control points required");

        var result = new List<Vector3>();

        for (int i = 0; i < controlPoints.Length - 3; i++)
        {
            for (int j = 0; j <= segmentsPerSpan; j++)
            {
                float t = (float)j / segmentsPerSpan;
                result.Add(Interpolate(controlPoints[i], controlPoints[i + 1], controlPoints[i + 2], controlPoints[i + 3], t));
            }
        }

        return result.ToArray();
    }
}
