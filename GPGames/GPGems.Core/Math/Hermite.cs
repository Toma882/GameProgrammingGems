namespace GPGems.Core.Math;

/// <summary>
/// 埃尔米特插值
/// 支持起点/终点的位置和切线控制，适合角色移动动画
/// </summary>
public static class Hermite
{
    /// <summary>
    /// 埃尔米特曲线插值
    /// </summary>
    /// <param name="p0">起点位置</param>
    /// <param name="v0">起点切线（速度向量）</param>
    /// <param name="p1">终点位置</param>
    /// <param name="v1">终点切线（速度向量）</param>
    /// <param name="t">插值参数 [0, 1]</param>
    public static Vector3 Interpolate(Vector3 p0, Vector3 v0, Vector3 p1, Vector3 v1, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;

        float h1 = 2 * t3 - 3 * t2 + 1;
        float h2 = -2 * t3 + 3 * t2;
        float h3 = t3 - 2 * t2 + t;
        float h4 = t3 - t2;

        return h1 * p0 + h2 * p1 + h3 * v0 + h4 * v1;
    }

    /// <summary>一阶导数（速度）</summary>
    public static Vector3 Derivative(Vector3 p0, Vector3 v0, Vector3 p1, Vector3 v1, float t)
    {
        float t2 = t * t;

        float h1 = 6 * t2 - 6 * t;
        float h2 = -6 * t2 + 6 * t;
        float h3 = 3 * t2 - 4 * t + 1;
        float h4 = 3 * t2 - 2 * t;

        return h1 * p0 + h2 * p1 + h3 * v0 + h4 * v1;
    }

    /// <summary>二阶导数（加速度）</summary>
    public static Vector3 SecondDerivative(Vector3 p0, Vector3 v0, Vector3 p1, Vector3 v1, float t)
    {
        float h1 = 12 * t - 6;
        float h2 = -12 * t + 6;
        float h3 = 6 * t - 4;
        float h4 = 6 * t - 2;

        return h1 * p0 + h2 * p1 + h3 * v0 + h4 * v1;
    }
}

/// <summary>
/// TCB 样条 (Tension/Continuity/Bias)
/// 埃尔米特插值的扩展，支持张力、连续性、偏移参数调整
/// </summary>
public class TCBSpline
{
    private readonly Vector3[] _points;

    public float Tension { get; set; }
    public float Continuity { get; set; }
    public float Bias { get; set; }

    public TCBSpline(Vector3[] points)
    {
        _points = points ?? throw new ArgumentNullException(nameof(points));
    }

    /// <summary>获取曲线上的点</summary>
    /// <param name="index">控制点段索引</param>
    /// <param name="t">插值参数 [0, 1]</param>
    public Vector3 GetPoint(int index, float t)
    {
        if (index < 0 || index >= _points.Length - 1)
            throw new ArgumentOutOfRangeException(nameof(index));

        var (v0, v1) = ComputeTangents(index);
        return Hermite.Interpolate(_points[index], v0, _points[index + 1], v1, t);
    }

    private (Vector3 inTangent, Vector3 outTangent) ComputeTangents(int index)
    {
        Vector3 prev = index > 0 ? _points[index - 1] : _points[index];
        Vector3 curr = _points[index];
        Vector3 next = index < _points.Length - 1 ? _points[index + 1] : _points[index];

        float t = (1 - Tension) * (1 + Continuity) * (1 + Bias) * 0.5f;
        float s = (1 - Tension) * (1 - Continuity) * (1 - Bias) * 0.5f;

        Vector3 outTangent = t * (curr - prev) + s * (next - curr);

        t = (1 - Tension) * (1 - Continuity) * (1 + Bias) * 0.5f;
        s = (1 - Tension) * (1 + Continuity) * (1 - Bias) * 0.5f;

        Vector3 inTangent = t * (next - curr) + s * (curr - prev);

        return (outTangent, inTangent);
    }
}
