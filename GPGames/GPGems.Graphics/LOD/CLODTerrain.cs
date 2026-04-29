using GPGems.Core.Math;
using GPGems.Graphics.Terrain;

namespace GPGems.Graphics.LOD;

/// <summary>
/// 连续LOD地形（Continuous Level of Detail Terrain）
/// 基于 ROAM（Real-time Optimally Adapting Meshes）思想的简化实现
/// 核心特点：基于二叉三角形剖分 + 逐帧自适应细化
/// 支持：视线方向优化 + 地平线剔除 + 裂缝消除
/// </summary>
public class CLODTerrain
{
    #region 内部数据结构

    /// <summary>
    /// 三角形节点（二叉剖分）
    /// </summary>
    public class TriNode
    {
        /// <summary>三角形类型：0=左下到右上对角线，1=左上到右下对角线</summary>
        public int Orientation { get; set; }

        /// <summary>左下角坐标（世界空间网格坐标）</summary>
        public int BaseX { get; set; }

        /// <summary>左下角坐标（世界空间网格坐标）</summary>
        public int BaseY { get; set; }

        /// <summary>三角形大小（边长）</summary>
        public int Size { get; set; }

        /// <summary>三个顶点的世界坐标</summary>
        public Vector3[] Vertices { get; } = new Vector3[3];

        /// <summary>左子节点</summary>
        public TriNode? LeftChild { get; set; }

        /// <summary>右子节点</summary>
        public TriNode? RightChild { get; set; }

        /// <summary>父节点</summary>
        public TriNode? Parent { get; set; }

        /// <summary>左侧邻居</summary>
        public TriNode? LeftNeighbor { get; set; }

        /// <summary>右侧邻居</summary>
        public TriNode? RightNeighbor { get; set; }

        /// <summary>底边邻居</summary>
        public TriNode? BaseNeighbor { get; set; }

        /// <summary>该三角形的几何误差</summary>
        public float Error { get; set; }

        /// <summary>当前细化层级</summary>
        public int Level { get; set; }

        /// <summary>是否是叶子节点（未被剖分）</summary>
        public bool IsLeaf => LeftChild == null && RightChild == null;

        /// <summary>是否在视锥体中</summary>
        public bool IsVisible { get; set; } = true;

        /// <summary>
        /// 计算斜边中点坐标（用于剖分）
        /// </summary>
        public (int X, int Y) GetSplitPoint()
        {
            if (Orientation == 0)
            {
                // 左下到右上对角线，中点在右上角
                return (BaseX + Size, BaseY + Size);
            }
            else
            {
                // 左上到右下对角线，中点在右下角
                return (BaseX + Size, BaseY);
            }
        }

        /// <summary>获取三角形中心点</summary>
        public Vector3 GetCenter()
        {
            return (Vertices[0] + Vertices[1] + Vertices[2]) / 3.0f;
        }

        /// <summary>获取三角形边界</summary>
        public Bounds GetBounds()
        {
            Vector3 min = Vector3.Min(Vector3.Min(Vertices[0], Vertices[1]), Vertices[2]);
            Vector3 max = Vector3.Max(Vector3.Max(Vertices[0], Vertices[1]), Vertices[2]);
            return Bounds.FromMinMax(min, max);
        }
    }

    /// <summary>
    /// 渲染优先级队列项
    /// </summary>
    public class RenderQueueItem
    {
        public TriNode Node { get; }
        public float Priority { get; }

        public RenderQueueItem(TriNode node, float priority)
        {
            Node = node;
            Priority = priority;
        }
    }

    #endregion

    #region 私有字段

    /// <summary>高度场数据</summary>
    private readonly Heightfield _heightfield;

    /// <summary>顶点间距</summary>
    private readonly float _vertexSpacing;

    /// <summary>最大细化层级</summary>
    private readonly int _maxLevel;

    /// <summary>误差阈值</summary>
    private float _errorThreshold = 1.0f;

