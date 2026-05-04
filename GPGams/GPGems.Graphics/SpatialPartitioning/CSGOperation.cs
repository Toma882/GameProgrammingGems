using GPGems.Core.Graphics;
using System.Numerics;
using GPGems.Core.Math;
using GPGems.Core.Geometry;

namespace GPGems.Graphics.SpatialPartitioning;

/// <summary>
/// CSG 操作类型
/// </summary>
public enum CSGOperationType
{
    Union,        // 并集：A ∪ B
    Intersection, // 交集：A ∩ B
    Subtraction,  // 差集：A - B
}

/// <summary>
/// CSG 布尔运算
/// 基于 BSP 树实现实体建模操作
/// 基于 Game Programming Gems 5 Chapter 1.09
/// </summary>
public class CSGOperation
{
    private readonly BSPTree _treeA;
    private readonly BSPTree _treeB;

    public CSGOperation(BSPTree treeA, BSPTree treeB)
    {
        _treeA = treeA ?? throw new ArgumentNullException(nameof(treeA));
        _treeB = treeB ?? throw new ArgumentNullException(nameof(treeB));
    }

    /// <summary>执行 CSG 操作</summary>
    public List<Polygon> Execute(CSGOperationType operation)
    {
        return operation switch
        {
            CSGOperationType.Union => Union(),
            CSGOperationType.Intersection => Intersection(),
            CSGOperationType.Subtraction => Subtraction(),
            _ => throw new ArgumentOutOfRangeException(nameof(operation))
        };
    }

    /// <summary>并集运算：A ∪ B</summary>
    public List<Polygon> Union()
    {
        var result = new List<Polygon>();

        // A中在B外部的部分
        var aOutside = GetPolygonsOutsideOther(_treeA, _treeB);
        result.AddRange(aOutside);

        // B中在A外部的部分
        var bOutside = GetPolygonsOutsideOther(_treeB, _treeA);
        result.AddRange(bOutside);

        return result;
    }

    /// <summary>交集运算：A ∩ B</summary>
    public List<Polygon> Intersection()
    {
        var result = new List<Polygon>();

        // A中在B内部的部分
        var aInside = GetPolygonsInsideOther(_treeA, _treeB);
        result.AddRange(aInside);

        // B中在A内部的部分
        var bInside = GetPolygonsInsideOther(_treeB, _treeA);
        result.AddRange(bInside);

        return result;
    }

    /// <summary>差集运算：A - B</summary>
    public List<Polygon> Subtraction()
    {
        var result = new List<Polygon>();

        // A中在B外部的部分
        var aOutside = GetPolygonsOutsideOther(_treeA, _treeB);
        result.AddRange(aOutside);

        // B中在A内部且反转的部分
        var bInsideFlipped = GetPolygonsInsideOtherFlipped(_treeB, _treeA);
        result.AddRange(bInsideFlipped);

        return result;
    }

    /// <summary>获取A中在B外部的多边形</summary>
    private List<Polygon> GetPolygonsOutsideOther(BSPTree treeA, BSPTree treeB)
    {
        var result = new List<Polygon>();
        var allPolygonsA = treeA.GetAllPolygons();

        foreach (var poly in allPolygonsA)
        {
            var classification = ClassifyPolygonAgainstTree(poly, treeB);
            if (classification == PolygonTreeRelation.Outside ||
                classification == PolygonTreeRelation.Spanning)
            {
                // 分割并取外部部分
                poly.SplitByTree(treeB, out var outside, out var inside, out _);
                if (outside != null)
                {
                    result.AddRange(outside);
                }
            }
        }

        return result;
    }

    /// <summary>获取A中在B内部的多边形</summary>
    private List<Polygon> GetPolygonsInsideOther(BSPTree treeA, BSPTree treeB)
    {
        var result = new List<Polygon>();
        var allPolygonsA = treeA.GetAllPolygons();

        foreach (var poly in allPolygonsA)
        {
            var classification = ClassifyPolygonAgainstTree(poly, treeB);
            if (classification == PolygonTreeRelation.Inside ||
                classification == PolygonTreeRelation.Spanning)
            {
                poly.SplitByTree(treeB, out _, out var inside, out _);
                if (inside != null)
                {
                    result.AddRange(inside);
                }
            }
        }

        return result;
    }

    /// <summary>获取B中在A内部且反转的多边形</summary>
    private List<Polygon> GetPolygonsInsideOtherFlipped(BSPTree treeB, BSPTree treeA)
    {
        var result = new List<Polygon>();
        var allPolygonsB = treeB.GetAllPolygons();

        foreach (var poly in allPolygonsB)
        {
            var classification = ClassifyPolygonAgainstTree(poly, treeA);
            if (classification == PolygonTreeRelation.Inside ||
                classification == PolygonTreeRelation.Spanning)
            {
                poly.SplitByTree(treeA, out _, out var inside, out _);
                if (inside != null)
                {
                    foreach (var p in inside)
                    {
                        result.Add(p.Flipped());
                    }
                }
            }
        }

        return result;
    }

