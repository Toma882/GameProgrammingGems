/* Copyright (C) Steven Woodcock, 2000.
 * Ported to C# from Game Programming Gems 1
 */

using System;

namespace GPGems.Core.Math;

/// <summary>
/// 3D 向量 - 封装了向量运算和变换
/// 用于 Boids 群体行为的物理计算
/// </summary>
/// <summary>
/// 2D 向量
/// </summary>
public readonly struct Vector2
{
    public readonly float X, Y;

    public static Vector2 Zero => new(0, 0);
    public static Vector2 One => new(1, 1);
    public static Vector2 UnitX => new(1, 0);
    public static Vector2 UnitY => new(0, 1);

    public Vector2(float x, float y)
    {
        X = x;
        Y = y;
    }

    public float Length() => MathF.Sqrt(X * X + Y * Y);
    public float LengthSquared() => X * X + Y * Y;

    public Vector2 Normalize()
    {
        float len = Length();
        return len > 1e-6f ? new Vector2(X / len, Y / len) : Zero;
    }

    public static float Dot(Vector2 a, Vector2 b) => a.X * b.X + a.Y * b.Y;
    public static float Cross(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;
    public static float Distance(Vector2 a, Vector2 b) => MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    public static Vector2 operator +(Vector2 a, Vector2 b) => new(a.X + b.X, a.Y + b.Y);
    public static Vector2 operator -(Vector2 a, Vector2 b) => new(a.X - b.X, a.Y - b.Y);
    public static Vector2 operator -(Vector2 v) => new(-v.X, -v.Y);
    public static Vector2 operator *(Vector2 v, float s) => new(v.X * s, v.Y * s);
    public static Vector2 operator /(Vector2 v, float s) => new(v.X / s, v.Y / s);
}

/// <summary>
/// 3D 向量 - 封装了向量运算和变换
/// 用于 Boids 群体行为的物理计算
/// </summary>
public readonly struct Vector3 : IEquatable<Vector3>
{
    public readonly float X, Y, Z;

    public static Vector3 Zero => new(0, 0, 0);
    public static Vector3 One => new(1, 1, 1);
    public static Vector3 UnitX => new(1, 0, 0);
    public static Vector3 UnitY => new(0, 1, 0);
    public static Vector3 UnitZ => new(0, 0, 1);

    public Vector3(float x, float y, float z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    /// <summary>向量长度（模）</summary>
    public float Length() => MathF.Sqrt(X * X + Y * Y + Z * Z);

    /// <summary>向量长度的平方（避免开平方，更高效）</summary>
    public float LengthSquared() => X * X + Y * Y + Z * Z;

    /// <summary>单位化向量</summary>
    public Vector3 Normalize()
    {
        float len = Length();
        return len > 1e-6f ? new Vector3(X / len, Y / len, Z / len) : Zero;
    }

    /// <summary>设置向量的长度</summary>
    public Vector3 SetMagnitude(float magnitude)
    {
        return Normalize() * magnitude;
    }

    /// <summary>两点距离</summary>
    public static float Distance(Vector3 a, Vector3 b)
    {
        float dx = a.X - b.X;
        float dy = a.Y - b.Y;
        float dz = a.Z - b.Z;
        return MathF.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    /// <summary>点积</summary>
    public static float Dot(Vector3 a, Vector3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

    /// <summary>叉积</summary>
    public static Vector3 Cross(Vector3 a, Vector3 b)
    {
        return new Vector3(
            a.Y * b.Z - a.Z * b.Y,
            a.Z * b.X - a.X * b.Z,
            a.X * b.Y - a.Y * b.X
        );
    }

    /// <summary>按分量取最小值</summary>
    public static Vector3 Min(Vector3 a, Vector3 b)
    {
        return new Vector3(
            MathF.Min(a.X, b.X),
            MathF.Min(a.Y, b.Y),
            MathF.Min(a.Z, b.Z)
        );
    }

    /// <summary>按分量取最大值</summary>
    public static Vector3 Max(Vector3 a, Vector3 b)
    {
        return new Vector3(
            MathF.Max(a.X, b.X),
            MathF.Max(a.Y, b.Y),
            MathF.Max(a.Z, b.Z)
        );
    }

    /// <summary>两点距离的平方（避免开平方，更高效）</summary>
    /// <summary>两点距离的平方（避免开平方，更高效）</summary>
    public static float DistanceSquared(Vector3 a, Vector3 b)
    {
        float dx = a.X - b.X;
        float dy = a.Y - b.Y;
        float dz = a.Z - b.Z;
        return dx * dx + dy * dy + dz * dz;
    }

    /// <summary>将向量各分量限制在指定范围内</summary>
    public static Vector3 Clamp(Vector3 value, Vector3 min, Vector3 max)
    {
        return new Vector3(
            MathUtil.Clamp(value.X, min.X, max.X),
            MathUtil.Clamp(value.Y, min.Y, max.Y),
            MathUtil.Clamp(value.Z, min.Z, max.Z)
        );
    }

    // 运算符重载
    public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static Vector3 operator -(Vector3 a, Vector3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static Vector3 operator -(Vector3 v) => new(-v.X, -v.Y, -v.Z);
    public static Vector3 operator *(Vector3 v, float s) => new(v.X * s, v.Y * s, v.Z * s);
    public static Vector3 operator *(float s, Vector3 v) => new(v.X * s, v.Y * s, v.Z * s);
    public static Vector3 operator /(Vector3 v, float s) => new(v.X / s, v.Y / s, v.Z / s);

    public float this[int index] =>
        index switch
        {
            0 => X,
            1 => Y,
            2 => Z,
            _ => throw new ArgumentOutOfRangeException(nameof(index))
        };

    public bool Equals(Vector3 other) => X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z);
    public override bool Equals(object? obj) => obj is Vector3 v && Equals(v);
    public override int GetHashCode() => HashCode.Combine(X, Y, Z);
    public override string ToString() => $"({X:F3}, {Y:F3}, {Z:F3})";

    public static bool operator ==(Vector3 left, Vector3 right) => left.Equals(right);
    public static bool operator !=(Vector3 left, Vector3 right) => !left.Equals(right);
}
