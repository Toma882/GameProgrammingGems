using System.Numerics;

namespace GPGems.Core.Math;

/// <summary>
/// 轴对齐包围盒（AABB）
/// 用于空间分割的边界表示
/// </summary>
public struct Bounds
{
    public Vector3 Center { get; }
    public Vector3 Extents { get; }
    public Vector3 Min => Center - Extents;
    public Vector3 Max => Center + Extents;

    public float Width => Extents.X * 2;
    public float Height => Extents.Y * 2;
    public float Depth => Extents.Z * 2;

    public Bounds(Vector3 center, Vector3 extents)
    {
        Center = center;
        Extents = extents;
    }

    /// <summary>从最小/最大点创建包围盒</summary>
    public static Bounds FromMinMax(Vector3 min, Vector3 max)
    {
        Vector3 center = (min + max) / 2;
        Vector3 extents = (max - min) / 2;
        return new Bounds(center, extents);
    }

    /// <summary>检查点是否在包围盒内</summary>
    public bool Contains(Vector3 point)
    {
        return point.X >= Min.X && point.X <= Max.X &&
               point.Y >= Min.Y && point.Y <= Max.Y &&
               point.Z >= Min.Z && point.Z <= Max.Z;
    }

    /// <summary>检查与另一个包围盒是否相交</summary>
    public bool Intersects(Bounds other)
    {
        return Min.X <= other.Max.X && Max.X >= other.Min.X &&
               Min.Y <= other.Max.Y && Max.Y >= other.Min.Y &&
               Min.Z <= other.Max.Z && Max.Z >= other.Min.Z;
    }

    /// <summary>检查是否完全包含另一个包围盒</summary>
    public bool Contains(Bounds other)
    {
        return Min.X <= other.Min.X && Max.X >= other.Max.X &&
               Min.Y <= other.Min.Y && Max.Y >= other.Max.Y &&
               Min.Z <= other.Min.Z && Max.Z >= other.Max.Z;
    }

    /// <summary>计算表面积（用于启发式评估）</summary>
    public float SurfaceArea()
    {
        float wx = Max.X - Min.X;
        float wy = Max.Y - Min.Y;
        float wz = Max.Z - Min.Z;
        return 2 * (wx * wy + wy * wz + wz * wx);
    }
}
