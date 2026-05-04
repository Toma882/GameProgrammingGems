using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using GPGems.ManorSimulation;
using System.Numerics;
using GPGems.Core.Math;


using Rectangle = System.Windows.Shapes.Rectangle;

namespace GPGems.Visualization.ManorGameDemos
{
    /// <summary>
    /// 场景1: 游客人流演示
    /// 算法：流场寻路 + ORCA避障
    /// </summary>
    public class VisitorFlowScene : IDemoScene
    {
        private VisitorFlowSystem _visitorSystem = null!;
        private int _mapWidth = 100;
        private int _mapHeight = 50;

        public void Reset(int count, float speed)
        {
            // 初始化算法门面
            var facade = ManorAlgorithmFacade.Instance;
            facade.Initialize(_mapWidth, _mapHeight);

            // 创建游客人流系统
            _visitorSystem = facade.CreateVisitorFlowSystem(
                visitorCount: count,
                entranceX: 0,
                entranceY: 23,
                speedMultiplier: speed);
        }

        public void Update(float deltaTime)
        {
            _visitorSystem.Update(deltaTime);
        }

        public void RenderBackground(Canvas canvas, List<Shape> cache)
        {
            float scale = 8f;
            canvas.Width = _mapWidth * scale;
            canvas.Height = _mapHeight * scale;

            // 道路/广场
            AddRect(canvas, cache, 0, 15, 15, 20, "#6B8E23", scale);    // 入口广场
            AddRect(canvas, cache, 0, 23, 100, 4, "#DAA520", scale);    // 主路
            AddRect(canvas, cache, 20, 8, 35, 4, "#DAA520", scale);     // 到A的路
            AddRect(canvas, cache, 20, 38, 35, 4, "#DAA520", scale);    // 到B的路
            AddRect(canvas, cache, 45, 5, 10, 10, "#8FBC8F", scale);    // 景点A广场
            AddRect(canvas, cache, 45, 35, 10, 10, "#8FBC8F", scale);    // 景点B广场
            AddRect(canvas, cache, 90, 20, 10, 10, "#CD853F", scale);   // 出口广场

            // 景点建筑
            AddRect(canvas, cache, 47, 7, 6, 6, "#8B4513", scale);
            AddRect(canvas, cache, 47, 37, 6, 6, "#8B4513", scale);

            // 标注
            AddText(canvas, cache, "入口", 2, 22, scale, Brushes.White);
            AddText(canvas, cache, "景点A", 46, 12, scale, Brushes.White);
            AddText(canvas, cache, "景点B", 46, 42, scale, Brushes.White);
            AddText(canvas, cache, "出口", 92, 27, scale, Brushes.White);
        }

        public void RenderAgents(Canvas canvas, List<Shape> cache)
        {
            float scale = 8f;

            for (int i = 0; i < _visitorSystem.AgentCount; i++)
            {
                var agent = _visitorSystem.GetAgent(i);
                var state = _visitorSystem.GetVisitorState(i);

                var color = state switch
                {
                    0 => "#FF6B6B",  // 去A - 红
                    1 => "#4ECDC4",  // 去B - 青
                    2 => "#FFE66D",  // 去出口 - 黄
                    _ => "#888888"
                };

                AddCircle(canvas, cache, agent.Position.X, agent.Position.Y, 0.4f, color, scale);
            }
        }

        public int GetStat(string name)
        {
            return name switch
            {
                "collision" => _visitorSystem.CollisionCount,
                "throughput" => _visitorSystem.ArrivedCount,
                _ => 0
            };
        }

        #region 渲染辅助
        private void AddRect(Canvas c, List<Shape> cache, float x, float y, float w, float h, string color, float scale)
        {
            var rect = new Rectangle
            {
                Width = w * scale,
                Height = h * scale,
                Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color))
            };
            Canvas.SetLeft(rect, x * scale);
            Canvas.SetTop(rect, y * scale);
            c.Children.Add(rect);
            cache.Add(rect);
        }

        private void AddCircle(Canvas c, List<Shape> cache, float x, float y, float r, string color, float scale)
        {
            var circle = new Ellipse
            {
                Width = r * 2 * scale,
                Height = r * 2 * scale,
                Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color))
            };
            Canvas.SetLeft(circle, (x - r) * scale);
            Canvas.SetTop(circle, (y - r) * scale);
            c.Children.Add(circle);
            cache.Add(circle);
        }

        private void AddText(Canvas c, List<Shape> cache, string text, float x, float y, float scale, Brush color)
        {
            var tb = new TextBlock
            {
                Text = text,
                Foreground = color,
                FontSize = 10,
                FontWeight = FontWeights.Bold
            };
            Canvas.SetLeft(tb, x * scale);
            Canvas.SetTop(tb, y * scale);
            c.Children.Add(tb);
        }
        #endregion
    }
}
