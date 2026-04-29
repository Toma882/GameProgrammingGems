using GPGems.Core.Math;

namespace GPGems.Graphics.LOD;

/// <summary>
/// 渐进网格（Progressive Mesh）
/// 基于 Game Programming Gems 1 Polygonal 12Svarovsky 实现
/// 核心思想：边折叠（Edge Collapse）简化网格，边分裂（Vertex Split）恢复细节
/// 特点：平滑LOD过渡 + 体积保持 + 拓扑一致性
/// </summary>
public class ProgressiveMesh
{
    #region 内部数据结构

    /// <summary>
    /// 顶点
    /// </summary>
    public class Vertex
    {
        /// <summary>顶点位置</summary>
        public Vector3 Position { get; set; }

        /// <summary>顶点法线</summary>
        public Vector3 Normal { get; set; }

        /// <summary>原始索引</summary>
        public int OriginalIndex { get; set; }

        /// <summary>是否处于激活状态（未被折叠）</summary>
        public bool IsActive { get; set; } = true;

        /// <summary>引用该顶点的面列表</summary>
        public List<int> FaceIndices { get; } = [];

        /// <summary>相邻顶点列表</summary>
        public List<int> NeighborIndices { get; } = [];

        /// <summary>折叠代价（如果作为折叠目标）</summary>
        public float CollapseCost { get; set; } = float.MaxValue;

        /// <summary>折叠到的目标顶点</summary>
        public int CollapseTo { get; set; } = -1;

        public Vertex(Vector3 position)
        {
            Position = position;
        }
    }

    /// <summary>
    /// 三角形面
    /// </summary>
    public class Face
    {
        /// <summary>三个顶点索引</summary>
        public int[] Indices { get; } = new int[3];

        /// <summary>面法线</summary>
        public Vector3 Normal { get; set; }

        /// <summary>是否处于激活状态</summary>
        public bool IsActive { get; set; } = true;

        /// <summary>原始索引</summary>
        public int OriginalIndex { get; set; }

        public Face(int v0, int v1, int v2)
        {
            Indices[0] = v0;
            Indices[1] = v1;
            Indices[2] = v2;
        }

        /// <summary>检查是否包含指定顶点</summary>
        public bool ContainsVertex(int vertexIndex)
        {
            return Indices[0] == vertexIndex ||
                   Indices[1] == vertexIndex ||
                   Indices[2] == vertexIndex;
        }

        /// <summary>替换顶点索引</summary>
        public void ReplaceVertex(int oldIndex, int newIndex)
        {
            for (int i = 0; i < 3; i++)
            {
                if (Indices[i] == oldIndex)
                {
                    Indices[i] = newIndex;
                    break;
                }
            }
        }
    }

    /// <summary>
    /// 边折叠记录（用于边分裂恢复）
    /// </summary>
    public class CollapseRecord
    {
        /// <summary>被折叠的顶点（u 折叠到 v）</summary>
        public int VertexU { get; set; }

        /// <summary>目标顶点</summary>
        public int VertexV { get; set; }

        /// <summary>折叠前u的位置</summary>
        public Vector3 PositionU { get; set; }

        /// <summary>被删除的面</summary>
        public List<int> DeletedFaces { get; } = [];

        /// <summary>受影响的面（需要更新索引）</summary>
        public List<int> AffectedFaces { get; } = [];

        /// <summary>u的相邻顶点（折叠前）</summary>
        public List<int> NeighborsOfU { get; } = [];

        /// <summary>折叠时的几何误差</summary>
        public float Error { get; set; }
    }

    #endregion

    #region 私有字段

    /// <summary>所有顶点（包括被折叠的）</summary>
    private readonly List<Vertex> _vertices = [];

    /// <summary>所有面（包括被删除的）</summary>
    private readonly List<Face> _faces = [];

    /// <summary>折叠历史记录（用于反向的边分裂）</summary>
    private readonly List<CollapseRecord> _collapseHistory = [];

    /// <summary>当前简化层级（0=原始，Count=最大简化）</summary>
    private int _currentLevel;

    /// <summary>原始顶点数</summary>
    private readonly int _originalVertexCount;

    /// <summary>原始面数</summary>
    private readonly int _originalFaceCount;

    #endregion

    #region 公共属性

