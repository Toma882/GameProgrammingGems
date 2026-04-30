/*
 * 供需平衡引擎 Supply-Demand Engine
 * 时间复杂度: O(n) 单步更新, n=商品种类
 *
 * 经营游戏核心用途:
 *   - 动态价格系统: 供需关系决定价格波动
 *   - 通货膨胀/紧缩控制: 经济系统稳定性
 *   - NPC 交易行为: 商人根据市场情况买卖
 *   - 任务奖励调整: 稀有商品奖励更高
 */

using System;
using System.Collections.Generic;

namespace GPGems.Manor;

/// <summary>
/// 商品数据
/// </summary>
public class Commodity
{
    /// <summary>商品 ID</summary>
    public string Id { get; }

    /// <summary>商品名称</summary>
    public string Name { get; }

    /// <summary>基础价格（供需平衡时的价格）</summary>
    public float BasePrice { get; set; }

    /// <summary>当前市场价格</summary>
    public float CurrentPrice { get; set; }

    /// <summary>供给量（单位时间的产量）</summary>
    public float Supply { get; set; }

    /// <summary>需求量（单位时间的消耗量）</summary>
    public float Demand { get; set; }

    /// <summary>库存总量</summary>
    public float Stock { get; set; }

    /// <summary>价格弹性（供给变化对价格的影响程度）</summary>
    public float PriceElasticity { get; set; } = 0.5f;

    /// <summary>价格下限</summary>
    public float MinPrice { get; set; } = 1.0f;

    /// <summary>价格上限</summary>
    public float MaxPrice { get; set; } = float.MaxValue;

    /// <summary>价格波动率（平滑因子，越小越稳定）</summary>
    public float Volatility { get; set; } = 0.1f;

    public Commodity(string id, string name, float basePrice)
    {
        Id = id;
        Name = name;
        BasePrice = basePrice;
        CurrentPrice = basePrice;
    }

    /// <summary>供需比率</summary>
    public float SupplyDemandRatio => Demand > 0 ? Supply / Demand : float.MaxValue;
}

/// <summary>
/// 供需平衡引擎
/// 模拟动态经济系统，价格随供需关系自动调整
/// </summary>
public class SupplyDemandEngine
{
    #region 字段与属性

    private readonly Dictionary<string, Commodity> _commodities;
    private readonly RingBuffer<float> _priceHistory;
    private float _inflationRate;
    private float _moneySupply;

    /// <summary>通货膨胀率</summary>
    public float InflationRate => _inflationRate;

    /// <summary>货币供应量</summary>
    public float MoneySupply
    {
        get => _moneySupply;
        set => _moneySupply = value;
    }

    /// <summary>商品数量</summary>
    public int CommodityCount => _commodities.Count;

    /// <summary>价格历史记录长度</summary>
    public int PriceHistoryLength => _priceHistory.Count;

    #endregion

    #region 构造函数

    public SupplyDemandEngine(int historyLength = 100)
    {
        _commodities = new Dictionary<string, Commodity>();
        _priceHistory = new RingBuffer<float>(historyLength);
        _inflationRate = 0f;
        _moneySupply = 10000f;
    }

    #endregion

    #region 商品管理

    /// <summary>
    /// 注册商品
    /// </summary>
    public void RegisterCommodity(string id, string name, float basePrice)
    {
        _commodities[id] = new Commodity(id, name, basePrice);
    }

    /// <summary>
    /// 注册商品
    /// </summary>
    public void RegisterCommodity(Commodity commodity)
    {
        _commodities[commodity.Id] = commodity;
    }

    /// <summary>
    /// 获取商品信息
    /// </summary>
    public Commodity? GetCommodity(string id)
    {
        return _commodities.TryGetValue(id, out var c) ? c : null;
    }

    /// <summary>
    /// 获取所有商品
    /// </summary>
    public IEnumerable<Commodity> GetAllCommodities()
    {
        return _commodities.Values;
    }

    #endregion

    #region 核心供需算法