    /// <summary>分类多边形相对于BSP树的位置</summary>
    public static PolygonTreeRelation ClassifyPolygonAgainstTree(Polygon polygon, BSPTree tree)
    {
        return ClassifyPolygonAgainstNode(polygon, tree.Root);
    }

    private static PolygonTreeRelation ClassifyPolygonAgainstNode(Polygon polygon, BSPNode? node)
    {
        if (node == null)
        {
            // 空节点视为外部
            return PolygonTreeRelation.Outside;
        }

        if (node.SplitPlane.HasValue)
        {
            var classification = BSPTree.ClassifyPolygon(polygon, node.SplitPlane.Value);

            switch (classification)
            {
                case PlaneSide.Front:
                    return ClassifyPolygonAgainstNode(polygon, node.FrontChild);
                case PlaneSide.Back:
                    return ClassifyPolygonAgainstNode(polygon, node.BackChild);
                case PlaneSide.Spanning:
                    return PolygonTreeRelation.Spanning;
                case PlaneSide.OnPlane:
                    // 平面上的多边形继续遍历
                    var frontResult = ClassifyPolygonAgainstNode(polygon, node.FrontChild);
                    var backResult = ClassifyPolygonAgainstNode(polygon, node.BackChild);

                    if (frontResult == backResult) return frontResult;
                    if (frontResult == PolygonTreeRelation.Spanning ||
                        backResult == PolygonTreeRelation.Spanning)
                        return PolygonTreeRelation.Spanning;
                    return PolygonTreeRelation.Spanning;
            }
        }

        // 叶子节点：如果有几何体，检查多边形与几何体的关系
        // 简化实现：叶子节点视为外部
        return PolygonTreeRelation.Outside;
    }

    /// <summary>
    /// 使用采样点检测多边形相对于BSP树的内部/外部关系
    /// 更准确但稍慢的方法
    /// </summary>
    public static PolygonTreeRelation ClassifyPolygonBySampling(Polygon polygon, BSPTree tree)
    {
        // 在多边形中心采样
        var center = ComputePolygonCenter(polygon);
        bool isInside = BSPTechniques.PointInsideGeometry(tree, center);

        // 如果需要更高精度，可以采样多个点
        return isInside ? PolygonTreeRelation.Inside : PolygonTreeRelation.Outside;
    }

    private static Vector3 ComputePolygonCenter(Polygon polygon)
    {
        Vector3 sum = Vector3.Zero;
        foreach (var v in polygon.Vertices)
        {
            sum += v.Position;
        }
        return sum / polygon.VertexCount;
    }
}

/// <summary>
/// 多边形相对于 BSP 树的关系
/// </summary>
public enum PolygonTreeRelation
{
    Inside,   // 完全在内部
    Outside,  // 完全在外部
    Spanning  // 跨边界
}

/// <summary>
/// 多边形 CSG 扩展方法
/// </summary>
public static class PolygonCSGExtensions
{
    /// <summary>用BSP树分割多边形</summary>
    public static void SplitByTree(this Polygon polygon, BSPTree tree,
        out List<Polygon>? outside, out List<Polygon>? inside, out List<Polygon>? onPlane)
    {
        outside = [];
        inside = [];
        onPlane = [];

        var fragments = new List<Polygon> { polygon };
        SplitByNodeRecursive(fragments, tree.Root, outside, inside, onPlane);
    }

    private static void SplitByNodeRecursive(List<Polygon> fragments, BSPNode? node,
        List<Polygon> outside, List<Polygon> inside, List<Polygon> onPlane)
    {
        if (node == null || fragments.Count == 0)
        {
            // 到达叶子：简化处理 - 假设空节点为外部
            outside.AddRange(fragments);
            return;
        }

        if (!node.SplitPlane.HasValue)
        {
            // 没有分割面，继续向下
            SplitByNodeRecursive(fragments, node.FrontChild, outside, inside, onPlane);
            SplitByNodeRecursive(fragments, node.BackChild, outside, inside, onPlane);
            return;
        }

        var plane = node.SplitPlane.Value;
        var frontFragments = new List<Polygon>();
        var backFragments = new List<Polygon>();

        foreach (var frag in fragments)
        {
            var classification = BSPTree.ClassifyPolygon(frag, plane);

            switch (classification)
            {
                case PlaneSide.Front:
                    frontFragments.Add(frag);
                    break;
                case PlaneSide.Back:
                    backFragments.Add(frag);
                    break;
                case PlaneSide.OnPlane:
                    onPlane.Add(frag);
                    break;
                case PlaneSide.Spanning:
                    frag.SplitByPlane(plane, out var frontPoly, out var backPoly);
                    if (frontPoly != null) frontFragments.Add(frontPoly);
                    if (backPoly != null) backFragments.Add(backPoly);
                    break;
            }
        }

        // 递归分割
        if (frontFragments.Count > 0)
        {
            SplitByNodeRecursive(frontFragments, node.FrontChild, outside, inside, onPlane);
        }

        if (backFragments.Count > 0)
        {
            SplitByNodeRecursive(backFragments, node.BackChild, outside, inside, onPlane);
        }
    }
}