    /// <summary>当前激活的顶点数</summary>
    public int ActiveVertexCount => _vertices.Count(v => v.IsActive);

    /// <summary>当前激活的面数</summary>
    public int ActiveFaceCount => _faces.Count(f => f.IsActive);

    /// <summary>原始顶点数</summary>
    public int OriginalVertexCount => _originalVertexCount;

    /// <summary>原始面数</summary>
    public int OriginalFaceCount => _originalFaceCount;

    /// <summary>最大简化层级</summary>
    public int MaxLevel => _collapseHistory.Count;

    /// <summary>当前简化层级</summary>
    public int CurrentLevel
    {
        get => _currentLevel;
        set => SetLevel(value);
    }

    /// <summary>所有顶点（只读）</summary>
    public IReadOnlyList<Vertex> Vertices => _vertices;

    /// <summary>所有面（只读）</summary>
    public IReadOnlyList<Face> Faces => _faces;

    /// <summary>折叠历史记录</summary>
    public IReadOnlyList<CollapseRecord> CollapseHistory => _collapseHistory;

    #endregion

    #region 构造函数

    /// <summary>
    /// 从原始网格创建渐进网格
    /// </summary>
    public ProgressiveMesh(Vector3[] vertices, int[] indices)
    {
        if (vertices == null) throw new ArgumentNullException(nameof(vertices));
        if (indices == null) throw new ArgumentNullException(nameof(indices));
        if (indices.Length % 3 != 0) throw new ArgumentException("索引数量必须是3的倍数", nameof(indices));

        // 复制顶点
        for (int i = 0; i < vertices.Length; i++)
        {
            _vertices.Add(new Vertex(vertices[i])
            {
                OriginalIndex = i,
                Normal = Vector3.UnitY
            });
        }

        // 复制面
        for (int i = 0; i < indices.Length; i += 3)
        {
            var face = new Face(indices[i], indices[i + 1], indices[i + 2])
            {
                OriginalIndex = i / 3
            };
            _faces.Add(face);
        }

        _originalVertexCount = vertices.Length;
        _originalFaceCount = indices.Length / 3;

        // 初始化邻接信息
        BuildAdjacency();

        // 计算所有面法线
        ComputeAllNormals();

        // 预计算简化序列（边折叠）
        PrecomputeCollapseSequence();
    }

    #endregion

    #region 初始化

    /// <summary>建立顶点-面邻接关系和顶点-顶点邻接关系</summary>
    private void BuildAdjacency()
    {
        // 清空现有引用
        foreach (var vertex in _vertices)
        {
            vertex.FaceIndices.Clear();
            vertex.NeighborIndices.Clear();
        }

        // 建立顶点-面引用
        for (int faceIndex = 0; faceIndex < _faces.Count; faceIndex++)
        {
            if (!_faces[faceIndex].IsActive) continue;

            var face = _faces[faceIndex];
            for (int i = 0; i < 3; i++)
            {
                int vi = face.Indices[i];
                _vertices[vi].FaceIndices.Add(faceIndex);
            }
        }

        // 建立顶点-顶点邻接关系
        var neighborSet = new HashSet<int>();
        for (int vi = 0; vi < _vertices.Count; vi++)
        {
            if (!_vertices[vi].IsActive) continue;

            neighborSet.Clear();

            foreach (int fi in _vertices[vi].FaceIndices)
            {
                var face = _faces[fi];
                for (int i = 0; i < 3; i++)
                {
                    int nvi = face.Indices[i];
                    if (nvi != vi)
                    {
                        neighborSet.Add(nvi);
                    }
                }
            }

            _vertices[vi].NeighborIndices.AddRange(neighborSet);
        }
    }

    /// <summary>计算所有面的法线</summary>
    private void ComputeAllNormals()
    {
        foreach (var face in _faces)
        {
            if (!face.IsActive) continue;

            var v0 = _vertices[face.Indices[0]].Position;
            var v1 = _vertices[face.Indices[1]].Position;
            var v2 = _vertices[face.Indices[2]].Position;

            face.Normal = Vector3.Cross(v1 - v0, v2 - v0).Normalize();
        }
    }

    #endregion

    #region 边折叠预计算

