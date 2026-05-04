# 建筑系统 (Building System)

## 架构设计理念

**微内核 + 享元行为 + 数据逻辑分离 + 配置驱动**

> 组合优于继承。90% 的建筑不需要继承，只要行为组合不同就行。
>
> **享元核心：1000 个建筑 = 1 套行为实例，节省 99.9% 内存**

## 核心架构图

```
┌─────────────────────────────────────────────────────────────────┐
│                    BehaviorFactory (享元工厂)                     │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐            │
│  │ SelectBehavior│  │ ProduceBehavior│  │ UpgradeBehavior│  ...  │
│  │  (1 Instance) │  │  (1 Instance) │  │  (1 Instance) │         │
│  └───────┬──────┘  └───────┬──────┘  └───────┬──────┘            │
└─────────────────────────────┼──────────────────┼───────────────────┘
                              │                  │
                ┌─────────────┼──────────────────┼─────────────────┐
                │ BuildingUnit 1                  │                  │
                │   ├─ Id/Position/Floor         │                  │
                │   ├─ SelectBehaviorData        │                  │
                │   └─ ProduceBehaviorData       │                  │
                ├─────────────────────────────────────────────────────┤
                │ BuildingUnit 2                                     │
                │   ├─ Id/Position/Floor                             │
                │   ├─ SelectBehaviorData                            │
                │   └─ ProduceBehaviorData                           │
                ├─────────────────────────────────────────────────────┤
                │ BuildingUnit 3                                     │
                │ ... (1000 个建筑只复制数据，不复制逻辑)            │
                └─────────────────────────────────────────────────────┘
                                    │
                                    ▼
                    ┌───────────────────────────┐
                    │    CommunicationBus       │
                    │  Event / Push / Query     │
                    └───────────────────────────┘
```

## 目录结构

```
Building/
├── IBuildingUnit.cs       # 建筑单元接口
├── IBehavior.cs           # 行为附件接口（无状态）
├── BuildingUnit.cs        # 建筑核心单元实现
├── BuildingData.cs        # 建筑配置数据
├── BuildingBuilder.cs     # 建造者（配置驱动注入享元行为）
├── BuildingManager.cs     # 建筑管理器（CRUD + 遍历）
├── BehaviorFactory.cs     # 行为享元工厂 + 事件常量定义
├── Behaviors.cs           # 8种基础行为实现（全享元）
├── InteractionPipeline.cs # 建筑交互管线（复用 PipelineHub）
└── README.md             # 本文档
```

## 快速开始

### 1. 定义建筑配置（推荐用配置表加载）

```csharp
// 普通农田配置
var farmConfig = new BuildingData
{
    ConfigId = "farm_001",
    Name = "普通农田",
    Size = (width: 2, height: 2),
    FootprintShape = FootprintShapeType.Rectangle,  // 占位形状
    CanMove = true,
    CanRotate = true,
    CanStore = false,
    CanSell = true,

    // ✅ 核心：行为列表决定建筑有什么功能
    // 所有行为都是享元，全局只有一个实例
    BehaviorList = new List<string>
    {
        "Select",   // 可选择
        "Place",    // 可放置
        "Produce",  // 可生产
        "Upgrade"   // 可升级
    },

    // 自定义配置（生产行为读取）
    CustomConfig =
    {
        ["ProduceInterval"] = 30f,    // 30秒产出一次
        ["ProduceAmount"] = 10        // 每次产出10金币
    }
};
```

### 2. 创建建筑（享元自动生效）

```csharp
var manager = new BuildingManager();

// 通过配置创建建筑（建造者自动注入享元行为）
// ✅ 1000 个农田共享 4 个行为实例
for (var i = 0; i < 1000; i++)
{
    var farm = manager.CreateBuilding(
        config: farmConfig,
        x: i, y: 10,
        floor: 0,
        rotation: 0
    );
}

// 建筑创建完成后，行为都是共享的享元实例
// 不需要写任何子类！
```

### 3. 使用行为功能（数据 + 逻辑分离）

```csharp
var farm = manager.GetBuilding(1);

// 选中建筑（享元行为 + 独有数据）
var selectBehavior = farm.GetBehavior<SelectBehavior>();
var selectData = farm.GetBehaviorData<SelectBehaviorData>("Select");
selectBehavior?.SetSelected(farm, selectData, true);

// 生产行为（每帧自动更新）
var produceBehavior = farm.GetBehavior<ProduceBehavior>();
var produceData = farm.GetBehaviorData<ProduceBehaviorData>("Produce");
produceData.ProductionInterval = 30f;

// 每帧 Update 驱动所有可更新行为
manager.UpdateAll(deltaTime: 0.16f);

// 处理推送的进度数据（如 UI 更新）
CommunicationBus.Instance.ProcessData(farm);
```

## 核心设计原则

### ✅ 数据与逻辑分离

| 层级 | 存储内容 | 生命周期 |
|------|---------|---------|
| `IBehavior` | 纯逻辑、无状态 | 全局单例（享元） |
| `BehaviorData` | 状态数据 | 每个建筑独有 |
| `BuildingUnit` | 位置、尺寸、引用 | 每个建筑独有 |

### ✅ 享元模式内存对比

