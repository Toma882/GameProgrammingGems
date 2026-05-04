/*
 * 并查集 Union-Find / Disjoint Set Union (DSU)
 * 时间复杂度: 几乎 O(1) - 路径压缩 + 按秩合并
 *
 * 经营游戏核心用途:
 *   - 农田连块产量加成: 3x3 → +10%, 5x5 → +30%
 *   - 道路连通性检测: 仓库到工厂是否通路
 *   - 区域解锁条件: 连通N块特定区域
 *   - 洪水/火灾蔓延模拟
 */

using System;
using System.Collections.Generic;
using System.Drawing;

namespace GPGems.Core.DataStructures
{
    /// <summary>
    /// 网格并查集 - 专门针对经营游戏格子地图优化
    /// </summary>
    public class GridUnionFind
    {
        private readonly int _width;
        private readonly int _height;
        private readonly int[] _parent;
        private readonly int[] _rank;     // 树高，用于按秩合并
        private readonly int[] _size;     // 每个连通块的大小
        private readonly bool[] _isValid; // 该格子是否被占用/有效

        public int Width => _width;
        public int Height => _height;

        public GridUnionFind(int width, int height)
        {
            _width = width;
            _height = height;
            int total = width * height;

            _parent = new int[total];
            _rank = new int[total];
            _size = new int[total];
            _isValid = new bool[total];

            for (int i = 0; i < total; i++)
            {
                _parent[i] = i;
                _rank[i] = 1;
                _size[i] = 1;
                _isValid[i] = false;
            }
        }

        #region 基础操作

        /// <summary>
        /// 查找根节点（带路径压缩）
        /// </summary>
        public int Find(int x, int y)
        {
            int idx = ToIndex(x, y);
            return Find(idx);
        }

        private int Find(int idx)
        {
            if (_parent[idx] != idx)
                _parent[idx] = Find(_parent[idx]); // 路径压缩
            return _parent[idx];
        }

        /// <summary>
        /// 合并两个相邻格子
        /// </summary>
        public void Union(int x1, int y1, int x2, int y2)
        {
            int idx1 = ToIndex(x1, y1);
            int idx2 = ToIndex(x2, y2);

            if (!_isValid[idx1] || !_isValid[idx2])
                return;

            int root1 = Find(idx1);
            int root2 = Find(idx2);

            if (root1 == root2)
                return;

            // 按秩合并: 小树合并到大树上
            if (_rank[root1] < _rank[root2])
            {
                _parent[root1] = root2;
                _size[root2] += _size[root1];
            }
            else
            {
                _parent[root2] = root1;
                _size[root1] += _size[root2];
                if (_rank[root1] == _rank[root2])
                    _rank[root1]++;
            }
        }

        /// <summary>
        /// 判断两个格子是否连通
        /// </summary>
        public bool IsConnected(int x1, int y1, int x2, int y2)
        {
            if (!IsValid(x1, y1) || !IsValid(x2, y2))
                return false;
            return Find(x1, y1) == Find(x2, y2);
        }

        #endregion

        #region 格子管理

        /// <summary>
        /// 标记格子为有效（放置了农田/建筑）
        /// </summary>
        public void Activate(int x, int y)
        {
            int idx = ToIndex(x, y);
            if (!_isValid[idx])
            {
                _isValid[idx] = true;

                // 自动与4邻接的有效格子合并
                TryUnionWithNeighbor(x, y, x - 1, y);
                TryUnionWithNeighbor(x, y, x + 1, y);
                TryUnionWithNeighbor(x, y, x, y - 1);
                TryUnionWithNeighbor(x, y, x, y + 1);
            }
        }

        /// <summary>
        /// 移除格子（取消激活）
        /// </summary>
        public void Deactivate(int x, int y)
        {
            int idx = ToIndex(x, y);
            if (_isValid[idx])
            {
                _isValid[idx] = false;
                // 注意: 移除操作需要重建连通块，或用懒标记
                // 经营游戏中移除操作较少，可以接受全量重建
            }
        }

        public bool IsValid(int x, int y)
        {
            if (x < 0 || x >= _width || y < 0 || y >= _height)
                return false;
            return _isValid[ToIndex(x, y)];
        }

        private void TryUnionWithNeighbor(int x, int y, int nx, int ny)
        {
            if (nx >= 0 && nx < _width && ny >= 0 && ny < _height)
            {
                if (_isValid[ToIndex(nx, ny)])
                    Union(x, y, nx, ny);
            }
        }

        #endregion

        #region 连通块统计

        /// <summary>
        /// 获取格子所在连通块的大小
        /// </summary>
        public int GetSize(int x, int y)
        {
            if (!IsValid(x, y))
                return 0;
            return _size[Find(x, y)];
        }

        /// <summary>
        /// 获取所有连通块的大小统计
        /// </summary>
        public Dictionary<int, int> GetAllClusterSizes()
        {
            var result = new Dictionary<int, int>();
            var visited = new HashSet<int>();

            for (int y = 0; y < _height; y++)
            {
                for (int x = 0; x < _width; x++)
                {
                    if (IsValid(x, y))
                    {
                        int root = Find(x, y);
                        if (!visited.Contains(root))
                        {
                            visited.Add(root);
                            result[root] = _size[root];
                        }
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// 获取最大连通块的位置和大小
        /// </summary>
        public (int x, int y, int size) GetLargestCluster()
        {
            int maxSize = 0;
            int maxIdx = -1;

            for (int y = 0; y < _height; y++)
            {
                for (int x = 0; x < _width; x++)
                {
                    if (IsValid(x, y))
                    {
                        int root = Find(x, y);
                        if (_size[root] > maxSize)
                        {
                            maxSize = _size[root];
                            maxIdx = root;
                        }
                    }
                }
            }

            if (maxIdx == -1)
                return (-1, -1, 0);

            return (maxIdx % _width, maxIdx / _width, maxSize);
        }

        #endregion

        #region 辅助

        private int ToIndex(int x, int y) => y * _width + x;

        public (int x, int y) FromIndex(int idx) => (idx % _width, idx / _width);

        #endregion
    }

}
