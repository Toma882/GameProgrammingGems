namespace GPGems.Core;

/// <summary>
/// 推送数据上下文
/// </summary>
public class PushDataContext
{
    /// <summary>
    /// 目标订阅者
    /// </summary>
    public object Subscriber { get; set; } = null!;

    /// <summary>
    /// 数据类型
    /// </summary>
    public string DataType { get; set; } = string.Empty;

    /// <summary>
    /// 数据内容
    /// </summary>
    public object? Data { get; set; }
}
