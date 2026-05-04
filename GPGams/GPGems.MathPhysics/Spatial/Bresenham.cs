/*
 * Bresenham 算法 - 直线/圆光栅化
 * 时间复杂度: O(n), n=像素数（仅整数运算，无浮点）
 *
 * 经营游戏核心用途:
 *   - 视线检测: 玩家/单位能否看到目标
 *   - 弹道轨迹: 子弹/投射物路径
 *   - 区域填充: 选择/高亮画线
 *   - 地图编辑: 画笔工具
 */

using System;
using System.Collections.Generic;

namespace GPGems.MathPhysics.Spatial;

/// <summary>
/// Bresenham 算法 - 整数运算的光栅化算法
/// 用于直线、圆、椭圆绘制，以及视线检测（LOS）
/// </summary>
public static class Bresenham
{
    #region 直线算法

    /// <summary>
    /// 获取直线上的所有格子（从起点到终点）
    /// 使用 Bresenham 直线算法（全整数运算）
    /// </summary>
    /// <returns>格子坐标列表</returns>
    public static List<(int x, int y)> Line(int x0, int y0, int x1, int y1)
    {
        var result = new List<(int, int)>();

        int dx = Math.Abs(x1 - x0);
        int dy = Math.Abs(y1 - y0);

        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int err = dx - dy;

        int x = x0;
        int y = y0;

        while (true)
        {
            result.Add((x, y));

            if (x == x1 && y == y1)
                break;

            int e2 = 2 * err;
            if (e2 > -dy)
            {
                err -= dy;
                x += sx;
            }
            if (e2 < dx)
            {
                err += dx;
                y += sy;
            }
        }

        return result;
    }

    /// <summary>
    /// 直线迭代器版本（无内存分配）
    /// </summary>
    public static IEnumerable<(int x, int y)> LineIter(int x0, int y0, int x1, int y1)
    {
        int dx = Math.Abs(x1 - x0);
        int dy = Math.Abs(y1 - y0);

        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int err = dx - dy;

        int x = x0;
        int y = y0;

        while (true)
        {
            yield return (x, y);

            if (x == x1 && y == y1)
                break;

            int e2 = 2 * err;
            if (e2 > -dy)
            {
                err -= dy;
                x += sx;
            }
            if (e2 < dx)
            {
                err += dx;
                y += sy;
            }
        }
    }

    /// <summary>
    /// 获取直线上的格子（带最大步数限制）
    /// </summary>
    public static List<(int x, int y)> LineLimited(int x0, int y0, int x1, int y1, int maxSteps)
    {
        var result = new List<(int, int)>();

        int dx = Math.Abs(x1 - x0);
        int dy = Math.Abs(y1 - y0);

        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int err = dx - dy;

        int x = x0;
        int y = y0;
        int steps = 0;

        while (steps < maxSteps)
        {
            result.Add((x, y));

            if (x == x1 && y == y1)
                break;

            int e2 = 2 * err;
            if (e2 > -dy)
            {
                err -= dy;
                x += sx;
            }
            if (e2 < dx)
            {
                err += dx;
                y += sy;
            }
            steps++;
        }

        return result;
    }

    #endregion

    #region 视线检测 (Line of Sight)

    /// <summary>
    /// 检测两点间是否有视线
    /// </summary>
    /// <param name="x0">起点 X</param>
    /// <param name="y0">起点 Y</param>
    /// <param name="x1">终点 X</param>
    /// <param name="y1">终点 Y</param>
    /// <param name="isBlocked">判断格子是否阻挡的函数</param>
    /// <returns>是否能看到（起点和终点都不阻挡）</returns>
    public static bool HasLineOfSight(int x0, int y0, int x1, int y1, Func<int, int, bool> isBlocked)
    {
        int dx = Math.Abs(x1 - x0);
        int dy = Math.Abs(y1 - y0);

        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int err = dx - dy;

        int x = x0;
        int y = y0;

        while (true)
        {
            // 到达终点
            if (x == x1 && y == y1)
                return true;

            // 检查是否被阻挡（起点和终点不算）
            if ((x != x0 || y != y0) && isBlocked(x, y))
                return false;

            int e2 = 2 * err;
            if (e2 > -dy)
            {
                err -= dy;
                x += sx;
            }
            if (e2 < dx)
            {
                err += dx;
                y += sy;
            }
        }
    }

    /// <summary>
    /// 获取视线被阻挡的位置
    /// </summary>
    /// <returns>被阻挡的坐标，如果能看到则返回 null</returns>
    public static (int x, int y)? GetBlockedPosition(
        int x0, int y0, int x1, int y1, Func<int, int, bool> isBlocked)
    {
        int dx = Math.Abs(x1 - x0);
        int dy = Math.Abs(y1 - y0);

        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int err = dx - dy;

        int x = x0;
        int y = y0;

        while (true)
        {
            if (x == x1 && y == y1)
                return null;

            if ((x != x0 || y != y0) && isBlocked(x, y))
                return (x, y);

            int e2 = 2 * err;
            if (e2 > -dy)
            {
                err -= dy;
                x += sx;
            }
            if (e2 < dx)
            {
                err += dx;
                y += sy;
            }
        }
    }

