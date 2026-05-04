using GPGems.Core.Graphics;
using System.Numerics;
using GPGems.Core.Math;
using GPGems.Core.Geometry;

namespace GPGems.Graphics.SpatialPartitioning;

/// <summary>
/// ABT 节点
/// 自适应二叉树节点
/// </summary>
public class ABTNode
{
    /// <summary>节点的边界盒</summary>
    public Bounds Bounds { get; set; }

    /// <summary>左子节点（沿分割轴负方向）</summary>
    public ABTNode? LeftChild { get; set; }

    /// <summary>右子节点（沿分割轴正方向）</summary>
    public ABTNode? RightChild { get; set; }

    /// <summary>该节点的几何体</summary>
    public List<Polygon> Polygons { get; } = [];

    /// <summary>分割轴（0=X, 1=Y, 2=Z）</summary>
    public int SplitAxis { get; set; }

    /// <summary>分割位置</summary>
    public float SplitPosition { get; set; }

    /// <summary>节点深度</summary>
    public int Depth { get; set; }

    /// <summary>是否是叶子节点</summary>
    public bool IsLeaf => LeftChild == null && RightChild == null;
}

/// <summary>
/// 自适应二叉树（Adaptive Binary Tree）
/// 用于光线追踪的空间分割结构，使用表面积启发式（SAH）
/// 基于 Game Programming Gems 5 Chapter 1.14 / Gems 6 Chapter 5.2
/// </summary>
public class ABT
{
    /// <summary>根节点</summary>
    public ABTNode? Root { get; private set; }

    /// <summary>最大树深度</summary>
    private readonly int _maxDepth;

    /// <summary>叶子节点最大几何体数量</summary>
    private readonly int _maxPolygonsPerLeaf;

    /// <summary>SAH 旅行成本</summary>
    private readonly float _traversalCost = 1.0f;

    /// <summary>SAH 相交成本</summary>
    private readonly float _intersectionCost = 2.0f;

    public ABT(int maxDepth = 20, int maxPolygonsPerLeaf = 4)
    {
        _maxDepth = maxDepth;
        _maxPolygonsPerLeaf = maxPolygonsPerLeaf;
    }

    /// <summary>构建自适应二叉树</summary>
    public void Build(List<Polygon> polygons)
    {
        if (polygons.Count == 0) return;

        // 计算场景边界盒
        var bounds = ComputeSceneBounds(polygons);

        // 递归构建
        Root = BuildRecursive(polygons, bounds, 0);
    }

    private ABTNode BuildRecursive(List<Polygon> polygons, Bounds bounds, int depth)
    {
        var node = new ABTNode
        {
            Bounds = bounds,
            Depth = depth
        };

        // 停止条件
        if (polygons.Count <= _maxPolygonsPerLeaf || depth >= _maxDepth)
        {
            node.Polygons.AddRange(polygons);
            return node;
        }

        // 找到最优分割
        var split = FindOptimalSplit(polygons, bounds);

        if (!split.IsValid)
        {
            // 没有找到好的分割，创建叶子节点
            node.Polygons.AddRange(polygons);
            return node;
        }

        node.SplitAxis = split.Axis;
        node.SplitPosition = split.Position;

        // 分割几何体
        var leftPolygons = new List<Polygon>();
        var rightPolygons = new List<Polygon>();

        var splitPlane = CreateSplitPlane(split.Axis, split.Position, bounds);

        foreach (var poly in polygons)
        {
            var classification = ClassifyPolygonAgainstAxis(poly, split.Axis, split.Position);

            switch (classification)
            {
                case PlaneSide.Front:
                    rightPolygons.Add(poly);
                    break;
                case PlaneSide.Back:
                    leftPolygons.Add(poly);
                    break;
                case PlaneSide.Spanning:
                    // 分割跨平面的多边形
                    poly.SplitByPlane(splitPlane, out var left, out var right);
                    if (left != null) leftPolygons.Add(left);
                    if (right != null) rightPolygons.Add(right);
                    break;
                case PlaneSide.OnPlane:
                    leftPolygons.Add(poly);
                    break;
            }
        }

        // 计算子节点边界盒
        var (leftBounds, rightBounds) = SplitBounds(bounds, split.Axis, split.Position);

        // 递归构建子节点
        if (leftPolygons.Count > 0)
        {
            node.LeftChild = BuildRecursive(leftPolygons, leftBounds, depth + 1);
        }

        if (rightPolygons.Count > 0)
        {
            node.RightChild = BuildRecursive(rightPolygons, rightBounds, depth + 1);
        }

        return node;
    }

