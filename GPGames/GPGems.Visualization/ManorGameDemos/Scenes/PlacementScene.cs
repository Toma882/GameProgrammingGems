using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using GPGems.AI.ManorSimulation;
using GPGems.AI.ManorSimulation.Placement;

namespace GPGems.Visualization.ManorGameDemos;

/// <summary>
/// 放置演示场景 - 三层楼ISO等距视角
/// </summary>
public class PlacementScene : IDemoScene
{
    private const int MapWidth = 40;
    private const int MapHeight = 30;

    // ISO 投影参数
    private const float TileWidth = 32;    // ISO 瓦片宽度
    private const float TileHeight = 16;     // ISO 瓦片高度
    private const float FloorOffset = 20;       // 每层楼之间的Y偏移

    // 当前状态
    private BuildingFootprint? _selectedBuilding;
    private int _previewX = 20;
    private int _previewY = 15;
    private int _currentFloor = 0; // 0=一楼, 1=二楼, 2=三楼

    // 预设建筑物
    private readonly List<BuildingFootprint> _buildingPresets;

    // 楼层颜色
    private readonly Color[] _floorColors = new[]
    {
        Color.FromRgb(70, 100, 70),   // 一楼：绿色
        Color.FromRgb(70, 70, 100),   // 二楼：蓝色
        Color.FromRgb(100, 70, 70)      // 三楼：红色
    };

    public PlacementScene()
    {
        _buildingPresets = new List<BuildingFootprint>
        {
            // 小商店 2x2
            new BuildingFootprint(BuildingType.Shop, 2, 2)
            {
                BlocksMovement = true,
                FloorCount = 1
            },
            // 大商店 3x3
            new BuildingFootprint(BuildingType.Shop, 3, 3)
            {
                BlocksMovement = true,
                FloorCount = 2
            },
            // 餐厅 4x3
            new BuildingFootprint(BuildingType.Restaurant, 4, 3)
            {
                BlocksMovement = true,
                FloorCount = 1
            },
            // 高楼 2x2 高2层
            new BuildingFootprint(BuildingType.Attraction, 2, 2)
            {
                BlocksMovement = true,
                FloorCount = 2
            },
            // 楼梯 2x1
            new BuildingFootprint(BuildingType.Road, 2, 1)
            {
                BlocksMovement = false,
                FloorCount = 0
            }
        };

        _selectedBuilding = _buildingPresets[0];
    }

    /// <summary>当前楼层（0=一楼，1=二楼，2=三楼）</summary>
    public int CurrentFloor
    {
        get => _currentFloor;
        set => _currentFloor = Math.Clamp(value, 0, 2);
    }

    /// <summary>预设建筑物列表</summary>
    public IReadOnlyList<BuildingFootprint> BuildingPresets => _buildingPresets;

    /// <summary>当前选中的建筑物</summary>
    public BuildingFootprint? SelectedBuilding
    {
        get => _selectedBuilding;
        set => _selectedBuilding = value;
    }

    /// <summary>预览位置 X</summary>
    public int PreviewX
    {
        get => _previewX;
        set => _previewX = Math.Clamp(value, 0, MapWidth - 1);
    }

    /// <summary>预览位置 Y</summary>
    public int PreviewY
    {
        get => _previewY;
        set => _previewY = Math.Clamp(value, 0, MapHeight - 1);
    }

    #region ISO 坐标转换

    /// <summary>
    /// 网格坐标转屏幕坐标（ISO）
    /// </summary>
    public (float screenX, float screenY) GridToScreen(int gridX, int gridY, int floor = 0)
    {
        float isoX = (gridX - gridY) * (TileWidth / 2);
        float isoY = (gridX + gridY) * (TileHeight / 2);
        isoY -= floor * FloorOffset;
        return (isoX + 400, isoY + 50); // 居中偏移
    }