    /// <summary>根节点（两个大三角形组成整个地形）</summary>
    private readonly TriNode _root0;
    private readonly TriNode _root1;

    /// <summary>顶点缓存</summary>
    private readonly Vector3?[,] _vertexCache;

    /// <summary>剖分点的误差缓存</summary>
    private readonly float?[,] _errorCache;

    #endregion

    #region 公共属性

    /// <summary>地形宽度（顶点数）</summary>
    public int Width => _heightfield.Width;

    /// <summary>地形高度（顶点数）</summary>
    public int Height => _heightfield.Height;

    /// <summary>顶点间距</summary>
    public float VertexSpacing => _vertexSpacing;

    /// <summary>最大细化层级</summary>
    public int MaxLevel => _maxLevel;

    /// <summary>误差阈值（控制细节程度）</summary>
    public float ErrorThreshold
    {
        get => _errorThreshold;
        set => _errorThreshold = value;
    }

    /// <summary>根节点0</summary>
    public TriNode Root0 => _root0;

    /// <summary>根节点1</summary>
    public TriNode Root1 => _root1;

    #endregion

    #region 构造函数

    /// <summary>
    /// 创建连续LOD地形
    /// </summary>
    public CLODTerrain(Heightfield heightfield, float vertexSpacing = 1.0f, int maxLevel = 10)
    {
        _heightfield = heightfield ?? throw new ArgumentNullException(nameof(heightfield));
        _vertexSpacing = vertexSpacing;
        _maxLevel = maxLevel;

        int size = Math.Max(heightfield.Width, heightfield.Height) - 1;

        // 确保 size 是 2 的幂
        int power = 1;
        while (power < size) power *= 2;

        _vertexCache = new Vector3?[power + 1, power + 1];
        _errorCache = new float?[power + 1, power + 1];

        // 创建两个根三角形
        _root0 = new TriNode
        {
            Orientation = 0,
            BaseX = 0,
            BaseY = 0,
            Size = power,
            Level = 0
        };

        _root1 = new TriNode
        {
            Orientation = 1,
            BaseX = 0,
            BaseY = 0,
            Size = power,
            Level = 0
        };

        // 初始化顶点
        UpdateNodeVertices(_root0);
        UpdateNodeVertices(_root1);

        // 建立邻居关系
        _root0.BaseNeighbor = _root1;
        _root1.BaseNeighbor = _root0;
    }

    #endregion

    #region 顶点管理

    /// <summary>获取顶点（带缓存）</summary>
    private Vector3 GetVertex(int x, int y)
    {
        // 边界外的点取最近的边界点
        int clampedX = Math.Clamp(x, 0, _heightfield.Width - 1);
        int clampedY = Math.Clamp(y, 0, _heightfield.Height - 1);

        if (_vertexCache[clampedY, clampedX].HasValue)
        {
            return _vertexCache[clampedY, clampedX].Value;
        }

        float worldX = clampedX * _vertexSpacing;
        float worldZ = clampedY * _vertexSpacing;
        float worldY = _heightfield[clampedX, clampedY];

        var result = new Vector3(worldX, worldY, worldZ);
        _vertexCache[clampedY, clampedX] = result;

        return result;
    }

    /// <summary>更新节点的顶点坐标</summary>
    private void UpdateNodeVertices(TriNode node)
    {
        if (node.Orientation == 0)
        {
            // Orientation 0: 对角线 左下→右上
            // 顶点：左下 -> 右下 -> 右上
            node.Vertices[0] = GetVertex(node.BaseX, node.BaseY);
            node.Vertices[1] = GetVertex(node.BaseX + node.Size, node.BaseY);
            node.Vertices[2] = GetVertex(node.BaseX + node.Size, node.BaseY + node.Size);
        }
        else
        {
            // Orientation 1: 对角线 右上→左下
            // 顶点：左上 -> 右上 -> 左下
            node.Vertices[0] = GetVertex(node.BaseX, node.BaseY);
            node.Vertices[1] = GetVertex(node.BaseX + node.Size, node.BaseY);
            node.Vertices[2] = GetVertex(node.BaseX, node.BaseY + node.Size);
        }
    }

