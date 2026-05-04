/*
 * Morton 编码 / Z 序曲线
 * 时间复杂度: O(1) 编码/解码
 * 空间局部性: 将 2D/3D 坐标映射为 1D，保留空间邻近性
 *
 * 经营游戏核心用途:
 *   - 八叉树/四叉树空间索引优化
 *   - 纹理/地形数据缓存友好的存储顺序
 *   - 碰撞检测 Broadphase 加速
 *   - 地图块加载优先级排序
 */

using System;
using System.Numerics;

namespace GPGems.MathPhysics.Spatial;

/// <summary>
/// Morton 编码 - Z 序曲线空间编码
/// 将多维坐标映射为一维，保留空间局部性
/// </summary>
public static class MortonCode
{
    #region 2D Morton 编码

    /// <summary>
    /// 对 16 位整数进行位穿插（隔位插入 0）
    /// Magic number 方法: x & 0x0000FFFF → 隔位插入 0
    /// </summary>
    private static uint Part1By1(uint x)
    {
        x &= 0x0000FFFF;                  // x = ---- ---- ---- ---- fedc ba98 7654 3210
        x = (x ^ (x << 8)) & 0x00FF00FF; // x = ---- ---- fedc ba98 ---- ---- 7654 3210
        x = (x ^ (x << 4)) & 0x0F0F0F0F; // x = ---- fedc ---- ba98 ---- 7654 ---- 3210
        x = (x ^ (x << 2)) & 0x33333333; // x = --fe --dc --ba --98 --76 --54 --32 --10
        x = (x ^ (x << 1)) & 0x55555555; // x = -f-e -d-c -b-a -9-8 -7-6 -5-4 -3-2 -1-0
        return x;
    }

    /// <summary>
    /// 隔位提取（Part1By1 的逆操作）
    /// </summary>
    private static uint Compact1By1(uint x)
    {
        x &= 0x55555555;                  // x = -f-e -d-c -b-a -9-8 -7-6 -5-4 -3-2 -1-0
        x = (x ^ (x >> 1)) & 0x33333333; // x = --fe --dc --ba --98 --76 --54 --32 --10
        x = (x ^ (x >> 2)) & 0x0F0F0F0F; // x = ---- fedc ---- ba98 ---- 7654 ---- 3210
        x = (x ^ (x >> 4)) & 0x00FF00FF; // x = ---- ---- fedc ba98 ---- ---- 7654 3210
        x = (x ^ (x >> 8)) & 0x0000FFFF; // x = ---- ---- ---- ---- fedc ba98 7654 3210
        return x;
    }

    /// <summary>
    /// 2D Morton 编码（将两个 16 位整数编码为 32 位）
    /// </summary>
    /// <param name="x">X 坐标 (0-65535)</param>
    /// <param name="y">Y 坐标 (0-65535)</param>
    /// <returns>32 位 Morton 编码</returns>
    public static uint Encode2D(ushort x, ushort y)
    {
        return (Part1By1(y) << 1) | Part1By1(x);
    }

    /// <summary>
    /// 2D Morton 编码（整数版本，自动归一化）
    /// </summary>
    /// <param name="x">X 坐标</param>
    /// <param name="y">Y 坐标</param>
    /// <param name="maxValue">坐标最大值（用于归一化）</param>
    public static uint Encode2D(int x, int y, int maxValue = 65535)
    {
        float scale = 65535.0f / maxValue;
        ushort ux = (ushort)Math.Clamp((int)(x * scale), 0, 65535);
        ushort uy = (ushort)Math.Clamp((int)(y * scale), 0, 65535);
        return Encode2D(ux, uy);
    }

    /// <summary>
    /// 2D Morton 编码（浮点版本）
    /// </summary>
    /// <param name="x">X 坐标 (0.0-1.0)</param>
    /// <param name="y">Y 坐标 (0.0-1.0)</param>
    public static uint Encode2D(float x, float y)
    {
        ushort ux = (ushort)Math.Clamp((int)(x * 65535.0f), 0, 65535);
        ushort uy = (ushort)Math.Clamp((int)(y * 65535.0f), 0, 65535);
        return Encode2D(ux, uy);
    }

