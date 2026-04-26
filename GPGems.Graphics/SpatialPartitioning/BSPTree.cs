using GPGems.Core.Graphics;
using GPGems.Core.Math;
using GPGems.Core.Geometry;

namespace GPGems.Graphics.SpatialPartitioning;

/// <summary>
/// BSP 树节点
/// 基于 Game Programming Gems 5 Chapter 1.05
/// </summary>
public class BSPNode
{
    /// <summary>分割平面（内部节点有效）</summary>
    public Plane? SplitPlane { get; set; }

    /// <summary>前方子节点</summary>
    public BSPNode? FrontChild { get; set; }

    /// <summary>后方子节点</summary>
    public BSPNode? BackChild { get; set; }

    /// <summary>该节点的多边形列表（叶子节点或跨平面的多边形）</summary>
    public List<Polygon> Polygons { get; } = [];

    /// <summary>是否是叶子节点</summary>
    public bool IsLeaf => FrontChild == null && BackChild == null;

    /// <summary>节点深度（根节点为0）</summary>
    public int Depth { get; set; }
}

/// <summary>
/// BSP 树（二叉空间分割树）
/// 用于3D场景的空间分割、可见性裁剪、碰撞检测
/// 基于 Game Programming Gems 5 Chapter 1.05
/// </summary>
public class BSPTree
{
    /// <summary>根节点</summary>
    public BSPNode? Root { get; set; }

    /// <summary>树的深度</summary>
    public int Depth { get; private set; }

    /// <summary>总节点数</summary>
    public int NodeCount { get; private set; }

    /// <summary>总多边形数</summary>
    public int PolygonCount { get; private set; }

    /// <summary>创建空BSP树</summary>
    public BSPTree()
    {
    }

    /// <summary>从多边形列表创建BSP树</summary>
    public BSPTree(List<Polygon> polygons)
    {
        Build(polygons);
    }

    /// <summary>构建BSP树</summary>
    /// <param name="polygons">输入多边形列表</param>
    public void Build(List<Polygon> polygons)
    {
        Root = BuildRecursive(polygons, 0);
        UpdateStats();
    }

    /// <summary>递归构建BSP树</summary>
    protected virtual BSPNode BuildRecursive(List<Polygon> polygons, int depth)
    {
        var node = new BSPNode { Depth = depth };

        if (polygons.Count == 0)
        {
            return node;
        }

        // 选择分割面（使用第一个多边形的平面）
        var splitter = SelectSplitterPolygon(polygons);
        if (splitter == null)
        {
            node.Polygons.AddRange(polygons);
            return node;
        }

        node.SplitPlane = splitter.Plane;

        var frontList = new List<Polygon>();
        var backList = new List<Polygon>();

        // 将所有多边形分类到分割面的前后
        foreach (var poly in polygons)
        {
            var classification = ClassifyPolygon(poly, node.SplitPlane.Value);

            switch (classification)
            {
                case PlaneSide.Front:
                    frontList.Add(poly);
                    break;
                case PlaneSide.Back:
                    backList.Add(poly);
                    break;
                case PlaneSide.OnPlane:
                    node.Polygons.Add(poly);
                    break;
                case PlaneSide.Spanning:
                    // 分割跨平面的多边形
                    poly.SplitByPlane(node.SplitPlane.Value, out var front, out var back);
                    if (front != null) frontList.Add(front);
                    if (back != null) backList.Add(back);
                    break;
            }
        }

        // 递归构建子节点
        if (frontList.Count > 0)
        {
            node.FrontChild = BuildRecursive(frontList, depth + 1);
        }

        if (backList.Count > 0)
        {
            node.BackChild = BuildRecursive(backList, depth + 1);
        }

        return node;
    }

