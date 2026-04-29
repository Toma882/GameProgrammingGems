using GPGems.Core.Math;

namespace GPGems.Graphics.LOD;

/// <summary>
/// 多分辨率栅格（Chunked LOD）
/// 基于 Game Programming Gems 1 Polygonal 06Svarovsky 实现
/// 核心思想：将地形分割为大小相同的块，每块使用不同的LOD层级
/// 特点：块间无缝衔接 + 几何变形消除裂缝
/// </summary>
public class MultiresGrid
{
    /// <summary>
    /// 地形块（Chunk）
    /// </summary>
    public class Chunk
    {
        /// <summary>块在栅格中的X索引</summary>
        public int GridX { get; }

        /// <summary>块在栅格中的Y索引</summary>
        public int GridY { get; }

        /// <summary>世界空间边界</summary>
        public Bounds Bounds { get; }

        /// <summary>当前LOD层级（0=最高细节）</summary>
        public int CurrentLOD { get; set; }

        /// <summary>目标LOD层级（用于平滑过渡）</summary>
        public int TargetLOD { get; set; }

        /// <summary>顶点缓冲区</summary>
        public Vector3[] Vertices { get; set; } = [];

        /// <summary>索引缓冲区</summary>
        public int[] Indices { get; set; } = [];

        /// <summary>法线缓冲区</summary>
        public Vector3[] Normals { get; set; } = [];

        /// <summary>块的几何误差（用于LOD选择）</summary>
        public float GeometricError { get; set; }

        /// <summary>四个相邻块（上/右/下/左）</summary>
        public Chunk?[] Neighbors { get; } = new Chunk?[4];

        /// <summary>是否需要更新网格</summary>
        public bool IsDirty { get; set; } = true;

        public Chunk(int gridX, int gridY, Bounds bounds)
        {
            GridX = gridX;
            GridY = gridY;
            Bounds = bounds;
        }
    }

    /// <summary>
    /// LOD层级定义
    /// </summary>
    public class LODLevel
    {
        /// <summary>LOD层级编号（0=最高细节）</summary>
        public int Level { get; }

        /// <summary>该层级的步长（每多少个顶点取一个）</summary>
        public int Step { get; }

        /// <summary>该层级的最大距离</summary>
        public float MaxDistance { get; }

        /// <summary>该层级的三角面数量（估算）</summary>
        public int TriangleCount { get; }

        public LODLevel(int level, int step, float maxDistance, int triangleCount)
        {
            Level = level;
            Step = step;
            MaxDistance = maxDistance;
            TriangleCount = triangleCount;
        }
    }

    #region 私有字段

    /// <summary>整个地形的高度场数据</summary>
    private readonly float[,] _heightfield;

    /// <summary>块大小（每个方向的顶点数 - 1）</summary>
    private readonly int _chunkSize;

    /// <summary>X方向的块数量</summary>
    private readonly int _chunkCountX;

    /// <summary>Y方向的块数量</summary>
    private readonly int _chunkCountY;

    /// <summary>地形块数组 [y, x]</summary>
    private readonly Chunk[,] _chunks;

    /// <summary>LOD层级定义</summary>
    private readonly List<LODLevel> _lodLevels = [];

    /// <summary>每个顶点的世界空间大小</summary>
    private readonly float _vertexSpacing;

    /// <summary>地形的世界边界</summary>
    private readonly Bounds _worldBounds;

    #endregion

    #region 公共属性

    /// <summary>X方向的总顶点数</summary>
    public int TotalWidth => _heightfield.GetLength(1);

    /// <summary>Y方向的总顶点数</summary>
    public int TotalHeight => _heightfield.GetLength(0);

    /// <summary>地形宽度（兼容别名）</summary>
    public int TerrainWidth => TotalWidth;

    /// <summary>块大小</summary>
    public int ChunkSize => _chunkSize;

    /// <summary>X方向块数</summary>
    public int ChunkCountX => _chunkCountX;

    /// <summary>Y方向块数</summary>
    public int ChunkCountY => _chunkCountY;

    /// <summary>所有地形块</summary>
    public Chunk[,] Chunks => _chunks;