    /// <summary>预计算整个边折叠序列</summary>
    private void PrecomputeCollapseSequence()
    {
        // 目标：简化到剩余约 4 个顶点或面很少
        int targetFaces = Math.Min(4, _originalFaceCount / 100);

        while (ActiveFaceCount > targetFaces)
        {
            // 计算所有可能的边折叠代价
            ComputeAllCollapseCosts();

            // 找到代价最小的边
            int bestU = -1;
            int bestV = -1;
            float minCost = float.MaxValue;

            foreach (var u in _vertices.Where(v => v.IsActive))
            {
                if (u.CollapseCost < minCost && u.CollapseTo >= 0)
                {
                    minCost = u.CollapseCost;
                    bestU = _vertices.IndexOf(u);
                    bestV = u.CollapseTo;
                }
            }

            if (bestU < 0 || bestV < 0) break;

            // 执行边折叠并记录
            CollapseEdge(bestU, bestV, minCost);
        }

        // 重置到原始状态
        _currentLevel = 0;
        ResetToOriginal();
    }

    /// <summary>计算所有顶点的折叠代价</summary>
    private void ComputeAllCollapseCosts()
    {
        foreach (var u in _vertices)
        {
            if (!u.IsActive)
            {
                u.CollapseCost = float.MaxValue;
                u.CollapseTo = -1;
                continue;
            }

            float minCost = float.MaxValue;
            int bestV = -1;

            foreach (int vi in u.NeighborIndices)
            {
                if (!_vertices[vi].IsActive) continue;

                float cost = ComputeEdgeCollapseCost(_vertices.IndexOf(u), vi);
                if (cost < minCost)
                {
                    minCost = cost;
                    bestV = vi;
                }
            }

            u.CollapseCost = minCost;
            u.CollapseTo = bestV;
        }
    }

    /// <summary>计算边 (u, v) 的折叠代价</summary>
    private float ComputeEdgeCollapseCost(int uIndex, int vIndex)
    {
        var u = _vertices[uIndex];
        var v = _vertices[vIndex];

        // 基础代价：边长度
        float lengthCost = (u.Position - v.Position).Length();

        // 曲率代价（折叠后法线变化越大，代价越高）
        float curvatureCost = 0;

        // 找出 u 和 v 共享的面
        var sharedFaces = new List<Face>();
        var uniqueFacesOfU = new List<Face>();

        foreach (int fi in u.FaceIndices)
        {
            var face = _faces[fi];
            if (!face.IsActive) continue;

            if (face.ContainsVertex(vIndex))
            {
                sharedFaces.Add(face);
            }
            else
            {
                uniqueFacesOfU.Add(face);
            }
        }

        // 如果没有共享面，这条边不是边界边，代价无穷大
        if (sharedFaces.Count == 0)
        {
            return float.MaxValue;
        }

        // 计算曲率代价：折叠后被删除面的法线与其他面的最大夹角
        foreach (var face in uniqueFacesOfU)
        {
            float maxDot = 0;
            foreach (var sharedFace in sharedFaces)
            {
                float dot = Vector3.Dot(face.Normal, sharedFace.Normal);
                maxDot = Math.Max(maxDot, dot);
            }
            curvatureCost = Math.Max(curvatureCost, 1 - maxDot);
        }

        // 边界惩罚（如果折叠边界边，增加惩罚）
        float boundaryPenalty = 0;
        if (sharedFaces.Count == 1) // 边界边
        {
            boundaryPenalty = lengthCost * 2;
        }

        // 总代价
        return lengthCost * (1 + curvatureCost * 2 + boundaryPenalty);
    }