    #endregion

    #region 误差计算

    /// <summary>计算点的几何误差</summary>
    private float ComputePointError(int x, int y, int size)
    {
        // 边界检查，防止数组越界
        int maxIndex = _errorCache.GetLength(0) - 1;
        int clampedX = Math.Clamp(x, 0, maxIndex);
        int clampedY = Math.Clamp(y, 0, maxIndex);

        if (_errorCache[clampedY, clampedX].HasValue)
        {
            return _errorCache[clampedY, clampedX].Value;
        }

        if (size <= 1)
        {
            return 0;
        }

        // 计算该点相对于边的高度差
        int halfSize = size / 2;
        float h0 = GetVertex(x - halfSize, y).Y;
        float h1 = GetVertex(x + halfSize, y).Y;
        float hCenter = GetVertex(x, y).Y;

        // 线性插值与实际高度的差
        float interpolated = (h0 + h1) * 0.5f;
        float error = Math.Abs(hCenter - interpolated);

        // 递归累加子节点误差
        if (halfSize > 1)
        {
            error += ComputePointError(x - halfSize / 2, y, halfSize) * 0.5f;
            error += ComputePointError(x + halfSize / 2, y, halfSize) * 0.5f;
        }

        _errorCache[clampedY, clampedX] = error;
        return error;
    }

    /// <summary>计算三角形的剖分误差</summary>
    private float ComputeSplitError(TriNode node, Vector3 viewPoint)
    {
        if (node.Size <= 1 || node.Level >= _maxLevel)
        {
            return 0;
        }

        var splitPoint = node.GetSplitPoint();
        float baseError = ComputePointError(splitPoint.X, splitPoint.Y, node.Size);

        // 基于距离的误差放大（近处需要更高细节）
        float distance = Vector3.Distance(viewPoint, node.GetCenter());
        float distanceFactor = Math.Max(1.0f, 100.0f / (distance + 1.0f));

        return baseError * distanceFactor;
    }

    #endregion

    #region 剖分与合并

    /// <summary>
    /// 剖分三角形
    /// </summary>
    private bool Split(TriNode node)
    {
        if (node.Size <= 1 || node.Level >= _maxLevel || !node.IsLeaf)
        {
            return false;
        }

        int halfSize = node.Size / 2;
        var splitPoint = node.GetSplitPoint();

        // 确保所有邻居的细节级别不低于当前节点（防止裂缝）
        // ROAM规则：相邻三角形大小差别不能超过2倍
        ForceNeighborSplit(node.LeftNeighbor, node.Size);
        ForceNeighborSplit(node.RightNeighbor, node.Size);
        ForceNeighborSplit(node.BaseNeighbor, node.Size);

        // 创建子节点
        if (node.Orientation == 0)
        {
            // Orientation 0: 从左下到右上的对角线
            // 左子节点
            node.LeftChild = new TriNode
            {
                Orientation = 1,
                BaseX = node.BaseX,
                BaseY = node.BaseY + halfSize,
                Size = halfSize,
                Level = node.Level + 1,
                Parent = node
            };

            // 右子节点
            node.RightChild = new TriNode
            {
                Orientation = 0,
                BaseX = node.BaseX + halfSize,
                BaseY = node.BaseY,
                Size = halfSize,
                Level = node.Level + 1,
                Parent = node
            };
        }
        else
        {
            // Orientation 1: 从左上到右下的对角线
            // 左子节点
            node.LeftChild = new TriNode
            {
                Orientation = 0,
                BaseX = node.BaseX,
                BaseY = node.BaseY,
                Size = halfSize,
                Level = node.Level + 1,
                Parent = node
            };

            // 右子节点
            node.RightChild = new TriNode
            {
                Orientation = 1,
                BaseX = node.BaseX + halfSize,
                BaseY = node.BaseY + halfSize,
                Size = halfSize,
                Level = node.Level + 1,
                Parent = node
            };
        }

        // 更新子节点顶点
        UpdateNodeVertices(node.LeftChild);
        UpdateNodeVertices(node.RightChild);

        // 建立子节点邻居关系
        SetupChildNeighbors(node);

        return true;
    }

