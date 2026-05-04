using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using GPGems.ManorSimulation;
using GPGems.AI.Boids;
using System.Numerics;
using GPGems.Core.Math;

namespace GPGems.Visualization.ManorGameDemos;

/// <summary>
/// 整合式庄园场景 - 所有算法系统整合
/// </summary>
public class ManorIntegratedScene : IDemoScene
{
    private const int MapWidth = 50;
    private const int MapHeight = 50;

    // ISO 投影参数
    private const float TileWidth = 32/2;
    private const float TileHeight = 16/2;
    private const float FloorOffset = 100;

    // 当前状态
    private BuildingFootprint? _selectedBuilding;
    private int _previewX = 40;
    private int _previewY = 30;
    private int _currentFloor = 0;
    private bool _isSimulating = false;

    // 模拟系统
    private VisitorFlowSystem? _visitorSystem;
    private AnimalGroupSystem? _animalSystem;
    private EmployeeTaskSystem? _employeeSystem;
    private EvacuationSystem? _evacuationSystem;

    // 建筑-任务关联映射 (建筑ID -> 任务位置)
    private readonly Dictionary<int, Vector2> _buildingTaskMap = new();

    // 系统开关
    public bool EnableVisitorFlow { get; set; } = true;
    public bool EnableAnimalGroup { get; set; } = false;
    public bool EnableEmployeeTask { get; set; } = false;
    public bool EnableEvacuation { get; set; } = false;

    // 参数设置
    public int VisitorCount { get; set; } = 100;
    public float VisitorSpeed { get; set; } = 1f;
    public bool EnableORCA { get; set; } = true;

    public int AnimalCount { get; set; } = 50;
    public float BoidsCohesion { get; set; } = 1f;
    public float BoidsAlignment { get; set; } = 1f;
    public float BoidsSeparation { get; set; } = 1.5f;

    public int EmployeeCount { get; set; } = 5;
    public int TaskCount { get; set; } = 10;

    public int EvacuationCount { get; set; } = 300;

    public ManorIntegratedScene()
    {
        _selectedBuilding = ManorGamePresets.IntegratedSceneBuildings[0];
    }

    #region 属性

    public int CurrentFloor
    {
        get => _currentFloor;
        set => _currentFloor = Math.Clamp(value, 0, 2);
    }

    public IReadOnlyList<BuildingFootprint> BuildingPresets => ManorGamePresets.IntegratedSceneBuildings;

    public BuildingFootprint? SelectedBuilding
    {
        get => _selectedBuilding;
        set => _selectedBuilding = value;
    }

    public int PreviewX
    {
        get => _previewX;
        set => _previewX = Math.Clamp(value, 0, MapWidth - 1);
    }

    public int PreviewY
    {
        get => _previewY;
        set => _previewY = Math.Clamp(value, 0, MapHeight - 1);
    }

    public bool IsSimulating => _isSimulating;

    #endregion

    #region ISO 坐标转换

    public (float screenX, float screenY) GridToScreen(int gridX, int gridY, int floor = 0)
    {
        float isoX = (gridX - gridY) * (TileWidth / 2f);
        float isoY = (gridX + gridY) * (TileHeight / 2f);
        isoY -= floor * FloorOffset;
        return (isoX + 400, isoY + 50);
    }

    public (int gridX, int gridY) ScreenToGrid(double screenX, double screenY)
    {
        float sx = (float)(screenX - 400);
        float sy = (float)(screenY - 50);
        sy += _currentFloor * FloorOffset;

        int gridX = (int)((sx / (TileWidth / 2f) + sy / (TileHeight / 2f)) / 2f);
        int gridY = (int)((sy / (TileHeight / 2f) - sx / (TileWidth / 2f)) / 2f);

        return (Math.Clamp(gridX, 0, MapWidth - 1), Math.Clamp(gridY, 0, MapHeight - 1));
    }

    #endregion

    #region 放置操作

    public PlacementResult CheckPlacement()
    {
        if (_selectedBuilding == null)
            return PlacementResult.OutOfBounds;

        var facade = ManorAlgorithmFacade.Instance;
        return facade.CanPlace(_selectedBuilding, _previewX, _previewY, _currentFloor);
    }