    /// <summary>使用SAH寻找最优分割</summary>
    private SplitCandidate FindOptimalSplit(List<Polygon> polygons, Bounds bounds)
    {
        var bestSplit = new SplitCandidate { IsValid = false, Cost = float.MaxValue };

        float totalArea = bounds.SurfaceArea();
        float leafCost = _intersectionCost * polygons.Count;

        // 在三个轴上尝试
        for (int axis = 0; axis < 3; axis++)
        {
            var candidates = GenerateSplitCandidates(polygons, axis, bounds);

            foreach (var candidate in candidates)
            {
                // 计算SAH成本
                var (leftBounds, rightBounds) = SplitBounds(bounds, axis, candidate);
                int leftCount = CountPolygonsOnSide(polygons, axis, candidate, PlaneSide.Back);
                int rightCount = CountPolygonsOnSide(polygons, axis, candidate, PlaneSide.Front);

                float leftProbability = leftBounds.SurfaceArea() / totalArea;
                float rightProbability = rightBounds.SurfaceArea() / totalArea;

                float splitCost = _traversalCost +
                                 leftProbability * _intersectionCost * leftCount +
                                 rightProbability * _intersectionCost * rightCount;

                if (splitCost < bestSplit.Cost)
                {
                    bestSplit.IsValid = true;
                    bestSplit.Axis = axis;
                    bestSplit.Position = candidate;
                    bestSplit.Cost = splitCost;
                }
            }
        }

        // 只有当分割成本低于叶子节点成本时才进行分割
        if (bestSplit.Cost >= leafCost * 0.95f)
        {
            bestSplit.IsValid = false;
        }

        return bestSplit;
    }

    /// <summary>生成分割候选位置</summary>
    private List<float> GenerateSplitCandidates(List<Polygon> polygons, int axis, Bounds bounds)
    {
        var candidates = new HashSet<float>();

        // 在每个多边形的边界处采样
        foreach (var poly in polygons)
        {
            var polyBounds = poly.ComputeBounds();
            float min = GetAxisValue(polyBounds.Min, axis);
            float max = GetAxisValue(polyBounds.Max, axis);

            candidates.Add(min);
            candidates.Add(max);
        }

        // 添加中间点
        var sorted = candidates.OrderBy(x => x).ToList();
        var result = new List<float>();

        for (int i = 1; i < sorted.Count; i++)
        {
            result.Add((sorted[i - 1] + sorted[i]) * 0.5f);
        }

        // 如果候选太多，只取均匀分布的几个
        if (result.Count > 20)
        {
            float step = (result.Count - 1) / 19.0f;
            var sampled = new List<float>();
            for (int i = 0; i < 20; i++)
            {
                int idx = (int)(i * step);
                sampled.Add(result[Math.Clamp(idx, 0, result.Count - 1)]);
            }
            result = sampled;
        }

        return result;
    }

    /// <summary>统计分割面一侧的多边形数量</summary>
    private int CountPolygonsOnSide(List<Polygon> polygons, int axis, float position, PlaneSide side)
    {
        int count = 0;
        foreach (var poly in polygons)
        {
            var bounds = poly.ComputeBounds();
            float min = GetAxisValue(bounds.Min, axis);
            float max = GetAxisValue(bounds.Max, axis);

            if (side == PlaneSide.Back)
            {
                if (min < position) count++;
            }
            else
            {
                if (max > position) count++;
            }
        }
        return count;
    }

