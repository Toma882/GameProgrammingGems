using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using GPGems.AI.Pathfinding;
using GPGems.AI.CollisionAvoidance;

namespace GPGems.Visualization.ManorGameDemos
{
    /// <summary>
    /// 场景1: 游客人流演示
    /// 算法：流场寻路 + ORCA避障
    /// </summary>
    public class VisitorFlowScene : IDemoScene
    {
        private int _mapWidth = 100;
        private int _mapHeight = 50;
        private GridMap _map;
        private FlowFieldPathfinder _flowFieldA;
        private FlowFieldPathfinder _flowFieldB;
        private FlowFieldPathfinder _flowFieldExit;
        private ORCASimulation _orca;
        private Dictionary<int, int> _visitorState; // 0→A, 1→B, 2→出口
        private int _collisions;
        private int _arrivedTotal;
        private Random _rand;

        public void Reset(int count, float speed)
        {
            _map = CreateManorMap(_mapWidth, _mapHeight);
            _flowFieldA = new FlowFieldPathfinder(_map);
            _flowFieldB = new FlowFieldPathfinder(_map);
            _flowFieldExit = new FlowFieldPathfinder(_map);
            _flowFieldA.CalculateFlowField(50, 10);
            _flowFieldB.CalculateFlowField(50, 40);
            _flowFieldExit.CalculateFlowField(100, 25);

            _orca = new ORCASimulation();
            _visitorState = new Dictionary<int, int>();
            _rand = new Random(42);
            _collisions = 0;
            _arrivedTotal = 0;

            // 生成游客
            for (int i = 0; i < count; i++)
            {
                var visitor = _orca.AddAgent(
                    new Vector2(_rand.Next(0, 5), _rand.Next(20, 30)),
                    radius: 0.5f,
                    maxSpeed: 1.8f * speed
                );
                visitor.PreferredVel = new Vector2(1, 0);
                _visitorState[i] = _rand.Next(2); // 随机分配目的地
            }
        }

        public void Update(float deltaTime)
        {
            _collisions = 0;

            for (int i = 0; i < _orca.Agents.Count; i++)
            {
                var agent = _orca.Agents[i];
                int state = _visitorState[i];

                Vector2 flowDir = Vector2.Zero;
                switch (state)
                {
                    case 0: flowDir = _flowFieldA.GetDirection(agent.Position.X, agent.Position.Y); break;
                    case 1: flowDir = _flowFieldB.GetDirection(agent.Position.X, agent.Position.Y); break;
                    case 2: flowDir = _flowFieldExit.GetDirection(agent.Position.X, agent.Position.Y); break;
                }

                agent.PreferredVel = flowDir * agent.MaxSpeed;

                // 检查到达
                float distToA = Vector2.Distance(agent.Position, new Vector2(50, 10));
                float distToB = Vector2.Distance(agent.Position, new Vector2(50, 40));
                float distToExit = Vector2.Distance(agent.Position, new Vector2(100, 25));

                if (state == 0 && distToA < 5f) { _visitorState[i] = 2; _arrivedTotal++; }
                if (state == 1 && distToB < 5f) { _visitorState[i] = 2; _arrivedTotal++; }
                if (state == 2 && distToExit < 5f)
                {
                    // 到达出口，回到入口重新开始（循环演示）
                    agent.Position = new Vector2(_rand.Next(0, 5), _rand.Next(20, 30));
                    _visitorState[i] = _rand.Next(2);
                    _arrivedTotal++;
                }
            }

            _orca.Update(deltaTime);

            // 统计碰撞
            for (int i = 0; i < _orca.Agents.Count; i++)
                for (int j = i + 1; j < _orca.Agents.Count; j++)
                    if (Vector2.Distance(_orca.Agents[i].Position, _orca.Agents[j].Position) < 0.9f)
                        _collisions++;
        }

        public void RenderBackground(Canvas canvas, List<Shape> cache)
        {
            float scale = 8f; // 每格8像素
            canvas.Width = _mapWidth * scale;
            canvas.Height = _mapHeight * scale;

            // 道路/广场
            AddRect(canvas, cache, 0, 15, 15, 20, "#6B8E23, scale);    // 入口广场
            AddRect(canvas, cache, 0, 23, 100, 4, "#DAA520, scale);     // 主路
            AddRect(canvas, cache, 20, 8, 35, 4, "#DAA520", scale); // 到A的路
            AddRect(canvas, cache, 20, 38, 35, 4, "#DAA520", scale); // 到B的路
            AddRect(canvas, cache, 45, 5, 10, 10, "#8FBC8F", scale);  // 景点A广场
            AddRect(canvas, cache, 45, 35, 10, 10, "#8FBC8F", scale);  // 景点B广场
            AddRect(canvas, cache, 90, 20, 10, 10, "#CD853F", scale); // 出口广场

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

            for (int i = 0; i < _orca.Agents.Count; i++)
            {
                var agent = _orca.Agents[i];
                var color = _visitorState[i] switch
                {
                    0 => "#FF6B6B,  // 去A - 红
                    1 => "#4ECDC4",  // 去B - 青
                    2 => "#FFE66D",  // 去出口 - 黄
                    _ => "#888888
                };

                AddCircle(canvas, cache, agent.Position.X, agent.Position.Y, 0.4f, color, scale);
            }
        }

        public int GetStat(string name)
        {
            return name switch
            {
                "collision" => _collisions,
                "throughput" => _arrivedTotal,
                _ => 0
            };
        }

        #region 辅助
        private GridMap CreateManorMap(int width, int height)
        {
            var map = new GridMap(width, height);
            // 默认全可走（简化版
            return map;
        }

        private void AddRect(Canvas c, List<Shape> cache, float x, float y, float w, float h, string color, float scale)
        {
            var rect = new Rectangle
            {
                Width = w * scale, Height = h * scale, Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)
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
            //  cache.Add(tb);
        }
        #endregion
    }
}
