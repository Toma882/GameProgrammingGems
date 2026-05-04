/*
 * GPGems.AI - FSM State
 * IState: 状态接口
 * 支持进入/退出/更新/消息处理
 */

using GPGems.Core.Messages;
using GPGems.AI.Decision.Blackboards;

namespace GPGems.AI.Decision.FSM;

/// <summary>
/// 状态接口
/// </summary>
public interface IState
{
    /// <summary>状态名称</summary>
    string Name { get; }

    /// <summary>状态进入时调用</summary>
    /// <param name="contextt">上下文黑板</param>
    /// <param name="previousState">前一个状态</param>
    void OnEnter(Blackboard context, IState? previousState);

    /// <summary>状态更新时调用（每帧）</summary>
    /// <param name="contextt">上下文黑板</param>
    void OnUpdate(Blackboard context);

    /// <summary>状态退出时调用</summary>
    /// <param name="contextt">上下文黑板</param>
    /// <param name="nextState">下一个状态</param>
    void OnExit(Blackboard context, IState? nextState);

    /// <summary>处理消息</summary>
    /// <param name="message">消息</param>
    /// <param name="contextt">上下文黑板</param>
    /// <returns>处理结果</returns>
    MessageResult HandleMessage(Message message, Blackboard context);
}

/// <summary>
/// 状态基类（方便实现
/// </summary>
public abstract class StateBase : IState
{
    /// <inheritdoc />
    public string Name { get; }

    /// <summary>进入状态已进入的时间
    protected float EnterTime { get; private set; }

    /// <summary>状态持续时间</summary>
    public float Duration => (float)(DateTime.UtcNow - DateTime.UnixEpoch).TotalSeconds - EnterTime;

    protected StateBase(string name)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
    }

    /// <inheritdoc />
    public virtual void OnEnter(Blackboard context, IState? previousState)
    {
        EnterTime = (float)(DateTime.UtcNow - DateTime.UnixEpoch).TotalSeconds;
    }

    /// <inheritdoc />
    public virtual void OnUpdate(Blackboard context)
    {
    }

    /// <inheritdoc />
    public virtual void OnExit(Blackboard context, IState? nextState)
    {
    }

    /// <inheritdoc />
    public virtual MessageResult HandleMessage(Message message, Blackboard context)
    {
        return MessageResult.Unhandled;
    }

    public override string ToString() => Name;
}

/// <summary>
/// 泛型委托状态（快速实现状态）
/// </summary>
public class DelegateState : StateBase
{
    public Action<Blackboard, IState?>? EnterAction { get; set; }
    public Action<Blackboard>? UpdateAction { get; set; }
    public Action<Blackboard, IState?>? ExitAction { get; set; }
    public Func<Message, Blackboard, MessageResult>? MessageHandler { get; set; }

    public DelegateState(string name) : base(name)
    {
    }

    public override void OnEnter(Blackboard context, IState? previousState)
    {
        base.OnEnter(context, previousState);
        EnterAction?.Invoke(context, previousState);
    }

    public override void OnUpdate(Blackboard context)
    {
        base.OnUpdate(context);
        UpdateAction?.Invoke(context);
    }

    public override void OnExit(Blackboard context, IState? nextState)
    {
        base.OnExit(context, nextState);
        ExitAction?.Invoke(context, nextState);
    }

    public override MessageResult HandleMessage(Message message, Blackboard context)
    {
        return MessageHandler?.Invoke(message, context) ?? base.HandleMessage(message, context);
    }
}
