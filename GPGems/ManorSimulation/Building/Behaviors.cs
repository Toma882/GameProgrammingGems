using GPGems.Core;

namespace GPGems.ManorSimulation.Building;

#region 行为数据类（存储状态）

/// <summary>
/// 行为数据基类
/// 存储行为的状态，与行为逻辑分离
/// </summary>
public class BehaviorData
{
}

/// <summary>
/// 无状态行为数据（用于没有状态的行为�?/// </summary>
public sealed class EmptyBehaviorData : BehaviorData
{
    public static readonly EmptyBehaviorData Instance = new();
    private EmptyBehaviorData() { }
}

/// <summary>
/// 选中行为数据
/// </summary>
public class SelectBehaviorData : BehaviorData
{
    public bool IsSelected { get; set; }
}

/// <summary>
/// 移动行为数据
/// </summary>
public class MoveBehaviorData : BehaviorData
{
    public bool IsMoving { get; set; }
}

/// <summary>
/// 升级行为数据
/// </summary>
public class UpgradeBehaviorData : BehaviorData
{
    public int CurrentLevel { get; set; } = 1;
    public int MaxLevel { get; set; } = 10;
    public float Progress { get; set; }
    public bool IsUpgrading { get; set; }
    public float UpgradeDuration { get; set; }
}

/// <summary>
/// 生产行为数据
/// </summary>
public class ProduceBehaviorData : BehaviorData
{
    public float ProductionInterval { get; set; } = 10f;
    public int ProduceAmount { get; set; } = 1;
    public float ProductionProgress { get; set; }
    public float Timer { get; set; }
}

#endregion

#region 无状态行为类（纯逻辑，可享元�?
/// <summary>
/// 选择行为（无状态，可享元）
/// </summary>
public class SelectBehavior : IBehavior
{
    public string Name => "Select";

    public BehaviorData CreateData() => new SelectBehaviorData();

    public void OnInitialize(IBuildingUnit building, BehaviorData data)
    {
    }

    public void SetSelected(IBuildingUnit building, SelectBehaviorData data, bool value)
    {
        if (data.IsSelected == value) return;
        data.IsSelected = value;

        CommunicationBus.Instance.Publish(BuildingEvents.SelectionChanged, new
        {
            BuildingId = building.Id,
            IsSelected = data.IsSelected
        });

        CommunicationBus.Instance.PushData(
            subscriber: building,
            dataType: BuildingPushTypes.Highlight,
            data: new { IsHighlighted = data.IsSelected }
        );
    }

    public void OnDestroy(IBuildingUnit building, BehaviorData data)
    {
    }
}

/// <summary>
/// 移动行为（无状态，可享元）
/// </summary>
public class MoveBehavior : IBehavior
{
    public string Name => "Move";

    public BehaviorData CreateData() => new MoveBehaviorData();

    public void OnInitialize(IBuildingUnit building, BehaviorData data)
    {
        CommunicationBus.Instance.AddQueryDelegate(
            subscriber: building,
            dataType: BuildingQueries.CanMove,
            func: args => building.CanMove
        );
    }

    public void SetMoving(IBuildingUnit building, MoveBehaviorData data, bool value)
    {
        if (data.IsMoving == value) return;
        data.IsMoving = value;

        CommunicationBus.Instance.Publish(BuildingEvents.MoveStateChanged, new
        {
            BuildingId = building.Id,
            IsMoving = data.IsMoving
        });
    }

    public void OnDestroy(IBuildingUnit building, BehaviorData data)
    {
    }
}

/// <summary>
/// 旋转行为（无状态，可享元）
/// </summary>
public class RotateBehavior : IBehavior
{
    public string Name => "Rotate";

    public BehaviorData CreateData() => EmptyBehaviorData.Instance; // 无额外状�?
    public void OnInitialize(IBuildingUnit building, BehaviorData data)
    {
    }

    public void Rotate(IBuildingUnit building, int deltaAngle)
    {
        var newRotation = (building.Rotation + deltaAngle) % 360;
        if (newRotation < 0) newRotation += 360;
        building.Rotation = newRotation;

        CommunicationBus.Instance.Publish(BuildingEvents.RotationChanged, new
        {
            BuildingId = building.Id,
            Rotation = newRotation
        });

        CommunicationBus.Instance.PushData(
            subscriber: building,
            dataType: BuildingPushTypes.PositionUpdate,
            data: new { building.Rotation }
        );
    }

    public void OnDestroy(IBuildingUnit building, BehaviorData data)
    {
    }
}

/// <summary>
/// 放置行为（无状态，可享元）
/// </summary>
public class PlaceBehavior : IBehavior
{
    public string Name => "Place";

    public BehaviorData CreateData() => EmptyBehaviorData.Instance; // 无额外状�?
    public void OnInitialize(IBuildingUnit building, BehaviorData data)
    {
        CommunicationBus.Instance.AddQueryDelegate(
            subscriber: building,
            dataType: BuildingQueries.CanPlace,
            func: args => building.IsPlaced
        );
    }