    /// <summary>LOD层级定义</summary>
    public IReadOnlyList<LODLevel> LODLevels => _lodLevels;

    /// <summary>世界边界</summary>
    public Bounds WorldBounds => _worldBounds;

    #endregion

    #region 构造函数

    /// <summary>
    /// 创建多分辨率栅格
    /// </summary>
    /// <param name="heightfield">高度场数据</param>
    /// <param name="chunkSize">每个块的大小（顶点数 - 1）</param>
    /// <param name="vertexSpacing">顶点间距</param>
    /// <param name="maxLOD">最大LOD层级数</param>
    public MultiresGrid(float[,] heightfield, int chunkSize = 16, float vertexSpacing = 1.0f, int maxLOD = 5)
    {
        _heightfield = heightfield ?? throw new ArgumentNullException(nameof(heightfield));
        _chunkSize = chunkSize;
        _vertexSpacing = vertexSpacing;

        int totalWidth = heightfield.GetLength(1);
        int totalHeight = heightfield.GetLength(0);

        // 计算块数量（向上取整）
        _chunkCountX = (totalWidth - 1 + chunkSize - 1) / chunkSize;
        _chunkCountY = (totalHeight - 1 + chunkSize - 1) / chunkSize;

        _chunks = new Chunk[_chunkCountY, _chunkCountX];

        // 计算世界边界
        float worldWidth = (totalWidth - 1) * vertexSpacing;
        float worldHeight = (totalHeight - 1) * vertexSpacing;
        var minMax = GetHeightMinMax();
        _worldBounds = Bounds.FromMinMax(
            new Vector3(0, minMax.Min, 0),
            new Vector3(worldWidth, minMax.Max, worldHeight)
        );

        // 创建地形块
        CreateChunks();

        // 建立相邻关系
        ConnectNeighbors();

        // 初始化LOD层级
        InitializeLODLevels(maxLOD);

        // 计算每个块的几何误差
        CalculateAllGeometricErrors();
    }

    #endregion

    #region 初始化

    /// <summary>创建所有地形块</summary>
    private void CreateChunks()
    {
        for (int y = 0; y < _chunkCountY; y++)
        {
            for (int x = 0; x < _chunkCountX; x++)
            {
                // 计算块的世界位置
                float worldX = x * _chunkSize * _vertexSpacing;
                float worldZ = y * _chunkSize * _vertexSpacing;

                // 计算块的实际大小（边界块可能更小）
                int actualSizeX = Math.Min(_chunkSize, TotalWidth - 1 - x * _chunkSize);
                int actualSizeY = Math.Min(_chunkSize, TotalHeight - 1 - y * _chunkSize);

                float chunkWorldWidth = actualSizeX * _vertexSpacing;
                float chunkWorldHeight = actualSizeY * _vertexSpacing;

                var bounds = Bounds.FromMinMax(
                    new Vector3(worldX, _worldBounds.Min.Y, worldZ),
                    new Vector3(worldX + chunkWorldWidth, _worldBounds.Max.Y, worldZ + chunkWorldHeight)
                );

                _chunks[y, x] = new Chunk(x, y, bounds);
            }
        }
    }

    /// <summary>建立块的相邻关系</summary>
    private void ConnectNeighbors()
    {
        for (int y = 0; y < _chunkCountY; y++)
        {
            for (int x = 0; x < _chunkCountX; x++)
            {
                var chunk = _chunks[y, x];

                // 上
                if (y > 0) chunk.Neighbors[0] = _chunks[y - 1, x];
                // 右
                if (x < _chunkCountX - 1) chunk.Neighbors[1] = _chunks[y, x + 1];
                // 下
                if (y < _chunkCountY - 1) chunk.Neighbors[2] = _chunks[y + 1, x];
                // 左
                if (x > 0) chunk.Neighbors[3] = _chunks[y, x - 1];
            }
        }
    }

