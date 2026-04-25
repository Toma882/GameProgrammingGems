/*
 * GPGems.AI - Fuzzy Logic: Fuzzy Set
 * 模糊集合：梯形隶属度函数定义（GPG1 AI\08McCuskey）
 */

namespace GPGems.AI.Decision.FuzzyLogic;

/// <summary>
/// 模糊集合
/// 使用梯形隶属度函数，由4个点定义：
///
///     1.0 |       /------\
///         |      /        \
///         |     /          \
///     0.0 +----*------------*----
///          Left0 Left1  Right1 Right0
/// </summary>
public class FuzzySet
{
    /// <summary>集合名称</summary>
    public string Name { get; }

    /// <summary>左端点（隶属度从0开始上升的点）</summary>
    public float Left0 { get; }

    /// <summary>左顶点（隶属度达到1的点）</summary>
    public float Left1 { get; }

    /// <summary>右顶点（隶属度开始从1下降的点）</summary>
    public float Right1 { get; }

    /// <summary>右端点（隶属度下降到0的点）</summary>
    public float Right0 { get; }

    /// <summary>
    /// 创建梯形模糊集合
    /// </summary>
    /// <param name="name">集合名称</param>
    /// <param name="left0">左端点（隶属度=0）</param>
    /// <param name="left1">左顶点（隶属度=1）</param>
    /// <param name="right1">右顶点（隶属度=1）</param>
    /// <param name="right0">右端点（隶属度=0）</param>
    public FuzzySet(string name, float left0, float left1, float right1, float right0)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Left0 = left0;
        Left1 = left1;
        Right1 = right1;
        Right0 = right0;
    }

    /// <summary>
    /// 计算输入值的隶属度
    /// </summary>
    /// <param name="value">输入值</param>
    /// <returns>隶属度 (0-1)</returns>
    public float CalculateMembership(float value)
    {
        // 在左侧上升区
        if (value >= Left0 && value < Left1)
        {
            return (value - Left0) / (Left1 - Left0);
        }

        // 在平顶区
        if (value >= Left1 && value <= Right1)
        {
            return 1f;
        }

        // 在右侧下降区
        if (value > Right1 && value <= Right0)
        {
            return (Right0 - value) / (Right0 - Right1);
        }

        // 在集合外
        return 0f;
    }

    /// <summary>
    /// 获取隶属度函数的峰值（重心 x 坐标近似）
    /// </summary>
    public float GetPeak() => (Left1 + Right1) / 2f;

    /// <summary>
    /// 创建左肩型模糊集合（Left0 = Left1）
    /// 例如："Cold"，"Low"，"Close"
    /// </summary>
    public static FuzzySet CreateLeftShoulder(string name, float peak, float end)
    {
        return new FuzzySet(name, peak, peak, peak, end);
    }

    /// <summary>
    /// 创建右肩型模糊集合（Right0 = Right1）
    /// 例如："Hot"，"High"，"Far"
    /// </summary>
    public static FuzzySet CreateRightShoulder(string name, float start, float peak)
    {
        return new FuzzySet(name, start, peak, peak, peak);
    }

    /// <summary>
    /// 创建三角型模糊集合（Left1 = Right1）
    /// </summary>
    public static FuzzySet CreateTriangle(string name, float left, float center, float right)
    {
        return new FuzzySet(name, left, center, center, right);
    }

    public override string ToString() => $"{Name} [{Left0},{Left1},{Right1},{Right0}]";
}