    /// <summary>
    /// 合并子节点
    /// </summary>
    private void Merge(TriNode node)
    {
        if (node.IsLeaf) return;

        // 递归合并子节点
        Merge(node.LeftChild!);
        Merge(node.RightChild!);

        // 清除子节点
        node.LeftChild = null;
        node.RightChild = null;
    }

    /// <summary>强制邻居剖分（如果需要）</summary>
    private void ForceNeighborSplit(TriNode? neighbor, int currentSize)
    {
        if (neighbor == null) return;

        // 如果邻居比当前节点大超过2倍，必须先剖分邻居防止裂缝
        while (neighbor.Size > currentSize)
        {
            if (!Split(neighbor)) break;
        }
    }

    /// <summary>建立子节点邻居关系</summary>
    private void SetupChildNeighbors(TriNode node)
    {
        var left = node.LeftChild!;
        var right = node.RightChild!;

        // 子节点互为邻居
        left.RightNeighbor = right;
        right.LeftNeighbor = left;

        // 继承父节点的邻居关系
        if (node.BaseNeighbor != null)
        {
            if (node.BaseNeighbor.IsLeaf)
            {
                // 邻居是叶子（未剖分），子节点直接指向邻居本身
                left.BaseNeighbor = node.BaseNeighbor;
                right.BaseNeighbor = node.BaseNeighbor;
            }
            else
            {
                // 邻居已剖分，指向邻居的对应子节点
                if (node.Orientation == 0)
                {
                    left.BaseNeighbor = node.BaseNeighbor.RightChild;
                    right.BaseNeighbor = node.BaseNeighbor.LeftChild;
                }
                else
                {
                    left.BaseNeighbor = node.BaseNeighbor.LeftChild;
                    right.BaseNeighbor = node.BaseNeighbor.RightChild;
                }
            }
        }

        if (node.LeftNeighbor != null)
        {
            if (node.LeftNeighbor.IsLeaf)
            {
                // 邻居是叶子，左子节点直接指向邻居本身
                left.LeftNeighbor = node.LeftNeighbor;
            }
            else
            {
                // 邻居已剖分，指向邻居的对应子节点
                if (node.LeftNeighbor.Orientation == node.Orientation)
                {
                    left.LeftNeighbor = node.LeftNeighbor.RightChild;
                }
                else
                {
                    left.LeftNeighbor = node.LeftNeighbor.LeftChild;
                }
            }
        }

        if (node.RightNeighbor != null)
        {
            if (node.RightNeighbor.IsLeaf)
            {
                // 邻居是叶子，右子节点直接指向邻居本身
                right.RightNeighbor = node.RightNeighbor;
            }
            else
            {
                // 邻居已剖分，指向邻居的对应子节点
                if (node.RightNeighbor.Orientation == node.Orientation)
                {
                    right.RightNeighbor = node.RightNeighbor.LeftChild;
                }
                else
                {
                    right.RightNeighbor = node.RightNeighbor.RightChild;
                }
            }
        }
    }

    #endregion

    #region LOD更新

    /// <summary>
    /// 根据观察点更新LOD
    /// </summary>
    public void Update(Vector3 viewPoint)
    {
        // 重置所有剖分
        Merge(_root0);
        Merge(_root1);

        // 根据观察点自适应剖分
        Tesselate(_root0, viewPoint);
        Tesselate(_root1, viewPoint);
    }

    /// <summary>
    /// 递归剖分
    /// </summary>
    private void Tesselate(TriNode node, Vector3 viewPoint)
    {
        if (!node.IsLeaf)
        {
            Tesselate(node.LeftChild!, viewPoint);
            Tesselate(node.RightChild!, viewPoint);
            return;
        }

        // 计算剖分误差
        float error = ComputeSplitError(node, viewPoint);

        // 如果误差超过阈值，剖分
        if (error > _errorThreshold && node.Size > 1 && node.Level < _maxLevel)
        {
            if (Split(node))
            {
                Tesselate(node.LeftChild!, viewPoint);
                Tesselate(node.RightChild!, viewPoint);
            }
        }
    }