    /// <summary>
    /// 解码 2D Morton 编码为原始坐标
    /// </summary>
    public static (ushort x, ushort y) Decode2D(uint code)
    {
        ushort x = (ushort)Compact1By1(code);
        ushort y = (ushort)Compact1By1(code >> 1);
        return (x, y);
    }

    #endregion

    #region 3D Morton 编码

    /// <summary>
    /// 对 10 位整数进行两位穿插（每两位插入 00）
    /// </summary>
    private static uint Part1By2(uint x)
    {
        x &= 0x000003FF;                  // x = ---- ---- ---- ---- ---- --98 7654 3210
        x = (x ^ (x << 16)) & 0xFF0000FF; // x = ---- --98 ---- ---- ---- ---- 7654 3210
        x = (x ^ (x << 8)) & 0x0300F00F;  // x = ---- --98 ---- ---- 7654 ---- ---- 3210
        x = (x ^ (x << 4)) & 0x030C30C3;  // x = --9- --8- --7- --6- --5- --4- --3- --2-
        x = (x ^ (x << 2)) & 0x09249249;  // x = -9-- -8-- -7-- -6-- -5-- -4-- -3-- -2--
        return x;
    }

    /// <summary>
    /// 两位提取（Part1By2 的逆操作）
    /// </summary>
    private static uint Compact1By2(uint x)
    {
        x &= 0x09249249;                  // x = -9-- -8-- -7-- -6-- -5-- -4-- -3-- -2--
        x = (x ^ (x >> 2)) & 0x030C30C3;  // x = --9- --8- --7- --6- --5- --4- --3- --2-
        x = (x ^ (x >> 4)) & 0x0300F00F;  // x = ---- --98 ---- ---- 7654 ---- ---- 3210
        x = (x ^ (x >> 8)) & 0xFF0000FF;  // x = ---- --98 ---- ---- ---- ---- 7654 3210
        x = (x ^ (x >> 16)) & 0x000003FF; // x = ---- ---- ---- ---- ---- --98 7654 3210
        return x;
    }

    /// <summary>
    /// 3D Morton 编码（将三个 10 位整数编码为 32 位）
    /// </summary>
    /// <param name="x">X 坐标 (0-1023)</param>
    /// <param name="y">Y 坐标 (0-1023)</param>
    /// <param name="z">Z 坐标 (0-1023)</param>
    /// <returns>32 位 Morton 编码</returns>
    public static uint Encode3D(ushort x, ushort y, ushort z)
    {
        return (Part1By2(z) << 2) | (Part1By2(y) << 1) | Part1By2(x);
    }

    /// <summary>
    /// 3D Morton 编码（浮点版本）
    /// </summary>
    /// <param name="x">X 坐标 (0.0-1.0)</param>
    /// <param name="y">Y 坐标 (0.0-1.0)</param>
    /// <param name="z">Z 坐标 (0.0-1.0)</param>
    public static uint Encode3D(float x, float y, float z)
    {
        ushort ux = (ushort)Math.Clamp((int)(x * 1023.0f), 0, 1023);
        ushort uy = (ushort)Math.Clamp((int)(y * 1023.0f), 0, 1023);
        ushort uz = (ushort)Math.Clamp((int)(z * 1023.0f), 0, 1023);
        return Encode3D(ux, uy, uz);
    }

    /// <summary>
    /// 解码 3D Morton 编码为原始坐标
    /// </summary>
    public static (ushort x, ushort y, ushort z) Decode3D(uint code)
    {
        ushort x = (ushort)Compact1By2(code);
        ushort y = (ushort)Compact1By2(code >> 1);
        ushort z = (ushort)Compact1By2(code >> 2);
        return (x, y, z);
    }

    #endregion

    #region 64 位高精度编码

