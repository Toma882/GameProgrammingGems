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
/// 放置演示场景
/// 位掩码多层地图的建筑物放置演示
/// </summary>
public class PlacementScene : IDemoScene
{
    private const float Scale = 8f;
    private const int MapWidth = 100;
    private const int MapHeight = 50;

    // 放置状态
    private BuildingFootprint? _selectedBuilding;
    private int _previewX = 50;
    private int _previewY = 25;
    private bool _isDragging = false;

    // 预设建筑物
    private readonly List<BuildingFootprint> _buildingPresets;

    public PlacementScene()
    {
        _buildingPresets = new List<BuildingFootprint>
        {
            // 小商店 2x2
            new BuildingFootprint(BuildingType.Shop, 2, 2)
            {
                Layer = MapLayer.Building,
                BlocksMovement = true,
                AnchorX = 0.5f,
                AnchorY = 0.5f
            },
            // 大商店 3x3
            new BuildingFootprint(BuildingType.Shop, 3, 3)
            {
                Layer = MapLayer.Building,
                BlocksMovement = true
            },
            // 餐厅 4x3
            new BuildingFootprint(BuildingType.Restaurant, 4, 3)
            {
                Layer = MapLayer.Building,
                BlocksMovement = true
            },
            // 景点 5x5
            new BuildingFootprint(BuildingType.Attraction, 5, 5)
            {
                Layer = MapLayer.Building,
                BlocksMovement = true
            },
            // 圆形喷泉 (半径2)
            BuildingFootprint.CreateCircle(BuildingType.Decoration, 2),
            // 路径 1x5
            new BuildingFootprint(BuildingType.Road, 1, 5)
            {
                Layer = MapLayer.Path,
                BlocksMovement = false
            }
        };

        _selectedBuilding = _buildingPresets[0];
    }

    /// <summary>
    /// 预设建筑物列表
    /// </summary>
    public IReadOnlyList<BuildingFootprint> BuildingPresets => _buildingPresets;

    /// <summary>
    /// 当前选中的建筑物
    /// </summary>
    public BuildingFootprint? SelectedBuilding
    {
        get => _selectedBuilding;
        set => _selectedBuilding = value;
    }

    /// <summary>
    /// 预览位置 X
    /// </summary>
    public int PreviewX
    {
        get => _previewX;
        set => _previewX = Math.Clamp(value, 0, MapWidth - 1);
    }

    /// <summary>
    /// 预览位置 Y
    /// </summary>
    public int PreviewY
    {
        get => _previewY;
        set => _previewY = Math.Clamp(value, 0, MapHeight - 1);
    }

    /// <summary>
    /// 检查当前预览位置是否可放置
    /// </summary>
    public PlacementResult CheckPlacement()
    {
        if (_selectedBuilding == null)
            return PlacementResult.OutOfBounds;

        var facade = ManorAlgorithmFacade.Instance;
        return facade.CanPlace(_selectedBuilding, _previewX, _previewY);
    }

    /// <summary>
    /// 放置当前选中的建筑物
    /// </summary>
    public int PlaceCurrent()
    {
        if (_selectedBuilding == null)
            return 0;

        var facade = ManorAlgorithmFacade.Instance;
        return facade.PlaceObject(_selectedBuilding, _previewX, _previewY);
    }

    /// <summary>
    /// 删除指定位置的建筑物
    /// </summary>
    public bool RemoveAt(int worldX, int worldY, MapLayer layer = MapLayer.Building)
    {
        var facade = ManorAlgorithmFacade.Instance;
        var obj = facade.GetObjectAt(worldX, worldY, layer);
        if (obj == null)
            return false;

        return facade.RemoveObject(obj.Id, layer);
    }

    /// <summary>
    /// 屏幕坐标转地图格子
    /// </summary>
    public (int x, int y) ScreenToGrid(double screenX, double screenY)
    {
        return ((int)(screenX / Scale), (int)(screenY / Scale));
    }

    public void Reset(int count, float speed)
    {
        var facade = ManorAlgorithmFacade.Instance;
        facade.Initialize(MapWidth, MapHeight);
    }