    /// <summary>选择分割面策略</summary>
    /// <remarks>基础实现：选择分割最平衡的多边形</remarks>
    protected virtual Polygon? SelectSplitterPolygon(List<Polygon> polygons)
    {
        if (polygons.Count == 0) return null;

        // 简单策略：尝试几个候选，选择最平衡的
        int bestScore = int.MaxValue;
        Polygon? bestSplitter = polygons[0];

        int sampleCount = Math.Min(polygons.Count, 5);
        for (int i = 0; i < sampleCount; i++)
        {
            var candidate = polygons[i];
            int frontCount = 0;
            int backCount = 0;
            int splitCount = 0;

            foreach (var poly in polygons)
            {
                var classification = ClassifyPolygon(poly, candidate.Plane);
                switch (classification)
                {
                    case PlaneSide.Front: frontCount++; break;
                    case PlaneSide.Back: backCount++; break;
                    case PlaneSide.Spanning: splitCount++; break;
                }
            }

            // 评分：平衡度优先，尽量少分割
            int score = Math.Abs(frontCount - backCount) + splitCount * 2;
            if (score < bestScore)
            {
                bestScore = score;
                bestSplitter = candidate;
            }
        }

        return bestSplitter;
    }

    /// <summary>分类多边形相对于平面</summary>
    public static PlaneSide ClassifyPolygon(Polygon polygon, Plane plane, float epsilon = 1e-6f)
    {
        int frontCount = 0;
        int backCount = 0;

        foreach (var vertex in polygon.Vertices)
        {
            var side = plane.ClassifyPoint(vertex.Position, epsilon);
            if (side == PlaneSide.Front) frontCount++;
            else if (side == PlaneSide.Back) backCount++;
        }

        if (frontCount > 0 && backCount == 0) return PlaneSide.Front;
        if (backCount > 0 && frontCount == 0) return PlaneSide.Back;
        if (frontCount == 0 && backCount == 0) return PlaneSide.OnPlane;
        return PlaneSide.Spanning;
    }

    /// <summary>按从后到前顺序遍历多边形（用于画家算法）</summary>
    public List<Polygon> TraverseBackToFront(Vector3 viewpoint)
    {
        var result = new List<Polygon>();
        TraverseBackToFrontRecursive(Root, viewpoint, result);
        return result;
    }

    private void TraverseBackToFrontRecursive(BSPNode? node, Vector3 viewpoint, List<Polygon> result)
    {
        if (node == null) return;

        if (node.SplitPlane.HasValue)
        {
            var side = node.SplitPlane.Value.ClassifyPoint(viewpoint);

            if (side == PlaneSide.Front)
            {
                // 视点在前方：先画后方，再画当前，最后画前方
                TraverseBackToFrontRecursive(node.BackChild, viewpoint, result);
                result.AddRange(node.Polygons);
                TraverseBackToFrontRecursive(node.FrontChild, viewpoint, result);
            }
            else
            {
                // 视点在后方或平面上：先画前方，再画当前，最后画后方
                TraverseBackToFrontRecursive(node.FrontChild, viewpoint, result);
                result.AddRange(node.Polygons);
                TraverseBackToFrontRecursive(node.BackChild, viewpoint, result);
            }
        }
        else
        {
            // 叶子节点
            result.AddRange(node.Polygons);
        }
    }

    /// <summary>收集所有多边形</summary>
    public List<Polygon> GetAllPolygons()
    {
        var result = new List<Polygon>();
        GetAllPolygonsRecursive(Root, result);
        return result;
    }

    private void GetAllPolygonsRecursive(BSPNode? node, List<Polygon> result)
    {
        if (node == null) return;

        result.AddRange(node.Polygons);
        GetAllPolygonsRecursive(node.FrontChild, result);
        GetAllPolygonsRecursive(node.BackChild, result);
    }

    /// <summary>收集所有节点</summary>
    public List<BSPNode> GetAllNodes()
    {
        var result = new List<BSPNode>();
        GetAllNodesRecursive(Root, result);
        return result;
    }

    private void GetAllNodesRecursive(BSPNode? node, List<BSPNode> result)
    {
        if (node == null) return;

        result.Add(node);
        GetAllNodesRecursive(node.FrontChild, result);
        GetAllNodesRecursive(node.BackChild, result);
    }

    /// <summary>更新统计信息</summary>
    public void UpdateStats()
    {
        var nodes = GetAllNodes();
        NodeCount = nodes.Count;
        Depth = nodes.Count > 0 ? nodes.Max(n => n.Depth) : 0;
        PolygonCount = GetAllPolygons().Count;
    }

    /// <summary>清空BSP树</summary>
    public void Clear()
    {
        Root = null;
        Depth = 0;
        NodeCount = 0;
        PolygonCount = 0;
    }
}
