/*
 * GPGems.AI - FSM Message System
 * Message: 消息基类与标准消息类型
 * 支持立即消息、延迟消息、广播消息
 */

namespace GPGems.Core.Messages
{
/// <summary>
/// 消息优先级
/// 高优先级消息会被优先处理
/// </summary>
public enum MessagePriority
{
    Low = 0,
    Normal = 1,
    High = 2,
    Critical = 3
}

/// <summary>
/// 消息基类
/// </summary>
public class Message
{
    /// <summary>消息类型标识</summary>
    public string Type { get; }

    /// <summary>发送者ID</summary>
    public string? SenderId { get; set; }

    /// <summary>接收者ID（null表示广播）</summary>
    public string? ReceiverId { get; set; }

    /// <summary>优先级</summary>
    public MessagePriority Priority { get; set; } = MessagePriority.Normal;

    /// <summary>消息创建时间</summary>
    public float Timestamp { get; }

    /// <summary>延迟发送时间（秒），0表示立即发送</summary>
    public float Delay { get; set; }

    /// <summary>消息数据（装箱）</summary>
    public object? Data { get; set; }

    public Message(string type)
    {
        Type = type ?? throw new ArgumentNullException(nameof(type));
        Timestamp = CurrentTime;
    }

    public Message(string type, object? data) : this(type)
    {
        Data = data;
    }

    /// <summary>当前时间提供器（便于测试）</summary>
    public static Func<float> CurrentTimeProvider { get; set; } = () =>
        (float)(DateTime.UtcNow - DateTime.UnixEpoch).TotalSeconds;

    internal static float CurrentTime => CurrentTimeProvider();

    /// <summary>获取类型安全的数据</summary>
    public T? GetData<T>()
    {
        return Data is T typed ? typed : default;
    }

    public override string ToString()
    {
        var delay = Delay > 0 ? $" (delay: {Delay}s)" : "";
        var data = Data != null ? $" [{Data}]" : "";
        return $"{Type} from {SenderId ?? "unknown"} to {ReceiverId ?? "broadcast"}{delay}{data}";
    }
}

/// <summary>
/// 泛型消息（类型安全的数据）
/// </summary>
/// <typeparam name="T">数据类型</typeparam>
public class Message<T> : Message
{
    /// <summary>类型安全的消息数据</summary>
    public new T? Data { get; set; }

    public Message(string type) : base(type)
    {
    }

    public Message(string type, T? data) : base(type)
    {
        Data = data;
        base.Data = data;
    }
}

/// <summary>
/// 标准内置消息类型
/// </summary>
public static class StandardMessages
{
    /// <summary>状态进入消息</summary>
    public const string StateEnter = "FSM.StateEnter";

    /// <summary>状态退出消息</summary>
    public const string StateExit = "FSM.StateExit";

    /// <summary>状态更新消息</summary>
    public const string StateUpdate = "FSM.StateUpdate";

    /// <summary>状态转换消息</summary>
    public const string StateTransition = "FSM.Transition";

    /// <summary>NPC 感知消息</summary>
    public const string SensorDetected = "Sensor.Detected";

    /// <summary>伤害消息</summary>
    public const string DamageReceived = "Combat.Damage";

    /// <summary>死亡消息</summary>
    public const string AgentDied = "Agent.Died";

    /// <summary>命令消息</summary>
    public const string Command = "Command.Execute";
}
}