    /// <summary>
    /// 屏幕坐标转网格坐标
    /// </summary>
    public (int gridX, int gridY) ScreenToGrid(double screenX, double screenY)
    {
        float sx = (float)(screenX - 400);
        float sy = (float)(screenY - 50);
        sy += _currentFloor * FloorOffset;

        int gridX = (int)((sx / (TileWidth / 2) + sy / (TileHeight / 2)) / 2);
        int gridY = (int)((sy / (TileHeight / 2) - sx / (TileWidth / 2)) / 2);

        return (Math.Clamp(gridX, 0, MapWidth - 1), Math.Clamp(gridY, 0, MapHeight - 1));
    }

    #endregion

    /// <summary>检查当前预览位置是否可放置</summary>
    public PlacementResult CheckPlacement()
    {
        if (_selectedBuilding == null)
            return PlacementResult.OutOfBounds;

        var facade = ManorAlgorithmFacade.Instance;
        return facade.CanPlace(_selectedBuilding, _previewX, _previewY, _currentFloor);
    }

    /// <summary>放置当前选中的建筑物</summary>
    public int PlaceCurrent()
    {
        if (_selectedBuilding == null)
            return 0;

        var facade = ManorAlgorithmFacade.Instance;
        return facade.PlaceObject(_selectedBuilding, _previewX, _previewY, _currentFloor);
    }

    /// <summary>删除指定位置的建筑物</summary>
    public bool RemoveAt(int worldX, int worldY)
    {
        var facade = ManorAlgorithmFacade.Instance;
        var obj = facade.GetObjectAt(worldX, worldY, _currentFloor);
        if (obj == null)
            return false;

        return facade.RemoveObject(obj.Id);
    }

    public void Reset(int count, float speed)
    {
        var facade = ManorAlgorithmFacade.Instance;
        facade.Initialize(MapWidth, MapHeight);
    }

    public void Update(float deltaTime)
    {
    }

    public void RenderBackground(Canvas canvas, List<Shape> cache)
    {
        var facade = ManorAlgorithmFacade.Instance;

        // 绘制地面网格（所有可见楼层）
        for (int f = 0; f <= _currentFloor; f++)
        {
            DrawFloorGrid(canvas, cache, f);
        }

        // 绘制所有建筑物（按楼层从下到上绘制）
        for (int f = 0; f <= _currentFloor; f++)
        {
            foreach (var obj in facade.GetAllObjectsOnFloor(f))
            {
                DrawBuilding(canvas, cache, obj);
            }
        }

        // 绘制放置预览
        if (_selectedBuilding != null)
        {
            DrawPlacementPreview(canvas, cache);
        }
    }

    private void DrawFloorGrid(Canvas canvas, List<Shape> cache, int floor)
    {
        var floorColor = _floorColors[floor];
        float alpha = floor == _currentFloor ? 0.9f : 0.4f;

        for (int y = 0; y < MapHeight; y++)
        for (int x = 0; x < MapWidth; x++)
        {
            var (sx, sy) = GridToScreen(x, y, floor);

            // 绘制菱形格子
            var polygon = new Polygon
            {
                Fill = new SolidColorBrush(Color.FromArgb(
                    (byte)(alpha * 255),
                    floorColor.R,
                    floorColor.G,
                    floorColor.B)),
                Stroke = new SolidColorBrush(Color.FromArgb((byte)(alpha * 128), 100, 100, 100)),
                StrokeThickness = 1,
                Points =
                {
                    new Point(sx, sy),
                    new Point(sx + TileWidth / 2, sy + TileHeight / 2),
                    new Point(sx, sy + TileHeight),
                    new Point(sx - TileWidth / 2, sy + TileHeight / 2)
                },
            };
            canvas.Children.Add(polygon);
            cache.Add(polygon);
        }
    }

