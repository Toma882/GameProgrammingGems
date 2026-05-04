using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using GPGems.AI.Boids;
using GPGems.ManorSimulation;
using System.Numerics;
using GPGems.Core.Math;

namespace GPGems.Visualization.ManorGameDemos
{
    /// <summary>
    /// 场景2: 动物群体演示
    /// 算法：Boids三种参数对比
    /// </summary>
    public class AnimalGroupScene : IDemoScene
    {
        private Flock _fishFlock;
        private Flock _grazingFlock;
        private Flock _butterflyFlock;
        private BoidSettings _fishSettings;
        private BoidSettings _grazingSettings;
        private BoidSettings _butterflySettings;
        private BoundingBox _bounds;
        private float _speedMultiplier;

        public void Reset(int count, float speed)
        {
            _speedMultiplier = speed;
            int perGroup = Math.Max(10, count / 3);

            // 鱼群：左上区域
            _fishFlock = new Flock();
            _fishSettings = ManorGamePresets.FishSchool;
            for (int i = 0; i < perGroup; i++)
            {
                _fishFlock.AddBoid(
                    new Vector3(Rand(5, 30), 0, Rand(5, 25)),
                    new Vector3(Rand(-1, 1), 0, Rand(-1, 1)).Normalized() * 2
                );
            }

            // 放牧：右上区域
            _grazingFlock = new Flock();
            _grazingSettings = ManorGamePresets.GrazingAnimal;
            for (int i = 0; i < perGroup; i++)
            {
                _grazingFlock.AddBoid(
                    new Vector3(Rand(40, 65), 0, Rand(5, 25)),
                    new Vector3(Rand(-0.5f, 0.5f), 0, Rand(-0.5f, 0.5f)).Normalized() * 0.5f
                );
            }

            // 蝴蝶：下方区域
            _butterflyFlock = new Flock();
            _butterflySettings = ManorGamePresets.Butterfly;
            for (int i = 0; i < perGroup; i++)
            {
                _butterflyFlock.AddBoid(
                    new Vector3(Rand(15, 55), 0, Rand(35, 45)),
                    new Vector3(Rand(-2, 2), 0, Rand(-2, 2)).Normalized() * 1.5f
                );
            }

            _bounds = new BoundingBox(0, 70, -5, 5, 0, 50);
        }

        public void Update(float deltaTime)
        {
            deltaTime *= _speedMultiplier;

            // 鱼群：有目标吸引（食饵点
            foreach (var fish in _fishFlock.Boids)
                fish.TargetPosition = new Vector3(20, 0, 15);
            _fishFlock.Update(_fishSettings, _bounds);

            // 放牧：无目标，漫游
            var grazingSettings = _grazingSettings with { WanderWeight = 2f, SeekTargetWeight = 0 };
            _grazingFlock.Update(grazingSettings, _bounds);

            // 蝴蝶：完全随机漫游
            var butterflySettings = _butterflySettings with { WanderWeight = 3f, AlignmentWeight = 0.2f, CohesionWeight = 0.3f };
            _butterflyFlock.Update(butterflySettings, _bounds);
        }

        public void RenderBackground(Canvas canvas, List<Shape> cache)
        {
            float scale = 10f;
            canvas.Width = 70 * scale;
            canvas.Height = 50 * scale;

            // 池塘
            AddRect(canvas, cache, 2, 2, 30, 25, "#1E90FF", scale);

            // 草地
            AddRect(canvas, cache, 37, 2, 30, 25, "#90EE90", scale);

            // 花园
            AddRect(canvas, cache, 12, 32, 45, 15, "#FFB6C1", scale);

            // 分隔线
            AddLine(canvas, cache, 35, 0, 35, 50, "#555", 1, scale);
            AddLine(canvas, cache, 0, 30, 70, 30, "#555", 1, scale);

            // 标注
            AddText(canvas, cache, "🐟 鱼群", 13, 13, scale, Brushes.White);
            AddText(canvas, cache, "🐄 放牧", 48, 13, scale, Brushes.Brown);
            AddText(canvas, cache, "🦋 蝴蝶", 30, 38, scale, Brushes.Purple);
        }

        public void RenderAgents(Canvas canvas, List<Shape> cache)
        {
            float scale = 10f;

            // 鱼群 - 蓝
            foreach (var b in _fishFlock.Boids)
                AddCircle(canvas, cache, b.Position.X, b.Position.Z, 0.3f, "#4169E1", scale);

            // 牛羊 - 棕
            foreach (var b in _grazingFlock.Boids)
                AddCircle(canvas, cache, b.Position.X, b.Position.Z, 0.6f, "#8B4513", scale);

            // 蝴蝶 - 粉紫小点
            foreach (var b in _butterflyFlock.Boids)
                AddCircle(canvas, cache, b.Position.X, b.Position.Z, 0.2f, "#FF69B4", scale);
        }

        public int GetStat(string name) => 0;

        #region 辅助
        private static Random _r = new Random(42);
        private float Rand(float min, float max) => min + (float)_r.NextDouble() * (max - min);

        private void AddRect(Canvas c, List<Shape> cache, float x, float y, float w, float h, string color, float scale)
        {
            var rect = new Rectangle { Width = w * scale, Height = h * scale, Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)) };
            Canvas.SetLeft(rect, x * scale);
            Canvas.SetTop(rect, y * scale);
            c.Children.Add(rect);
            cache.Add(rect);
        }

        private void AddCircle(Canvas c, List<Shape> cache, float x, float y, float r, string color, float scale)
        {
            var circle = new Ellipse { Width = r * 2 * scale, Height = r * 2 * scale, Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)) };
            Canvas.SetLeft(circle, (x - r) * scale);
            Canvas.SetTop(circle, y * scale);
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
            var tb = new TextBlock { Text = text, Foreground = color, FontSize = 12, FontWeight = FontWeights.Bold };
            Canvas.SetLeft(tb, x * scale);
            Canvas.SetTop(tb, y * scale);
            c.Children.Add(tb);
        }
        #endregion
    }
}
