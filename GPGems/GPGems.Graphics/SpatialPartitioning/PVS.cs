using GPGems.Core.Graphics;
using System.Numerics;
using GPGems.Core.Math;
using GPGems.Core.Geometry;
namespace GPGems.Graphics.SpatialPartitioning;

/// <summary>
/// 潜在可见集（Potentially Visible Set）
/// 预计算场景中每个区域可见的其他区域
/// 基于 Game Programming Gems 6 Chapter 1.5
/// </summary>
public class PVS
{
    private readonly PortalSystem _portalSystem;

    /// <summary>每个叶子的可见叶子集合</summary>
    private readonly Dictionary<int, HashSet<int>> _visibility = [];

    /// <summary>是否已经预计算完成</summary>
    public bool IsComputed { get; private set; }

    public PVS(PortalSystem portalSystem)
    {
        _portalSystem = portalSystem;
    }

    /// <summary>预计算所有叶子的可见性</summary>
    public void ComputePVS()
    {
        _visibility.Clear();

        foreach (var leaf in _portalSystem.Leaves)
        {
            var visible = _portalSystem.ComputeVisibilityForLeaf(leaf);
            _visibility[leaf.LeafId] = visible;

            // 也存储到叶子节点中
            leaf.VisibleLeaves.Clear();
            foreach (var visibleLeafId in visible)
            {
                leaf.VisibleLeaves.Add(visibleLeafId);
            }
        }

        IsComputed = true;
    }

    /// <summary>获取指定叶子可见的所有叶子</summary>
    public HashSet<int> GetVisibleLeaves(int leafId)
    {
        if (_visibility.TryGetValue(leafId, out var visible))
        {
            return visible;
        }
        return [];
    }

    /// <summary>从给定点获取所有可见几何体</summary>
    public List<Polygon> GetVisibleGeometry(Vector3 viewpoint)
    {
        var result = new List<Polygon>();

        var startLeaf = _portalSystem.FindLeafContainingPoint(viewpoint);
        if (startLeaf == null)
        {
            // 如果不在任何叶子中，返回所有几何体
            foreach (var leaf in _portalSystem.Leaves)
            {
                result.AddRange(leaf.Geometry);
            }
            return result;
        }

        // 使用预计算的PVS
        if (IsComputed)
        {
            var visibleLeafIds = GetVisibleLeaves(startLeaf.LeafId);
            foreach (var leafId in visibleLeafIds)
            {
                var leaf = _portalSystem.Leaves.FirstOrDefault(l => l.LeafId == leafId);
                if (leaf != null)
                {
                    result.AddRange(leaf.Geometry);
                }
            }
        }
        else
        {
            // 实时计算
            var visibleLeaves = _portalSystem.GetVisibleLeaves(viewpoint, Vector3.Zero);
            foreach (var leaf in visibleLeaves)
            {
                result.AddRange(leaf.Geometry);
            }
        }

        return result;
    }

    /// <summary>使用Portal遍历进行可见性判断（实时）</summary>
    public List<Polygon> GetVisibleGeometryPortalTraversal(Vector3 viewpoint, Vector3 viewDirection, float fov)
    {
        var startLeaf = _portalSystem.FindLeafContainingPoint(viewpoint);
        if (startLeaf == null)
        {
            return [];
        }

        var visiblePolygons = new List<Polygon>();
        var visitedLeaves = new HashSet<int>();

        PortalTraverseRecursive(startLeaf, viewpoint, viewDirection, fov,
            visiblePolygons, visitedLeaves);

        return visiblePolygons;
    }

    private void PortalTraverseRecursive(BSPLeaf leaf, Vector3 viewpoint,
        Vector3 viewDirection, float fov, List<Polygon> visiblePolygons, HashSet<int> visitedLeaves)
    {
        if (visitedLeaves.Contains(leaf.LeafId)) return;
        visitedLeaves.Add(leaf.LeafId);

        // 添加当前叶子的几何体
        visiblePolygons.AddRange(leaf.Geometry);

        // 通过门户递归
        foreach (var portal in leaf.Portals)
        {
            if (!portal.IsVisibleFrom(viewpoint)) continue;

            // 检查门户是否在视锥内
            if (!IsPortalInFrustum(portal, viewpoint, viewDirection, fov)) continue;

            var otherLeaf = portal.FrontLeaf == leaf ? portal.BackLeaf : portal.FrontLeaf;
            if (otherLeaf != null)
            {
                // 裁剪视锥体（简化）
                PortalTraverseRecursive(otherLeaf, viewpoint, viewDirection, fov,
                    visiblePolygons, visitedLeaves);
            }
        }
    }

    /// <summary>检查门户是否在视锥内</summary>
    private bool IsPortalInFrustum(Portal portal, Vector3 viewpoint, Vector3 viewDirection, float fov)
    {
        var portalCenter = ComputePolygonCenter(portal.Geometry);
        var toPortal = portalCenter - viewpoint;
        var distance = toPortal.Length();

        if (distance < 1e-6f) return true;

        var direction = toPortal / distance;

        // 检查角度
        var dot = Vector3.Dot(direction, viewDirection);
        var cosHalfFov = MathF.Cos(fov * 0.5f);

        return dot > cosHalfFov;
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

    /// <summary>计算PVS压缩统计</summary>
    public PVSStats ComputeStats()
    {
        int totalLeafCount = _portalSystem.Leaves.Count;
        int totalVisible = 0;
        int maxVisible = 0;
        int minVisible = int.MaxValue;

        foreach (var kvp in _visibility)
        {
            int count = kvp.Value.Count;
            totalVisible += count;
            maxVisible = Math.Max(maxVisible, count);
            minVisible = Math.Min(minVisible, count);
        }

        float averageVisible = totalLeafCount > 0 ? (float)totalVisible / totalLeafCount : 0;
        float reduction = 1.0f - (averageVisible / totalLeafCount);

        return new PVSStats
        {
            TotalLeaves = totalLeafCount,
            AverageVisibleLeaves = averageVisible,
            MaxVisibleLeaves = maxVisible,
            MinVisibleLeaves = minVisible == int.MaxValue ? 0 : minVisible,
            VisibilityReduction = reduction,
            IsComputed = IsComputed
        };
    }

    /// <summary>导出PVS数据</summary>
    public PVSData ExportData()
    {
        var data = new PVSData
        {
            LeafCount = _portalSystem.Leaves.Count,
            LeafVisibility = new int[_portalSystem.Leaves.Count][]
        };

        foreach (var leaf in _portalSystem.Leaves)
        {
            data.LeafVisibility[leaf.LeafId] = leaf.VisibleLeaves.ToArray();
        }

        return data;
    }
}

/// <summary>
/// PVS 统计信息
/// </summary>
public struct PVSStats
{
    public int TotalLeaves;
    public float AverageVisibleLeaves;
    public int MaxVisibleLeaves;
    public int MinVisibleLeaves;
    public float VisibilityReduction; // 0-1，越高表示压缩越多
    public bool IsComputed;

    public override string ToString()
    {
        return $"Leaves: {TotalLeaves}, " +
               $"Visible: avg={AverageVisibleLeaves:F1}, min={MinVisibleLeaves}, max={MaxVisibleLeaves}, " +
               $"Reduction: {VisibilityReduction:P1}";
    }
}

/// <summary>
/// PVS 序列化数据
/// </summary>
public class PVSData
{
    public int LeafCount;
    public int[][] LeafVisibility;
}
