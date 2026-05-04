using GPGems.Core.Graphics;
using System.Numerics;
using GPGems.Core.Math;
using GPGems.Core.Geometry;
namespace GPGems.Graphics.SpatialPartitioning;

/// <summary>
/// BSP 树编译器配置
/// </summary>
public class BSPCompilerOptions
{
    /// <summary>最大树深度</summary>
    public int MaxDepth { get; set; } = 20;

    /// <summary>叶子节点最大多边形数</summary>
    public int MaxPolygonsPerLeaf { get; set; } = 5;

    /// <summary>是否启用分割面优化</summary>
    public bool EnableOptimization { get; set; } = true;

    /// <summary>平衡因子权重（优先选择平衡的分割）</summary>
    public float BalanceWeight { get; set; } = 1.0f;

    /// <summary>分割次数权重（尽量避免分割多边形）</summary>
    public float SplitWeight { get; set; } = 2.0f;

    /// <summary>表面积启发式权重（用于ABT）</summary>
    public float SAHWeight { get; set; } = 1.0f;
}

/// <summary>
/// BSP 树编译器
/// 提供高级的BSP构建和优化算法
/// 基于 Game Programming Gems 5 Chapter 1.05 / Gems 6 Chapter 1.5
/// </summary>
public class BSPCompiler
{
    private readonly BSPCompilerOptions _options;

    public BSPCompiler(BSPCompilerOptions? options = null)
    {
        _options = options ?? new BSPCompilerOptions();
    }

    /// <summary>构建优化的BSP树</summary>
    public BSPTree BuildOptimized(List<Polygon> polygons)
    {
        var tree = new BSPTree();
        var root = BuildNodeRecursive(polygons, 0);
        tree.Root = root;
        tree.UpdateStats();
        return tree;
    }

    private BSPNode BuildNodeRecursive(List<Polygon> polygons, int depth)
    {
        var node = new BSPNode { Depth = depth };

        // 停止条件：多边形太少或达到最大深度
        if (polygons.Count <= _options.MaxPolygonsPerLeaf || depth >= _options.MaxDepth)
        {
            node.Polygons.AddRange(polygons);
            return node;
        }

        // 选择最优分割面
        var splitter = SelectOptimalSplitter(polygons);
        if (splitter == null)
        {
            node.Polygons.AddRange(polygons);
            return node;
        }

        node.SplitPlane = splitter.Plane;

        var frontList = new List<Polygon>();
        var backList = new List<Polygon>();
        var onPlaneList = new List<Polygon>();

        // 分割多边形
        foreach (var poly in polygons)
        {
            var classification = BSPTree.ClassifyPolygon(poly, node.SplitPlane.Value);

            switch (classification)
            {
                case PlaneSide.Front:
                    frontList.Add(poly);
                    break;
                case PlaneSide.Back:
                    backList.Add(poly);
                    break;
                case PlaneSide.OnPlane:
                    onPlaneList.Add(poly);
                    break;
                case PlaneSide.Spanning:
                    poly.SplitByPlane(node.SplitPlane.Value, out var front, out var back);
                    if (front != null) frontList.Add(front);
                    if (back != null) backList.Add(back);
                    break;
            }
        }

        node.Polygons.AddRange(onPlaneList);

        // 递归构建子节点
        if (frontList.Count > 0)
        {
            node.FrontChild = BuildNodeRecursive(frontList, depth + 1);
        }

        if (backList.Count > 0)
        {
            node.BackChild = BuildNodeRecursive(backList, depth + 1);
        }

        return node;
    }

    /// <summary>使用启发式选择最优分割面</summary>
    private Polygon? SelectOptimalSplitter(List<Polygon> polygons)
    {
        if (polygons.Count == 0) return null;

        float bestScore = float.MaxValue;
        Polygon? bestSplitter = polygons[0];

        // 采样候选多边形
        int sampleCount = Math.Min(polygons.Count, 10);
        for (int i = 0; i < sampleCount; i++)
        {
            var candidate = polygons[i];
            var score = EvaluateSplitter(candidate, polygons);

            if (score < bestScore)
            {
                bestScore = score;
                bestSplitter = candidate;
            }
        }

        return bestSplitter;
    }

    /// <summary>评估分割面质量</summary>
    private float EvaluateSplitter(Polygon splitter, List<Polygon> allPolygons)
    {
        int frontCount = 0;
        int backCount = 0;
        int onPlaneCount = 0;
        int splitCount = 0;

        foreach (var poly in allPolygons)
        {
            var classification = BSPTree.ClassifyPolygon(poly, splitter.Plane);
            switch (classification)
            {
                case PlaneSide.Front: frontCount++; break;
                case PlaneSide.Back: backCount++; break;
                case PlaneSide.OnPlane: onPlaneCount++; break;
                case PlaneSide.Spanning: splitCount++; break;
            }
        }

        // 平衡度评分：0 = 完美平衡，越高越不平衡
        float balanceScore = Math.Abs(frontCount - backCount) / (float)(frontCount + backCount + 1);

        // 分割评分：分割越多越差
        float splitScore = splitCount / (float)allPolygons.Count;

        // 综合评分
        return balanceScore * _options.BalanceWeight + splitScore * _options.SplitWeight;
    }

