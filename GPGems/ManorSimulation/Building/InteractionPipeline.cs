using GPGems.Core.PipelineHub;

namespace GPGems.ManorSimulation.Building;

/// <summary>
/// 建筑交互管线工厂
/// 依托 Pipeline 框架创建各种建筑交互流程
///
/// 设计原则：不重复造轮子，直接复用 Pipeline �?When+Execute 模式
/// </summary>
public static class BuildingInteractionPipelines
{
    /// <summary>
    /// 创建点击选择管线
    /// </summary>
    public static Pipeline CreateClickSelectPipeline()
    {
        var pipeline = new Pipeline("ClickSelect");
        pipeline.RegisterNodes(new IPipelineNode[]
        {
            new ScreenCaptureNode(),
            new MapPositionCheckNode(),
            new BuildingClickHandleNode(),
            new VisualHighlightNode(),
        });
        pipeline.AddNodes(new[] { "ScreenCapture", "MapPositionCheck", "BuildingClickHandle", "VisualHighlight" });
        return pipeline;
    }

    /// <summary>
    /// 创建拖拽移动管线
    /// </summary>
    public static Pipeline CreateDragMovePipeline()
    {
        var pipeline = new Pipeline("DragMove");
        pipeline.RegisterNodes(new IPipelineNode[]
        {
            new DragCaptureNode(),
            new PositionValidateNode(),
            new CollisionFeedbackNode(),
            new ConfirmPlaceNode(),
        });
        pipeline.AddNodes(new[] { "DragCapture", "PositionValidate", "CollisionFeedback", "ConfirmPlace" });
        return pipeline;
    }

    /// <summary>
    /// 创建商店购买创建管线
    /// </summary>
    public static Pipeline CreateShopBuyPipeline()
    {
        var pipeline = new Pipeline("ShopBuy");
        pipeline.RegisterNodes(new IPipelineNode[]
        {
            new BuyRequestNode(),
            new FindPositionNode(),
            new CreateBuildingNode(),
            new LoadVisualNode(),
            new ShowSelectStateNode(),
            new OpenPanelNode(),
        });
        pipeline.AddNodes(new[] { "BuyRequest", "FindPosition", "CreateBuilding", "LoadVisual", "ShowSelectState", "OpenPanel" });
        return pipeline;
    }

    /// <summary>
    /// 创建收纳管线
    /// </summary>
    public static Pipeline CreateStorePipeline()
    {
        var pipeline = new Pipeline("Store");
        pipeline.RegisterNodes(new IPipelineNode[]
        {
            new StoreRequestNode(),
            new StoreConditionCheckNode(),
            new ConfirmDialogNode(),
            new ExecuteStoreNode(),
        });
        pipeline.AddNodes(new[] { "StoreRequest", "StoreConditionCheck", "ConfirmDialog", "ExecuteStore" });
        return pipeline;
    }

    /// <summary>
    /// 创建出售管线
    /// </summary>
    public static Pipeline CreateSellPipeline()
    {
        var pipeline = new Pipeline("Sell");
        pipeline.RegisterNodes(new IPipelineNode[]
        {
            new SellRequestNode(),
            new SellConditionCheckNode(),
            new CalculateRefundNode(),
            new ConfirmDialogNode(),
            new ExecuteSellNode(),
        });
        pipeline.AddNodes(new[] { "SellRequest", "SellConditionCheck", "CalculateRefund", "ConfirmDialog", "ExecuteSell" });
        return pipeline;
    }
}

#region 基础节点实现（空实现，留待上层填充具体逻辑�?
public class ScreenCaptureNode : PipelineNodeBase
{
    public override string Name => "ScreenCapture";
    public override bool When(PipelineContext context) => true;
    public override Dictionary<string, object> Execute(PipelineContext context) => Output();
}

public class MapPositionCheckNode : PipelineNodeBase
{
    public override string Name => "MapPositionCheck";
    public override bool When(PipelineContext context) => context.Has("grid_x");
    public override Dictionary<string, object> Execute(PipelineContext context) => Output();
}

public class BuildingClickHandleNode : PipelineNodeBase
{
    public override string Name => "BuildingClickHandle";
    public override bool When(PipelineContext context) => context.Has("building_id");
    public override Dictionary<string, object> Execute(PipelineContext context) => Output();
}

public class VisualHighlightNode : PipelineNodeBase
{
    public override string Name => "VisualHighlight";
    public override bool When(PipelineContext context) => true;
    public override Dictionary<string, object> Execute(PipelineContext context) => Output();
}

