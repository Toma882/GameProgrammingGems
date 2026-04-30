/*
 * 图算法 Graph Algorithms
 * 包含 BFS, DFS, Dijkstra, A*, Bellman-Ford, Floyd-Warshall 等常用算法
 *
 * 经营游戏核心用途:
 *   - 最短路径: NPC 寻路, 贸易路线规划
 *   - 可达性检测: 区域是否连通
 *   - 路径规划: 任务/科技解锁顺序
 *   - 最小生成树: 路网/运输网络优化
 */

using System;
using System.Collections.Generic;
using System.Numerics;

namespace GPGems.Core.Graph;

/// <summary>
/// 通用图算法
/// </summary>
public static class GraphAlgorithms
{
    #region BFS - 广度优先搜索

    /// <summary>
    /// BFS 查找最短路径（无权图）
    /// 返回从起点到终点的路径序列（含端点），不可达返回 null
    /// </summary>
    public static List<TVertex>? BFSShortestPath<TVertex, TWeight>(
        IGraph<TVertex, TWeight> graph,
        TVertex start,
        TVertex end)
        where TVertex : notnull
        where TWeight : INumber<TWeight>
    {
        if (!graph.HasVertex(start) || !graph.HasVertex(end))
            return null;

        var parent = new Dictionary<TVertex, TVertex?>();
        var queue = new Queue<TVertex>();
        parent[start] = default;
        queue.Enqueue(start);

        bool found = false;
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            if (EqualityComparer<TVertex>.Default.Equals(current, end))
            {
                found = true;
                break;
            }

            foreach (var (neighbor, _) in graph.GetNeighbors(current))
            {
                if (!parent.ContainsKey(neighbor))
                {
                    parent[neighbor] = current;
                    queue.Enqueue(neighbor);
                }
            }
        }

        if (!found)
            return null;

