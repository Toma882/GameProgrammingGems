using GPGems.Core.Graphics;
using GPGems.Core.Math;
using GPGems.Core.Geometry;

namespace GPGems.Graphics.SpatialPartitioning;

/// <summary>
/// BSP 高级技术
/// 基于 Game Programming Gems 6 Chapter 1.5
/// 包含：移除冗余分割面、树简化、碰撞检测优化
/// </summary>
public static class BSPTechniques
{
    /// <summary>
    /// 移除冗余的分割面
    /// 当一个节点的分割面两侧的几何体没有视觉上的遮挡关系时，可以移除它
    /// </summary>
    public static void RemoveRedundantSplitPlanes(BSPTree tree)
    {
        RemoveRedundantSplitPlanesRecursive(tree.Root);
        tree.UpdateStats();
    }

    private static void RemoveRedundantSplitPlanesRecursive(BSPNode? node)
    {
        if (node == null || node.IsLeaf) return;

        // 先递归处理子节点
        RemoveRedundantSplitPlanesRecursive(node.FrontChild);
        RemoveRedundantSplitPlanesRecursive(node.BackChild);

        // 检查是否可以合并：
        // 1. 两个子节点都是叶子
        // 2. 或者子节点中只有一侧有几何体
        if (node.FrontChild != null && node.BackChild != null &&
            node.FrontChild.IsLeaf && node.BackChild.IsLeaf)
        {
            int frontCount = CountTotalPolygons(node.FrontChild);
            int backCount = CountTotalPolygons(node.BackChild);

            // 如果一侧为空，或者两侧都很少，可以合并
            if (frontCount == 0 || backCount == 0 || (frontCount + backCount < 5))
            {
                MergeChildrenIntoNode(node);
            }
        }
    }

    private static int CountTotalPolygons(BSPNode? node)
    {
        if (node == null) return 0;
        return node.Polygons.Count +
               CountTotalPolygons(node.FrontChild) +
               CountTotalPolygons(node.BackChild);
    }

    private static void MergeChildrenIntoNode(BSPNode node)
    {
        if (node.FrontChild != null)
        {
            node.Polygons.AddRange(node.FrontChild.Polygons);
            node.FrontChild = null;
        }

        if (node.BackChild != null)
        {
            node.Polygons.AddRange(node.BackChild.Polygons);
            node.BackChild = null;
        }

        node.SplitPlane = null;
    }

    /// <summary>
    /// BSP 树碰撞检测
    /// 检测射线与场景的交点
    /// </summary>
    public static bool RayCast(BSPTree tree, Vector3 origin, Vector3 direction,
        out RayCastResult result, float maxDistance = float.MaxValue)
    {
        result = default;
        float closestDistance = maxDistance;
        bool hit = false;

        RayCastRecursive(tree.Root, origin, direction, maxDistance,
            ref closestDistance, ref result, ref hit);

        return hit;
    }

    private static void RayCastRecursive(BSPNode? node, Vector3 origin, Vector3 direction,
        float maxDistance, ref float closestDistance, ref RayCastResult result, ref bool hit)
    {
        if (node == null) return;

        // 首先检查当前节点的多边形
        foreach (var poly in node.Polygons)
        {
            if (RayPolygonIntersection(origin, direction, poly, out float distance, out Vector3 hitPoint) &&
                distance < closestDistance && distance > 0)
            {
                closestDistance = distance;
                result = new RayCastResult
                {
                    Hit = true,
                    Distance = distance,
                    Point = hitPoint,
                    Normal = poly.Plane.Normal,
                    HitPolygon = poly
                };
                hit = true;
            }
        }

        if (node.SplitPlane.HasValue)
        {
            var plane = node.SplitPlane.Value;
            float originDist = plane.DistanceToPoint(origin);

            // 根据射线起点选择遍历顺序
            if (originDist > 0)
            {
                // 起点在前方：先遍历前方子树
                RayCastRecursive(node.FrontChild, origin, direction, maxDistance,
                    ref closestDistance, ref result, ref hit);

                // 如果前方没找到足够近的交点，再检查射线是否会进入后方
                if (!hit || closestDistance > Math.Abs(originDist))
                {
                    RayCastRecursive(node.BackChild, origin, direction, maxDistance,
                        ref closestDistance, ref result, ref hit);
                }
            }
            else
            {
                // 起点在后方：先遍历后方子树
                RayCastRecursive(node.BackChild, origin, direction, maxDistance,
                    ref closestDistance, ref result, ref hit);

                if (!hit || closestDistance > Math.Abs(originDist))
                {
                    RayCastRecursive(node.FrontChild, origin, direction, maxDistance,
                        ref closestDistance, ref result, ref hit);
                }
            }
        }
    }

