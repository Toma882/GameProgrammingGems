# 享元模式占位分配器 - 使用示例

## 核心设计

```
FootprintFactory (享元工厂)
    ├─ 缓存池: Dictionary<string, IFootprint>
    └─ GetOrCreate(configName, creator)
              ↓  已存在 → 直接返回（复用）
              ↓  不存在 → 创建并缓存
           IFootprint (不可变享元对象)
              ↓
BuildingUnit [持有引用] → 1000 个农田共享 1 个 Footprint
```

## 内存对比

| 方案 | 1000 个 2x2 农田 | 内存占用 |
|------|------------------|----------|
| 传统（每个建筑 new Footprint） | 1000 个实例 | ~100KB |
| 享元模式（共享） | 1 个实例 | ~100B |

**节省 99.9% 的内存！**

---

## 快速开始

### 1. 配置建筑数据

```csharp
// 2x2 农田配置（矩形）
var farmConfig = new BuildingData
{
    ConfigId = "farm_001",           // 享元 key
    Name = "普通农田",
    Size = (2, 2),
    FootprintShape = FootprintShapeType.Rectangle,  // 形状类型
    BehaviorList = new List<string> { "Select", "Produce" }
};

// L 形装饰配置
var decorConfig = new BuildingData
{
    ConfigId = "decor_l_shape",
    Name = "L形装饰",
    Size = (3, 3),
    FootprintShape = FootprintShapeType.LShape,
    BlocksMovement = true
};

// 自定义形状（镂空建筑）
var customBuilding = new BuildingData
{
    ConfigId = "custom_hollow",
    Name = "镂空建筑",
    Size = (4, 4),
    FootprintShape = FootprintShapeType.Custom,
    // X = 占用, . = 不占用
    ShapeConfig = new[]
    {
        "XXXX",
        "X..X",
        "X..X",
        "XXXX"
    }
};
```

### 2. 创建建筑（享元自动生效）

```csharp
var manager = new BuildingManager();

// 创建 1000 个农田... 但只共享 1 个 Footprint！
for (var i = 0; i < 10; i++)
for (var j = 0; j < 100; j++)
{
    var building = manager.CreateBuilding(
        config: farmConfig,
        x: i, y: j, floor: 0
    );

    // 验证：所有建筑的 Footprint 都是同一个实例
    // building.Footprint == sameReference ✓
}
```

### 3. 直接使用 FootprintFactory（高级）

```csharp
// 获取矩形占位
var rectFootprint = FootprintFactory.Instance.GetRectangle(
    configName: "my_rect",
    width: 3,
    height: 3
);

// 自定义形状
var customFootprint = FootprintFactory.Instance.GetOrCreate(
    configName: "cross_3x3",
    configCreator: () => new FootprintConfig
    {
        Name = "cross_3x3",
        ShapeType = FootprintShapeType.Cross,
        Width = 3,
        Height = 3
    }
);

// 场景切换时清除缓存
FootprintFactory.Instance.ClearCache();
```

---

## 形状类型说明

| 形状类型 | 图示 (3x3) | 适用场景 |
|---------|-----------|----------|
| `Rectangle` | `XXX / XXX / XXX` | 大多数建筑（默认） |
| `Cross` | `.X. / XXX / .X.` | 装饰物、柱子 |
| `LShape` | `XXX / X.. / X..` | 角落建筑、围墙 |
| `Custom` | 配置 ShapeConfig | 异形建筑、镂空 |

### Custom 形状配置示例

```csharp
// 心形（简化版）
ShapeConfig = new[]
{
    ".XX.",
    "XXXX",
    "XXXX",
    ".XX."
};

// 马蹄形
ShapeConfig = new[]
{
    "XXX",
    "X.X",
    "XXX"
};
```

---

## 与地图系统集成

```csharp
var map = new LayeredGridMap(width: 50, height: 50, floors: 3);
var building = manager.CreateBuilding(farmConfig, x: 10, y: 10);

// 放置建筑（使用 Footprint 检测碰撞）
if (map.CanPlace(building.Footprint, 10, 10, floor: 0))
{
    map.PlaceBuilding(building.Footprint, 10, 10, floor: 0, building);
    building.IsPlaced = true;
}
```

---

## 设计要点

### ✅ 为什么这是正确的享元模式？

1. **不可变性**：所有 `IFootprint` 实现类都是不可变的
   - 没有 setter，只有 getter
   - 创建后无法修改形状

2. **内部状态 vs 外部状态**
   - 内部状态（享元）：形状、尺寸、名称 → 存储在 Footprint
   - 外部状态（上下文）：位置、旋转、拥有者 → 存储在 BuildingUnit

3. **按配置共享**
   - 相同 `ConfigId` 的建筑必然共享同一个 Footprint
   - 不同 ConfigId 即使形状相同也不共享（便于后续扩展配置差异）

### ❌ 常见反模式

```csharp
// 错误：每个建筑 new 一个 Footprint
var building = new BuildingUnit();
building.Footprint = new RectangleFootprint(2, 2);  // 内存浪费！

// 正确：通过工厂获取
building.Footprint = FootprintFactory.Instance.GetRectangle("farm_001", 2, 2);
```

---

## 缓存管理

```csharp
// 获取缓存统计
var stats = FootprintFactory.Instance.GetCacheStats();
Console.WriteLine($"缓存 Footprint 数量: {stats.count}");

// 场景切换时清除（释放内存）
FootprintFactory.Instance.ClearCache();

// 也可以注入自定义工厂（便于单元测试）
var testFactory = new FootprintFactory();
var builder = new BuildingBuilder(behaviorFactory, testFactory);
```
