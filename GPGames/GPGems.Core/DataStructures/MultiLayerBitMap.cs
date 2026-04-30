/*
 * 多层位图 MultiLayerBitMap
 * 时间复杂度: O(1) 单格查询/修改, O(w) 行掩码连续扫描
 *
 * 核心用途:
 *   - 多维度标记: 同一网格的不同属性
 *   - 跨层操作: 多层合并、冲突检测
 *   - 批量查询: 跨层连通性、区域统计
 */

using System;
using System.Numerics;

namespace GPGems.Core.DataStructures;

/// <summary>
/// 多层位图 - 基于多个 BitMap2D 组合实现
/// </summary>
public class MultiLayerBitMap
{
    #region 常量定义

    /// <summary>默认层数</summary>
    public const int DefaultLayerCount = 8;

    #endregion

    #region 字段与属性

    private readonly BitMap2D[] _layers;
    private readonly int _width;
    private readonly int _height;
    private readonly int _layerCount;

    public int Width => _width;
    public int Height => _height;
    public int LayerCount => _layerCount;

    #endregion

    #region 构造函数

    public MultiLayerBitMap(int width, int height, int layerCount = DefaultLayerCount)
    {
        if (layerCount <= 0)
            throw new ArgumentException("Layer count must be positive", nameof(layerCount));

        _width = width;
        _height = height;
        _layerCount = layerCount;
        _layers = new BitMap2D[layerCount];

        for (int i = 0; i < layerCount; i++)
            _layers[i] = new BitMap2D(width, height);
    }

    #endregion

    #region 单层操作（委托给 BitMap2D）

    /// <summary>
    /// 获取指定层的位图实例
    /// </summary>
    public BitMap2D GetLayer(int layer)
    {
        ValidateLayer(layer);
        return _layers[layer];
    }

    /// <summary>
    /// 设置指定位置的值
    /// </summary>
    public void Set(int layer, int x, int y, bool value)
    {
        ValidateLayer(layer);
        _layers[layer].Set(x, y, value);
    }

    /// <summary>
    /// 获取指定位置的值
    /// </summary>
    public bool Get(int layer, int x, int y)
    {
        ValidateLayer(layer);
        return _layers[layer].Get(x, y);
    }

    /// <summary>
    /// 批量设置矩形区域
    /// </summary>
    public void SetRect(int layer, int minX, int minY, int maxX, int maxY, bool value)
    {
        ValidateLayer(layer);
        _layers[layer].SetRect(minX, minY, maxX, maxY, value);
    }

    /// <summary>
    /// 检查矩形区域是否全部为空
    /// </summary>
    public bool IsRectEmpty(int layer, int minX, int minY, int maxX, int maxY)
    {
        ValidateLayer(layer);
        return _layers[layer].IsRectEmpty(minX, minY, maxX, maxY);
    }

    #endregion

    #region 跨层操作

    /// <summary>
    /// 检查多层是否有冲突（任意层有值即冲突）
    /// </summary>
    public bool HasConflict(int[] layers, int x, int y)
    {
        foreach (var layer in layers)
        {
            if (Get(layer, x, y))
                return true;
        }
        return false;
    }

    /// <summary>
    /// 检查多层在矩形区域是否有冲突
    /// </summary>
    public bool HasConflictInRect(int[] layers, int minX, int minY, int maxX, int maxY)
    {
        foreach (var layer in layers)
        {
            if (!_layers[layer].IsRectEmpty(minX, minY, maxX, maxY))
                return true;
        }
        return false;
    }

    /// <summary>
    /// 合并多层为目标层（OR 操作）
    /// </summary>
    public void CombineLayers(int[] sourceLayers, int targetLayer)
    {
        ValidateLayer(targetLayer);
        _layers[targetLayer].Clear();

        foreach (var source in sourceLayers)
        {
            ValidateLayer(source);
            _layers[targetLayer].Or(_layers[source]);
        }
    }