    public void Update(float deltaTime)
    {
        // 放置场景不需要动态更新
    }

    public void RenderBackground(Canvas canvas, List<Shape> cache)
    {
        var facade = ManorAlgorithmFacade.Instance;

        // 绘制网格
        for (int y = 0; y < MapHeight; y++)
        for (int x = 0; x < MapWidth; x++)
        {
            byte bitmapValue = facade.LayeredMap!.GetBitmapValue(x, y);
            Color cellColor;

            if (bitmapValue == 0)
            {
                // 空地 - 深色网格
                cellColor = Color.FromRgb(30, 40, 60);
            }
            else
            {
                // 根据图层着色
                if ((bitmapValue & (1 << (int)MapLayer.Building)) != 0)
                    cellColor = Color.FromRgb(100, 120, 180);
                else if ((bitmapValue & (1 << (int)MapLayer.Path)) != 0)
                    cellColor = Color.FromRgb(80, 80, 80);
                else if ((bitmapValue & (1 << (int)MapLayer.Decoration)) != 0)
                    cellColor = Color.FromRgb(80, 160, 100);
                else
                    cellColor = Color.FromRgb(60, 60, 90);
            }

            var rect = new Rectangle
            {
                Width = Scale,
                Height = Scale,
                Fill = new SolidColorBrush(cellColor)
            };
            Canvas.SetLeft(rect, x * Scale);
            Canvas.SetTop(rect, y * Scale);
            canvas.Children.Add(rect);
        }

        // 绘制放置预览
        if (_selectedBuilding != null)
        {
            var result = CheckPlacement();
            var previewColor = result == PlacementResult.Valid
                ? Color.FromArgb(180, 80, 200, 80)    // 绿色 - 可放置
                : Color.FromArgb(180, 200, 80, 80);    // 红色 - 碰撞

            var bounds = _selectedBuilding.GetBounds();
            var (offsetX, offsetY) = _selectedBuilding.GetAnchorOffset();

            foreach (var (dx, dy) in _selectedBuilding.EnumerateOccupiedCells())
            {
                int worldX = _previewX + dx;
                int worldY = _previewY + dy;

                var rect = new Rectangle
                {
                    Width = Scale,
                    Height = Scale,
                    Fill = new SolidColorBrush(previewColor),
                    Stroke = new SolidColorBrush(Colors.White),
                    StrokeThickness = 0.5f
                };
                Canvas.SetLeft(rect, worldX * Scale);
                Canvas.SetTop(rect, worldY * Scale);
                canvas.Children.Add(rect);
            }

            // 绘制锚点
            var anchor = new Ellipse
            {
                Width = 6,
                Height = 6,
                Fill = new SolidColorBrush(Colors.Yellow)
            };
            Canvas.SetLeft(anchor, _previewX * Scale - 3);
            Canvas.SetTop(anchor, _previewY * Scale - 3);
            canvas.Children.Add(anchor);
        }

        // 绘制所有已放置建筑物的边框
        foreach (var layer in new[] { MapLayer.Building, MapLayer.Decoration, MapLayer.Path })
        {
            foreach (var obj in facade.GetAllObjects(layer))
            {
                var bounds = obj.GetWorldBounds();
                var border = new Rectangle
                {
                    Width = (bounds.maxX - bounds.minX + 1) * Scale,
                    Height = (bounds.maxY - bounds.minY + 1) * Scale,
                    Stroke = new SolidColorBrush(Colors.White),
                    StrokeThickness = 1,
                    Fill = null
                };
                Canvas.SetLeft(border, bounds.minX * Scale);
                Canvas.SetTop(border, bounds.minY * Scale);
                canvas.Children.Add(border);
            }
        }
    }

    public void RenderAgents(Canvas canvas, List<Shape> cache)
    {
        // 放置场景没有 agent
    }

    public int GetStat(string name)
    {
        var facade = ManorAlgorithmFacade.Instance;
        return facade.GetAllObjects(MapLayer.Building).Count +
               facade.GetAllObjects(MapLayer.Decoration).Count +
               facade.GetAllObjects(MapLayer.Path).Count;
    }
}