    /// <summary>射线与多边形相交检测</summary>
    private static bool RayPolygonIntersection(Vector3 origin, Vector3 direction,
        Polygon polygon, out float distance, out Vector3 hitPoint)
    {
        distance = 0;
        hitPoint = Vector3.Zero;

        var plane = polygon.Plane;

        // 射线与平面相交
        float denom = Vector3.Dot(plane.Normal, direction);
        if (MathF.Abs(denom) < 1e-6f) return false; // 平行

        float t = -(Vector3.Dot(plane.Normal, origin) + plane.D) / denom;
        if (t < 0) return false; // 在射线反方向

        hitPoint = origin + direction * t;
        distance = t;

        // 检查点是否在多边形内（使用重心坐标法）
        return IsPointInPolygon(hitPoint, polygon);
    }

    /// <summary>检查点是否在多边形内</summary>
    private static bool IsPointInPolygon(Vector3 point, Polygon polygon)
    {
        // 投影到2D进行检测
        var normal = polygon.Plane.Normal;
        float ax = MathF.Abs(normal.X);
        float ay = MathF.Abs(normal.Y);
        float az = MathF.Abs(normal.Z);

        // 选择投影到哪个平面（丢弃绝对值最大的坐标轴）
        Func<Vector3, (float, float)> project;
        if (ax >= ay && ax >= az)
            project = v => (v.Y, v.Z);
        else if (ay >= ax && ay >= az)
            project = v => (v.X, v.Z);
        else
            project = v => (v.X, v.Y);

        var (px, py) = project(point);

        // 射线法检测点在多边形内
        bool inside = false;
        int count = polygon.VertexCount;
        for (int i = 0, j = count - 1; i < count; j = i++)
        {
            var (ix, iy) = project(polygon.Vertices[i].Position);
            var (jx, jy) = project(polygon.Vertices[j].Position);

            if (((iy > py) != (jy > py)) &&
                (px < (jx - ix) * (py - iy) / (jy - iy) + ix))
            {
                inside = !inside;
            }
        }

        return inside;
    }

    /// <summary>
    /// 点与BSP树表示的几何体进行包含检测
    /// 用于判断点是否在封闭网格内部
    /// </summary>
    public static bool PointInsideGeometry(BSPTree tree, Vector3 point)
    {
        // 统计从点发出的射线与几何体的相交次数
        // 奇数次表示在内部，偶数次表示在外部
        int hitCount = 0;
        var direction = new Vector3(1, 0.001f, 0.0001f); // 避免轴对齐

        CountRayHitsRecursive(tree.Root, point, direction, ref hitCount);

        return hitCount % 2 == 1;
    }

    private static void CountRayHitsRecursive(BSPNode? node, Vector3 origin, Vector3 direction, ref int hitCount)
    {
        if (node == null) return;

        foreach (var poly in node.Polygons)
        {
            if (RayPolygonIntersection(origin, direction, poly, out float distance, out _) && distance > 0)
            {
                hitCount++;
            }
        }

        if (node.SplitPlane.HasValue)
        {
            float originDist = node.SplitPlane.Value.DistanceToPoint(origin);
            if (originDist > 0)
            {
                CountRayHitsRecursive(node.FrontChild, origin, direction, ref hitCount);
                CountRayHitsRecursive(node.BackChild, origin, direction, ref hitCount);
            }
            else
            {
                CountRayHitsRecursive(node.BackChild, origin, direction, ref hitCount);
                CountRayHitsRecursive(node.FrontChild, origin, direction, ref hitCount);
            }
        }
    }
}

/// <summary>
/// 射线检测结果
/// </summary>
public struct RayCastResult
{
    public bool Hit;
    public float Distance;
    public Vector3 Point;
    public Vector3 Normal;
    public Polygon? HitPolygon;
}