| 场景 | 传统方案 | 享元方案 | 节省 |
|------|---------|---------|------|
| 1000 个农田，4 种行为 | 4000 个行为实例 | **4 个行为实例** | **99.9%** |
| 8 种内置行为全局 | 8 × N 个实例 | **8 个实例（永远）** | **无限** |

### ✅ 行为方法签名

```csharp
// 无状态行为：所有必要数据都通过参数传入
public interface IBehavior
{
    string Name { get; }
    BehaviorData CreateData();
    void OnInitialize(IBuildingUnit building, BehaviorData data);
    void OnDestroy(IBuildingUnit building, BehaviorData data);
}

// 可更新行为
public interface IUpdatableBehavior : IBehavior
{
    void OnUpdate(IBuildingUnit building, BehaviorData data, float deltaTime);
}
```

## 内置 8 种基础行为（全享元）

| 行为名称 | 功能 | 数据类 | 通讯方式 |
|---------|------|--------|---------|
| `SelectBehavior` | 选中/取消选中 | `SelectBehaviorData` | Event + Push |
| `MoveBehavior` | 拖拽移动 | `MoveBehaviorData` | Event |
| `RotateBehavior` | 旋转建筑 | `EmptyBehaviorData` | Event + Push |
| `PlaceBehavior` | 放置确认 | `EmptyBehaviorData` | Event |
| `StoreBehavior` | 收纳回仓库 | `EmptyBehaviorData` | Event |
| `SellBehavior` | 出售建筑 | `EmptyBehaviorData` | Event |
| `UpgradeBehavior` | 升级进度 | `UpgradeBehaviorData` | Push + Event |
| `ProduceBehavior` | 生产产出 | `ProduceBehaviorData` | Push + Event |

## 事件常量 (BuildingEvents)

```csharp
BuildingEvents.SelectionChanged   // 选中状态变化
BuildingEvents.MoveStateChanged   // 移动状态变化
BuildingEvents.RotationChanged    // 旋转变化
BuildingEvents.Placed            // 放置完成
BuildingEvents.Stored            // 收纳完成
BuildingEvents.Sold              // 出售完成
BuildingEvents.LevelUp           // 升级完成
BuildingEvents.UpgradeProgress   // 升级进度
BuildingEvents.Produced          // 生产完成
BuildingEvents.ProduceProgress   // 生产进度
```

## 交互管线（基于 PipelineHub）

| 管线名称 | 用途 |
|---------|------|
| `ClickSelect` | 点击选择建筑流程 |
| `DragMove` | 拖拽移动 + 碰撞检测流程 |
| `ShopBuy` | 商店购买创建建筑流程 |
| `Store` | 收纳回仓库流程 |
| `Sell` | 出售返还资源流程 |

```csharp
// 使用示例
var pipeline = BuildingInteractionPipelines.CreateDragMovePipeline();
var result = pipeline.Execute(subject: building, initialData);
```

## 自定义行为扩展

需要新增建筑功能时，**不要继承 BuildingUnit**，而是新增行为：

```csharp
// ✅ 正确方式：新增无状态行为
public class AnimalCareBehavior : IUpdatableBehavior
{
    public string Name => "AnimalCare";

    // 创建对应的数据类
    public BehaviorData CreateData() => new AnimalCareData();

    public void OnInitialize(IBuildingUnit building, BehaviorData data)
    {
        // 初始化，注册事件监听、查询处理器等
    }

    public void OnUpdate(IBuildingUnit building, BehaviorData data, float deltaTime)
    {
        var careData = (AnimalCareData)data;
        // 每帧更新逻辑（喂食、收蛋等）
        // 所有状态都存在 careData 中
    }

    public void OnDestroy(IBuildingUnit building, BehaviorData data)
    {
        // 清理资源
    }
}

// 对应的数据类（只存储状态）
public class AnimalCareData : BehaviorData
{
    public int AnimalCount { get; set; }
    public float FeedTimer { get; set; }
}

// ✅ 注册到工厂（全局只创建一个实例）
// 然后配置里加行为名就行，建筑系统完全不需要改
var barnConfig = new BuildingData
{
    BehaviorList = new List<string> { "Select", "AnimalCare" }
};
```

## 常见问题

### Q: 什么情况才需要继承？

**A: 只有当建筑有独有的配置字段时**，才继承 `BuildingData`（不是 BuildingUnit）：

```csharp
// 只有动物棚需要这些特殊字段
public class AnimalBarnConfig : BuildingData
{
    public int AnimalCapacity { get; set; }
    public string[] AllowedAnimalTypes { get; set; }
}
```

### Q: 行为之间怎么通信？

**A: 通过 CommunicationBus，不直接引用：**

```csharp
// 生产完成发事件，UI 系统监听这个事件
CommunicationBus.Instance.Publish(
    BuildingEvents.Produced,
    new { BuildingId = building.Id, Amount = 10 }
);
```

### Q: 行为有状态吗？

**A: 行为本身无状态，所有状态都存在 `BehaviorData` 中：**

```csharp
// ❌ 错误：行为内部存储状态
public class ProduceBehavior
{
    private float _timer;  // 会被所有建筑共享！
}

// ✅ 正确：状态存在数据类中
public class ProduceBehaviorData : BehaviorData
{
    public float Timer { get; set; }  // 每个建筑独有
}
```

---

**记住：组合优于继承。数据与逻辑分离。享元节省内存。**
