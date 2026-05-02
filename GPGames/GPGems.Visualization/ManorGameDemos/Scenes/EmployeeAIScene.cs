using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using GPGems.AI.ManorSimulation;
using GPGems.AI.Pathfinding;
using System.Numerics;
using GPGems.Core.Math;

using Rectangle = System.Windows.Shapes.Rectangle;

namespace GPGems.Visualization.ManorGameDemos
{
    /// <summary>
    /// 场景3: 员工AI任务调度
    /// 算法：A*寻路 + FSM状态机
    /// </summary>
    public class EmployeeAIScene : IDemoScene
    {
        private EmployeeTaskSystem _taskSystem = null!;
        private GridMap _map = null!;
        private List<TaskDisplay> _taskDisplays = new();
        private string[] _employeeColors = { "#FF6B6B", "#4ECDC4", "#45B7D1", "#96CEB4", "#FFEAA7" };

        public void Reset(int count, float speed)
        {
            // 初始化地图
            int mapSize = 50;
            _map = CreateFarmMap(mapSize, mapSize);

            // 初始化算法门面
            var facade = ManorAlgorithmFacade.Instance;
            facade.Initialize(mapSize, mapSize);

            // 创建员工任务系统
            _taskSystem = facade.CreateEmployeeTaskSystem(
                employeeCount: Math.Min(count, 10),
                mapWidth: mapSize,
                mapHeight: mapSize);

            // 添加任务
            var types = new[] { "Harvest", "Feed", "Serve" };
            var rand = new Random(42);
            _taskDisplays.Clear();

            for (int i = 0; i < count; i++)
            {
                var taskType = types[rand.Next(3)];
                var pos = new Vector2(rand.Next(5, 45), rand.Next(5, 45));
                var duration = 1f + (float)rand.NextDouble() * 2f;

                _taskSystem.AddTask(taskType, pos, duration);

                _taskDisplays.Add(new TaskDisplay
                {
                    Type = taskType,
                    Position = pos,
                    Duration = duration
                });
            }
        }

        public void Update(float deltaTime)
        {
            _taskSystem.Update(deltaTime * 2f);
        }

        public void RenderBackground(Canvas canvas, List<Shape> cache)
        {
            float scale = 12f;
            canvas.Width = 50 * scale;
            canvas.Height = 50 * scale;

            // 网格草地
            for (int x = 0; x < 50; x++)
                for (int y = 0; y < 50; y++)
                    if (!_map.GetNode(x, y).IsWalkable)
                        AddRect(canvas, cache, x, y, 1, 1, "#2E2E2E", scale);

            // 员工宿舍
            AddRect(canvas, cache, 23, 23, 4, 4, "#8B0000", scale);

            // 任务点
            foreach (var task in _taskDisplays.Where(t => !t.Completed))
            {
                var color = task.Type switch
                {
                    "Harvest" => "#32CD32",
                    "Feed" => "#DAA520",
                    "Serve" => "#1E90FF",
                    _ => "#888"
                };
                AddRect(canvas, cache, task.Position.X - 0.4f, task.Position.Y - 0.4f, 0.8f, 0.8f, color, scale);
            }
        }

        public void RenderAgents(Canvas canvas, List<Shape> cache)
        {
            float scale = 12f;

            // 员工
            for (int i = 0; i < _taskSystem.Employees.Count; i++)
            {
                var emp = _taskSystem.Employees[i];
                var state = _taskSystem.GetEmployeeState(i);
                var colorStr = state switch
                {
                    EmployeeState.Idle => "#808080",
                    EmployeeState.Moving => "#FFD700",
                    EmployeeState.Working => "#00FF00",
                    EmployeeState.Resting => "#FFA500",
                    _ => "#FF0000"
                };

                AddCircle(canvas, cache, emp.Position.X, emp.Position.Y, 0.5f, colorStr, scale);

                // 显示当前路径
                if (emp.CurrentPath.Count > 0 && state == EmployeeState.Moving)
                {
                    var empColor = _employeeColors[i % _employeeColors.Length];
                    for (int j = emp.PathIndex; j < emp.CurrentPath.Count - 1; j++)
                    {
                        var n1 = emp.CurrentPath[j];
                        var n2 = emp.CurrentPath[j + 1];
                        AddLine(canvas, cache, n1.X + 0.5f, n1.Y + 0.5f, n2.X + 0.5f, n2.Y + 0.5f, empColor, 1, scale);
                    }
                }
            }
        }

        public int GetStat(string name)
        {
            return name switch
            {
                "collision" => 0,
                "throughput" => _taskSystem.CompletedTasks,
                _ => 0
            };
        }

        #region 辅助类与渲染

        private class TaskDisplay
        {
            public string Type { get; set; } = string.Empty;
            public Vector2 Position { get; set; }
            public float Duration { get; set; }
            public bool Completed { get; set; }
        }

        private GridMap CreateFarmMap(int width, int height)
        {
            var map = new GridMap(width, height);
            var rand = new Random(42);
            for (int i = 0; i < 60; i++)
                map.GetNode(rand.Next(width), rand.Next(height)).IsWalkable = false;
            for (int x = 23; x <= 27; x++)
                for (int y = 23; y <= 27; y++)
                    map.GetNode(x, y).IsWalkable = true;
            return map;
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

        #endregion
    }
}