    private void DrawBuilding(Canvas canvas, List<Shape> cache, PlacedObject obj)
    {
        var bounds = obj.GetWorldBounds();
        var floor = obj.Floor;

        // 获取建筑锚点位置
        var (baseX, baseY) = GridToScreen(obj.AnchorX, obj.AnchorY, floor);

        // 使用 ManorColors 获取建筑物颜色
        var buildingColor = ManorColors.GetBuildingColor(obj.Footprint);

        // 根据建筑大小调整绘制尺寸
        float scale = Math.Min(obj.Footprint.Width, obj.Footprint.Height);

        // 绘制建筑物顶面（ISO菱形）
        var topPolygon = new Polygon
        {
            Fill = new SolidColorBrush(buildingColor),
            Stroke = new SolidColorBrush(Colors.White),
            StrokeThickness = 2,
            Points =
            {
                new Point(baseX, baseY - 5 * scale),
                new Point(baseX + 7.5f * scale, baseY - 1.25f * scale),
                new Point(baseX, baseY + 2.5f * scale),
                new Point(baseX - 7.5f * scale, baseY - 1.25f * scale)
            }
        };
        canvas.Children.Add(topPolygon);
        cache.Add(topPolygon);

        // 绘制建筑标签：格式 "图标类型-Id-F楼层"
        var (typeName, icon) = GetBuildingTypeInfo(obj.Footprint.Type);
        var label = new TextBlock
        {
            Text = $"{icon}{typeName}-{obj.Id}-F{floor + 1}",
            Foreground = new SolidColorBrush(Colors.White),
            FontSize = 10,
            FontWeight = FontWeights.Bold
        };
        Canvas.SetLeft(label, baseX - 20);
        Canvas.SetTop(label, baseY - 2);
        canvas.Children.Add(label);
    }

    private static (string name, string icon) GetBuildingTypeInfo(BuildingType type)
    {
        return type switch
        {
            BuildingType.Entrance => ("入口", "🚪"),
            BuildingType.Exit => ("出口", "🏁"),
            BuildingType.Attraction => ("景点", "🎡"),
            BuildingType.Shop => ("商店", "🏪"),
            BuildingType.Restaurant => ("餐厅", "🍽️"),
            BuildingType.Road => ("楼梯", "🪜"),
            BuildingType.Fence => ("围栏", "📦"),
            BuildingType.House => ("房屋", "🏠"),
            BuildingType.Habitat => ("栖息地", "🦁"),
            BuildingType.StaffFacility => ("员工", "👷"),
            BuildingType.Plant => ("植物", "🌳"),
            BuildingType.Decoration => ("装饰", "🎀"),
            _ => ("", "")
        };
    }

    private void DrawPlacementPreview(Canvas canvas, List<Shape> cache)
    {
        var result = CheckPlacement();
        var previewColor = result == PlacementResult.Valid
            ? Color.FromArgb(180, 80, 200, 80)    // 绿色 - 可放置
            : Color.FromArgb(180, 200, 80, 80);    // 红色 - 碰撞

        var bounds = _selectedBuilding!.GetBounds();
        var (baseX, baseY) = GridToScreen(_previewX, _previewY, _currentFloor);

        // 绘制预览范围
        var previewPolygon = new Polygon
        {
            Fill = new SolidColorBrush(previewColor),
            Stroke = new SolidColorBrush(Colors.Yellow),
            StrokeThickness = 2,
            StrokeDashArray = { 4, 2 },
            Points =
            {
                new Point(baseX, baseY - 10),
                new Point(baseX + 20, baseY),
                new Point(baseX, baseY + 10),
                new Point(baseX - 20, baseY)
            }
        };
        canvas.Children.Add(previewPolygon);
        cache.Add(previewPolygon);
    }

    public void RenderAgents(Canvas canvas, List<Shape> cache)
    {
    }

    public int GetStat(string name)
    {
        var facade = ManorAlgorithmFacade.Instance;
        return facade.CountObjectsOnFloor(_currentFloor);
    }
}
