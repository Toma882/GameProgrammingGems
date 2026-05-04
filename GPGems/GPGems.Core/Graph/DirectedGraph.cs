/*
 * 有向图 Directed Graph
 * 时间复杂度: O(1) 边/顶点添加, O(V+E) 遍历
 *
 * 经营游戏核心用途:
 *   - 任务树: 任务依赖关系（有向）
 *   - 科技树: 科技研究前置条件
 *   - 对话树: NPC 对话分支
 *   - 成就系统: 成就解锁条件
 */

using System;
using System.Collections.Generic;
using System.Numerics;

namespace GPGems.Core.Graph;

/// <summary>
/// 有向图 - 邻接表实现
/// </summary>
/// <typeparam name="TVertex">顶点类型</typeparam>
/// <typeparam name="TWeight">权重类型</typeparam>
public class DirectedGraph<TVertex, TWeight> : IGraph<TVertex, TWeight>
    where TVertex : notnull
    where TWeight : struct, INumber<TWeight>
{
    #region 字段与属性

    private readonly Dictionary<TVertex, Dictionary<TVertex, TWeight>> _adjacency;
    private int _edgeCount;

    public int VertexCount => _adjacency.Count;
    public int EdgeCount => _edgeCount;
    public bool IsDirected => true;

    #endregion

    #region 构造函数

    public DirectedGraph()
    {
        _adjacency = new Dictionary<TVertex, Dictionary<TVertex, TWeight>>();
        _edgeCount = 0;
    }

    #endregion

    #region 顶点操作

    public bool AddVertex(TVertex vertex)
    {
        if (_adjacency.ContainsKey(vertex))
            return false;

        _adjacency[vertex] = new Dictionary<TVertex, TWeight>();
        return true;
    }

    public bool RemoveVertex(TVertex vertex)
    {
        if (!_adjacency.ContainsKey(vertex))
            return false;

        // 移除从该顶点出发的边
        _edgeCount -= _adjacency[vertex].Count;
        _adjacency.Remove(vertex);

        // 移除指向该顶点的边
        foreach (var adj in _adjacency.Values)
        {
            if (adj.Remove(vertex))
                _edgeCount--;
        }

        return true;
    }

    public bool HasVertex(TVertex vertex)
    {
        return _adjacency.ContainsKey(vertex);
    }

    public IEnumerable<TVertex> GetAllVertices()
    {
        return _adjacency.Keys;
    }

    public void Clear()
    {
        _adjacency.Clear();
        _edgeCount = 0;
    }

    #endregion

    #region 边操作

    public bool AddEdge(TVertex from, TVertex to, TWeight weight)
    {
        if (!_adjacency.TryGetValue(from, out var fromAdj))
        {
            fromAdj = new Dictionary<TVertex, TWeight>();
            _adjacency[from] = fromAdj;
        }

        if (!_adjacency.ContainsKey(to))
        {
            _adjacency[to] = new Dictionary<TVertex, TWeight>();
        }

        if (fromAdj.TryGetValue(to, out var existing))
        {
            fromAdj[to] = weight;
            return false;  // 边已存在，更新权重
        }

        fromAdj[to] = weight;
        _edgeCount++;
        return true;
    }

    public bool RemoveEdge(TVertex from, TVertex to)
    {
        if (_adjacency.TryGetValue(from, out var adj) && adj.Remove(to))
        {
            _edgeCount--;
            return true;
        }
        return false;
    }

    public bool HasEdge(TVertex from, TVertex to)
    {
        return _adjacency.TryGetValue(from, out var adj) && adj.ContainsKey(to);
    }

    public TWeight GetWeight(TVertex from, TVertex to)
    {
        if (_adjacency.TryGetValue(from, out var adj) && adj.TryGetValue(to, out var weight))
            return weight;

        throw new InvalidOperationException("Edge does not exist");
    }

    public IEnumerable<(TVertex neighbor, TWeight weight)> GetNeighbors(TVertex vertex)
    {
        if (_adjacency.TryGetValue(vertex, out var adj))
        {
            foreach (var kvp in adj)
            {
                yield return (kvp.Key, kvp.Value);
            }
        }
    }

    #endregion

    #region 拓扑排序

    /// <summary>
    /// 拓扑排序（Kahn 算法）
    /// </summary>
    /// <returns>拓扑序列，如果有环返回 null</returns>
    public List<TVertex>? TopologicalSort()
    {
        // 计算入度
        var inDegree = new Dictionary<TVertex, int>();
        foreach (var v in _adjacency.Keys)
            inDegree[v] = 0;

        foreach (var adj in _adjacency.Values)
        {
            foreach (var to in adj.Keys)
                inDegree[to] = inDegree.GetValueOrDefault(to) + 1;
        }

        // 入度为 0 的顶点入队
        var queue = new Queue<TVertex>();
        foreach (var kvp in inDegree)
        {
            if (kvp.Value == 0)
                queue.Enqueue(kvp.Key);
        }

        var result = new List<TVertex>();
        while (queue.Count > 0)
        {
            var v = queue.Dequeue();
            result.Add(v);

            foreach (var (neighbor, _) in GetNeighbors(v))
            {
                inDegree[neighbor]--;
                if (inDegree[neighbor] == 0)
                    queue.Enqueue(neighbor);
            }
        }

        // 如果结果长度不等于顶点数，说明有环
        return result.Count == VertexCount ? result : null;
    }

    /// <summary>
    /// 检测是否有环
    /// </summary>
    public bool HasCycle()
    {
        return TopologicalSort() == null;
    }

    #endregion
}