    /// <summary>沿指定轴分割边界盒</summary>
    private (Bounds left, Bounds right) SplitBounds(Bounds bounds, int axis, float position)
    {
        var min = bounds.Min;
        var max = bounds.Max;

        Vector3 leftMax, rightMin;

        switch (axis)
        {
            case 0: // X轴
                leftMax = new Vector3(position, max.Y, max.Z);
                rightMin = new Vector3(position, min.Y, min.Z);
                break;
            case 1: // Y轴
                leftMax = new Vector3(max.X, position, max.Z);
                rightMin = new Vector3(min.X, position, min.Z);
                break;
            default: // Z轴
                leftMax = new Vector3(max.X, max.Y, position);
                rightMin = new Vector3(min.X, min.Y, position);
                break;
        }

        var left = Bounds.FromMinMax(min, leftMax);
        var right = Bounds.FromMinMax(rightMin, max);

        return (left, right);
    }

    /// <summary>创建分割平面</summary>
    private Plane CreateSplitPlane(int axis, float position, Bounds bounds)
    {
        Vector3 normal = axis switch
        {
            0 => new Vector3(1, 0, 0),
            1 => new Vector3(0, 1, 0),
            _ => new Vector3(0, 0, 1)
        };

        Vector3 point = axis switch
        {
            0 => new Vector3(position, bounds.Center.Y, bounds.Center.Z),
            1 => new Vector3(bounds.Center.X, position, bounds.Center.Z),
            _ => new Vector3(bounds.Center.X, bounds.Center.Y, position)
        };

        return new Plane(normal, -Vector3.Dot(normal, point));
    }

    /// <summary>获取向量在指定轴上的值</summary>
    private static float GetAxisValue(Vector3 v, int axis)
    {
        return axis switch
        {
            0 => v.X,
            1 => v.Y,
            _ => v.Z
        };
    }

    /// <summary>沿轴分类多边形</summary>
    private PlaneSide ClassifyPolygonAgainstAxis(Polygon polygon, int axis, float position)
    {
        bool hasLeft = false;
        bool hasRight = false;

        foreach (var vertex in polygon.Vertices)
        {
            float value = GetAxisValue(vertex.Position, axis);
            if (value < position - 1e-6f) hasLeft = true;
            if (value > position + 1e-6f) hasRight = true;
        }

        if (hasLeft && hasRight) return PlaneSide.Spanning;
        if (hasLeft) return PlaneSide.Back;
        if (hasRight) return PlaneSide.Front;
        return PlaneSide.OnPlane;
    }

    /// <summary>计算场景的边界盒</summary>
    private Bounds ComputeSceneBounds(List<Polygon> polygons)
    {
        Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);

        foreach (var poly in polygons)
        {
            var bounds = poly.ComputeBounds();
            min = new Vector3(
                MathF.Min(min.X, bounds.Min.X),
                MathF.Min(min.Y, bounds.Min.Y),
                MathF.Min(min.Z, bounds.Min.Z)
            );
            max = new Vector3(
                MathF.Max(max.X, bounds.Max.X),
                MathF.Max(max.Y, bounds.Max.Y),
                MathF.Max(max.Z, bounds.Max.Z)
            );
        }

