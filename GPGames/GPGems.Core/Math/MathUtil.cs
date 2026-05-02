using System.Numerics;
namespace GPGems.Core.Math;

/// <summary>
/// 数学工具函数
/// </summary>
public static class MathUtil
{
    /// <summary>将值限制在指定范围内</summary>
    public static float Clamp(float value, float min, float max)
    {
        return value < min ? min : value > max ? max : value;
    }

    /// <summary>返回两个值中的较小者</summary>
    public static float Min(float a, float b)
    {
        return a < b ? a : b;
    }

    /// <summary>返回两个值中的较大者</summary>
    public static float Max(float a, float b)
    {
        return a > b ? a : b;
    }

    /// <summary>返回两个值中的较小者</summary>
    public static int Min(int a, int b)
    {
        return a < b ? a : b;
    }

    /// <summary>返回两个值中的较大者</summary>
    public static int Max(int a, int b)
    {
        return a > b ? a : b;
    }
}
