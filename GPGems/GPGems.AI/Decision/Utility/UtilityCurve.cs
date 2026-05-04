/*
 * GPGems.AI - Utility System Curves
 * 效用曲线：将输入值转换为 0-1 的效用得分
 */

namespace GPGems.AI.Decision.Utility;

/// <summary>
/// 曲线类型
/// </summary>
public enum CurveType
{
    /// <summary>线性</summary>
    Linear,
    /// <summary>二次方</summary>
    Quadratic,
    /// <summary>三次方</summary>
    Cubic,
    /// <summary>平方根</summary>
    SquareRoot,
    /// <summary>Sigmoid</summary>
    Sigmoid,
    /// <summary>阶梯</summary>
    Step,
    /// <summary>高斯</summary>
    Gaussian
}

/// <summary>
/// 效用曲线基类
/// </summary>
public abstract class UtilityCurve
{
    /// <summary>输入最小值</summary>
    public float MinX { get; set; } = 0f;
    /// <summary>输入最大值</summary>
    public float MaxX { get; set; } = 1f;
    /// <summary>输出最小值</summary>
    public float MinY { get; set; } = 0f;
    /// <summary>输出最大值</summary>
    public float MaxY { get; set; } = 1f;
    /// <summary>是否反转</summary>
    public bool Inverted { get; set; }

    /// <summary>计算效用值</summary>
    public float Evaluate(float x)
    {
        // 归一化输入
        var normalizedX = Math.Clamp((x - MinX) / (MaxX - MinX), 0f, 1f);

        if (Inverted)
            normalizedX = 1f - normalizedX;

        // 计算曲线值
        var y = CalculateCurve(normalizedX);

        // 映射到输出范围
        return MinY + y * (MaxY - MinY);
    }

    protected abstract float CalculateCurve(float normalizedX);
}

/// <summary>
/// 线性曲线
/// </summary>
public class LinearCurve : UtilityCurve
{
    public float Slope { get; set; } = 1f;

    protected override float CalculateCurve(float x) => Math.Clamp(Slope * x, 0f, 1f);
}

/// <summary>
/// 二次方曲线
/// </summary>
public class QuadraticCurve : UtilityCurve
{
    protected override float CalculateCurve(float x) => x * x;
}

/// <summary>
/// 三次方曲线
/// </summary>
public class CubicCurve : UtilityCurve
{
    protected override float CalculateCurve(float x) => x * x * x;
}

/// <summary>
/// 平方根曲线
/// </summary>
public class SquareRootCurve : UtilityCurve
{
    protected override float CalculateCurve(float x) => MathF.Sqrt(x);
}

/// <summary>
/// Sigmoid 曲线
/// </summary>
public class SigmoidCurve : UtilityCurve
{
    public float Steepness { get; set; } = 5f;
    public float Midpoint { get; set; } = 0.5f;

    protected override float CalculateCurve(float x)
    {
        return 1f / (1f + MathF.Exp(-Steepness * (x - Midpoint)));
    }
}

/// <summary>
/// 高斯曲线
/// </summary>
public class GaussianCurve : UtilityCurve
{
    public float Center { get; set; } = 0.5f;
    public float Width { get; set; } = 0.2f;

    protected override float CalculateCurve(float x)
    {
        var diff = x - Center;
        return MathF.Exp(-(diff * diff) / (2f * Width * Width));
    }
}

/// <summary>
/// 阶梯曲线
/// </summary>
public class StepCurve : UtilityCurve
{
    public float Threshold { get; set; } = 0.5f;

    protected override float CalculateCurve(float x) => x >= Threshold ? 1f : 0f;
}

/// <summary>
/// 常数曲线
/// </summary>
public class ConstantCurve : UtilityCurve
{
    public float Value { get; set; } = 0.5f;

    protected override float CalculateCurve(float x) => Value;
}
