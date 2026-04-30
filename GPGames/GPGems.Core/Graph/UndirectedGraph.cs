/*
 * 无向图 Undirected Graph
 * 时间复杂度: O(1) 边/顶点添加, O(V+E) 遍历
 *
 * 经营游戏核心用途:
 *   - 区域连接图: 地图区域连通性
 *   - 贸易路线: 城市间贸易网络
 *   - 社交网络: 玩家好友关系
 *   - 联盟系统: 公会/联盟关系
 */

using System;
using System.Collections.Generic;
using System.Numerics;

namespace GPGems.Core.Graph;

/// <summary>
/// 无向图 - 邻接表实现
/// 每条边存储在两个方向（from-to 和 to-from）
/// </summary>
/// <typeparam name="TVertex">顶点类型</typeparam>
/// <typeparam name="TWeight">权重类型</typeparam>
public class UndirectedGraph<TVertex, TWeight> : IGraph<TVertex, TWeight>
    where TVertex : notnull
    where TWeight : struct, INumber<TWeight>
{
    #region 字段与属性

    private readonly Dictionary<TVertex, Dictionary<TVertex, TWeight>> _adjacency;
    private int _edgeCount;

    public int VertexCount => _adjacency.Count;
    public int EdgeCount => _edgeCount;
    public bool IsDirected => false;

    #endregion

    #region 构造函数

    public UndirectedGraph()
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

        // 移除所有关联边
        var neighbors = new List<TVertex>(_adjacency[vertex].Keys);
        foreach (var neighbor in neighbors)
        {
            _adjacency[neighbor].Remove(vertex);
            _edgeCount--;
        }

        _adjacency.Remove(vertex);
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
        if (EqualityComparer<TVertex>.Default.Equals(from, to))
            throw new ArgumentException("Cannot add self-loop to undirected graph");

        // 确保顶点存在
        if (!_adjacency.ContainsKey(from))
            _adjacency[from] = new Dictionary<TVertex, TWeight>();
        if (!_adjacency.ContainsKey(to))
            _adjacency[to] = new Dictionary<TVertex, TWeight>();

        bool exists = _adjacency[from].ContainsKey(to);

        // 双向添加
        _adjacency[from][to] = weight;
        _adjacency[to][from] = weight;

        if (!exists)
            _edgeCount++;

        return !exists;
    }

    public bool RemoveEdge(TVertex from, TVertex to)
    {
        if (_adjacency.TryGetValue(from, out var fromAdj) &&
            _adjacency.TryGetValue(to, out var toAdj))
        {
            bool removed = fromAdj.Remove(to);
            toAdj.Remove(from);

            if (removed)
                _edgeCount--;

            return removed;
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

    #region 连通分量

    /// <summary>
    /// 获取所有连通分量
    /// </summary>
    public List<List<TVertex>> GetConnectedComponents()
    {
        var result = new List<List<TVertex>>();
        var visited = new HashSet<TVertex>();

        foreach (var vertex in _adjacency.Keys)
        {
            if (!visited.Contains(vertex))
            {
                var component = new List<TVertex>();
                var queue = new Queue<TVertex>();
                queue.Enqueue(vertex);
                visited.Add(vertex);

                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    component.Add(current);

                    foreach (var (neighbor, _) in GetNeighbors(current))
                    {
                        if (!visited.Contains(neighbor))
                        {
                            visited.Add(neighbor);
                            queue.Enqueue(neighbor);
                        }
                    }
                }

                result.Add(component);
            }
        }

        return result;
    }

    /// <summary>
    /// 检查两个顶点是否连通
    /// </summary>
    public bool AreConnected(TVertex a, TVertex b)
    {
        if (!_adjacency.ContainsKey(a) || !_adjacency.ContainsKey(b))
            return false;

        var visited = new HashSet<TVertex>();
        var queue = new Queue<TVertex>();
        queue.Enqueue(a);
        visited.Add(a);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (EqualityComparer<TVertex>.Default.Equals(current, b))
                return true;

            foreach (var (neighbor, _) in GetNeighbors(current))
            {
                if (!visited.Contains(neighbor))
                {
                    visited.Add(neighbor);
                    queue.Enqueue(neighbor);
                }
            }
        }

        return false;
    }

    #endregion
}
