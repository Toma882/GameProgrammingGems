using GPGems.Core.Graphics;
using System.Numerics;
using GPGems.Core.Math;
using GPGems.Core.Geometry;
namespace GPGems.Graphics.SpatialPartitioning;

/// <summary>
/// Portal 门户
/// 连接两个区域的可见性通道
/// 基于 Game Programming Gems 6 Chapter 1.5
/// </summary>
public class Portal
{
    /// <summary>门户多边形</summary>
    public Polygon Geometry { get; }

    /// <summary>连接的前方区域</summary>
    public BSPLeaf? FrontLeaf { get; set; }

    /// <summary>连接的后方区域</summary>
    public BSPLeaf? BackLeaf { get; set; }

    /// <summary>门户是否双向可见</summary>
    public bool IsBidirectional { get; set; } = true;

    public Portal(Polygon geometry)
    {
        Geometry = geometry;
    }

    /// <summary>从给定视点判断门户是否可见</summary>
    public bool IsVisibleFrom(Vector3 viewpoint)
    {
        var side = Geometry.Plane.ClassifyPoint(viewpoint);
        return side == PlaneSide.Front || (IsBidirectional && side == PlaneSide.Back);
    }

    /// <summary>计算穿过门户后的视锥体裁剪</summary>
    public Bounds? ClipViewFrustum(Vector3 viewpoint, Bounds frustum)
    {
        // 简化实现：返回门户的边界盒
        return Geometry.ComputeBounds();
    }
}

/// <summary>
/// BSP 叶子节点（区域）
/// 表示场景中的一个凸子空间
/// </summary>
public class BSPLeaf
{
    /// <summary>叶子节点的边界盒</summary>
    public Bounds Bounds { get; set; }

    /// <summary>该区域内的几何体</summary>
    public List<Polygon> Geometry { get; } = [];

    /// <summary>连接到其他区域的门户</summary>
    public List<Portal> Portals { get; } = [];

    /// <summary>叶子节点ID</summary>
    public int LeafId { get; set; }

    /// <summary>潜在可见集（PVS）</summary>
    public HashSet<int> VisibleLeaves { get; } = [];
}

/// <summary>
/// Portal 系统
/// 用于基于门户的可见性裁剪
/// 基于 Game Programming Gems 6 Chapter 1.5
/// </summary>
public class PortalSystem
{
    private readonly List<BSPLeaf> _leaves = [];
    private readonly List<Portal> _portals = [];

    /// <summary>所有叶子节点</summary>
    public IReadOnlyList<BSPLeaf> Leaves => _leaves;

    /// <summary>所有门户</summary>
    public IReadOnlyList<Portal> Portals => _portals;

    /// <summary>从BSP树构建Portal系统</summary>
    public void BuildFromBSP(BSPTree tree)
    {
        _leaves.Clear();
        _portals.Clear();

        // 收集所有叶子节点
        CollectLeavesRecursive(tree.Root, null, PlaneSide.Front);

        // 为每个叶子节点分配ID
        for (int i = 0; i < _leaves.Count; i++)
        {
            _leaves[i].LeafId = i;
        }

        // 生成门户
        GeneratePortals();
    }

    private void CollectLeavesRecursive(BSPNode? node, BSPLeaf? parentLeaf, PlaneSide side)
    {
        if (node == null) return;

        if (node.IsLeaf)
        {
            var leaf = new BSPLeaf();

            // 计算叶子节点的边界盒
            if (node.Polygons.Count > 0)
            {
                leaf.Geometry.AddRange(node.Polygons);

                var bounds = node.Polygons[0].ComputeBounds();
                foreach (var poly in node.Polygons)
                {
                    var polyBounds = poly.ComputeBounds();
                    bounds = MergeBounds(bounds, polyBounds);
                }
                leaf.Bounds = bounds;
            }
            else if (parentLeaf != null)
            {
                leaf.Bounds = parentLeaf.Bounds;
            }

            _leaves.Add(leaf);
            return;
        }

        // 内部节点：递归处理子节点
        CollectLeavesRecursive(node.FrontChild, parentLeaf, PlaneSide.Front);
        CollectLeavesRecursive(node.BackChild, parentLeaf, PlaneSide.Back);
    }

    private Bounds MergeBounds(Bounds a, Bounds b)
    {
        var min = new Vector3(
            MathF.Min(a.Min.X, b.Min.X),
            MathF.Min(a.Min.Y, b.Min.Y),
            MathF.Min(a.Min.Z, b.Min.Z)
        );
        var max = new Vector3(
            MathF.Max(a.Max.X, b.Max.X),
            MathF.Max(a.Max.Y, b.Max.Y),
            MathF.Max(a.Max.Z, b.Max.Z)
        );
        return Bounds.FromMinMax(min, max);
    }

    /// <summary>在叶子节点之间生成门户</summary>
    private void GeneratePortals()
    {
        // 简单实现：检测相邻的叶子节点
        for (int i = 0; i < _leaves.Count; i++)
        {
            for (int j = i + 1; j < _leaves.Count; j++)
            {
                var leafA = _leaves[i];
                var leafB = _leaves[j];

                // 检查两个叶子是否相邻
                if (AreAdjacent(leafA, leafB))
                {
                    // 创建连接门户
                    var portal = CreatePortalBetween(leafA, leafB);
                    if (portal != null)
                    {
                        portal.FrontLeaf = leafA;
                        portal.BackLeaf = leafB;
                        leafA.Portals.Add(portal);
                        leafB.Portals.Add(portal);
                        _portals.Add(portal);
                    }
                }
            }
        }
    }