    /// <summary>使用表面积启发式（SAH）评估分割面</summary>
    /// <remarks>用于光线追踪优化的BSP树</remarks>
    public float EvaluateSplitterSAH(Polygon splitter, List<Polygon> allPolygons, Bounds nodeBounds)
    {
        int frontCount = 0;
        int backCount = 0;
        int splitCount = 0;

        foreach (var poly in allPolygons)
        {
            var classification = BSPTree.ClassifyPolygon(poly, splitter.Plane);
            switch (classification)
            {
                case PlaneSide.Front: frontCount++; break;
                case PlaneSide.Back: backCount++; break;
                case PlaneSide.Spanning: splitCount++; break;
            }
        }

        // 简化的SAH：假设子节点均匀分布
        float totalArea = nodeBounds.SurfaceArea();

        // 估算前后子节点的表面积比例
        float frontAreaRatio = 0.5f;
        float backAreaRatio = 0.5f;

        float cost = 1.0f +
                     frontAreaRatio * frontCount +
                     backAreaRatio * backCount +
                     (frontAreaRatio + backAreaRatio) * splitCount;

        return cost * _options.SAHWeight;
    }

    /// <summary>后处理优化：合并冗余的叶子节点</summary>
    public void MergeLeafNodes(BSPTree tree)
    {
        MergeLeafNodesRecursive(tree.Root);
        tree.UpdateStats();
    }

    private bool MergeLeafNodesRecursive(BSPNode? node)
    {
        if (node == null) return true;

        // 递归处理子节点
        bool frontEmpty = MergeLeafNodesRecursive(node.FrontChild);
        bool backEmpty = MergeLeafNodesRecursive(node.BackChild);

        // 如果两个子节点都是空的，合并到当前节点
        if (frontEmpty && backEmpty && node.FrontChild != null && node.BackChild != null)
        {
            // 收集子节点的多边形
            node.Polygons.AddRange(node.FrontChild.Polygons);
            node.Polygons.AddRange(node.BackChild.Polygons);

            // 删除子节点
            node.FrontChild = null;
            node.BackChild = null;
            node.SplitPlane = null;

            return true;
        }

        return node.Polygons.Count == 0 && frontEmpty && backEmpty;
    }

    /// <summary>移除退化的多边形（面积为0）</summary>
    public static List<Polygon> RemoveDegeneratePolygons(List<Polygon> polygons, float minArea = 1e-6f)
    {
        return polygons.Where(p => p.ComputeArea() > minArea).ToList();
    }

    /// <summary>统计BSP树质量</summary>
    public BSPTreeStats ComputeStats(BSPTree tree)
    {
        var allNodes = tree.GetAllNodes();
        var leafNodes = allNodes.Where(n => n.IsLeaf).ToList();

        return new BSPTreeStats
        {
            TotalNodes = allNodes.Count,
            LeafNodes = leafNodes.Count,
            InternalNodes = allNodes.Count - leafNodes.Count,
            MaxDepth = (int)(allNodes.Count > 0 ? allNodes.Max(n => n.Depth) : 0),
            AverageDepth = (int)(allNodes.Count > 0 ? allNodes.Average(n => n.Depth) : 0),
            TotalPolygons = tree.PolygonCount,
            AveragePolygonsPerLeaf = (float)(leafNodes.Count > 0 ? leafNodes.Average(n => n.Polygons.Count) : 0),
            MaxPolygonsPerLeaf = leafNodes.Count > 0 ? leafNodes.Max(n => n.Polygons.Count) : 0
        };
    }
}

/// <summary>
/// BSP 树质量统计
/// </summary>
public struct BSPTreeStats
{
    public int TotalNodes;
    public int LeafNodes;
    public int InternalNodes;
    public int MaxDepth;
    public float AverageDepth;
    public int TotalPolygons;
    public float AveragePolygonsPerLeaf;
    public int MaxPolygonsPerLeaf;

    public override string ToString()
    {
        return $"Nodes: {TotalNodes} ({InternalNodes} internal, {LeafNodes} leaves), " +
               $"Depth: {MaxDepth} max, {AverageDepth:F1} avg, " +
               $"Polygons: {TotalPolygons} total, {AveragePolygonsPerLeaf:F1}/leaf";
    }
}