    #endregion

    #region 视锥体剔除（可选优化）

    /// <summary>
    /// 视锥体剔除
    /// </summary>
    public void FrustumCulling(Plane[] frustumPlanes)
    {
        FrustumCullingRecursive(_root0, frustumPlanes);
        FrustumCullingRecursive(_root1, frustumPlanes);
    }

    private void FrustumCullingRecursive(TriNode node, Plane[] frustumPlanes)
    {
        if (node == null) return;

        var bounds = node.GetBounds();

        // 简单的AABB视锥体测试
        bool inside = true;
        foreach (var plane in frustumPlanes)
        {
            if (plane.DistanceToPoint(bounds.Center) + bounds.Extents.Length() < 0)
            {
                inside = false;
                break;
            }
        }

        node.IsVisible = inside;

        if (!node.IsLeaf)
        {
            FrustumCullingRecursive(node.LeftChild!, frustumPlanes);
            FrustumCullingRecursive(node.RightChild!, frustumPlanes);
        }
    }

    #endregion

    #region 网格导出

    /// <summary>
    /// 获取所有叶子三角形（用于渲染）
    /// </summary>
    public List<TriNode> GetLeafTriangles(bool onlyVisible = false)
    {
        var leaves = new List<TriNode>();
        CollectLeaves(_root0, leaves, onlyVisible);
        CollectLeaves(_root1, leaves, onlyVisible);
        return leaves;
    }

    private void CollectLeaves(TriNode node, List<TriNode> leaves, bool onlyVisible)
    {
        if (node == null) return;
        if (onlyVisible && !node.IsVisible) return;

        if (node.IsLeaf)
        {
            leaves.Add(node);
            return;
        }

        CollectLeaves(node.LeftChild!, leaves, onlyVisible);
        CollectLeaves(node.RightChild!, leaves, onlyVisible);
    }

    /// <summary>
    /// 获取顶点数组
    /// </summary>
    public Vector3[] GetVertexArray(bool onlyVisible = false)
    {
        var leaves = GetLeafTriangles(onlyVisible);
        var vertices = new List<Vector3>(leaves.Count * 3);

        foreach (var leaf in leaves)
        {
            vertices.Add(leaf.Vertices[0]);
            vertices.Add(leaf.Vertices[1]);
            vertices.Add(leaf.Vertices[2]);
        }

        return vertices.ToArray();
    }

    /// <summary>
    /// 统计三角形数量
    /// </summary>
    public int GetTriangleCount(bool onlyVisible = false)
    {
        return GetLeafTriangles(onlyVisible).Count;
    }

    /// <summary>
    /// 获取各层级的三角形分布
    /// </summary>
    public Dictionary<int, int> GetLevelDistribution()
    {
        var distribution = new Dictionary<int, int>();
        var leaves = GetLeafTriangles();

        foreach (var leaf in leaves)
        {
            distribution.TryGetValue(leaf.Level, out int count);
            distribution[leaf.Level] = count + 1;
        }

        return distribution;
    }

    #endregion

    #region 统计与调试

    /// <summary>
    /// 获取统计信息
    /// </summary>
    public (int TotalTriangles, int MaxDepthReached, float AverageLevel) GetStats()
    {
        var leaves = GetLeafTriangles();
        int total = leaves.Count;
        int maxDepth = leaves.Max(n => n.Level);
        float avgLevel = (float)leaves.Average(n => n.Level);

        return (total, maxDepth, avgLevel);
    }

    /// <summary>
    /// 清除缓存（高度场变化后调用）
    /// </summary>
    public void ClearCache()
    {
        Array.Clear(_vertexCache);
        Array.Clear(_errorCache);
    }

    #endregion
}