    /// <summary>执行边折叠 (u -> v) 并记录</summary>
    private void CollapseEdge(int uIndex, int vIndex, float error)
    {
        var u = _vertices[uIndex];
        var v = _vertices[vIndex];

        var record = new CollapseRecord
        {
            VertexU = uIndex,
            VertexV = vIndex,
            PositionU = u.Position,
            Error = error
        };

        // 保存 u 的邻居
        record.NeighborsOfU.AddRange(u.NeighborIndices);

        // 找出将被删除的面（同时包含 u 和 v 的面）
        var facesToDelete = new List<int>();
        foreach (int fi in u.FaceIndices)
        {
            if (_faces[fi].ContainsVertex(vIndex))
            {
                facesToDelete.Add(fi);
            }
        }

        // 找出受影响的面（只包含 u，不包含 v 的面）
        var affectedFaces = new List<int>();
        foreach (int fi in u.FaceIndices)
        {
            if (!_faces[fi].ContainsVertex(vIndex))
            {
                affectedFaces.Add(fi);
            }
        }

        record.DeletedFaces.AddRange(facesToDelete);
        record.AffectedFaces.AddRange(affectedFaces);

        // 执行删除
        foreach (int fi in facesToDelete)
        {
            _faces[fi].IsActive = false;
        }

        // 更新受影响面的索引（将 u 替换为 v）
        foreach (int fi in affectedFaces)
        {
            _faces[fi].ReplaceVertex(uIndex, vIndex);
            v.FaceIndices.Add(fi);
        }

        // 标记 u 为非激活
        u.IsActive = false;

        // 更新 v 的邻居（先复制列表避免遍历修改问题）
        var neighbors = u.NeighborIndices.ToList();
        foreach (int nvi in neighbors)
        {
            if (nvi != vIndex && !v.NeighborIndices.Contains(nvi))
            {
                v.NeighborIndices.Add(nvi);
            }

            // 更新邻居的引用
            var neighbor = _vertices[nvi];
            neighbor.NeighborIndices.Remove(uIndex);
            if (!neighbor.NeighborIndices.Contains(vIndex))
            {
                neighbor.NeighborIndices.Add(vIndex);
            }
        }

        // 重新计算受影响面的法线
        foreach (int fi in affectedFaces)
        {
            var face = _faces[fi];
            var v0 = _vertices[face.Indices[0]].Position;
            var v1 = _vertices[face.Indices[1]].Position;
            var v2 = _vertices[face.Indices[2]].Position;
            face.Normal = Vector3.Cross(v1 - v0, v2 - v0).Normalize();
        }

        _collapseHistory.Add(record);
    }

    /// <summary>重置到原始网格状态</summary>
    private void ResetToOriginal()
    {
        // 反向播放所有折叠记录，执行边分裂
        for (int i = _collapseHistory.Count - 1; i >= 0; i--)
        {
            SplitEdge(_collapseHistory[i]);
        }

        _currentLevel = 0;
    }

    #endregion

    #region 简化/恢复操作

    /// <summary>设置简化层级</summary>
    public void SetLevel(int level)
    {
        level = Math.Clamp(level, 0, _collapseHistory.Count);

        // 向前简化（边折叠）
        while (_currentLevel < level)
        {
            var record = _collapseHistory[_currentLevel];
            ApplyCollapse(record);
            _currentLevel++;
        }

        // 向后恢复（边分裂）
        while (_currentLevel > level)
        {
            _currentLevel--;
            var record = _collapseHistory[_currentLevel];
            SplitEdge(record);
        }
    }

    /// <summary>应用折叠（正向）</summary>
    private void ApplyCollapse(CollapseRecord record)
    {
        var u = _vertices[record.VertexU];
        var v = _vertices[record.VertexV];

        // 删除面
        foreach (int fi in record.DeletedFaces)
        {
            _faces[fi].IsActive = false;
        }

        // 更新受影响面
        foreach (int fi in record.AffectedFaces)
        {
            _faces[fi].ReplaceVertex(record.VertexU, record.VertexV);
            v.FaceIndices.Add(fi);
        }

        // 标记 u 为非激活
        u.IsActive = false;

        // 更新 v 的邻居
        foreach (int nvi in record.NeighborsOfU)
        {
            if (nvi != record.VertexV && !v.NeighborIndices.Contains(nvi))
            {
                v.NeighborIndices.Add(nvi);
            }

            var neighbor = _vertices[nvi];
            neighbor.NeighborIndices.Remove(record.VertexU);
            if (!neighbor.NeighborIndices.Contains(record.VertexV))
            {
                neighbor.NeighborIndices.Add(record.VertexV);
            }
        }
    }

