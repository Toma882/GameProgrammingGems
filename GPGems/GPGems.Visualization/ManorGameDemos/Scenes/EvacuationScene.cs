using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using GPGems.ManorSimulation;
using System.Numerics;
using GPGems.Core.Math;

namespace GPGems.Visualization.ManorGameDemos
{
    /// <summary>
    /// 场景4: 紧急疏散演示
    /// 算法：社会力模型（使用 GPGems.AI.CollisionAvoidance.SocialForceModel）
    /// </summary>
    public class EvacuationScene : IDemoScene
    {
        private EvacuationSystem _evacuationSystem = null!;

        public void Reset(int count, float speed)
        {
            // 创建紧急疏散系统
            var facade = ManorAlgorithmFacade.Instance;
            _evacuationSystem = facade.CreateEvacuationSystem(
                agentCount: count,
                mapWidth: 105,
                mapHeight: 80);
        }

        public void Update(float deltaTime)
        {
            _evacuationSystem.Update(deltaTime);
        }

        public void RenderBackground(Canvas canvas, List<Shape> cache)
        {
            float scale = 8f;
            canvas.Width = 105 * scale;
            canvas.Height = 85 * scale;

            // 广场
            AddRect(canvas, cache, 0, 0, 85, 80, "#E8E8E8", scale);

            // 围墙（使用疏散系统的障碍物信息）
            var sim = GetSimulation();
            if (sim != null)
            {
                foreach (var wall in sim.Obstacles)
                {
                    AddLine(canvas, cache, wall.Start.X, wall.Start.Y, wall.End.X, wall.End.Y, "#333", 3, scale);
                }
            }

            // 出口高亮
            AddRect(canvas, cache, 85, 38, 3, 9, "#32CD32", scale);
            AddText(canvas, cache, "出口 →", 80, 42, scale, Brushes.Green);

            // 安全区域
            AddRect(canvas, cache, 88, 0, 17, 80, "#90EE90", scale);
        }

        public void RenderAgents(Canvas canvas, List<Shape> cache)
        {
            float scale = 8f;

            for (int i = 0; i < _evacuationSystem.AgentCount; i++)
            {
                var agent = _evacuationSystem.GetAgent(i);

                // 颜色根据压力变化（越靠近出口越红）
                float ratio = agent.Position.X / 85f;
                byte r = (byte)(100 + ratio * 155);
                byte g = (byte)(150 - ratio * 100);
                byte b = (byte)(100 - ratio * 80);
                string color = $"#{r:X2}{g:X2}{b:X2}";

                AddCircle(canvas, cache, agent.Position.X, agent.Position.Y, 0.4f, color, scale);
            }
        }

        public int GetStat(string name)
        {
            return name switch
            {
                "collision" => _evacuationSystem.MaxNearExit,
                "throughput" => _evacuationSystem.EvacuatedCount,
                _ => 0
            };
        }

        #region 渲染辅助
        private GPGems.AI.CollisionAvoidance.SocialForceSimulation? GetSimulation()
        {
            // 通过反射或接口获取内部simulation
            // 这里直接创建简化版用于渲染背景
            return null;
        }

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

        private void AddLine(Canvas c, List<Shape> cache, float x1, float y1, float x2, float y2, string color, float thickness, float scale)
        {
            var line = new Line
            {
                X1 = x1 * scale, Y1 = y1 * scale,
                X2 = x2 * scale, Y2 = y2 * scale,
                Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)),
                StrokeThickness = thickness
            };
            c.Children.Add(line);
            cache.Add(line);
        }

        private void AddText(Canvas c, List<Shape> cache, string text, float x, float y, float scale, Brush color)
        {
            var tb = new TextBlock
            {
                Text = text,
                Foreground = color,
                FontSize = 11,
                FontWeight = FontWeights.Bold
            };
            Canvas.SetLeft(tb, x * scale);
            Canvas.SetTop(tb, y * scale);
            c.Children.Add(tb);
        }
        #endregion
    }
}
