/*
 * GPGems.AI - FSM Message System
 * IMessageReceiver: 消息接收者接口
 */

namespace GPGems.Core.Messages
{
/// <summary>
/// 消息接收者接口
/// 任何可以接收消息的对象都应该实现此接口
/// </summary>
public interface IMessageReceiver
{
    /// <summary>接收者唯一ID</summary>
    string ReceiverId { get; }

    /// <summary>接收并处理消息</summary>
    /// <param name="message">消息</param>
    /// <returns>是否处理成功</returns>
    bool ReceiveMessage(Message message);
}

/// <summary>
/// 消息处理结果
/// </summary>
public enum MessageResult
{
    /// <summary>消息已处理</summary>
    Handled,

    /// <summary>消息未处理（继续传递）</summary>
    Unhandled,

    /// <summary>消息被消费（停止传递）</summary>
    Consumed
}

/// <summary>
/// 消息处理器委托
/// </summary>
public delegate MessageResult MessageHandler<in T>(T message) where T : Message;
}