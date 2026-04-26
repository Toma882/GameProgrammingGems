namespace GPGems.Core.Math;

/// <summary>
/// 3D 平面
/// 使用 ax + by + cz + d = 0 表示，其中 (a,b,c) 是法向量
/// 用于 BSP 树的分割面、碰撞检测等
/// </summary>
public readonly struct Plane
{
    /// <summary>平面法向量（已单位化）</summary>
    public Vector3 Normal { get; }

    /// <summary>平面常数项 d</summary>
    public float D { get; }

    /// <summary>创建平面</summary>
    public Plane(Vector3 normal, float d)
    {
        Normal = normal.Normalize();
        D = d;
    }

    /// <summary>从法向量和平面上一点创建平面</summary>
    public Plane(Vector3 normal, Vector3 point)
    {
        Normal = normal.Normalize();
        D = -Vector3.Dot(Normal, point);
    }

    /// <summary>从三点创建平面（右手定则）</summary>
    public static Plane FromPoints(Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 normal = Vector3.Cross(b - a, c - a);
        return new Plane(normal, a);
    }

    /// <summary>计算点到平面的有符号距离</summary>
    /// <returns>正数在平面前方（法向量方向），负数在后方，0在平面上</returns>
    public float DistanceToPoint(Vector3 point)
    {
        return Vector3.Dot(Normal, point) + D;
    }

    /// <summary>判断点相对于平面的位置</summary>
    public PlaneSide ClassifyPoint(Vector3 point, float epsilon = 1e-6f)
    {
        float distance = DistanceToPoint(point);
        if (distance > epsilon) return PlaneSide.Front;
        if (distance < -epsilon) return PlaneSide.Back;
        return PlaneSide.OnPlane;
    }

    /// <summary>翻转平面方向</summary>
    public Plane Flipped() => new(-Normal, -D);
}

/// <summary>点/多边形相对于平面的分类结果</summary>
public enum PlaneSide
{
    Front,    // 在平面前方（法向量方向）
    Back,     // 在平面后方
    OnPlane,  // 在平面上
    Spanning  // 跨平面（仅用于多边形）
}
