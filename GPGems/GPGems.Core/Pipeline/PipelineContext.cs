using System;
using System.Collections.Generic;

namespace GPGems.Core.PipelineHub;

/// <summary>
/// 管线上下文 - 外观模式的数据聚合器
/// 节点要什么给什么，节点产出什么存什么
/// 它什么都不知道，但什么都能给你
///
/// 设计边界：
/// - 只做数据袋，不关心通讯
/// - 节点需要通讯时自己访问 CommunicationBus.Instance
/// </summary>
public class PipelineContext
{
    /// <summary>所有数据都在这里</summary>
    private readonly Dictionary<string, object> _dataBag = new();

    /// <summary>快照存储</summary>
    private readonly Dictionary<string, PipelineSnapshot> _snapshots = new();

    /// <summary>上下文唯一标识</summary>
    public string ContextId { get; }

    /// <summary>关联的主体对象（如Unit、Building等）</summary>
    public object? Subject { get; }

    public PipelineContext(object? subject = null)
    {
        Subject = subject;
        ContextId = $"{subject?.GetHashCode().ToString() ?? "null"}_{DateTime.Now.Ticks}";
    }

    /// <summary>
    /// 设置初始数据
    /// </summary>
    public void SetInitialData(IEnumerable<KeyValuePair<string, object>> data)
    {
        foreach (var kv in data)
        {
            _dataBag[kv.Key] = kv.Value;
        }
    }

    /// <summary>
    /// 设置数据
    /// </summary>
    public void Set<T>(string key, T value)
    {
        _dataBag[key] = value!;
    }

    /// <summary>
    /// 获取数据
    /// </summary>
    public T? Get<T>(string key)
    {
        if (_dataBag.TryGetValue(key, out var value))
            return (T)value;
        return default;
    }

    /// <summary>
    /// 尝试获取数据
    /// </summary>
    public bool TryGet<T>(string key, out T value)
    {
        if (_dataBag.TryGetValue(key, out var v) && v is T typed)
        {
            value = typed;
            return true;
        }
        value = default!;
        return false;
    }

    /// <summary>
    /// 检查是否有指定键
    /// </summary>
    public bool Has(string key) => _dataBag.ContainsKey(key);

    /// <summary>
    /// 检查节点需要的数据齐了吗
    /// </summary>
    public bool HasAllData(IEnumerable<string> requiredKeys)
    {
        foreach (var key in requiredKeys)
        {
            if (!_dataBag.ContainsKey(key))
                return false;
        }
        return true;
    }

    /// <summary>
    /// 为节点获取输入数据（按 requires 过滤）
    /// </summary>
    public Dictionary<string, object> GetInputForNode(IPipelineNode node)
    {
        var input = new Dictionary<string, object>();
        foreach (var key in node.Requires)
        {
            if (_dataBag.TryGetValue(key, out var value))
                input[key] = value;
        }
        return input;
    }

    /// <summary>
    /// 保存节点的输出数据（按 provides 过滤）
    /// </summary>
    public void SaveOutputFromNode(IPipelineNode node, Dictionary<string, object> output)
    {
        if (output == null) return;

        foreach (var key in node.Provides)
        {
            if (output.TryGetValue(key, out var value))
                _dataBag[key] = value;
        }
    }

    #region 快照与回滚

    /// <summary>
    /// 保存快照
    /// </summary>
    public void SaveSnapshot(string snapshotName = "default")
    {
        _snapshots[snapshotName] = new PipelineSnapshot
        {
            DataBag = new Dictionary<string, object>(_dataBag)
        };
    }

    /// <summary>
    /// 回滚到快照
    /// </summary>
    public void RollbackToSnapshot(string snapshotName = "default")
    {
        if (!_snapshots.TryGetValue(snapshotName, out var snapshot))
            return;

        // 回滚数据袋
        _dataBag.Clear();
        foreach (var kv in snapshot.DataBag)
            _dataBag[kv.Key] = kv.Value;

        // 注意：副作用回滚需要业务层自己处理（如 PushChannel 的回滚）
    }

    #endregion

    /// <summary>
    /// 获取所有数据（调试用）
    /// </summary>
    public IReadOnlyDictionary<string, object> GetAllData() => _dataBag;
}

/// <summary>
/// 快照数据
/// </summary>
public class PipelineSnapshot
{
    public Dictionary<string, object> DataBag { get; set; } = new();
}
