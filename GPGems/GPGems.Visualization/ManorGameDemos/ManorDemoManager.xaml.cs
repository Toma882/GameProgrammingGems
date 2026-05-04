using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Numerics;
using GPGems.Core.Math;
using GPGems.ManorSimulation;

namespace GPGems.Visualization.ManorGameDemos
{
    public partial class ManorDemoManager : UserControl
    {
        private DispatcherTimer _timer;
        private bool _isPlaying = false;
        private int _frame = 0;
        private float _deltaTime = 0.05f;
        private DateTime _lastTime;
        private int _fpsCounter = 0;
        private float _fpsTimer = 0;

        // 当前场景
        private IDemoScene _currentScene;

        // 场景
        private VisitorFlowScene _visitorScene;
        private AnimalGroupScene _animalScene;
        private EmployeeAIScene _employeeScene;
        private EvacuationScene _evacuationScene;
        private PlacementScene _placementScene;
        private ManorIntegratedScene _integratedScene;

        // 渲染缓存
        private List<Shape> _renderCache = new List<Shape>();

        public ManorDemoManager()
        {
            InitializeComponent();
            InitializeTimer();
            InitializeScenes();
            DemoSelector.SelectedIndex = 5; // 默认选中整合庄园模拟
        }

        private void InitializeTimer()
        {
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromMilliseconds(16); //  ~60fps
            _timer.Tick += (s, e) => OnFrame();
            _lastTime = DateTime.Now;
        }

        private void InitializeScenes()
        {
            _visitorScene = new VisitorFlowScene();
            _animalScene = new AnimalGroupScene();
            _employeeScene = new EmployeeAIScene();
            _evacuationScene = new EvacuationScene();
            _placementScene = new PlacementScene();
            _integratedScene = new ManorIntegratedScene();
        }

        private void DemoSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // 先移除鼠标事件（避免重复注册）
            RenderCanvas.MouseMove -= RenderCanvas_MouseMove;
            RenderCanvas.MouseLeftButtonDown -= RenderCanvas_MouseLeftButtonDown;
            RenderCanvas.MouseRightButtonDown -= RenderCanvas_MouseRightButtonDown;

            PlacementPanel.Visibility = Visibility.Collapsed;
            IntegratedPanel.Visibility = Visibility.Collapsed;

            switch (DemoSelector.SelectedIndex)
            {
                case 0:
                    _currentScene = _visitorScene;
                    SceneDescription.Text = "演示：100个游客从入口进入，分流到A、B两个景点，最后从出口离开。\n\n算法：流场寻路（全局路径一次性计算） + ORCA避障（局部无穿透）";
                    break;
                case 1:
                    _currentScene = _animalScene;
                    SceneDescription.Text = "演示：三种不同参数的Boids群体行为对比。\n\n🐟 鱼群：高凝聚、高同步、密集\n🐄 放牧：低凝聚、慢速度、漫游\n🦋 蝴蝶：低同步、快转向、飘忽";
                    break;
                case 2:
                    _currentScene = _employeeScene;
                    SceneDescription.Text = "演示：5个员工处理10个随机任务的调度过程。\n\n算法：A*寻路 + FSM状态机（空闲→移动→工作→完成）";
                    break;
                case 3:
                    _currentScene = _evacuationScene;
                    SceneDescription.Text = "演示：300人同时向唯一4米宽出口疏散。\n\n算法：社会力模型（人与人排斥、目标吸引、墙体排斥）";
                    break;
                case 4:
                    _currentScene = _placementScene;
                    PlacementPanel.Visibility = Visibility.Visible;
                    SceneDescription.Text = "演示：位掩码多层地图的建筑物放置系统。\n\n数据结构：byte[,] 位图，每个格子1字节 = 8个图层\n\n操作：鼠标悬停预览，左键放置，右键删除";
                    // 先初始化再设置选择器（避免 Render 时 Facade 未初始化）
                    ResetCurrentScene();
                    // 注册鼠标事件
                    RenderCanvas.MouseMove += RenderCanvas_MouseMove;
                    RenderCanvas.MouseLeftButtonDown += RenderCanvas_MouseLeftButtonDown;
                    RenderCanvas.MouseRightButtonDown += RenderCanvas_MouseRightButtonDown;
                    BuildingSelector.SelectedIndex = 0;
                    return; // 已调用 Reset，避免重复执行
                case 5:
                    _currentScene = _integratedScene;
                    IntegratedPanel.Visibility = Visibility.Visible;
                    SceneDescription.Text = "🏰 整合庄园模拟：通过放置建筑定义地图功能\n\n放置入口/出口/景点后，点击开始模拟即可观察客流动态\n\n操作：鼠标悬停预览，左键放置，右键删除建筑";
                    // 先初始化再设置选择器（避免 Render 时 Facade 未初始化）
                    ResetCurrentScene();
                    // 注册鼠标事件
                    RenderCanvas.MouseMove += IntegratedScene_MouseMove;
                    RenderCanvas.MouseLeftButtonDown += IntegratedScene_MouseLeftButtonDown;
                    RenderCanvas.MouseRightButtonDown += IntegratedScene_MouseRightButtonDown;
                    IntegratedBuildingSelector.SelectedIndex = 0;
                    return; // 已调用 Reset，避免重复执行
            }