    public int PlaceCurrent()
    {
        if (_selectedBuilding == null)
            return 0;

        var facade = ManorAlgorithmFacade.Instance;
        int buildingId = facade.PlaceObject(_selectedBuilding, _previewX, _previewY, _currentFloor);

        // 如果是员工设施，放置后自动添加任务点
        if (buildingId > 0 && _selectedBuilding.Type == BuildingType.StaffFacility)
        {
            var taskPos = new Vector2(_previewX, _previewY);
            _employeeSystem?.AddTask(
                GetTaskTypeForBuilding(_selectedBuilding),
                taskPos,
                3.0f);

            _buildingTaskMap[buildingId] = taskPos;
        }

        return buildingId;
    }

    private static string GetTaskTypeForBuilding(BuildingFootprint footprint)
    {
        // 根据预设索引区分任务类型
        var presets = ManorGamePresets.IntegratedSceneBuildings;
        int index = presets.IndexOf(footprint);
        return index switch
        {
            6 => "Rest",      // StaffDorm - 休息
            7 => "Harvest",   // HarvestPoint - 收获
            8 => "Feed",      // FeedPoint - 喂养
            9 => "Serve",     // ServePoint - 服务
            _ => "Work"
        };
    }

    public bool RemoveAt(int worldX, int worldY)
    {
        var facade = ManorAlgorithmFacade.Instance;
        var obj = facade.GetObjectAt(worldX, worldY, _currentFloor);
        if (obj == null)
            return false;

        // 如果是员工设施，移除前先删除对应任务点
        if (obj.Footprint.Type == BuildingType.StaffFacility &&
            _buildingTaskMap.TryGetValue(obj.Id, out var taskPos))
        {
            _employeeSystem?.RemoveTaskAt(taskPos);
            _buildingTaskMap.Remove(obj.Id);
        }

        return facade.RemoveObject(obj.Id);
    }

    #endregion

    #region 模拟控制

    public void StartSimulation()
    {
        var facade = ManorAlgorithmFacade.Instance;

        // 查找入口和出口
        PlacedObject? entrance = null;
        PlacedObject? exit = null;

        for (int f = 0; f < 3; f++)
        {
            foreach (var obj in facade.GetAllObjectsOnFloor(f))
            {
                if (obj.Footprint.Type == BuildingType.Entrance && entrance == null)
                    entrance = obj;
                if (obj.Footprint.Type == BuildingType.Exit && exit == null)
                    exit = obj;
            }
        }

        int entranceX = entrance?.AnchorX ?? 5;
        int entranceY = entrance?.AnchorY ?? 30;

        // 根据开关启用对应系统
        if (EnableVisitorFlow)
        {
            _visitorSystem = facade.CreateVisitorFlowSystem(
                VisitorCount, entranceX, entranceY, VisitorSpeed);
        }

        if (EnableAnimalGroup)
        {
            _animalSystem = facade.CreateAnimalSystem();
            _animalSystem.CreateFishSchool(AnimalCount / 2, new Vector3(MapWidth / 2, 0, MapHeight / 2));
            _animalSystem.CreateGrazingHerd(AnimalCount / 3, new Vector3(MapWidth / 2, 0, MapHeight / 2));
            _animalSystem.CreateButterflySwarm(AnimalCount - AnimalCount / 2 - AnimalCount / 3, new Vector3(MapWidth / 2, 0, MapHeight / 2));
        }

        if (EnableEmployeeTask)
        {
            _employeeSystem = facade.CreateEmployeeTaskSystem(EmployeeCount, TaskCount);
        }

        if (EnableEvacuation)
        {
            _evacuationSystem = facade.CreateEvacuationSystem(EvacuationCount, MapWidth, MapHeight);
        }

        _isSimulating = true;
    }

    public void StopSimulation()
    {
        _isSimulating = false;
        _visitorSystem = null;
        _animalSystem = null;
        _employeeSystem = null;
        _evacuationSystem = null;
    }

    #endregion

    public void Reset(int count, float speed)
    {
        var facade = ManorAlgorithmFacade.Instance;
        facade.Initialize(MapWidth, MapHeight);
        StopSimulation();
    }

    public void Update(float deltaTime)
    {
        if (!_isSimulating)
            return;

        _visitorSystem?.Update(deltaTime);
        if (_animalSystem != null)
        {
            // 享元模式：从工厂获取共享的参数实例
            var settings = ManorGamePresets.GetOrCreateCustom(
                BoidsCohesion, BoidsAlignment, BoidsSeparation);
            _animalSystem.UpdateWithSettings(deltaTime, settings);
        }
        _employeeSystem?.Update(deltaTime);
        _evacuationSystem?.Update(deltaTime);
    }