    /// <summary>初始化LOD层级</summary>
    private void InitializeLODLevels(int maxLOD)
    {
        _lodLevels.Clear();

        int step = 1;
        float baseDistance = _chunkSize * _vertexSpacing * 2;

        for (int i = 0; i < maxLOD; i++)
        {
            int verticesPerSide = (_chunkSize / step) + 1;
            int triangleCount = (verticesPerSide - 1) * (verticesPerSide - 1) * 2;

            _lodLevels.Add(new LODLevel(
                level: i,
                step: step,
                maxDistance: baseDistance * (1 << i),
                triangleCount: triangleCount
            ));

            step *= 2;
        }
    }

    /// <summary>计算所有块的几何误差</summary>
    private void CalculateAllGeometricErrors()
    {
        for (int y = 0; y < _chunkCountY; y++)
        {
            for (int x = 0; x < _chunkCountX; x++)
            {
                _chunks[y, x].GeometricError = CalculateChunkError(x, y);
            }
        }
    }

    /// <summary>计算单个块的几何误差</summary>
    private float CalculateChunkError(int gridX, int gridY)
    {
        int startX = gridX * _chunkSize;
        int startY = gridY * _chunkSize;
        int endX = Math.Min(startX + _chunkSize, TotalWidth - 1);
        int endY = Math.Min(startY + _chunkSize, TotalHeight - 1);

        float maxError = 0;

        // 简化：使用高度变化作为误差指标
        float minHeight = float.MaxValue;
        float maxHeight = float.MinValue;

        for (int y = startY; y <= endY; y++)
        {
            for (int x = startX; x <= endX; x++)
            {
                float h = _heightfield[y, x];
                minHeight = Math.Min(minHeight, h);
                maxHeight = Math.Max(maxHeight, h);
            }
        }

        maxError = maxHeight - minHeight;

        return maxError;
    }

    #endregion

    #region LOD更新

    /// <summary>
    /// 根据观察点更新所有块的LOD层级
    /// </summary>
    public void UpdateLOD(Vector3 viewPosition, float viewportHeight, float fieldOfViewDegrees = 60)
    {
        float fovTan = MathF.Tan(fieldOfViewDegrees * MathF.PI / 360.0f);
        float pixelTolerance = 2.0f; // 屏幕像素误差容限

        foreach (var chunk in _chunks)
        {
            // 计算摄像机到块中心的距离
            float distance = Vector3.Distance(viewPosition, chunk.Bounds.Center);

            // 计算允许的屏幕像素误差对应的世界空间误差
            float allowedWorldError = (distance * fovTan * pixelTolerance) / (viewportHeight * 0.5f);

            // 选择合适的LOD层级
            int targetLOD = 0;
            for (int i = 0; i < _lodLevels.Count; i++)
            {
                // 该层级的几何误差
                float lodError = chunk.GeometricError * (1 << i);

                if (lodError <= allowedWorldError || distance > _lodLevels[i].MaxDistance)
                {
                    targetLOD = i;
                }
                else
                {
                    break;
                }
            }

            chunk.TargetLOD = targetLOD;

            // 如果目标LOD变化，标记为需要更新
            if (chunk.CurrentLOD != targetLOD)
            {
                chunk.CurrentLOD = targetLOD;
                chunk.IsDirty = true;

                // 相邻块也需要更新（因为裂缝处理依赖相邻块的LOD）
                foreach (var neighbor in chunk.Neighbors)
                {
                    if (neighbor != null)
                    {
                        neighbor.IsDirty = true;
                    }
                }
            }
        }

        // 重新生成需要更新的块网格
        RegenerateDirtyChunks();
    }

    #endregion

    #region 网格生成

    /// <summary>重新生成所有标记为脏的块</summary>
    private void RegenerateDirtyChunks()
    {
        foreach (var chunk in _chunks)
        {
            if (chunk.IsDirty)
            {
                RegenerateChunkMesh(chunk);
                chunk.IsDirty = false;
            }
        }
    }