public class DragCaptureNode : PipelineNodeBase
{
    public override string Name => "DragCapture";
    public override bool When(PipelineContext context) => context.Has("building_id");
    public override Dictionary<string, object> Execute(PipelineContext context) => Output();
}

public class PositionValidateNode : PipelineNodeBase
{
    public override string Name => "PositionValidate";
    public override bool When(PipelineContext context) => true;
    public override Dictionary<string, object> Execute(PipelineContext context) => Output(("can_place", true));
}

public class CollisionFeedbackNode : PipelineNodeBase
{
    public override string Name => "CollisionFeedback";
    public override bool When(PipelineContext context) => true;
    public override Dictionary<string, object> Execute(PipelineContext context) => Output();
}

public class ConfirmPlaceNode : PipelineNodeBase
{
    public override string Name => "ConfirmPlace";
    public override bool When(PipelineContext context) => context.Get<bool>("can_place");
    public override Dictionary<string, object> Execute(PipelineContext context) => Output();
}

public class BuyRequestNode : PipelineNodeBase
{
    public override string Name => "BuyRequest";
    public override bool When(PipelineContext context) => true;
    public override Dictionary<string, object> Execute(PipelineContext context) => Output();
}

public class FindPositionNode : PipelineNodeBase
{
    public override string Name => "FindPosition";
    public override bool When(PipelineContext context) => context.Get<bool>("resource_enough");
    public override Dictionary<string, object> Execute(PipelineContext context) => Output(("found_position", true));
}

public class CreateBuildingNode : PipelineNodeBase
{
    public override string Name => "CreateBuilding";
    public override bool When(PipelineContext context) => context.Get<bool>("found_position");
    public override Dictionary<string, object> Execute(PipelineContext context) => Output(("building_created", true));
}

public class LoadVisualNode : PipelineNodeBase
{
    public override string Name => "LoadVisual";
    public override bool When(PipelineContext context) => context.Get<bool>("building_created");
    public override Dictionary<string, object> Execute(PipelineContext context) => Output(("visual_loaded", true));
}

public class ShowSelectStateNode : PipelineNodeBase
{
    public override string Name => "ShowSelectState";
    public override bool When(PipelineContext context) => context.Get<bool>("visual_loaded");
    public override Dictionary<string, object> Execute(PipelineContext context) => Output();
}

public class OpenPanelNode : PipelineNodeBase
{
    public override string Name => "OpenPanel";
    public override bool When(PipelineContext context) => true;
    public override Dictionary<string, object> Execute(PipelineContext context) => Output();
}

public class StoreRequestNode : PipelineNodeBase
{
    public override string Name => "StoreRequest";
    public override bool When(PipelineContext context) => true;
    public override Dictionary<string, object> Execute(PipelineContext context) => Output();
}

public class StoreConditionCheckNode : PipelineNodeBase
{
    public override string Name => "StoreConditionCheck";
    public override bool When(PipelineContext context) => true;
    public override Dictionary<string, object> Execute(PipelineContext context) => Output(("can_store", true));
}

public class SellRequestNode : PipelineNodeBase
{
    public override string Name => "SellRequest";
    public override bool When(PipelineContext context) => true;
    public override Dictionary<string, object> Execute(PipelineContext context) => Output();
}

public class SellConditionCheckNode : PipelineNodeBase
{
    public override string Name => "SellConditionCheck";
    public override bool When(PipelineContext context) => true;
    public override Dictionary<string, object> Execute(PipelineContext context) => Output(("can_sell", true));
}

public class CalculateRefundNode : PipelineNodeBase
{
    public override string Name => "CalculateRefund";
    public override bool When(PipelineContext context) => context.Get<bool>("can_sell");
    public override Dictionary<string, object> Execute(PipelineContext context) => Output(("refund_amount", 100));
}

public class ConfirmDialogNode : PipelineNodeBase
{
    public override string Name => "ConfirmDialog";
    public override bool When(PipelineContext context) => true;
    public override Dictionary<string, object> Execute(PipelineContext context) => Output(("confirmed", true));
}

public class ExecuteStoreNode : PipelineNodeBase
{
    public override string Name => "ExecuteStore";
    public override bool When(PipelineContext context) => context.Get<bool>("confirmed") && context.Get<bool>("can_store");
    public override Dictionary<string, object> Execute(PipelineContext context) => Output(("stored", true));
}

public class ExecuteSellNode : PipelineNodeBase
{
    public override string Name => "ExecuteSell";
    public override bool When(PipelineContext context) => context.Get<bool>("confirmed");
    public override Dictionary<string, object> Execute(PipelineContext context) => Output(("sold", true));
}

#endregion