    #endregion

    #region 圆算法

    /// <summary>
    /// 获取圆的轮廓点（8 对称）
    /// </summary>
    public static List<(int x, int y)> CircleOutline(int cx, int cy, int radius)
    {
        var result = new List<(int, int)>();

        int x = radius;
        int y = 0;
        int err = 0;

        while (x >= y)
        {
            // 8 对称点
            result.Add((cx + x, cy + y));
            result.Add((cx + y, cy + x));
            result.Add((cx - y, cy + x));
            result.Add((cx - x, cy + y));
            result.Add((cx - x, cy - y));
            result.Add((cx - y, cy - x));
            result.Add((cx + y, cy - x));
            result.Add((cx + x, cy - y));

            if (err <= 0)
            {
                y++;
                err += 2 * y + 1;
            }
            else
            {
                x--;
                err -= 2 * x + 1;
            }
        }

        return result;
    }

    /// <summary>
    /// 获取填充圆的所有点
    /// </summary>
    public static List<(int x, int y)> CircleFilled(int cx, int cy, int radius)
    {
        var result = new List<(int, int)>();

        int x = radius;
        int y = 0;
        int err = 0;

        while (x >= y)
        {
            // 填充水平线
            for (int i = -x; i <= x; i++)
            {
                result.Add((cx + i, cy + y));
                if (y != 0)
                    result.Add((cx + i, cy - y));
            }

            if (x != y)
            {
                for (int i = -y; i <= y; i++)
                {
                    result.Add((cx + i, cy + x));
                    result.Add((cx + i, cy - x));
                }
            }

            if (err <= 0)
            {
                y++;
                err += 2 * y + 1;
            }
            else
            {
                x--;
                err -= 2 * x + 1;
            }
        }

        return result;
    }

    #endregion

    #region 视野算法 (Field of View)

    /// <summary>
    /// 计算圆形视野范围（带障碍物阻挡）
    /// 使用射线法：向每个角度发射射线
    /// </summary>
    /// <param name="cx">中心 X</param>
    /// <param name="cy">中心 Y</param>
    /// <param name="radius">视野半径</param>
    /// <param name="isBlocked">阻挡检测函数</param>
    /// <returns>可见的格子集合</returns>
    public static HashSet<(int x, int y)> FieldOfView(int cx, int cy, int radius, Func<int, int, bool> isBlocked)
    {
        var visible = new HashSet<(int, int)>();

        // 添加中心
        visible.Add((cx, cy));

        // 对圆周上的每个点进行射线追踪
        // 使用 360 * 2 = 720 条射线保证精度
        int samples = radius * 8;
        for (int i = 0; i < samples; i++)
        {
            float angle = (float)i / samples * MathF.PI * 2;
            int dx = (int)Math.Round(MathF.Cos(angle) * radius);
            int dy = (int)Math.Round(MathF.Sin(angle) * radius);

            // 追踪射线
            foreach (var (x, y) in LineIter(cx, cy, cx + dx, cy + dy))
            {
                // 检查是否超出半径
                int distX = x - cx;
                int distY = y - cy;
                if (distX * distX + distY * distY > radius * radius)
                    break;

                visible.Add((x, y));

                // 遇到阻挡，停止
                if (isBlocked(x, y))
                    break;
            }
        }

        return visible;
    }

    /// <summary>
    /// 计算扇形视野范围
    /// </summary>
    /// <param name="cx">中心 X</param>
    /// <param name="cy">中心 Y</param>
    /// <param name="radius">视野半径</param>
    /// <param name="dirX">朝向 X 分量</param>
    /// <param name="dirY">朝向 Y 分量</param>
    /// <param name="fovAngle">视野角度（度）</param>
    /// <param name="isBlocked">阻挡检测函数</param>
    public static HashSet<(int x, int y)> FieldOfViewCone(
        int cx, int cy, int radius,
        float dirX, float dirY, float fovAngle,
        Func<int, int, bool> isBlocked)
    {
        var visible = new HashSet<(int, int)>();

        // 归一化方向
        float len = MathF.Sqrt(dirX * dirX + dirY * dirY);
        if (len > 0)
        {
            dirX /= len;
            dirY /= len;
        }

        // 朝向角度
        float facingAngle = MathF.Atan2(dirY, dirX);
        float halfFov = fovAngle * MathF.PI / 360.0f;  // 半角

        visible.Add((cx, cy));

        int samples = radius * 8;
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / samples - 0.5f;
            float angle = facingAngle + t * halfFov * 2;
            int dx = (int)Math.Round(MathF.Cos(angle) * radius);
            int dy = (int)Math.Round(MathF.Sin(angle) * radius);

            foreach (var (x, y) in LineIter(cx, cy, cx + dx, cy + dy))
            {
                int distX = x - cx;
                int distY = y - cy;
                if (distX * distX + distY * distY > radius * radius)
                    break;

                visible.Add((x, y));

                if (isBlocked(x, y))
                    break;
            }
        }

        return visible;
    }

    #endregion
}