    public void RenderBackground(Canvas canvas, List<Shape> cache)
    {
        var facade = ManorAlgorithmFacade.Instance;

        // 绘制地面网格
        for (int f = 0; f <= _currentFloor; f++)
        {
            DrawFloorGrid(canvas, cache, f);
        }

        // 绘制所有建筑物
        for (int f = 0; f <= _currentFloor; f++)
        {
            foreach (var obj in facade.GetAllObjectsOnFloor(f))
            {
                DrawBuilding(canvas, cache, obj);
            }
        }

        // 绘制放置预览
        if (_selectedBuilding != null && !_isSimulating)
        {
            DrawPlacementPreview(canvas, cache);
        }
    }

    private void DrawFloorGrid(Canvas canvas, List<Shape> cache, int floor)
    {
        var floorColor = ManorColors.GetFloorColor(floor);
        float alpha = floor == _currentFloor ? 0.9f : 0.4f;

        for (int y = 0; y < MapHeight; y++)
        for (int x = 0; x < MapWidth; x++)
        {
            var (sx, sy) = GridToScreen(x, y, floor);

            var polygon = new Polygon
            {
                Fill = new SolidColorBrush(Color.FromArgb(
                    (byte)(alpha * 255),
                    floorColor.R,
                    floorColor.G,
                    floorColor.B)),
                Stroke = new SolidColorBrush(Color.FromArgb((byte)(alpha * 80), 100, 100, 100)),
                StrokeThickness = 0.5f,
                Points =
                {
                    new Point(sx, sy),
                    new Point(sx + TileWidth / 2f, sy + TileHeight / 2f),
                    new Point(sx, sy + TileHeight),
                    new Point(sx - TileWidth / 2f, sy + TileHeight / 2f)
                },
            };
            canvas.Children.Add(polygon);
            cache.Add(polygon);
        }
    }

    private void DrawBuilding(Canvas canvas, List<Shape> cache, PlacedObject obj)
    {
        var (baseX, baseY) = GridToScreen(obj.AnchorX, obj.AnchorY, obj.Floor);
        var buildingColor = ManorColors.GetBuildingColor(obj.Footprint);
        float scale = Math.Min(obj.Footprint.Width, obj.Footprint.Height);

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

        var (typeName, icon) = GetBuildingTypeInfo(obj.Footprint.Type);
        var label = new TextBlock
        {
            Text = $"{icon}{typeName}-{obj.Id}-F{obj.Floor + 1}",
            Foreground = new SolidColorBrush(Colors.White),
            FontSize = 10,
            FontWeight = FontWeights.Bold
        };
        Canvas.SetLeft(label, baseX - 25);
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
        var previewColor = result == PlacementResult.Valid ? ManorColors.PlaceValid : ManorColors.PlaceInvalid;
        var (baseX, baseY) = GridToScreen(_previewX, _previewY, _currentFloor);
        float scale = Math.Min(_selectedBuilding!.Width, _selectedBuilding.Height);

        var previewPolygon = new Polygon
        {
            Fill = new SolidColorBrush(Color.FromArgb(180, previewColor.R, previewColor.G, previewColor.B)),
            Stroke = new SolidColorBrush(Colors.Yellow),
            StrokeThickness = 2,
            StrokeDashArray = { 4, 2 },
            Points =
            {
                new Point(baseX, baseY - 5 * scale),
                new Point(baseX + 7.5f * scale, baseY - 1.25f * scale),
                new Point(baseX, baseY + 2.5f * scale),
                new Point(baseX - 7.5f * scale, baseY - 1.25f * scale)
            }
        };
        canvas.Children.Add(previewPolygon);
        cache.Add(previewPolygon);
    }