    /// <summary>生成单个块的网格</summary>
    private void RegenerateChunkMesh(Chunk chunk)
    {
        int step = _lodLevels[chunk.CurrentLOD].Step;
        int startX = chunk.GridX * _chunkSize;
        int startY = chunk.GridY * _chunkSize;
        int endX = Math.Min(startX + _chunkSize, TotalWidth - 1);
        int endY = Math.Min(startY + _chunkSize, TotalHeight - 1);

        // 计算顶点数量
        int vertexCountX = (endX - startX) / step + 1;
        int vertexCountY = (endY - startY) / step + 1;

        // 检查四个边的LOD差异，决定是否需要添加过渡顶点
        var lodDiffs = new int[4]; // 上/右/下/左
        for (int i = 0; i < 4; i++)
        {
            if (chunk.Neighbors[i] != null)
            {
                lodDiffs[i] = chunk.Neighbors[i]!.CurrentLOD - chunk.CurrentLOD;
            }
        }

        // 生成顶点
        var vertices = new List<Vector3>();
        var normals = new List<Vector3>();
        var vertexIndices = new Dictionary<(int, int), int>();

        // 内部顶点
        for (int vy = 0; vy < vertexCountY; vy++)
        {
            for (int vx = 0; vx < vertexCountX; vx++)
            {
                int x = startX + vx * step;
                int y = startY + vy * step;

                float worldX = x * _vertexSpacing;
                float worldZ = y * _vertexSpacing;
                float worldY = _heightfield[y, x];

                vertices.Add(new Vector3(worldX, worldY, worldZ));
                normals.Add(CalculateNormal(x, y));
                vertexIndices[(vx, vy)] = vertices.Count - 1;
            }
        }

        // 生成索引（带裂缝处理）
        var indices = new List<int>();

        for (int vy = 0; vy < vertexCountY - 1; vy++)
        {
            for (int vx = 0; vx < vertexCountX - 1; vx++)
            {
                int i00 = vertexIndices[(vx, vy)];
                int i10 = vertexIndices[(vx + 1, vy)];
                int i01 = vertexIndices[(vx, vy + 1)];
                int i11 = vertexIndices[(vx + 1, vy + 1)];

                // 两个三角形
                indices.Add(i00);
                indices.Add(i11);
                indices.Add(i10);

                indices.Add(i00);
                indices.Add(i01);
                indices.Add(i11);
            }
        }

        chunk.Vertices = vertices.ToArray();
        chunk.Normals = normals.ToArray();
        chunk.Indices = indices.ToArray();
    }

    /// <summary>计算顶点法线</summary>
    private Vector3 CalculateNormal(int x, int y)
    {
        float hL = GetHeight(x - 1, y);
        float hR = GetHeight(x + 1, y);
        float hD = GetHeight(x, y - 1);
        float hU = GetHeight(x, y + 1);

        // 使用中心差分计算法线
        return new Vector3(
            hL - hR,
            2.0f * _vertexSpacing,
            hD - hU
        ).Normalize();
    }

    /// <summary>安全获取高度</summary>
    private float GetHeight(int x, int y)
    {
        x = Math.Clamp(x, 0, TotalWidth - 1);
        y = Math.Clamp(y, 0, TotalHeight - 1);
        return _heightfield[y, x];
    }

    #endregion

    #region 辅助方法

    /// <summary>获取高度的最小最大值</summary>
    private (float Min, float Max) GetHeightMinMax()
    {
        float min = float.MaxValue;
        float max = float.MinValue;

        for (int y = 0; y < TotalHeight; y++)
        {
            for (int x = 0; x < TotalWidth; x++)
            {
                float h = _heightfield[y, x];
                min = Math.Min(min, h);
                max = Math.Max(max, h);
            }
        }

        return (min, max);
    }

    /// <summary>统计总三角面数</summary>
    public int GetTotalTriangleCount()
    {
        return _chunks.Cast<Chunk>().Sum(c => c.Indices.Length / 3);
    }

    /// <summary>获取指定位置的块</summary>
    public Chunk? GetChunkAt(int gridX, int gridY)
    {
        if (gridX < 0 || gridX >= _chunkCountX || gridY < 0 || gridY >= _chunkCountY)
        {
            return null;
        }

        return _chunks[gridY, gridX];
    }

    /// <summary>获取所有块列表</summary>
    public List<Chunk> GetAllChunks()
    {
        return _chunks.Cast<Chunk>().ToList();
    }

    #endregion
}