    /// <summary>
    /// 单步更新经济状态
    /// </summary>
    /// <param name="deltaTime">时间步长</param>
    public void Update(float deltaTime = 1.0f)
    {
        // 计算平均价格（用于计算通胀）
        float avgPrice = 0f;
        int count = 0;

        foreach (var commodity in _commodities.Values)
        {
            // 计算目标价格
            float targetPrice = CalculateTargetPrice(commodity);

            // 平滑过渡到目标价格（避免突变）
            float priceDiff = targetPrice - commodity.CurrentPrice;
            commodity.CurrentPrice += priceDiff * commodity.Volatility * deltaTime;

            // 限制在价格范围内
            commodity.CurrentPrice = Math.Clamp(
                commodity.CurrentPrice,
                commodity.MinPrice,
                commodity.MaxPrice);

            // 更新库存（供给 - 需求）
            commodity.Stock += (commodity.Supply - commodity.Demand) * deltaTime;

            // 库存为负时会进一步推高价格
            if (commodity.Stock < 0)
            {
                float shortageFactor = 1.0f + Math.Abs(commodity.Stock) * 0.01f;
                commodity.CurrentPrice = Math.Min(
                    commodity.CurrentPrice * shortageFactor,
                    commodity.MaxPrice);
                commodity.Stock = 0;  // 不能有负库存
            }

            avgPrice += commodity.CurrentPrice;
            count++;
        }

        // 更新平均价格历史
        if (count > 0)
        {
            avgPrice /= count;
            _priceHistory.Enqueue(avgPrice);

            // 计算通胀率（与初始平均价格比较）
            if (_priceHistory.Count >= 2)
            {
                float oldAvg = _priceHistory[0];
                _inflationRate = (avgPrice - oldAvg) / oldAvg;
            }
        }
    }

    /// <summary>
    /// 计算商品的目标价格（基于供需关系）
    /// </summary>
    private float CalculateTargetPrice(Commodity commodity)
    {
        // 供需比率 = 供给 / 需求
        float ratio = commodity.SupplyDemandRatio;

        // 避免除零
        if (ratio <= 0) ratio = 0.01f;

        // 价格 = 基础价格 * (需求 / 供给)^弹性
        // 需求 > 供给 → 价格上涨
        // 需求 < 供给 → 价格下跌
        float supplyDemandFactor = MathF.Pow(1.0f / ratio, commodity.PriceElasticity);

        // 通胀影响
        float inflationFactor = 1.0f + _inflationRate * 0.1f;

        // 库存影响：库存越多价格越低
        float stockFactor = 1.0f;
        if (commodity.Stock > commodity.Supply * 10)  // 库存超过 10 倍产量
        {
            stockFactor = 1.0f / (1.0f + (commodity.Stock / (commodity.Supply * 100)));
        }

        return commodity.BasePrice * supplyDemandFactor * inflationFactor * stockFactor;
    }

    #endregion

    #region 经济操作

    /// <summary>
    /// 增加供给（如玩家生产了商品）
    /// </summary>
    public void AddSupply(string commodityId, float amount)
    {
        if (_commodities.TryGetValue(commodityId, out var c))
        {
            c.Supply += amount;
        }
    }

    /// <summary>
    /// 增加需求（如 NPC 购买了商品）
    /// </summary>
    public void AddDemand(string commodityId, float amount)
    {
        if (_commodities.TryGetValue(commodityId, out var c))
        {
            c.Demand += amount;
        }
    }

    /// <summary>
    /// 设置供给量
    /// </summary>
    public void SetSupply(string commodityId, float amount)
    {
        if (_commodities.TryGetValue(commodityId, out var c))
        {
            c.Supply = amount;
        }
    }

    /// <summary>
    /// 设置需求量
    /// </summary>
    public void SetDemand(string commodityId, float amount)
    {
        if (_commodities.TryGetValue(commodityId, out var c))
        {
            c.Demand = amount;
        }
    }

