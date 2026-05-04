/*
 * GPGems.AI - FSM Message System
 * MessageRouter: 核心消息路由器
 * 支持单播、广播、延迟消息、消息过滤
 */

using System.Collections.Concurrent;

namespace GPGems.Core.Messages
{

    /// <summary>
    /// 消息路由器
    /// 中心消息分发枢纽，管理所有接收者和消息队列
    /// </summary>
    public class MessageRouter
    {
        /// <summary>全局默认路由器实例</summary>
        public static MessageRouter Default { get; } = new MessageRouter("Global");

        /// <summary>路由器名称</summary>
        public string Name { get; }

        /// <summary>消息记录事件（上层可订阅此事件来记录消息日志）</summary>
        public event Action<Message>? MessageLogged;

        // 接收者注册表
        private readonly ConcurrentDictionary<string, IMessageReceiver> _receivers = new();

        // 按消息类型注册的处理器
        private readonly ConcurrentDictionary<string, List<MessageHandler<Message>>> _handlers = new();

        // 立即消息队列（按优先级）
        private readonly PriorityQueue<Message, int> _immediateQueue = new();

        // 延迟消息
        private readonly SortedDictionary<float, List<Message>> _delayedMessages = new();

        private readonly object _queueLock = new();

        /// <summary>已注册的接收者数量</summary>
        public int ReceiverCount => _receivers.Count;

        /// <summary>待处理消息总数</summary>
        public int PendingMessageCount
        {
            get
            {
                lock (_queueLock)
                {
                    return _immediateQueue.UnorderedItems.Count()
                        + _delayedMessages.Values.Sum(list => list.Count);
                }
            }
        }

        public MessageRouter(string name)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }

        #region 接收者注册

        /// <summary>注册消息接收者</summary>
        public void RegisterReceiver(IMessageReceiver receiver)
        {
            if (receiver == null) throw new ArgumentNullException(nameof(receiver));
            _receivers[receiver.ReceiverId] = receiver;
        }

        /// <summary>注销消息接收者</summary>
        public bool UnregisterReceiver(string receiverId)
        {
            return _receivers.TryRemove(receiverId, out _);
        }

        /// <summary>获取已注册的接收者</summary>
        public IMessageReceiver? GetReceiver(string receiverId)
        {
            _receivers.TryGetValue(receiverId, out var receiver);
            return receiver;
        }

        #endregion

        #region 消息处理器注册

        /// <summary>注册特定消息类型的处理器</summary>
        public void RegisterHandler<T>(string messageType, MessageHandler<T> handler) where T : Message
        {
            var handlers = _handlers.GetOrAdd(messageType, _ => new List<MessageHandler<Message>>());
            handlers.Add(msg => msg is T typed ? handler(typed) : MessageResult.Unhandled);
        }

        /// <summary>注销特定消息类型的所有处理器</summary>
        public void UnregisterHandlers(string messageType)
        {
            _handlers.TryRemove(messageType, out _);
        }

        #endregion

        #region 消息发送

        /// <summary>发送立即消息（单播）</summary>
        public void Send(Message message, string receiverId)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));
            if (string.IsNullOrEmpty(receiverId)) throw new ArgumentException("Receiver ID cannot be empty", nameof(receiverId));

            message.ReceiverId = receiverId;
            message.Delay = 0;

            EnqueueImmediate(message);
        }

        /// <summary>发送延迟消息</summary>
        public void SendDelayed(Message message, string receiverId, float delaySeconds)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));
            if (string.IsNullOrEmpty(receiverId)) throw new ArgumentException("Receiver ID cannot be empty", nameof(receiverId));

            message.ReceiverId = receiverId;
            message.Delay = delaySeconds;

            EnqueueDelayed(message);
        }

        /// <summary>广播消息给所有接收者</summary>
        public void Broadcast(Message message)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));

            message.ReceiverId = null;
            message.Delay = 0;

            EnqueueImmediate(message);
        }

        /// <summary>延迟广播</summary>
        public void BroadcastDelayed(Message message, float delaySeconds)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));

            message.ReceiverId = null;
            message.Delay = delaySeconds;

            EnqueueDelayed(message);
        }

        private void EnqueueImmediate(Message message)
        {
            lock (_queueLock)
            {
                _immediateQueue.Enqueue(message, -(int)message.Priority);
            }

            // 触发消息记录事件
            MessageLogged?.Invoke(message);
        }

        private void EnqueueDelayed(Message message)
        {
            var deliveryTime = message.Timestamp + message.Delay;

            lock (_queueLock)
            {
                if (!_delayedMessages.TryGetValue(deliveryTime, out var list))
                {
                    list = new List<Message>();
                    _delayedMessages[deliveryTime] = list;
                }
                list.Add(message);
            }
        }

        #endregion

        #region 消息分发

        /// <summary>处理所有到期消息</summary>
        /// <returns>处理的消息数量</returns>
        public int Dispatch()
        {
            var currentTime = Message.CurrentTime;
            var processedCount = 0;

            // 1. 先处理到期的延迟消息，移到立即队列
            ProcessDelayedMessages(currentTime);

            // 2. 处理所有立即消息
            while (true)
            {
                Message? message = null;
                lock (_queueLock)
                {
                    if (_immediateQueue.Count == 0) break;
                    message = _immediateQueue.Dequeue();
                }

                DispatchMessage(message);
                processedCount++;
            }

            return processedCount;
        }

        private void ProcessDelayedMessages(float currentTime)
        {
            lock (_queueLock)
            {
                // 找出所有已到期的消息
                var expiredKeys = _delayedMessages.Keys.Where(t => t <= currentTime).ToList();

                foreach (var key in expiredKeys)
                {
                    if (_delayedMessages.Remove(key, out var messages))
                    {
                        foreach (var msg in messages)
                        {
                            _immediateQueue.Enqueue(msg, -(int)msg.Priority);
                        }
                    }
                }
            }
        }

        private void DispatchMessage(Message message)
        {
            // 先调用类型处理器
            if (_handlers.TryGetValue(message.Type, out var handlers))
            {
                foreach (var handler in handlers)
                {
                    var result = handler(message);
                    if (result == MessageResult.Consumed) return;
                }
            }

            // 单播：发送给特定接收者
            if (!string.IsNullOrEmpty(message.ReceiverId))
            {
                if (_receivers.TryGetValue(message.ReceiverId, out var receiver))
                {
                    receiver.ReceiveMessage(message);
                }
                return;
            }

            // 广播：发送给所有接收者
            foreach (var receiver in _receivers.Values)
            {
                receiver.ReceiveMessage(message);
            }
        }

        #endregion

        #region 调试与诊断

        /// <summary>清空所有消息和接收者</summary>
        public void Clear()
        {
            lock (_queueLock)
            {
                _receivers.Clear();
                _handlers.Clear();
                _delayedMessages.Clear();

                // 清空 PriorityQueue
                while (_immediateQueue.Count > 0)
                {
                    _immediateQueue.Dequeue();
                }
            }
        }

        /// <summary>获取路由器状态快照</summary>
        public string Dump()
        {
            var lines = new List<string>
            {
                $"=== MessageRouter '{Name}' ===",
                $"Receivers: {ReceiverCount}",
                $"Pending messages: {PendingMessageCount}"
            };

            if (_receivers.Count > 0)
            {
                lines.Add("Registered receivers:");
                foreach (var id in _receivers.Keys)
                {
                    lines.Add($"  - {id}");
                }
            }

            return string.Join(Environment.NewLine, lines);
        }

        #endregion
    }
}