using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GPGems.Graphics.Terrain;

namespace GPGems.DemoApp.Graphics;

/// <summary>
/// 地形生成算法对比演示窗口
/// 三种算法并行对比：断层生成、中点位移分形、粒子沉积侵蚀
/// </summary>
public partial class TerrainGenerationDemoWindow : Window
{
    private Heightfield? _faultTerrain;
    private Heightfield? _midpointTerrain;
    private Heightfield? _erosionTerrain;

    public TerrainGenerationDemoWindow()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            InitializeSliders();
            RegenerateAll();
        };
    }

    private void InitializeSliders()
    {
        LevelSlider.ValueChanged += (_, _) =>
        {
            int size = (1 << (int)LevelSlider.Value) + 1;
            LevelValue.Text = $"{size}x{size}";
        };

        SeedSlider.ValueChanged += (_, _) =>
        {
            SeedValue.Text = SeedSlider.Value.ToString();
        };

        FaultCountSlider.ValueChanged += (_, _) =>
        {
            FaultCountValue.Text = FaultCountSlider.Value.ToString();
        };

        FilterRadiusSlider.ValueChanged += (_, _) =>
        {
            FilterRadiusValue.Text = FilterRadiusSlider.Value.ToString("F2");
        };

        RoughnessSlider.ValueChanged += (_, _) =>
        {
            RoughnessValue.Text = RoughnessSlider.Value.ToString("F2");
        };

        ParticleCountSlider.ValueChanged += (_, _) =>
        {
            ParticleCountValue.Text = $"{ParticleCountSlider.Value}万";
        };

        ErosionRateSlider.ValueChanged += (_, _) =>
        {
            ErosionRateValue.Text = ErosionRateSlider.Value.ToString("F2");
        };
    }

    private void RegenerateAllButton_Click(object sender, RoutedEventArgs e)
    {
        RegenerateAll();
    }

    private void ApplyErosionButton_Click(object sender, RoutedEventArgs e)
    {
        if (_faultTerrain == null) return;

        int? seed = (int)SeedSlider.Value == -1 ? null : (int)SeedSlider.Value;
        int particles = (int)ParticleCountSlider.Value * 10000;
        float erosionRate = (float)ErosionRateSlider.Value;

        // 在断层地形基础上应用侵蚀
        _erosionTerrain = _faultTerrain.Clone();

        var sw = Stopwatch.StartNew();
        ParticleDeposition.Erode(_erosionTerrain, particles, erosionRate, erosionRate, 0.05f, 0.01f, 6f, 0.02f, seed);
        _erosionTerrain.Normalize();
        sw.Stop();

        ErosionTimeText.Text = $"侵蚀: {sw.Elapsed.TotalMilliseconds:F0} ms";
        ErosionTimeValue.Text = $"{sw.Elapsed.TotalMilliseconds:F0}ms";

        RedrawTerrain(ErosionCanvas, _erosionTerrain);
        Log($"已应用侵蚀：{particles:N0}粒子, 耗时 {sw.Elapsed.TotalMilliseconds:F0}ms");
    }

    private void DisplayModeChanged(object sender, RoutedEventArgs e)
    {
        RedrawAll();
    }

    private void RegenerateAll()
    {
        int level = (int)LevelSlider.Value;
        int size = (1 << level) + 1;
        int? seed = (int)SeedSlider.Value == -1 ? null : (int)SeedSlider.Value;

        Log($"开始生成地形... 尺寸: {size}x{size}, 种子: {seed ?? -1}");

        // 1. 断层地形
        var sw = Stopwatch.StartNew();
        int faultCount = (int)FaultCountSlider.Value;
        float filterRadius = (float)FilterRadiusSlider.Value;
        _faultTerrain = FaultFormation.Generate(size, faultCount, 1.0f, filterRadius, seed);
        _faultTerrain.Normalize();
        sw.Stop();
        FaultTimeText.Text = $"断层: {sw.Elapsed.TotalMilliseconds:F0} ms";
        FaultTimeValue.Text = $"{sw.Elapsed.TotalMilliseconds:F0}ms";
        Log($"✅ 断层地形完成: {faultCount}断层, {sw.Elapsed.TotalMilliseconds:F0}ms");

        // 2. 中点位移分形
        sw.Restart();
        float roughness = (float)RoughnessSlider.Value;
        _midpointTerrain = MidpointDisplacement.Generate(level, roughness, 1.0f, seed);
        _midpointTerrain.Normalize();
        sw.Stop();
        MidpointTimeText.Text = $"分形: {sw.Elapsed.TotalMilliseconds:F0} ms";
        MidpointTimeValue.Text = $"{sw.Elapsed.TotalMilliseconds:F0}ms";
        Log($"✅ 分形地形完成: 粗糙度{roughness:F2}, {sw.Elapsed.TotalMilliseconds:F0}ms");

        // 3. 粒子沉积侵蚀（在断层地形基础上）
        sw.Restart();
        int particles = (int)ParticleCountSlider.Value * 10000;
        float erosionRate = (float)ErosionRateSlider.Value;
        _erosionTerrain = _faultTerrain.Clone();
        ParticleDeposition.Erode(_erosionTerrain, particles, erosionRate, erosionRate, 0.05f, 0.01f, 6f, 0.02f, seed);
        _erosionTerrain.Normalize();
        sw.Stop();
        ErosionTimeText.Text = $"侵蚀: {sw.Elapsed.TotalMilliseconds:F0} ms";
        ErosionTimeValue.Text = $"{sw.Elapsed.TotalMilliseconds:F0}ms";
        Log($"✅ 侵蚀地形完成: {particles:N0}粒子, {sw.Elapsed.TotalMilliseconds:F0}ms");

        RedrawAll();
        Log("🎉 全部地形生成完成!");
    }

    private void RedrawAll()
    {
        if (_faultTerrain != null) RedrawTerrain(FaultCanvas, _faultTerrain);
        if (_midpointTerrain != null) RedrawTerrain(MidpointCanvas, _midpointTerrain);
        if (_erosionTerrain != null) RedrawTerrain(ErosionCanvas, _erosionTerrain);
    }

    private void RedrawTerrain(Canvas canvas, Heightfield terrain)
    {
        canvas.Children.Clear();

        double canvasWidth = canvas.ActualWidth > 10 ? canvas.ActualWidth : 350;
        double canvasHeight = canvas.ActualHeight > 10 ? canvas.ActualHeight : 350;

        int size = terrain.Width;
        var writeableBitmap = new WriteableBitmap(size, size, 96, 96, PixelFormats.Bgr32, null);

        byte[] pixels = new byte[size * size * 4];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float height = terrain[x, y];
                int idx = (y * size + x) * 4;

                if (GrayscaleMode.IsChecked == true)
                {
                    // 灰度模式
                    byte gray = (byte)(height * 255);
                    pixels[idx] = gray;     // B
                    pixels[idx + 1] = gray; // G
                    pixels[idx + 2] = gray; // R
                }
                else
                {
                    // 高度彩色模式
                    var color = HeightToColor(height);
                    pixels[idx] = color.B;     // B
                    pixels[idx + 1] = color.G; // G
                    pixels[idx + 2] = color.R; // R
                }
                pixels[idx + 3] = 255; // A
            }
        }

        writeableBitmap.WritePixels(
            new Int32Rect(0, 0, size, size),
            pixels,
            size * 4,
            0);

        var image = new System.Windows.Controls.Image
        {
            Source = writeableBitmap,
            Width = canvasWidth,
            Height = canvasHeight,
            Stretch = Stretch.Uniform
        };

        canvas.Children.Add(image);
    }

    /// <summary>
    /// 高度到颜色映射：深蓝(水) -> 绿(平原) -> 黄(山丘) -> 棕(山地) -> 白(雪顶)
    /// </summary>
    private static (byte R, byte G, byte B) HeightToColor(float height)
    {
        if (height < 0.2f)
        {
            // 深蓝 -> 浅蓝（水）
            float t = height / 0.2f;
            return (
                R: (byte)(20 + t * 30),
                G: (byte)(50 + t * 100),
                B: (byte)(150 + t * 105)
            );
        }
        else if (height < 0.4f)
        {
            // 深绿 -> 浅绿（平原）
            float t = (height - 0.2f) / 0.2f;
            return (
                R: (byte)(34 + t * 80),
                G: (byte)(139 + t * 80),
                B: (byte)(34 + t * 30)
            );
        }
        else if (height < 0.6f)
        {
            // 黄绿 -> 黄（山丘）
            float t = (height - 0.4f) / 0.2f;
            return (
                R: (byte)(154 + t * 101),
                G: (byte)(205 + t * 50),
                B: (byte)(50 + t * 50)
            );
        }
        else if (height < 0.8f)
        {
            // 黄棕 -> 棕（山地）
            float t = (height - 0.6f) / 0.2f;
            return (
                R: (byte)(210 + t * 30),
                G: (byte)(180 - t * 50),
                B: (byte)(140 - t * 80)
            );
        }
        else
        {
            // 灰 -> 白（雪顶）
            float t = (height - 0.8f) / 0.2f;
            return (
                R: (byte)(160 + t * 95),
                G: (byte)(160 + t * 95),
                B: (byte)(160 + t * 95)
            );
        }
    }

    private void PresetMountains_Click(object sender, RoutedEventArgs e)
    {
        LevelSlider.Value = 8;
        FaultCountSlider.Value = 200;
        FilterRadiusSlider.Value = 0.2;
        RoughnessSlider.Value = 0.55;
        ParticleCountSlider.Value = 8;
        ErosionRateSlider.Value = 0.35;
        Log("已应用预设: ⛰️ 山地");
        RegenerateAll();
    }

    private void PresetHills_Click(object sender, RoutedEventArgs e)
    {
        LevelSlider.Value = 7;
        FaultCountSlider.Value = 80;
        FilterRadiusSlider.Value = 0.5;
        RoughnessSlider.Value = 0.4;
        ParticleCountSlider.Value = 3;
        ErosionRateSlider.Value = 0.2;
        Log("已应用预设: 🌊 丘陵湖泊");
        RegenerateAll();
    }

    private void PresetCanyon_Click(object sender, RoutedEventArgs e)
    {
        LevelSlider.Value = 8;
        FaultCountSlider.Value = 100;
        FilterRadiusSlider.Value = 0.15;
        RoughnessSlider.Value = 0.65;
        ParticleCountSlider.Value = 15;
        ErosionRateSlider.Value = 0.5;
        Log("已应用预设: 🏜️ 峡谷侵蚀");
        RegenerateAll();
    }

    private void Log(string message)
    {
        string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        LogText.Text += $"[{timestamp}] {message}\n";

        Dispatcher.BeginInvoke(() =>
        {
            var scrollViewer = VisualTreeHelper.GetParent(LogText) as ScrollViewer;
            scrollViewer?.ScrollToEnd();
        });
    }
}