    /// <summary>
    /// 买入商品（玩家购买）
    /// </summary>
    public bool Buy(string commodityId, float quantity, out float totalCost)
    {
        totalCost = 0f;
        if (!_commodities.TryGetValue(commodityId, out var c))
            return false;

        if (c.Stock < quantity)
            return false;

        totalCost = c.CurrentPrice * quantity;
        c.Stock -= quantity;

        // 购买行为增加需求预期，推高价格
        c.Demand += quantity * 0.1f;

        return true;
    }

    /// <summary>
    /// 卖出商品（玩家卖出）
    /// </summary>
    public bool Sell(string commodityId, float quantity, out float totalRevenue)
    {
        totalRevenue = 0f;
        if (!_commodities.TryGetValue(commodityId, out var c))
            return false;

        totalRevenue = c.CurrentPrice * quantity;
        c.Stock += quantity;

        // 卖出行为增加供给预期，压低价格
        c.Supply += quantity * 0.1f;

        return true;
    }

    /// <summary>
    /// 调整货币供应量（模拟央行货币政策）
    /// </summary>
    public void AdjustMoneySupply(float delta)
    {
        _moneySupply += delta;

        // 货币供应量变化影响通胀
        // 货币数量论：MV = PQ → P = MV/Q
        // 简化：通胀率与货币供应量增长率正相关
        float supplyFactor = delta / (_moneySupply - delta + 1);
        _inflationRate += supplyFactor * 0.5f;
    }

    #endregion

    #region 经济分析

    /// <summary>
    /// 获取价格趋势
    /// </summary>
    /// <returns>正数 = 上涨，负数 = 下跌</returns>
    public float GetPriceTrend(string commodityId)
    {
        if (!_commodities.TryGetValue(commodityId, out var c))
            return 0f;

        // 趋势 = （当前价格 - 基础价格）/ 基础价格
        return (c.CurrentPrice - c.BasePrice) / c.BasePrice;
    }

    /// <summary>
    /// 获取市场热度（总体供需状况）
    /// </summary>
    public (float avgSupplyDemandRatio, float priceLevel) GetMarketHeat()
    {
        float totalRatio = 0f;
        float totalPrice = 0f;
        int count = 0;

        foreach (var c in _commodities.Values)
        {
            totalRatio += c.SupplyDemandRatio;
            totalPrice += c.CurrentPrice / c.BasePrice;
            count++;
        }

        return count > 0
            ? (totalRatio / count, totalPrice / count)
            : (1.0f, 1.0f);
    }

    /// <summary>
    /// 预测未来价格（简单线性回归）
    /// </summary>
    public float PredictPrice(string commodityId, int stepsAhead = 10)
    {
        if (!_commodities.TryGetValue(commodityId, out var c))
            return 0f;

        // 基于当前趋势外推
        float trend = GetPriceTrend(commodityId);
        float factor = 1.0f + trend * 0.1f * stepsAhead;

        // 趋势不能无限持续，有回归均值的倾向
        float meanReversion = (c.BasePrice - c.CurrentPrice) * 0.01f * stepsAhead;

        return c.CurrentPrice * factor + meanReversion;
    }

    /// <summary>
    /// 获取推荐交易建议
    /// </summary>
    public (string recommendation, float confidence) GetTradingAdvice(string commodityId)
    {
        var c = GetCommodity(commodityId);
        if (c == null)
            return ("未知商品", 0f);

        float trend = GetPriceTrend(commodityId);
        float ratio = c.SupplyDemandRatio;

        // 严重供不应求（建议卖出）
        if (ratio < 0.5f && trend > 0.2f)
            return ("强烈建议卖出（严重短缺，价格高）", 0.9f);

        // 供不应求（建议持有/卖出）
        if (ratio < 0.8f && trend > 0.1f)
            return ("建议卖出（供应紧张，价格偏高）", 0.7f);

        // 严重供过于求（建议买入）
        if (ratio > 2.0f && trend < -0.2f)
            return ("强烈建议买入（严重过剩，价格低）", 0.9f);

        // 供过于求（建议买入）
        if (ratio > 1.5f && trend < -0.1f)
            return ("建议买入（供应充足，价格偏低）", 0.7f);

        // 供需平衡
        return ("持有观望（供需平衡）", 0.5f);
    }

    #endregion
}