    public void ConfirmPlace(IBuildingUnit building)
    {
        building.IsPlaced = true;

        CommunicationBus.Instance.Publish(BuildingEvents.Placed, new
        {
            BuildingId = building.Id,
            building.GridPosition,
            building.FloorIndex
        });
    }

    public void OnDestroy(IBuildingUnit building, BehaviorData data)
    {
    }
}

/// <summary>
/// 收纳行为（无状态，可享元）
/// </summary>
public class StoreBehavior : IBehavior
{
    public string Name => "Store";

    public BehaviorData CreateData() => EmptyBehaviorData.Instance; // 无额外状�?
    public void OnInitialize(IBuildingUnit building, BehaviorData data)
    {
    }

    public void Store(IBuildingUnit building)
    {
        building.IsPlaced = false;

        CommunicationBus.Instance.Publish(BuildingEvents.Stored, new
        {
            BuildingId = building.Id
        });
    }

    public void OnDestroy(IBuildingUnit building, BehaviorData data)
    {
    }
}

/// <summary>
/// 出售行为（无状态，可享元）
/// </summary>
public class SellBehavior : IBehavior
{
    public string Name => "Sell";

    public BehaviorData CreateData() => EmptyBehaviorData.Instance; // 无额外状�?
    public void OnInitialize(IBuildingUnit building, BehaviorData data)
    {
        CommunicationBus.Instance.AddQueryDelegate(
            subscriber: building,
            dataType: BuildingQueries.GetSellRefund,
            func: args => 100
        );
    }

    public void Sell(IBuildingUnit building, int resourceAmount)
    {
        CommunicationBus.Instance.Publish(BuildingEvents.Sold, new
        {
            BuildingId = building.Id,
            RefundAmount = resourceAmount
        });

        building.Destroy();
    }

    public void OnDestroy(IBuildingUnit building, BehaviorData data)
    {
    }
}

/// <summary>
/// 升级行为（无状态，可享元）
/// </summary>
public class UpgradeBehavior : IUpdatableBehavior
{
    public string Name => "Upgrade";

    public BehaviorData CreateData() => new UpgradeBehaviorData();

    public void OnInitialize(IBuildingUnit building, BehaviorData data)
    {
        CommunicationBus.Instance.SubscribeProgress(building);
    }

    public void StartUpgrade(UpgradeBehaviorData data, float duration)
    {
        if (data.CurrentLevel >= data.MaxLevel) return;
        data.CurrentLevel++;
        data.IsUpgrading = true;
        data.Progress = 0;
        data.UpgradeDuration = duration;
    }

    public void OnUpdate(IBuildingUnit building, BehaviorData data, float deltaTime)
    {
        var upgradeData = (UpgradeBehaviorData)data;
        if (!upgradeData.IsUpgrading) return;

        upgradeData.Progress += deltaTime / upgradeData.UpgradeDuration;

        CommunicationBus.Instance.PushData(
            subscriber: building,
            dataType: BuildingEvents.UpgradeProgress,
            data: new { BuildingId = building.Id, Progress = upgradeData.Progress }
        );

        if (upgradeData.Progress >= 1f)
        {
            upgradeData.IsUpgrading = false;
            upgradeData.Progress = 1f;

            CommunicationBus.Instance.Publish(BuildingEvents.LevelUp, new
            {
                BuildingId = building.Id,
                NewLevel = upgradeData.CurrentLevel
            });
        }
    }

    public void OnDestroy(IBuildingUnit building, BehaviorData data)
    {
        CommunicationBus.Instance.UnsubscribeProgress(building);
    }
}

/// <summary>
/// 生产行为（无状态，可享元）
/// </summary>
public class ProduceBehavior : IUpdatableBehavior
{
    public string Name => "Produce";

    public BehaviorData CreateData() => new ProduceBehaviorData();

    public void OnInitialize(IBuildingUnit building, BehaviorData data)
    {
        CommunicationBus.Instance.SubscribeProgress(building);
    }

    public void OnUpdate(IBuildingUnit building, BehaviorData data, float deltaTime)
    {
        var produceData = (ProduceBehaviorData)data;
        if (!building.IsPlaced) return;

        produceData.Timer += deltaTime;
        produceData.ProductionProgress = produceData.Timer / produceData.ProductionInterval;

        CommunicationBus.Instance.PushData(
            subscriber: building,
            dataType: BuildingEvents.ProduceProgress,
            data: new { BuildingId = building.Id, Progress = produceData.ProductionProgress }
        );

        if (produceData.Timer >= produceData.ProductionInterval)
        {
            produceData.Timer = 0;
            produceData.ProductionProgress = 0;

            CommunicationBus.Instance.Publish(BuildingEvents.Produced, new
            {
                BuildingId = building.Id,
                Amount = produceData.ProduceAmount
            });
        }
    }

    public void Collect(ProduceBehaviorData data)
    {
        data.Timer = 0;
        data.ProductionProgress = 0;
    }

    public void OnDestroy(IBuildingUnit building, BehaviorData data)
    {
        CommunicationBus.Instance.UnsubscribeProgress(building);
    }
}

#endregion
