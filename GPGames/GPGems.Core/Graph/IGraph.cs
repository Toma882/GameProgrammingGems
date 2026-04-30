/*
 * 图数据结构 - 通用接口
 * 时间复杂度: 取决于算法
 *
 * 经营游戏核心用途:
 *   - 任务树: 任务依赖关系
 *   - 科技树: 科技研究依赖
 *   - 导航图: 区域连接关系
 *   - 社交网络: 玩家关系图谱
 */

using System;
using System.Collections.Generic;

namespace GPGems.Core.Graph;

/// <summary>
/// 图接口 - 通用图操作
/// </summary>
/// <typeparam name="TVertex">顶点类型</typeparam>
/// <typeparam name="TWeight">权重类型</typeparam>
public interface IGraph<TVertex, TWeight>
    where TVertex : notnull
    where TWeight : INumber<TWeight>
{
    /// <summary>顶点数量</summary>
    int VertexCount { get; }

    /// <summary>边数量</summary>
    int EdgeCount { get; }

    /// <summary>是否有向图</summary>
    bool IsDirected { get; }

    /// <summary>添加顶点</summary>
    bool AddVertex(TVertex vertex);

    /// <summary>移除顶点</summary>
    bool RemoveVertex(TVertex vertex);

    /// <summary>检查顶点是否存在</summary>
    bool HasVertex(TVertex vertex);

    /// <summary>添加边</summary>
    bool AddEdge(TVertex from, TVertex to, TWeight weight);

    /// <summary>移除边</summary>
    bool RemoveEdge(TVertex from, TVertex to);

    /// <summary>检查边是否存在</summary>
    bool HasEdge(TVertex from, TVertex to);

    /// <summary>获取边的权重</summary>
    TWeight GetWeight(TVertex from, TVertex to);

    /// <summary>获取顶点的所有邻居</summary>
    IEnumerable<(TVertex neighbor, TWeight weight)> GetNeighbors(TVertex vertex);

    /// <summary>获取所有顶点</summary>
    IEnumerable<TVertex> GetAllVertices();

    /// <summary>清空图</summary>
    void Clear();
}