    /// <summary>
    /// 多层做 AND 运算到目标层
    /// </summary>
    public void IntersectLayers(int[] sourceLayers, int targetLayer)
    {
        ValidateLayer(targetLayer);
        if (sourceLayers.Length == 0)
        {
            _layers[targetLayer].Clear();
            return;
        }

        // 拷贝第一层，然后依次 AND
        _layers[targetLayer].Clear();
        _layers[targetLayer].Or(_layers[sourceLayers[0]]);

        for (int i = 1; i < sourceLayers.Length; i++)
        {
            _layers[targetLayer].And(_layers[sourceLayers[i]]);
        }
    }

    /// <summary>
    /// 检查任意层在该位置有值
    /// </summary>
    public bool Any(int x, int y)
    {
        for (int i = 0; i < _layerCount; i++)
        {
            if (_layers[i].Get(x, y))
                return true;
        }
        return false;
    }

    /// <summary>
    /// 检查所有层在该位置都有值
    /// </summary>
    public bool All(int x, int y)
    {
        for (int i = 0; i < _layerCount; i++)
        {
            if (!_layers[i].Get(x, y))
                return false;
        }
        return true;
    }

    #endregion

    #region 单层功能委托

    /// <summary>
    /// 在指定行中查找第一个连续 width 个空位的起始位置
    /// </summary>
    public int FindContinuousEmptyInRow(int layer, int y, int width)
    {
        ValidateLayer(layer);
        return _layers[layer].FindContinuousEmptyInRow(y, width);
    }

    /// <summary>
    /// 查找 w × h 的空矩形区域
    /// </summary>
    public (int x, int y) FindEmptyRect(int layer, int rectWidth, int rectHeight)
    {
        ValidateLayer(layer);
        return _layers[layer].FindEmptyRect(rectWidth, rectHeight);
    }

    /// <summary>
    /// 4 邻接连通性检测
    /// </summary>
    public bool Is4Connected(int layer, int x1, int y1, int x2, int y2)
    {
        ValidateLayer(layer);
        return _layers[layer].Is4Connected(x1, y1, x2, y2);
    }

    /// <summary>
    /// 8 邻接连通性检测
    /// </summary>
    public bool Is8Connected(int layer, int x1, int y1, int x2, int y2)
    {
        ValidateLayer(layer);
        return _layers[layer].Is8Connected(x1, y1, x2, y2);
    }

    /// <summary>
    /// 统计某层已设置的位数
    /// </summary>
    public int CountSetBits(int layer)
    {
        ValidateLayer(layer);
        return _layers[layer].CountSetBits();
    }

    /// <summary>
    /// 清空整层
    /// </summary>
    public void ClearLayer(int layer)
    {
        ValidateLayer(layer);
        _layers[layer].Clear();
    }

    /// <summary>
    /// 清空所有层
    /// </summary>
    public void ClearAll()
    {
        for (int i = 0; i < _layerCount; i++)
            _layers[i].Clear();
    }

    #endregion

    #region 跨层统计

    /// <summary>
    /// 统计所有层在该位置的设置层数
    /// </summary>
    public int CountLayersAt(int x, int y)
    {
        int count = 0;
        for (int i = 0; i < _layerCount; i++)
        {
            if (_layers[i].Get(x, y))
                count++;
        }
        return count;
    }

    /// <summary>
    /// 获取在该位置有值的所有层索引
    /// </summary>
    public int[] GetLayersAt(int x, int y)
    {
        var result = new System.Collections.Generic.List<int>();
        for (int i = 0; i < _layerCount; i++)
        {
            if (_layers[i].Get(x, y))
                result.Add(i);
        }
        return result.ToArray();
    }

    /// <summary>
    /// 统计所有层总设置位数
    /// </summary>
    public int CountAllSetBits()
    {
        int total = 0;
        for (int i = 0; i < _layerCount; i++)
            total += _layers[i].CountSetBits();
        return total;
    }

    #endregion

    #region 辅助方法

    private void ValidateLayer(int layer)
    {
        if (layer < 0 || layer >= _layerCount)
            throw new ArgumentOutOfRangeException(nameof(layer), $"Layer must be 0-{_layerCount - 1}");
    }

    #endregion
}
