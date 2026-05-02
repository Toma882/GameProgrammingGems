using System.Windows.Media;
using GPGems.AI.ManorSimulation.Placement;

namespace GPGems.Visualization.ManorGameDemos;

/// <summary>
/// 庄园模拟演示的颜色系统
/// 通过建筑物占位定义来动态获取颜色
/// </summary>
public static class ManorColors
{
    // 基础调色板
    public static readonly Color Background = Color.FromRgb(26, 26, 46);
    public static readonly Color GridLine = Color.FromRgb(50, 50, 80);

    // 楼层颜色
    private static readonly Color[] _floorColors = new[]
    {
        Color.FromRgb(70, 100, 70),   // 一楼：绿色
        Color.FromRgb(70, 70, 100),   // 二楼：蓝色
        Color.FromRgb(100, 70, 70)    // 三楼：红色
    };

    // 放置预览颜色
    public static readonly Color PlaceValid = Color.FromRgb(80, 200, 80);   // 可放置：绿色
    public static readonly Color PlaceInvalid = Color.FromRgb(200, 80, 80); // 不可放置：红色

    /// <summary>
    /// 根据楼层获取颜色
    /// </summary>
    public static Color GetFloorColor(int floor)
    {
        floor = Math.Clamp(floor, 0, _floorColors.Length - 1);
        return _floorColors[floor];
    }

    /// <summary>
    /// 根据建筑物占位获取建筑颜色
    /// </summary>
    public static Color GetBuildingColor(BuildingFootprint footprint)
    {
        // 根据建筑类型和高度动态计算颜色
        byte brightness = (byte)(120 + footprint.FloorCount * 40);

        return footprint.Type switch
        {
            BuildingType.Entrance => Color.FromRgb(46, 204, 113),
            BuildingType.Exit => Color.FromRgb(155, 89, 182),
            BuildingType.Shop => Color.FromRgb(241, 196, 15),
            BuildingType.Restaurant => Color.FromRgb(230, 126, 34),
            BuildingType.Attraction => Color.FromRgb(231, 76, 60),
            BuildingType.Road => Color.FromRgb(149, 165, 166),
            BuildingType.Fence => Color.FromRgb(127, 140, 141),
            BuildingType.House => Color.FromRgb(52, 152, 219),
            BuildingType.Habitat => Color.FromRgb(39, 174, 96),
            BuildingType.StaffFacility => Color.FromRgb(142, 68, 173),
            BuildingType.Plant => Color.FromRgb(46, 125, 50),
            BuildingType.Decoration => Color.FromRgb(189, 195, 199),
            _ => Color.FromRgb(brightness, brightness, brightness)
        };
    }

    /// <summary>
    /// 获取带有透明度的画刷
    /// </summary>
    public static Brush ToBrush(this Color color, byte alpha = 255)
    {
        return new SolidColorBrush(Color.FromArgb(alpha, color.R, color.G, color.B));
    }
}

