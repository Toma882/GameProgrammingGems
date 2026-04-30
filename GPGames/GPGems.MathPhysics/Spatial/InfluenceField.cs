/*
 * 影响场计算 Influence Field
 * 时间复杂度: 构建 O(n * m)，查询 O(1)
 *
 * 经营游戏核心用途:
 *   - 美观度叠加: 雕像/装饰影响周围居民满意度
 *   - 噪音污染: 工厂/市场噪音降低周围地价
 *   - 服务范围: 超市/学校/医院的覆盖区域
 *   - 光环效果: 某些建筑提供加成
 *   - 污染扩散: 垃圾/污染源传播
 *
 * 设计: 多源叠加，按距离衰减，支持正负值
 */

using System;
using System.Collections.Generic;

namespace GPGems.MathPhysics.Spatial
{
    /// <summary>
    /// 影响场计算器 - 多源影响叠加计算
    /// 支持任意数量的正负影响源，按距离衰减
    /// </summary>
    public class InfluenceField
    {
        private readonly int _width;
        private readonly int _height;
        private readonly float[,] _field;      // 当前叠加结果
        private readonly List<InfluenceSource> _sources = new();

        public int Width => _width;
        public int Height => _height;

        public InfluenceField(int width, int height)
        {
            _width = width;
            _height = height;
            _field = new float[width, height];
        }

        #region 影响源管理

        /// <summary>
        /// 添加影响源
        /// </summary>
        /// <param name="x">中心X</param>
        /// <param name="y">中心Y</param>
        /// <param name="radius">影响半径（格子）</param>
        /// <param name="strength">强度（正=增益，负=衰减）</param>
        /// <param name="falloff">衰减类型</param>
        public void AddSource(int x, int y, float radius, float strength, FalloffType falloff = FalloffType.Linear)
        {
            _sources.Add(new InfluenceSource(x, y, radius, strength, falloff));
        }

        /// <summary>
        /// 移除指定位置的影响源
        /// </summary>
        public void RemoveSourceAt(int x, int y)
        {
            _sources.RemoveAll(s => s.X == x && s.Y == y);
        }

        /// <summary>
        /// 清空所有影响源
        /// </summary>
        public void ClearSources()
        {
            _sources.Clear();
        }

        #endregion

        #region 场计算

        /// <summary>
        /// 全量重新计算（影响场（建筑变动后调用）
        /// </summary>
        public void RecalculateAll()
        {
            Array.Clear(_field, 0, _field.Length);

            foreach (var source in _sources)
            {
                int minX = Math.Max(0, (int)(source.X - source.Radius));
                int maxX = Math.Min(_width - 1, (int)(source.X + source.Radius));
                int minY = Math.Max(0, (int)(source.Y - source.Radius));
                int maxY = Math.Min(_height - 1, (int)(source.Y + source.Radius));

                float radiusSq = source.Radius * source.Radius;

                for (int y = minY; y <= maxY; y++)
                {
                    for (int x = minX; x <= maxX; x++)
                    {
                        float dx = x - source.X;
                        float dy = y - source.Y;
                        float distSq = dx * dx + dy * dy;

                        if (distSq <= radiusSq)
                        {
                            float dist = MathF.Sqrt(distSq);
                            float factor = CalculateFalloff(dist, source.Radius, source.Falloff);
                            _field[x, y] += source.Strength * factor;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 增量更新（只更新某个影响源（性能优化）
        /// </summary>
        public void UpdateSource(InfluenceSource source)
        {
            // 先移除旧影响
            // 再添加新影响
            // 适用于移动建筑时的优化
        }

        #endregion

        #region 查询

        /// <summary>
        /// 查询指定位置的影响值
        /// </summary>
        public float GetValue(int x, int y)
        {
            if (x < 0 || x >= _width || y < 0 || y >= _height)
                return 0f;
            return _field[x, y];
        }

        /// <summary>
        /// 查询区域平均影响
        /// </summary>
        public float GetAverage(int minX, int minY, int maxX, int maxY)
        {
            float sum = 0f;
            int count = 0;

            for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                    sum += GetValue(x, y);
                    count++;

            return count > 0 ? sum / count : 0f;
        }

        /// <summary>
        /// 查询范围内满足阈值的格子数
        /// </summary>
        public int CountAboveThreshold(float threshold, int minX, int minY, int maxX, int maxY)
        {
            int count = 0;
            for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                    if (GetValue(x, y) >= threshold)
                        count++;
            return count;
        }

        /// <summary>
        /// 获取全场统计
        /// </summary>
        public (float min, float max, float avg) GetStatistics()
        {
            float min = float.MaxValue;
            float max = float.MinValue;
            float sum = 0;

            for (int y = 0; y < _height; y++)
            {
                for (int x = 0; x < _width; x++)
                {
                    float v = _field[x, y];
                    min = Math.Min(min, v);
                    max = Math.Max(max, v);
                    sum += v;
                }
            }

            return (min, max, sum / (_width * _height));
        }

        #endregion

        #region 衰减函数

        private static float CalculateFalloff(float distance, float radius, FalloffType type)
        {
            float t = distance / radius;

            return type switch
            {
                FalloffType.Constant => 1f,
                FalloffType.Linear => 1f - t,
                FalloffType.Quadratic => 1f - t * t,
                FalloffType.Smoothstep => t * t * (3f - 2f * t),
                FalloffType.Inverse => 1f / (1f + t),
                _ => 1f - t
            };
        }

        #endregion

        #region 经营游戏便捷API

        /// <summary>
        /// 添加美观度建筑（雕像/花园
        /// </summary>
        public void AddBeautyBuilding(int x, int y, float radius = 5f, float beauty = 0.2f)
        {
            AddSource(x, y, radius, beauty, FalloffType.Smoothstep);
        }

        /// <summary>
        /// 添加噪音污染源（工厂/市场
        /// </summary>
        public void AddNoiseSource(int x, int y, float radius = 8f, float noise = -0.3f)
        {
            AddSource(x, y, radius, noise, FalloffType.Quadratic);
        }

        /// <summary>
        /// 添加服务建筑（商店/医院
        /// </summary>
        public void AddServiceBuilding(int x, int y, float radius = 10f, float coverage = 1f)
        {
            AddSource(x, y, radius, coverage, FalloffType.Linear);
        }

        /// <summary>
        /// 添加光环建筑（增益
        /// </summary>
        public void AddAuraBuilding(int x, int y, float radius = 6f, float bonus = 0.15f)
        {
            AddSource(x, y, radius, bonus, FalloffType.Constant);
        }

        /// <summary>
        /// 获取地块最终满意度
        /// </summary>
        public float GetSatisfaction(int x, int y)
        {
            // 基础 0.5 + 影响值，范围 0-1
            return Math.Clamp(0.5f + _field[x, y], 0f, 1f);
        }

        #endregion
    }

    #region 辅助类型

    public enum FalloffType
    {
        Constant,   // 无衰减，范围内全量
        Linear,      // 线性衰减
        Quadratic,   // 平方衰减（中间满，边缘快
        Smoothstep,  // 平滑过渡
        Inverse      // 反比例衰减
    }

    public class InfluenceSource
    {
        public int X, Y;
        public float Radius;
        public float Strength;
        public FalloffType Falloff;

        public InfluenceSource(int x, int y, float radius, float strength, FalloffType falloff)
        {
            X = x; Y = y;
            Radius = radius;
            Strength = strength;
            Falloff = falloff;
        }
    }

    #endregion
}