    public void RenderAgents(Canvas canvas, List<Shape> cache)
    {
        // 渲染游客
        if (_visitorSystem != null)
        {
            for (int i = 0; i < _visitorSystem.AgentCount; i++)
            {
                var agent = _visitorSystem.GetAgent(i);
                var (sx, sy) = GridToScreen((int)agent.Position.X, (int)agent.Position.Y, 0);

                var ellipse = new Ellipse
                {
                    Width = 6,
                    Height = 6,
                    Fill = new SolidColorBrush(Color.FromRgb(100, 200, 255))
                };
                Canvas.SetLeft(ellipse, sx - 3);
                Canvas.SetTop(ellipse, sy - 3);
                canvas.Children.Add(ellipse);
                cache.Add(ellipse);
            }
        }

        // 渲染动物
        if (_animalSystem != null)
        {
            var colors = new[] { Colors.Orange, Colors.LightBlue, Colors.LightGreen };
            int colorIndex = 0;
            foreach (var flock in _animalSystem.Flocks)
            {
                var fillColor = new SolidColorBrush(colors[colorIndex % colors.Length]);
                foreach (var boid in flock.Boids)
                {
                    var (sx, sy) = GridToScreen((int)boid.Position.X, (int)boid.Position.Z, 0);

                    var ellipse = new Ellipse
                    {
                        Width = 5,
                        Height = 5,
                        Fill = fillColor
                    };
                    Canvas.SetLeft(ellipse, sx - 2);
                    Canvas.SetTop(ellipse, sy - 2);
                    canvas.Children.Add(ellipse);
                    cache.Add(ellipse);
                }
                colorIndex++;
            }
        }

        // 渲染员工任务点
        if (_employeeSystem != null)
        {
            foreach (var task in _employeeSystem.Tasks)
            {
                var (sx, sy) = GridToScreen((int)task.Position.X, (int)task.Position.Y, 0);

                // 根据任务类型选择颜色
                Color taskColor = task.Type switch
                {
                    "Rest" => Color.FromRgb(128, 128, 255),    // 蓝色 - 休息
                    "Harvest" => Color.FromRgb(50, 205, 50),   // 绿色 - 收获
                    "Feed" => Color.FromRgb(255, 215, 0),      // 金色 - 喂养
                    "Serve" => Color.FromRgb(30, 144, 255),    // 深天蓝 - 服务
                    _ => Color.FromRgb(128, 128, 128)           // 灰色 - 默认
                };

                var ellipse = new Ellipse
                {
                    Width = 10,
                    Height = 10,
                    Fill = new SolidColorBrush(taskColor),
                    Stroke = new SolidColorBrush(Colors.White),
                    StrokeThickness = 1
                };
                Canvas.SetLeft(ellipse, sx - 5);
                Canvas.SetTop(ellipse, sy - 5);
                canvas.Children.Add(ellipse);
                cache.Add(ellipse);
            }

            // 渲染员工
            foreach (var emp in _employeeSystem.Employees)
            {
                var (sx, sy) = GridToScreen((int)emp.Position.X, (int)emp.Position.Y, 0);

                var rect = new Rectangle
                {
                    Width = 8,
                    Height = 8,
                    Fill = new SolidColorBrush(Color.FromRgb(255, 200, 100))
                };
                Canvas.SetLeft(rect, sx - 4);
                Canvas.SetTop(rect, sy - 4);
                canvas.Children.Add(rect);
                cache.Add(rect);
            }
        }

        // 渲染疏散人群
        if (_evacuationSystem != null)
        {
            for (int i = 0; i < _evacuationSystem.AgentCount; i++)
            {
                var agent = _evacuationSystem.GetAgent(i);
                var (sx, sy) = GridToScreen((int)agent.Position.X, (int)agent.Position.Y, 0);

                var ellipse = new Ellipse
                {
                    Width = 4,
                    Height = 4,
                    Fill = new SolidColorBrush(Colors.Red)
                };
                Canvas.SetLeft(ellipse, sx - 2);
                Canvas.SetTop(ellipse, sy - 2);
                canvas.Children.Add(ellipse);
                cache.Add(ellipse);
            }
        }
    }

    public int GetStat(string name)
    {
        var facade = ManorAlgorithmFacade.Instance;
        int total = 0;
        for (int f = 0; f < 3; f++)
            total += facade.GetAllObjectsOnFloor(f).Count;

        int animalCount = 0;
        if (_animalSystem != null)
        {
            foreach (var flock in _animalSystem.Flocks)
                animalCount += flock.Boids.Count;
        }

        return name switch
        {
            "buildingCount" => total,
            "visitorCount" => _visitorSystem?.AgentCount ?? 0,
            "animalCount" => animalCount,
            "employeeCount" => _employeeSystem?.Employees.Count ?? 0,
            "evacuationCount" => _evacuationSystem?.AgentCount ?? 0,
            _ => total
        };
    }
}
