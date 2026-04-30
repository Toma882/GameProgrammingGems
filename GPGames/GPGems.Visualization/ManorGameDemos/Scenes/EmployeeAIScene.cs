using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using GPGems.AI.Pathfinding;

namespace GPGems.Visualization.ManorGameDemos
{
    /// <summary>
    /// 场景3: 员工AI任务调度
    /// 算法：A*寻路 + FSM状态机
    /// </summary>
    public class EmployeeAIScene : IDemoScene
    {
        private GridMap _map;
        private AStarPathfinder _aStar;
        private List<Employee> _employees;
        private List<Task> _tasks;
        private Random _rand;
        private int _tasksCompleted;
        private float _time;

        public void Reset(int count, float speed)
        {
            _map = CreateFarmMap(50, 50);
            _aStar = new AStarPathfinder();
            _rand = new Random(42);
            _tasksCompleted = 0;
            _time = 0;

            // 5个员工
            _employees = new List<Employee>();
            for (int i = 0; i < Math.Min(count, 10); i++)
            {
                _employees.Add(new Employee
                {
                    Id = i,
                    Position = new Vector2(25, 25),
                    State = EmployeeState.Idle,
                    Color = _employeeColors[i % _employeeColors.Length]
                });
            }

            // count个任务
            _tasks = new List<Task>();
            var types = new[] { "Harvest", "Feed", "Serve" };
            for (int i = 0; i < count; i++)
            {
                _tasks.Add(new Task
                {
                    Id = i,
                    Type = types[_rand.Next(3)],
                    Position = new Vector2(_rand.Next(5, 45), _rand.Next(5, 45)),
                    Duration = 1f + (float)_rand.NextDouble() * 2f
                });
            }
        }

        public void Update(float deltaTime)
        {
            _time += deltaTime;

            foreach (var emp in _employees)
            {
                // 空闲员工分配任务
                if (emp.State == EmployeeState.Idle)
                {
                    var available = _tasks.Where(t => !t.Completed && !t.Assigned).ToList();
                    if (available.Any())
                    {
                        // 就近分配
                        var nearest = available.OrderBy(t => Distance(emp.Position, t.Position)).First();

                        var start = _map.GetNode((int)emp.Position.X, (int)emp.Position.Y);
                        var goal = _map.GetNode((int)nearest.Position.X, (int)nearest.Position.Y);
                        var path = _aStar.FindPath(_map, start, goal);

                        if (path.Count > 0)
                        {
                            emp.AssignTask(nearest, path);
                            nearest.Assigned = true;
                        }
                    }
                }

                emp.Update(deltaTime * 2f);

                if (emp.State == EmployeeState.TaskComplete)
                {
                    _tasksCompleted++;
                    emp.State = EmployeeState.Idle;
                }
            }
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
                        AddRect(canvas, cache, x, y, 1, 1, "#2E2E2E, scale);

            // 员工宿舍
            AddRect(canvas, cache, 23, 23, 4, 4, "#8B0000", scale);

            // 任务点
            foreach (var task in _tasks.Where(t => !t.Completed))
            {
                var color = task.Type switch
                {
                    "Harvest" => "#32CD32",  // 收菜
                    "Feed" => "#DAA520",     // 喂食
                    "Serve" => "#1E90FF",    // 服务
                    _ => "#888
                };
                AddRect(canvas, cache, task.Position.X - 0.4f, task.Position.Y - 0.4f, 0.8f, 0.8f, color, scale);
            }
        }

        public void RenderAgents(Canvas canvas, List<Shape> cache)
        {
            float scale = 12f;

            // 员工
            foreach (var emp in _employees)
            {
                var color = emp.State switch
                {
                    EmployeeState.Idle => "#808080",
                    EmployeeState.Moving => "#FFD700",
                    EmployeeState.Working => "#00FF00",
                    _ => "#FF0000
                };

                AddCircle(canvas, cache, emp.Position.X, emp.Position.Y, 0.5f, color, scale);

                // 显示当前路径
                if (emp.CurrentPath != null && emp.State == EmployeeState.Moving)
                {
                    for (int i = emp.PathIndex; i < emp.CurrentPath.Count - 1; i++)
                    {
                        var n1 = emp.CurrentPath[i];
                        var n2 = emp.CurrentPath[i + 1];
                        AddLine(canvas, cache, n1.X + 0.5f, n1.Y + 0.5f, n2.X + 0.5f, n2.Y + 0.5f, emp.Color, 1, scale);
                    }
                }
            }
        }

        public int GetStat(string name)
        {
            return name switch
            {
                "collision" => 0,
                "throughput" => _tasksCompleted,
                _ => 0
            };
        }

        #region 辅助
        enum EmployeeState { Idle, Moving, Working, TaskComplete }

        class Employee
        {
            public int Id;
            public Vector2 Position;
            public EmployeeState State;
            public Task CurrentTask;
            public List<GridNode> CurrentPath;
            public int PathIndex;
            public float WorkProgress;
            public string Color;

            public void AssignTask(Task task, List<GridNode> path)
            {
                CurrentTask = task;
                CurrentPath = path;
                PathIndex = 0;
                State = EmployeeState.Moving;
                WorkProgress = 0;
            }

            public void Update(float deltaTime)
            {
                if (State == EmployeeState.Moving && CurrentPath != null)
                {
                    // 每帧走一格
                    if (PathIndex < CurrentPath.Count)
                    {
                        var node = CurrentPath[PathIndex];
                        Position = new Vector2(node.X, node.Y);
                        PathIndex++;
                    }
                    else
                    {
                        State = EmployeeState.Working;
                    }
                }
                else if (State == EmployeeState.Working)
                {
                    WorkProgress += deltaTime;
                    if (WorkProgress >= CurrentTask.Duration)
                    {
                        CurrentTask.Completed = true;
                        State = EmployeeState.TaskComplete;
                    }
                }
            }
        }

        class Task
        {
            public int Id;
            public string Type;
            public Vector2 Position;
            public float Duration;
            public bool Assigned;
            public bool Completed;
        }

        private string[] _employeeColors = { "#FF6B6B", "#4ECDC4", "#45B7D1", "#96CEB4", "#FFEAA7" };

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

        private float Distance(Vector2 a, Vector2 b)
        {
            float dx = a.X - b.X;
            float dy = a.Y - b.Y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

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
        #endregion
    }
}