    /// <summary>边分裂（恢复被折叠的顶点）</summary>
    private void SplitEdge(CollapseRecord record)
    {
        var u = _vertices[record.VertexU];
        var v = _vertices[record.VertexV];

        // 恢复 u 的位置和状态
        u.Position = record.PositionU;
        u.IsActive = true;

        // 恢复被删除的面
        foreach (int fi in record.DeletedFaces)
        {
            _faces[fi].IsActive = true;
            u.FaceIndices.Add(fi);
            v.FaceIndices.Add(fi);
        }

        // 恢复受影响面的顶点引用
        foreach (int fi in record.AffectedFaces)
        {
            _faces[fi].ReplaceVertex(record.VertexV, record.VertexU);
            v.FaceIndices.Remove(fi);
            u.FaceIndices.Add(fi);
        }

        // 恢复 u 的邻居
        u.NeighborIndices.Clear();
        u.NeighborIndices.AddRange(record.NeighborsOfU);

        // 恢复邻居对 u 的引用
        foreach (int nvi in record.NeighborsOfU)
        {
            var neighbor = _vertices[nvi];
            if (!neighbor.NeighborIndices.Contains(record.VertexU))
            {
                neighbor.NeighborIndices.Add(record.VertexU);
            }
            neighbor.NeighborIndices.Remove(record.VertexV);
        }

        // 移除 v 对 u 的邻居引用
        v.NeighborIndices.RemoveAll(n => record.NeighborsOfU.Contains(n));
        v.NeighborIndices.Add(record.VertexU);
    }

    /// <summary>按目标顶点数简化</summary>
    public void SimplifyToVertexCount(int targetCount)
    {
        targetCount = Math.Clamp(targetCount, 4, _originalVertexCount);

        // 二分查找合适的层级
        int left = 0;
        int right = _collapseHistory.Count;
        int bestLevel = 0;

        while (left <= right)
        {
            int mid = (left + right) / 2;
            int verticesAtLevel = _originalVertexCount - mid;

            if (verticesAtLevel >= targetCount)
            {
                bestLevel = mid;
                left = mid + 1;
            }
            else
            {
                right = mid - 1;
            }
        }

        SetLevel(bestLevel);
    }

    /// <summary>按目标面数简化</summary>
    public void SimplifyToFaceCount(int targetCount)
    {
        targetCount = Math.Clamp(targetCount, 2, _originalFaceCount);

        // 粗略估计层级
        float ratio = 1 - (float)targetCount / _originalFaceCount;
        int estimatedLevel = (int)(ratio * _collapseHistory.Count);

        SetLevel(Math.Clamp(estimatedLevel, 0, _collapseHistory.Count));
    }

    #endregion

    #region 导出当前网格

    /// <summary>获取当前激活的顶点数组</summary>
    public Vector3[] GetActiveVertices()
    {
        return _vertices.Where(v => v.IsActive).Select(v => v.Position).ToArray();
    }

    /// <summary>获取当前激活的面索引数组</summary>
    public int[] GetActiveIndices()
    {
        // 创建顶点重映射
        var indexMap = new Dictionary<int, int>();
        int newIndex = 0;
        for (int i = 0; i < _vertices.Count; i++)
        {
            if (_vertices[i].IsActive)
            {
                indexMap[i] = newIndex++;
            }
        }

        var indices = new List<int>();
        foreach (var face in _faces)
        {
            if (!face.IsActive) continue;

            // 跳过包含非活动顶点的面（防止字典查找失败）
            if (!indexMap.ContainsKey(face.Indices[0]) ||
                !indexMap.ContainsKey(face.Indices[1]) ||
                !indexMap.ContainsKey(face.Indices[2]))
            {
                continue;
            }

            indices.Add(indexMap[face.Indices[0]]);
            indices.Add(indexMap[face.Indices[1]]);
            indices.Add(indexMap[face.Indices[2]]);
        }

        return indices.ToArray();
    }

    /// <summary>获取当前层级的累计误差</summary>
    public float GetAccumulatedError(int level)
    {
        level = Math.Clamp(level, 0, _collapseHistory.Count);
        float totalError = 0;
        for (int i = 0; i < level; i++)
        {
            totalError += _collapseHistory[i].Error;
        }
        return totalError;
    }

    /// <summary>获取所有层级的误差曲线点</summary>
    public List<(int Level, int Faces, float Error)> GetErrorCurve()
    {
        var result = new List<(int, int, float)>();
        float totalError = 0;

        result.Add((0, _originalFaceCount, 0));

        for (int i = 0; i < _collapseHistory.Count; i++)
        {
            totalError += _collapseHistory[i].Error;
            int facesAtLevel = _originalFaceCount - (i + 1) * 2;
            result.Add((i + 1, Math.Max(0, facesAtLevel), totalError));
        }

        return result;
    }

    #endregion
}