    /// <summary>
    /// 对 32 位整数进行位穿插（隔位插入 0）
    /// </summary>
    private static ulong Part1By1_64(uint x)
    {
        ulong i = x;
        i = (i ^ (i << 16)) & 0x0000FFFF0000FFFF;
        i = (i ^ (i << 8)) & 0x00FF00FF00FF00FF;
        i = (i ^ (i << 4)) & 0x0F0F0F0F0F0F0F0F;
        i = (i ^ (i << 2)) & 0x3333333333333333;
        i = (i ^ (i << 1)) & 0x5555555555555555;
        return i;
    }

    /// <summary>
    /// 隔位提取 64 位
    /// </summary>
    private static uint Compact1By1_64(ulong x)
    {
        x &= 0x5555555555555555;
        x = (x ^ (x >> 1)) & 0x3333333333333333;
        x = (x ^ (x >> 2)) & 0x0F0F0F0F0F0F0F0F;
        x = (x ^ (x >> 4)) & 0x00FF00FF00FF00FF;
        x = (x ^ (x >> 8)) & 0x0000FFFF0000FFFF;
        x = (x ^ (x >> 16)) & 0x00000000FFFFFFFF;
        return (uint)x;
    }

    /// <summary>
    /// 2D Morton 编码 64 位高精度（两个 32 位整数）
    /// </summary>
    public static ulong Encode2D64(uint x, uint y)
    {
        return (Part1By1_64(y) << 1) | Part1By1_64(x);
    }

    /// <summary>
    /// 解码 64 位 2D Morton 编码
    /// </summary>
    public static (uint x, uint y) Decode2D64(ulong code)
    {
        uint x = Compact1By1_64(code);
        uint y = Compact1By1_64(code >> 1);
        return (x, y);
    }

    #endregion

    #region 空间操作

    /// <summary>
    /// 计算两个 Morton 编码之间的空间距离（曼哈顿距离）
    /// </summary>
    public static int ManhattanDistance(uint codeA, uint codeB)
    {
        var (x1, y1) = Decode2D(codeA);
        var (x2, y2) = Decode2D(codeB);
        return Math.Abs(x1 - x2) + Math.Abs(y1 - y2);
    }

    /// <summary>
    /// 计算两个 Morton 编码之间的欧几里得距离平方
    /// </summary>
    public static float DistanceSquared(uint codeA, uint codeB)
    {
        var (x1, y1) = Decode2D(codeA);
        var (x2, y2) = Decode2D(codeB);
        float dx = x1 - x2;
        float dy = y1 - y2;
        return dx * dx + dy * dy;
    }

    /// <summary>
    /// 获取指定格子的 8 邻域 Morton 编码
    /// </summary>
    public static List<uint> Get8Neighbors(ushort x, ushort y)
    {
        var result = new List<uint>(8);
        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0) continue;

                int nx = x + dx;
                int ny = y + dy;
                if (nx >= 0 && nx <= 65535 && ny >= 0 && ny <= 65535)
                {
                    result.Add(Encode2D((ushort)nx, (ushort)ny));
                }
            }
        }
        return result;
    }

    #endregion

    #region 排序与分块

    /// <summary>
    /// 对 2D 点集进行 Morton 排序（提升空间局部性）
    /// </summary>
    public static void SortByMorton<T>(List<T> items, Func<T, (float x, float y)> getPosition)
    {
        // 计算编码并排序
        items.Sort((a, b) =>
        {
            var (ax, ay) = getPosition(a);
            var (bx, by) = getPosition(b);
            uint codeA = Encode2D(ax, ay);
            uint codeB = Encode2D(bx, by);
            return codeA.CompareTo(codeB);
        });
    }

    /// <summary>
    /// 对 3D 点集进行 Morton 排序
    /// </summary>
    public static void SortByMorton3D<T>(List<T> items, Func<T, (float x, float y, float z)> getPosition)
    {
        items.Sort((a, b) =>
        {
            var (ax, ay, az) = getPosition(a);
            var (bx, by, bz) = getPosition(b);
            uint codeA = Encode3D(ax, ay, az);
            uint codeB = Encode3D(bx, by, bz);
            return codeA.CompareTo(codeB);
        });
    }

    #endregion
}
