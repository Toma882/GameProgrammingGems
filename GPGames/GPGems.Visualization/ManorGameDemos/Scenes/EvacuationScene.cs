using System;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace GPGems.Visualization.ManorGameDemos
{
    /// <summary>
    /// 场景4: 紧急疏散演示
    /// 算法：社会力模型
    /// </summary>
    public class EvacuationScene : IDemoScene
    {
        private SocialForceSimulation _sim;
        private int _evacuated;
        private float _time;
        private int _maxNearExit;

        public void Reset(int count, float speed)
        {
            _sim = new SocialForceSimulation();
            var rand = new Random(42);
            _evacuated = 0;
            _time = 0;
            _maxNearExit = 0;

            // 生成游客
            for (int i = 0; i < count; i++)
            {
                var agent = _sim.AddAgent(
                    new Vector2(rand.Next(0, 80), rand.Next(0, 80)),
                    radius: 0.45f,
                    desiredSpeed: 2.5f * speed
                );
                agent.Target = new Vector2(105, 40);
            }

            // 围墙，出口在中间40-45
            for (int y = 0; y < 80; y++)
                if (y < 38 || y > 47)
                    _sim.AddWall(85, y, 86, y);

            // 左侧入口墙
            for (int y = 0; y < 80; y++)
                _sim.AddWall(0, y, 1, y);
            for (int x = 0; x < 85; x++)
            {
                _sim.AddWall(x, 0, x, 1);
                _sim.AddWall(x, 79, x, 80);
            }
        }

        public void Update(float deltaTime)
        {
            _time += deltaTime;
            _sim.Update(deltaTime);

            int nearExit = 0;
            for (int i = _sim.Agents.Count - 1; i >= 0; i--)
            {
                var agent = _sim.Agents[i];
                if (agent.Position.X > 88)
                {
                    // 已疏散，重置到另一边继续（循环演示
                    agent.Position = new Vector2(5, agent.Position.Y);
                    _evacuated++;
                }
                else if (agent.Position.X > 75)
                {
                    nearExit++;
                }
            }
            _maxNearExit = Math.Max(_maxNearExit, nearExit);
        }

        public void RenderBackground(Canvas canvas, List<Shape> cache)
        {
            float scale = 8f;
            canvas.Width = 100 * scale;
            canvas.Height = 85 * scale;

            // 广场
            AddRect(canvas, cache, 0, 0, 85, 80, "#E8E8E8, scale);

            // 围墙
            foreach (var wall in _sim.Walls)
            {
                AddLine(canvas, cache, wall.Start.X, wall.Start.Y, wall.End.X, wall.End.Y, "#333, 3, scale);
            }

            // 出口高亮
            AddRect(canvas, cache, 85, 38, 3, 9, "#32CD32, scale);
            AddText(canvas, cache, "出口 →", 80, 42, scale, Brushes.Green);

            // 安全区域
            AddRect(canvas, cache, 88, 0, 12, 80, "#90EE90, scale);
        }

        public void RenderAgents(Canvas canvas, List<Shape> cache)
        {
            float scale = 8f;

            foreach (var agent in _sim.Agents)
            {
                // 颜色根据压力变化（越靠近出口越红
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
                "collision" => _maxNearExit,
                "throughput" => _evacuated,
                _ => 0
            };
        }

        #region 简化社会力模型
        class SocialForceAgent
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public Vector2 Target;
            public float Radius;
            public float DesiredSpeed;
            public float Mass = 80f;
        }

        class Obstacle
        {
            public Vector2 Start, End;
            public Obstacle(Vector2 s, Vector2 e) { Start = s; End = e; }
        }

        class SocialForceSimulation
        {
            public List<SocialForceAgent> Agents = new();
            public List<Obstacle> Walls = new();

            public SocialForceAgent AddAgent(Vector2 pos, float radius, float desiredSpeed)
            {
                var agent = new SocialForceAgent { Position = pos, Radius = radius, DesiredSpeed = desiredSpeed };
                Agents.Add(agent);
                return agent;
            }

            public void AddWall(float x1, float y1, float x2, float y2)
            {
                Walls.Add(new Obstacle(new Vector2(x1, y1), new Vector2(x2, y2)));
            }

            public void Update(float deltaTime)
            {
                float A = 2.0f;
                float B = 0.3f;
                float tau = 0.5f;

                var forces = new Vector2[Agents.Count];

                for (int i = 0; i < Agents.Count; i++)
                {
                    var agent = Agents[i];

                    // 驱动力
                    Vector2 toTarget = agent.Target - agent.Position;
                    float dist = toTarget.Length();
                    if (dist > 0.1f)
                    {
                        Vector2 desiredVel = toTarget / dist * agent.DesiredSpeed;
                        forces[i] += (desiredVel - agent.Velocity) / tau * agent.Mass;
                    }

                    // 人与人排斥
                    for (int j = 0; j < Agents.Count; j++)
                    {
                        if (i == j) continue;
                        var other = Agents[j];
                        Vector2 diff = agent.Position - other.Position;
                        float d = diff.Length();
                        if (d < 0.01f) continue;
                        float force = A * (float)Math.Exp(-(d - agent.Radius - other.Radius) / B);
                        forces[i] += diff / d * force;
                    }

                    // 墙排斥
                    foreach (var wall in Walls)
                    {
                        Vector2 closest = ClosestPointOnLine(wall.Start, wall.End, agent.Position);
                        Vector2 diff = agent.Position - closest;
                        float d = diff.Length();
                        if (d < 0.01f) continue;
                        float force = A * 1.5f * (float)Math.Exp(-d / (B * 0.5f));
                        forces[i] += diff / d * force;
                    }
                }

                for (int i = 0; i < Agents.Count; i++)
                {
                    var agent = Agents[i];
                    Vector2 acceleration = forces[i] / agent.Mass;
                    agent.Velocity += acceleration * deltaTime;
                    float speed = agent.Velocity.Length();
                    if (speed > agent.DesiredSpeed * 1.5f)
                        agent.Velocity = agent.Velocity / speed * agent.DesiredSpeed * 1.5f;
                    agent.Position += agent.Velocity * deltaTime;
                }
            }

            private Vector2 ClosestPointOnLine(Vector2 a, Vector2 b, Vector2 p)
            {
                Vector2 ab = b - a;
                float t = Vector2.Dot(p - a, ab) / ab.LengthSquared();
                t = Math.Clamp(t, 0f, 1f);
                return a + ab * t;
            }
        }
        #endregion

        #region 渲染辅助
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
            var tb = new TextBlock { Text = text, Foreground = color, FontSize = 11, FontWeight = FontWeights.Bold };
            Canvas.SetLeft(tb, x * scale);
            Canvas.SetTop(tb, y * scale);
            c.Children.Add(tb);
        }
        #endregion
    }
}