        return Bounds.FromMinMax(min, max);
    }

    /// <summary>ABT 光线追踪</summary>
    public bool RayCast(Vector3 origin, Vector3 direction, out RayCastResult result,
        float maxDistance = float.MaxValue)
    {
        result = default;
        float closestDistance = maxDistance;
        bool hit = false;

        RayCastRecursive(Root, origin, direction, maxDistance, ref closestDistance, ref result, ref hit);

        return hit;
    }

    private void RayCastRecursive(ABTNode? node, Vector3 origin, Vector3 direction,
        float maxDistance, ref float closestDistance, ref RayCastResult result, ref bool hit)
    {
        if (node == null) return;

        // 首先检测与节点边界盒的相交
        if (!IntersectRayAABB(origin, direction, node.Bounds, maxDistance))
        {
            return;
        }

        // 叶子节点：检测与几何体的相交
        if (node.IsLeaf)
        {
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
            return;
        }

        // 内部节点：根据射线方向选择遍历顺序
        float originValue = GetAxisValue(origin, node.SplitAxis);
        float dirValue = GetAxisValue(direction, node.SplitAxis);

        ABTNode firstChild, secondChild;

        if (dirValue >= 0)
        {
            // 射线朝正方向：先遍历左侧
            firstChild = originValue < node.SplitPosition ? node.LeftChild : node.RightChild;
            secondChild = originValue < node.SplitPosition ? node.RightChild : node.LeftChild;
        }
        else
        {
            // 射线朝负方向：先遍历右侧
            firstChild = originValue > node.SplitPosition ? node.RightChild : node.LeftChild;
            secondChild = originValue > node.SplitPosition ? node.LeftChild : node.RightChild;
        }

        RayCastRecursive(firstChild, origin, direction, maxDistance, ref closestDistance, ref result, ref hit);

        // 只有当交点距离大于到分割平面的距离时，才需要遍历另一个子节点
        if (!hit || closestDistance > Math.Abs(node.SplitPosition - originValue) / Math.Abs(dirValue))
        {
            RayCastRecursive(secondChild, origin, direction, maxDistance, ref closestDistance, ref result, ref hit);
        }
    }

    /// <summary>射线与AABB相交检测</summary>
    private static bool IntersectRayAABB(Vector3 origin, Vector3 direction, Bounds aabb, float maxDistance)
    {
        float tMin = 0;
        float tMax = maxDistance;

        for (int i = 0; i < 3; i++)
        {
            float o = GetAxisValue(origin, i);
            float d = GetAxisValue(direction, i);
            float minVal = GetAxisValue(aabb.Min, i);
            float maxVal = GetAxisValue(aabb.Max, i);

            if (MathF.Abs(d) < 1e-6f)
            {
                if (o < minVal || o > maxVal) return false;
            }
            else
            {
                float invD = 1.0f / d;
                float t1 = (minVal - o) * invD;
                float t2 = (maxVal - o) * invD;

                if (t1 > t2) (t1, t2) = (t2, t1);

                tMin = MathF.Max(tMin, t1);
                tMax = MathF.Min(tMax, t2);

                if (tMin > tMax) return false;
            }
        }

        return tMin <= maxDistance;
    }

    /// <summary>射线与多边形相交检测</summary>
    private static bool RayPolygonIntersection(Vector3 origin, Vector3 direction,
        Polygon polygon, out float distance, out Vector3 hitPoint)
    {
        distance = 0;
        hitPoint = Vector3.Zero;

        var plane = polygon.Plane;

        float denom = Vector3.Dot(plane.Normal, direction);
        if (MathF.Abs(denom) < 1e-6f) return false;

        float t = -(Vector3.Dot(plane.Normal, origin) + plane.D) / denom;
        if (t < 0) return false;

        hitPoint = origin + direction * t;
        distance = t;

        return IsPointInPolygon(hitPoint, polygon);
    }

    /// <summary>检查点是否在多边形内</summary>
    private static bool IsPointInPolygon(Vector3 point, Polygon polygon)
    {
        var normal = polygon.Plane.Normal;
        float ax = MathF.Abs(normal.X);
        float ay = MathF.Abs(normal.Y);
        float az = MathF.Abs(normal.Z);

        Func<Vector3, (float, float)> project;
        if (ax >= ay && ax >= az)
            project = v => (v.Y, v.Z);
        else if (ay >= ax && ay >= az)
            project = v => (v.X, v.Z);
        else
            project = v => (v.X, v.Y);

        var (px, py) = project(point);

        bool inside = false;
        int count = polygon.VertexCount;
        for (int i = 0, j = count - 1; i < count; j = i++)
        {
            var (ix, iy) = project(polygon.Vertices[i].Position);
            var (jx, jy) = project(polygon.Vertices[j].Position);

            if (((iy > py) != (jy > py)) &&
                (px < (jx - ix) * (py - iy) / (jy - iy + 1e-10f) + ix))
            {
                inside = !inside;
            }
        }

        return inside;
    }

    /// <summary>收集所有节点</summary>
    public List<ABTNode> GetAllNodes()
    {
        var nodes = new List<ABTNode>();
        GetAllNodesRecursive(Root, nodes);
        return nodes;
    }

    private void GetAllNodesRecursive(ABTNode? node, List<ABTNode> result)
    {
        if (node == null) return;
        result.Add(node);
        GetAllNodesRecursive(node.LeftChild, result);
        GetAllNodesRecursive(node.RightChild, result);
    }
}

/// <summary>
/// 分割候选
/// </summary>
internal struct SplitCandidate
{
    public bool IsValid;
    public int Axis;
    public float Position;
    public float Cost;
}