    /// <summary>检查两个叶子节点是否相邻</summary>
    private bool AreAdjacent(BSPLeaf a, BSPLeaf b)
    {
        // 简化的相邻检测：检查边界盒是否接触
        var dx = MathF.Abs(a.Bounds.Center.X - b.Bounds.Center.X);
        var dy = MathF.Abs(a.Bounds.Center.Y - b.Bounds.Center.Y);
        var dz = MathF.Abs(a.Bounds.Center.Z - b.Bounds.Center.Z);

        var tolerance = (a.Bounds.Width + b.Bounds.Width) * 0.5f + 0.01f;

        return dx <= tolerance && dy <= tolerance && dz <= tolerance;
    }

    /// <summary>创建两个叶子之间的门户</summary>
    private Portal? CreatePortalBetween(BSPLeaf a, BSPLeaf b)
    {
        // 简化实现：创建一个简单的门户多边形
        var center = (a.Bounds.Center + b.Bounds.Center) * 0.5f;
        var direction = (b.Bounds.Center - a.Bounds.Center).Normalize();

        // 创建垂直于连接方向的四边形
        var up = new Vector3(0, 1, 0);
        if (MathF.Abs(Vector3.Dot(direction, up)) > 0.9f)
        {
            up = new Vector3(1, 0, 0);
        }

        var right = Vector3.Cross(direction, up).Normalize();
        up = Vector3.Cross(right, direction).Normalize();

        var size = MathF.Min(a.Bounds.Width, b.Bounds.Width) * 0.5f;

        var vertices = new List<Vertex>
        {
            new Vertex(center + right * size + up * size),
            new Vertex(center - right * size + up * size),
            new Vertex(center - right * size - up * size),
            new Vertex(center + right * size - up * size)
        };

        var portalPoly = new Polygon(vertices);
        return new Portal(portalPoly);
    }

    /// <summary>从视点进行可见性判断</summary>
    public List<BSPLeaf> GetVisibleLeaves(Vector3 viewpoint, Vector3 viewDirection, float fov = MathF.PI / 2)
    {
        var visible = new List<BSPLeaf>();
        var startLeaf = FindLeafContainingPoint(viewpoint);

        if (startLeaf == null)
        {
            // 如果不在任何叶子中，返回所有叶子
            return _leaves.ToList();
        }

        var visited = new HashSet<int>();
        GetVisibleLeavesRecursive(startLeaf, viewpoint, visible, visited);

        return visible;
    }

    private void GetVisibleLeavesRecursive(BSPLeaf leaf, Vector3 viewpoint, List<BSPLeaf> visible, HashSet<int> visited)
    {
        if (visited.Contains(leaf.LeafId)) return;
        visited.Add(leaf.LeafId);

        visible.Add(leaf);

        // 通过门户递归访问相邻叶子
        foreach (var portal in leaf.Portals)
        {
            if (!portal.IsVisibleFrom(viewpoint)) continue;

            var otherLeaf = portal.FrontLeaf == leaf ? portal.BackLeaf : portal.FrontLeaf;
            if (otherLeaf != null && !visited.Contains(otherLeaf.LeafId))
            {
                GetVisibleLeavesRecursive(otherLeaf, viewpoint, visible, visited);
            }
        }
    }

    /// <summary>查找包含给定点的叶子节点</summary>
    public BSPLeaf? FindLeafContainingPoint(Vector3 point)
    {
        foreach (var leaf in _leaves)
        {
            if (leaf.Bounds.Contains(point))
            {
                // 进一步检查：检测点是否在叶子的几何体包围中
                // 简化实现：直接返回边界盒包含的叶子
                return leaf;
            }
        }
        return null;
    }

    /// <summary>获取指定叶子周围的可见叶子（用于PVS构建）</summary>
    public HashSet<int> ComputeVisibilityForLeaf(BSPLeaf leaf)
    {
        var visible = new HashSet<int>();
        var visited = new HashSet<int>();

        // 从叶子中心出发，通过门户遍历
        var center = leaf.Bounds.Center;
        ComputeVisibilityRecursive(leaf, center, visible, visited, depth: 0, maxDepth: 10);

        return visible;
    }

    private void ComputeVisibilityRecursive(BSPLeaf leaf, Vector3 viewpoint,
        HashSet<int> visible, HashSet<int> visited, int depth, int maxDepth)
    {
        if (depth >= maxDepth || visited.Contains(leaf.LeafId)) return;

        visited.Add(leaf.LeafId);
        visible.Add(leaf.LeafId);

        foreach (var portal in leaf.Portals)
        {
            if (!portal.IsVisibleFrom(viewpoint)) continue;

            var otherLeaf = portal.FrontLeaf == leaf ? portal.BackLeaf : portal.FrontLeaf;
            if (otherLeaf != null)
            {
                ComputeVisibilityRecursive(otherLeaf, viewpoint, visible, visited, depth + 1, maxDepth);
            }
        }
    }
}