        // 回溯重建路径
        var path = new List<TVertex>();
        var node = end;
        while (node != null)
        {
            path.Add(node);
            node = parent[node];
        }
        path.Reverse();
        return path;
    }

    /// <summary>
    /// BFS 获取指定步数内可达的所有节点
    /// </summary>
    public static Dictionary<TVertex, int> BFSReachable<TVertex, TWeight>(
        IGraph<TVertex, TWeight> graph,
        TVertex start,
        int maxSteps)
        where TVertex : notnull
        where TWeight : INumber<TWeight>
    {
        var result = new Dictionary<TVertex, int>();
        var queue = new Queue<(TVertex node, int depth)>();
        queue.Enqueue((start, 0));
        result[start] = 0;

        while (queue.Count > 0)
        {
            var (current, depth) = queue.Dequeue();
            if (depth >= maxSteps)
                continue;

            foreach (var (neighbor, _) in graph.GetNeighbors(current))
            {
                if (!result.ContainsKey(neighbor))
                {
                    result[neighbor] = depth + 1;
                    queue.Enqueue((neighbor, depth + 1));
                }
            }
        }

        return result;
    }

    #endregion

    #region DFS - 深度优先搜索

    /// <summary>
    /// DFS 遍历
    /// </summary>
    public static List<TVertex> DFS<TVertex, TWeight>(
        IGraph<TVertex, TWeight> graph,
        TVertex start)
        where TVertex : notnull
        where TWeight : INumber<TWeight>
    {
        var result = new List<TVertex>();
        var visited = new HashSet<TVertex>();
        var stack = new Stack<TVertex>();
        stack.Push(start);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (!visited.Add(current))
                continue;

            result.Add(current);

            // 注意：逆序入栈以保持顺序
            var neighbors = new List<(TVertex, TWeight)>();
            foreach (var n in graph.GetNeighbors(current))
                neighbors.Add(n);
            neighbors.Reverse();

            foreach (var (neighbor, _) in neighbors)
            {
                if (!visited.Contains(neighbor))
                    stack.Push(neighbor);
            }
        }

        return result;
    }

    /// <summary>
    /// DFS 查找所有路径（小心使用，复杂度高）
    /// </summary>
    public static List<List<TVertex>> DFSAllPaths<TVertex, TWeight>(
        IGraph<TVertex, TWeight> graph,
        TVertex start,
        TVertex end,
        int maxDepth = 20)
        where TVertex : notnull
        where TWeight : INumber<TWeight>
    {
        var result = new List<List<TVertex>>();
        var currentPath = new List<TVertex>();
        var visited = new HashSet<TVertex>();

        void DFSRecursive(TVertex current, int depth)
        {
            if (depth > maxDepth)
                return;

            currentPath.Add(current);
            visited.Add(current);

            if (EqualityComparer<TVertex>.Default.Equals(current, end))
            {
                result.Add(new List<TVertex>(currentPath));
            }
            else
            {
                foreach (var (neighbor, _) in graph.GetNeighbors(current))
                {
                    if (!visited.Contains(neighbor))
                        DFSRecursive(neighbor, depth + 1);
                }
            }

            currentPath.RemoveAt(currentPath.Count - 1);
            visited.Remove(current);
        }

        DFSRecursive(start, 0);
        return result;
    }

    #endregion

    #region Dijkstra - 最短路径（非负权重）

    /// <summary>
    /// Dijkstra 算法 - 单源最短路径（非负权重）
    /// 返回 (距离字典, 前驱字典)
    /// </summary>
    public static (Dictionary<TVertex, TWeight> distances, Dictionary<TVertex, TVertex?> previous)
        Dijkstra<TVertex, TWeight>(
            IGraph<TVertex, TWeight> graph,
            TVertex source)
        where TVertex : notnull
        where TWeight : struct, INumber<TWeight>
    {
        var distances = new Dictionary<TVertex, TWeight>();
        var previous = new Dictionary<TVertex, TVertex?>();
        var priorityQueue = new PriorityQueue<TVertex, TWeight>();

        // 初始化
        foreach (var v in graph.GetAllVertices())
        {
            distances[v] = TWeight.CreateTruncating(double.MaxValue / 2);  // 避免溢出
            previous[v] = default;
        }
        distances[source] = TWeight.Zero;
        priorityQueue.Enqueue(source, TWeight.Zero);

        while (priorityQueue.Count > 0)
        {
            var current = priorityQueue.Dequeue();
            var currentDist = distances[current];

            foreach (var (neighbor, weight) in graph.GetNeighbors(current))
            {
                var alternative = currentDist + weight;
                if (alternative < distances[neighbor])
                {
                    distances[neighbor] = alternative;
                    previous[neighbor] = current;
                    priorityQueue.Enqueue(neighbor, alternative);
                }
            }
        }

        return (distances, previous);
    }

    /// <summary>
    /// Dijkstra 查找从起点到终点的最短路径
    /// 返回 (路径, 总距离)，不可达返回 (null, 无穷大)
    /// </summary>
    public static (List<TVertex>? path, TWeight distance) DijkstraShortestPath<TVertex, TWeight>(
        IGraph<TVertex, TWeight> graph,
        TVertex start,
        TVertex end)
        where TVertex : notnull
        where TWeight : struct, INumber<TWeight>
    {
        if (!graph.HasVertex(start) || !graph.HasVertex(end))
            return (null, TWeight.CreateTruncating(double.MaxValue));

        var (distances, previous) = Dijkstra(graph, start);
        var distance = distances[end];

        if (distance == TWeight.CreateTruncating(double.MaxValue / 2))
            return (null, distance);

        // 回溯重建路径
        var path = new List<TVertex>();
        var node = end;
        while (node != null)
        {
            path.Add(node);
            node = previous[node];
        }
        path.Reverse();
        return (path, distance);
    }

    #endregion

    #region A* - 启发式最短路径

    /// <summary>
    /// A* 算法 - 启发式最短路径
    /// 通常比 Dijkstra 快（有好的启发函数时）
    /// </summary>
    /// <param name="heuristic">启发函数 h(n)，估计当前节点到目标的代价</param>
    public static (List<TVertex>? path, TWeight distance) AStar<TVertex, TWeight>(
        IGraph<TVertex, TWeight> graph,
        TVertex start,
        TVertex end,
        Func<TVertex, TVertex, TWeight> heuristic)
        where TVertex : notnull
        where TWeight : struct, INumber<TWeight>
    {
        if (!graph.HasVertex(start) || !graph.HasVertex(end))
            return (null, TWeight.CreateTruncating(double.MaxValue));

        var gScore = new Dictionary<TVertex, TWeight>();
        var fScore = new Dictionary<TVertex, TWeight>();
        var previous = new Dictionary<TVertex, TVertex?>();
        var priorityQueue = new PriorityQueue<TVertex, TWeight>();

        // 初始化
        foreach (var v in graph.GetAllVertices())
        {
            gScore[v] = TWeight.CreateTruncating(double.MaxValue / 2);
            fScore[v] = TWeight.CreateTruncating(double.MaxValue / 2);
            previous[v] = default;
        }
        gScore[start] = TWeight.Zero;
        fScore[start] = heuristic(start, end);
        priorityQueue.Enqueue(start, fScore[start]);

        while (priorityQueue.Count > 0)
        {
            var current = priorityQueue.Dequeue();

            if (EqualityComparer<TVertex>.Default.Equals(current, end))
            {
                // 重建路径
                var path = new List<TVertex>();
                var node = end;
                while (node != null)
                {
                    path.Add(node);
                    node = previous[node];
                }
                path.Reverse();
                return (path, gScore[end]);
            }

            foreach (var (neighbor, weight) in graph.GetNeighbors(current))
            {
                var tentativeG = gScore[current] + weight;
                if (tentativeG < gScore[neighbor])
                {
                    previous[neighbor] = current;
                    gScore[neighbor] = tentativeG;
                    fScore[neighbor] = tentativeG + heuristic(neighbor, end);
                    priorityQueue.Enqueue(neighbor, fScore[neighbor]);
                }
            }
        }

        return (null, TWeight.CreateTruncating(double.MaxValue));
    }

    /// <summary>
    /// A* 针对 2D 网格的便捷版本（曼哈顿距离启发）
    /// </summary>
    public static (List<(int x, int y)>? path, int distance) AStarGrid(
        int width,
        int height,
        int startX,
        int startY,
        int endX,
        int endY,
        Func<int, int, bool> isBlocked)
    {
        // 简化实现：将坐标编码为 long
        var graph = new DirectedGraph<long, int>();

        // 构建 4 邻接网格（只在需要时动态构建，这里省略...）
        // 完整实现参考 AStarPathfinder.cs

        return (null, 0);
    }

    #endregion

    #region Bellman-Ford - 支持负权重

    /// <summary>
    /// Bellman-Ford 算法 - 支持负权重（可检测负环）
    /// </summary>
    /// <returns>(距离字典, 前驱字典, 有负环?)，有负环返回 (null, null, true)</returns>
    public static (Dictionary<TVertex, TWeight>? distances, Dictionary<TVertex, TVertex?>? previous, bool hasNegativeCycle)
        BellmanFord<TVertex, TWeight>(
            IGraph<TVertex, TWeight> graph,
            TVertex source)
        where TVertex : notnull
        where TWeight : struct, INumber<TWeight>
    {
        var distances = new Dictionary<TVertex, TWeight>();
        var previous = new Dictionary<TVertex, TVertex?>();

        // 初始化
        foreach (var v in graph.GetAllVertices())
        {
            distances[v] = TWeight.CreateTruncating(double.MaxValue / 2);
            previous[v] = default;
        }
        distances[source] = TWeight.Zero;

        // 松弛 V-1 次
        int vertexCount = graph.VertexCount;
        for (int i = 0; i < vertexCount - 1; i++)
        {
            bool updated = false;
            foreach (var u in graph.GetAllVertices())
            {
                foreach (var (v, weight) in graph.GetNeighbors(u))
                {
                    if (distances[u] != TWeight.CreateTruncating(double.MaxValue / 2))
                    {
                        var alt = distances[u] + weight;
                        if (alt < distances[v])
                        {
                            distances[v] = alt;
                            previous[v] = u;
                            updated = true;
                        }
                    }
                }
            }
            if (!updated) break;  // 提前结束
        }

        // 检测负环
        foreach (var u in graph.GetAllVertices())
        {
            foreach (var (v, weight) in graph.GetNeighbors(u))
            {
                if (distances[u] != TWeight.CreateTruncating(double.MaxValue / 2))
                {
                    var alt = distances[u] + weight;
                    if (alt < distances[v])
                    {
                        // 发现负环
                        return (null, null, true);
                    }
                }
            }
        }

        return (distances, previous, false);
    }

    #endregion

    #region Floyd-Warshall - 全源最短路径

    /// <summary>
    /// Floyd-Warshall 算法 - 全源最短路径
    /// 时间复杂度 O(V^3)，适合小图
    /// </summary>
    public static TWeight[,] FloydWarshall<TVertex, TWeight>(
        IGraph<TVertex, TWeight> graph,
        out TVertex[] vertexIndex)
        where TVertex : notnull
        where TWeight : struct, INumber<TWeight>
    {
        vertexIndex = new List<TVertex>(graph.GetAllVertices()).ToArray();
        int n = vertexIndex.Length;
        var vertexToIndex = new Dictionary<TVertex, int>();
        for (int i = 0; i < n; i++)
            vertexToIndex[vertexIndex[i]] = i;

        // 初始化距离矩阵
        var dist = new TWeight[n, n];
        var infinity = TWeight.CreateTruncating(double.MaxValue / 2);
        for (int i = 0; i < n; i++)
            for (int j = 0; j < n; j++)
                dist[i, j] = i == j ? TWeight.Zero : infinity;

        // 设置边的权重
        for (int i = 0; i < n; i++)
        {
            foreach (var (neighbor, weight) in graph.GetNeighbors(vertexIndex[i]))
            {
                int j = vertexToIndex[neighbor];
                dist[i, j] = weight;
            }
        }

        // Floyd-Warshall 核心
        for (int k = 0; k < n; k++)
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                {
                    if (dist[i, k] != infinity && dist[k, j] != infinity)
                    {
                        var alt = dist[i, k] + dist[k, j];
                        if (alt < dist[i, j])
                            dist[i, j] = alt;
                    }
                }

        return dist;
    }

    #endregion

    #region Prim - 最小生成树

    /// <summary>
    /// Prim 算法 - 最小生成树（MST）
    /// 返回构成 MST 的边列表
    /// </summary>
    public static List<(TVertex from, TVertex to, TWeight weight)> PrimMST<TVertex, TWeight>(
        IGraph<TVertex, TWeight> graph)
        where TVertex : notnull
        where TWeight : struct, INumber<TWeight>
    {
        var result = new List<(TVertex from, TVertex to, TWeight weight)>();
        var vertices = new List<TVertex>(graph.GetAllVertices());
        if (vertices.Count == 0)
            return result;

        var inMST = new HashSet<TVertex>();
        var key = new Dictionary<TVertex, TWeight>();
        var parent = new Dictionary<TVertex, TVertex?>();
        var infinity = TWeight.CreateTruncating(double.MaxValue / 2);

        // 初始化
        foreach (var v in vertices)
        {
            key[v] = infinity;
            parent[v] = default;
        }
        key[vertices[0]] = TWeight.Zero;

        var pq = new PriorityQueue<TVertex, TWeight>();
        pq.Enqueue(vertices[0], TWeight.Zero);

        while (pq.Count > 0)
        {
            var u = pq.Dequeue();
            if (!inMST.Add(u))
                continue;

            if (parent[u] != null)
                result.Add((parent[u]!, u, graph.GetWeight(parent[u]!, u)));

            foreach (var (v, weight) in graph.GetNeighbors(u))
            {
                if (!inMST.Contains(v) && weight < key[v])
                {
                    key[v] = weight;
                    parent[v] = u;
                    pq.Enqueue(v, weight);
                }
            }
        }

        return result;
    }

    #endregion

    #region Kruskal - 最小生成树

    /// <summary>
    /// Kruskal 算法 - 最小生成树（MST）
    /// 使用 Union-Find 数据结构
    /// </summary>
    public static List<(TVertex from, TVertex to, TWeight weight)> KruskalMST<TVertex, TWeight>(
        IGraph<TVertex, TWeight> graph)
        where TVertex : notnull
        where TWeight : struct, INumber<TWeight>
    {
        // 收集所有边
        var edges = new List<(TVertex from, TVertex to, TWeight weight)>();
        var visitedEdges = new HashSet<(TVertex, TVertex)>();

        foreach (var u in graph.GetAllVertices())
        {
            foreach (var (v, weight) in graph.GetNeighbors(u))
            {
                // 对无向图避免重复边
                var key = (u, v);
                var reverseKey = (v, u);
                if (!visitedEdges.Contains(key) && !visitedEdges.Contains(reverseKey))
                {
                    edges.Add((u, v, weight));
                    visitedEdges.Add(key);
                }
            }
        }

        // 按权重排序
        edges.Sort((a, b) => a.weight.CompareTo(b.weight));

        // Union-Find
        var parent = new Dictionary<TVertex, TVertex>();
        foreach (var v in graph.GetAllVertices())
            parent[v] = v;

        TVertex Find(TVertex x)
        {
            if (!EqualityComparer<TVertex>.Default.Equals(parent[x], x))
                parent[x] = Find(parent[x]);
            return parent[x];
        }

        void Union(TVertex x, TVertex y)
        {
            var rx = Find(x);
            var ry = Find(y);
            if (!EqualityComparer<TVertex>.Default.Equals(rx, ry))
                parent[ry] = rx;
        }

        // Kruskal 主循环
        var result = new List<(TVertex from, TVertex to, TWeight weight)>();
        foreach (var edge in edges)
        {
            var rootFrom = Find(edge.from);
            var rootTo = Find(edge.to);
            if (!EqualityComparer<TVertex>.Default.Equals(rootFrom, rootTo))
            {
                result.Add(edge);
                Union(rootFrom, rootTo);
            }
        }

        return result;
    }

    #endregion
}