            ResetCurrentScene();
        }

        private void BtnPlayPause_Click(object sender, RoutedEventArgs e)
        {
            _isPlaying = !_isPlaying;
            BtnPlayPause.Content = _isPlaying ? "⏸ 暂停" : "▶ 播放";

            if (_isPlaying)
                _timer.Start();
            else
                _timer.Stop();
        }

        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            ResetCurrentScene();
        }

        private void ResetCurrentScene()
        {
            _frame = 0;
            _currentScene?.Reset((int)SliderCount.Value, (float)SliderSpeed.Value);
            Render();
        }

        private void OnFrame()
        {
            if (_currentScene == null) return;

            // 计算真实DeltaTime
            var now = DateTime.Now;
            float dt = (float)(now - _lastTime).TotalSeconds;
            _lastTime = now;

            // 应用速度倍率
            dt *= (float)SliderSpeed.Value;

            // 更新场景
            _currentScene.Update(dt);
            _frame++;

            // FPS统计
            _fpsCounter++;
            _fpsTimer += dt;
            if (_fpsTimer >= 1f)
            {
                StatsFPS.Text = $"FPS: {_fpsCounter / _fpsTimer:F0}";
                _fpsCounter = 0;
                _fpsTimer = 0;
            }

            // 更新统计显示
            StatsFrame.Text = $"帧数: {_frame}";
            StatsTime.Text = $"模拟时间: {_frame * _deltaTime * SliderSpeed.Value:F1}s";
            StatsCollisions.Text = $"每帧碰撞: {_currentScene.GetStat("collision")}";
            StatsThroughput.Text = $"吞吐量: {_currentScene.GetStat("throughput")}/秒";

            // 渲染
            Render();
        }

        private void Render()
        {
            // 清除旧的渲染元素
            foreach (var shape in _renderCache)
                RenderCanvas.Children.Remove(shape);
            _renderCache.Clear();

            // 渲染场景背景/建筑
            _currentScene.RenderBackground(RenderCanvas, _renderCache);

            // 渲染Agent
            _currentScene.RenderAgents(RenderCanvas, _renderCache);
        }

        #region 放置场景事件

        private void Floor_Checked(object sender, RoutedEventArgs e)
        {
            if (_placementScene == null) return;

            if (Floor0.IsChecked == true) _placementScene.CurrentFloor = 0;
            else if (Floor1.IsChecked == true) _placementScene.CurrentFloor = 1;
            else if (Floor2.IsChecked == true) _placementScene.CurrentFloor = 2;

            if (_currentScene == _placementScene)
                Render();
        }

        private void BuildingSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_placementScene == null) return;

            if (BuildingSelector.SelectedItem is ComboBoxItem item && item.Tag is string tag)
            {
                int index = int.Parse(tag);
                if (index >= 0 && index < _placementScene.BuildingPresets.Count)
                {
                    _placementScene.SelectedBuilding = _placementScene.BuildingPresets[index];
                }
            }
            if (_currentScene == _placementScene)
                Render();
        }

        private void BtnPlace_Click(object sender, RoutedEventArgs e)
        {
            _placementScene.PlaceCurrent();
            StatsCollisions.Text = $"本层建筑: {_placementScene.GetStat("")}";
            Render();
        }

        private void BtnClearAll_Click(object sender, RoutedEventArgs e)
        {
            _placementScene.Reset(0, 0);
            StatsCollisions.Text = "本层建筑: 0";
            Render();
        }

        private void RenderCanvas_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            var pos = e.GetPosition(RenderCanvas);
            var (gx, gy) = _placementScene.ScreenToGrid(pos.X, pos.Y);
            _placementScene.PreviewX = gx;
            _placementScene.PreviewY = gy;
            Render();
        }

        private void RenderCanvas_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            BtnIntPlace_Click(sender, e);
        }

        private void RenderCanvas_MouseRightButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var pos = e.GetPosition(RenderCanvas);
            var (gx, gy) = _placementScene.ScreenToGrid(pos.X, pos.Y);

            bool removed = _placementScene.RemoveAt(gx, gy);
            if (removed)
            {
                StatsCollisions.Text = $"本层建筑: {_placementScene.GetStat("")}";
            }
            Render();
        }

        #endregion

        #region 整合庄园场景事件

        private void IntFloor_Checked(object sender, RoutedEventArgs e)
        {
            if (_integratedScene == null) return;

            if (IntFloor0.IsChecked == true) _integratedScene.CurrentFloor = 0;
            else if (IntFloor1.IsChecked == true) _integratedScene.CurrentFloor = 1;
            else if (IntFloor2.IsChecked == true) _integratedScene.CurrentFloor = 2;

            if (_currentScene == _integratedScene)
                Render();
        }

        private void IntegratedBuildingSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_integratedScene == null) return;

            if (IntegratedBuildingSelector.SelectedItem is ComboBoxItem item && item.Tag is string tag)
            {
                int index = int.Parse(tag);
                if (index >= 0 && index < _integratedScene.BuildingPresets.Count)
                {
                    _integratedScene.SelectedBuilding = _integratedScene.BuildingPresets[index];
                }
            }
            if (_currentScene == _integratedScene)
                Render();
        }

        private void BtnIntPlace_Click(object sender, RoutedEventArgs e)
        {
            _integratedScene.PlaceCurrent();
            StatsCollisions.Text = $"建筑总数: {_integratedScene.GetStat("buildingCount")}";
            Render();
        }

        private void BtnIntClear_Click(object sender, RoutedEventArgs e)
        {
            _integratedScene.Reset(0, 0);
            StatsCollisions.Text = "建筑总数: 0";
            Render();
        }

        private void BtnStartSim_Click(object sender, RoutedEventArgs e)
        {
            // 应用所有参数
            _integratedScene.EnableVisitorFlow = ChkVisitorFlow.IsChecked == true;
            _integratedScene.EnableAnimalGroup = ChkAnimalGroup.IsChecked == true;
            _integratedScene.EnableEmployeeTask = ChkEmployeeTask.IsChecked == true;
            _integratedScene.EnableEvacuation = ChkEvacuation.IsChecked == true;

            _integratedScene.VisitorCount = (int)SliderVisitorCount.Value;
            _integratedScene.VisitorSpeed = (float)SliderVisitorSpeed.Value;
            _integratedScene.EnableORCA = ChkORCA.IsChecked == true;

            _integratedScene.AnimalCount = (int)SliderAnimalCount.Value;
            _integratedScene.BoidsCohesion = (float)SliderCohesion.Value;
            _integratedScene.BoidsAlignment = (float)SliderAlignment.Value;
            _integratedScene.BoidsSeparation = (float)SliderSeparation.Value;

            _integratedScene.EmployeeCount = (int)SliderEmployeeCount.Value;
            _integratedScene.TaskCount = (int)SliderTaskCount.Value;

            _integratedScene.EvacuationCount = (int)SliderEvacCount.Value;

            _integratedScene.StartSimulation();
            StatsCollisions.Text = $"建筑: {_integratedScene.GetStat("buildingCount")} | 游客: {_integratedScene.GetStat("visitorCount")} | 动物: {_integratedScene.GetStat("animalCount")}";
        }

        private void BtnStopSim_Click(object sender, RoutedEventArgs e)
        {
            _integratedScene.StopSimulation();
            StatsCollisions.Text = $"本层建筑: {_integratedScene.GetStat("buildingCount")}";
        }

        private void IntegratedScene_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            var pos = e.GetPosition(RenderCanvas);
            var (gx, gy) = _integratedScene.ScreenToGrid(pos.X, pos.Y);
            _integratedScene.PreviewX = gx;
            _integratedScene.PreviewY = gy;
            Render();
        }

        private void IntegratedScene_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {

            var pos = e.GetPosition(RenderCanvas);
            var (gx, gy) = _integratedScene.ScreenToGrid(pos.X, pos.Y);
            _integratedScene.PreviewX = gx;
            _integratedScene.PreviewY = gy;

            int id = _integratedScene.PlaceCurrent();
            if (id > 0)
            {
                StatsCollisions.Text = $"本层建筑: {_integratedScene.GetStat("buildingCount")}";
            }
            Render();
        }

        private void IntegratedScene_MouseRightButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var pos = e.GetPosition(RenderCanvas);
            var (gx, gy) = _integratedScene.ScreenToGrid(pos.X, pos.Y);

            bool removed = _integratedScene.RemoveAt(gx, gy);
            if (removed)
            {
                StatsCollisions.Text = $"本层建筑: {_integratedScene.GetStat("buildingCount")}";
            }
            Render();
        }

        #endregion
    }

    // 场景接口
    public interface IDemoScene
    {
        void Reset(int count, float speed);
        void Update(float deltaTime);
        void RenderBackground(Canvas canvas, List<Shape> cache);
        void RenderAgents(Canvas canvas, List<Shape> cache);
        int GetStat(string name);
    }
}
