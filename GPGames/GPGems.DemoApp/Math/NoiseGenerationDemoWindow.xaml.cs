using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Numerics;
using GPGems.Core.Math;

namespace GPGems.DemoApp.Math;

/// <summary>
/// 噪声生成器演示窗口
/// </summary>
public partial class NoiseGenerationDemoWindow : Window
{
    private PerlinNoise? _perlinNoise;
    private SimplexNoise? _simplexNoise;
    private int _resolution = 256;
    private bool _isAnimating;
    private float _animOffset;

    public NoiseGenerationDemoWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => InitializeDemo();
    }

    private void InitializeDemo()
    {
        _perlinNoise = new PerlinNoise(seed: 42);
        _simplexNoise = new SimplexNoise(seed: 42);
        RegenerateNoise();
        Log("噪声生成器已启动");
    }

    private void RegenerateNoise()
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();

        float scale = (float)ScaleSlider.Value;
        int octaves = (int)OctavesSlider.Value;
        float persistence = (float)PersistenceSlider.Value;

        var noiseData = new float[_resolution, _resolution];
        float min = float.MaxValue, max = float.MinValue;
        float sum = 0;

        for (int y = 0; y < _resolution; y++)
        {
            for (int x = 0; x < _resolution; x++)
            {
                float noise = 0;
                if (TypePerlin.IsChecked == true)
                    noise = _perlinNoise!.FractalNoise(x * scale, y * scale, octaves, persistence);
                else
                    noise = _simplexNoise!.FractalNoise(x * scale, y * scale, octaves, persistence);

                noiseData[x, y] = noise;
                min = System.Math.Min(min, noise);
                max = System.Math.Max(max, noise);
                sum += noise;
            }
        }

        float mean = sum / (_resolution * _resolution);
        float variance = 0;
        for (int y = 0; y < _resolution; y++)
            for (int x = 0; x < _resolution; x++)
                variance += (noiseData[x, y] - mean) * (noiseData[x, y] - mean);
        float stdDev = (float)System.Math.Sqrt(variance / (_resolution * _resolution));

        StatsMin.Text = $"最小值: {min:F3}";
        StatsMax.Text = $"最大值: {max:F3}";
        StatsMean.Text = $"平均值: {mean:F3}";
        StatsStdDev.Text = $"标准差: {stdDev:F3}";

        var bitmap = new WriteableBitmap(_resolution, _resolution, 96, 96, PixelFormats.Bgr32, null);
        var pixels = new byte[_resolution * _resolution * 4];

        for (int y = 0; y < _resolution; y++)
        {
            for (int x = 0; x < _resolution; x++)
            {
                float normalized = (noiseData[x, y] - min) / (max - min);
                var color = GetColorForValue(normalized);

                int idx = (y * _resolution + x) * 4;
                pixels[idx] = color.B;
                pixels[idx + 1] = color.G;
                pixels[idx + 2] = color.R;
                pixels[idx + 3] = 255;
            }
        }

        bitmap.WritePixels(new Int32Rect(0, 0, _resolution, _resolution), pixels, _resolution * 4, 0);
        NoiseImage.Source = bitmap;

        watch.Stop();
        GenTimeText.Text = $"生成时间: {watch.ElapsedMilliseconds} ms";
    }

    private (byte R, byte G, byte B) GetColorForValue(float value)
    {
        if (ColorGrayscale.IsChecked == true)
        {
            byte v = (byte)(value * 255);
            return (v, v, v);
        }
        else if (ColorTerrain.IsChecked == true)
        {
            if (value < 0.3f) // 深水
                return (20, 50, 120);
            if (value < 0.4f) // 浅水
                return (40, 100, 180);
            if (value < 0.45f) // 沙滩
                return (210, 190, 140);
            if (value < 0.6f) // 草地
                return (50, 160, 50);
            if (value < 0.75f) // 森林
                return (30, 110, 30);
            if (value < 0.88f) // 山石
                return (100, 90, 80);
            return (255, 255, 255); // 雪
        }
        else // 热力图
        {
            if (value < 0.25f)
                return ((byte)(value * 4 * 128), 0, 128);
            if (value < 0.5f)
                return (0, (byte)((value - 0.25f) * 4 * 255), 255);
            if (value < 0.75f)
                return (0, 255, (byte)(255 - (value - 0.5f) * 4 * 255));
            return ((byte)((value - 0.75f) * 4 * 255), 255, 0);
        }
    }

    private void Log(string message)
    {
        LogText.Text = $"[{DateTime.Now:HH:mm:ss}] {message}\n" + LogText.Text;
        if (LogText.Text.Length > 5000)
            LogText.Text = LogText.Text.Substring(0, 5000);
    }

    #region 事件处理

    private void ParameterChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        ScaleValue.Text = ScaleSlider.Value.ToString("F3");
        OctavesValue.Text = OctavesSlider.Value.ToString();
        PersistenceValue.Text = PersistenceSlider.Value.ToString("F2");

        if (IsLoaded) RegenerateNoise();
    }

    private void ResolutionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ResolutionCombo.SelectedItem is ComboBoxItem item)
        {
            var text = item.Content.ToString()!;
            _resolution = int.Parse(text.Split('x')[0]);
            ResolutionText.Text = $"分辨率: {_resolution}x{_resolution}";

            if (IsLoaded) RegenerateNoise();
        }
    }

    private void NoiseTypeChanged(object sender, RoutedEventArgs e)
    {
        if (TypePerlin.IsChecked == true)
        {
            AlgorithmTitle.Text = "Perlin 噪声";
            AlgorithmDesc.Text = "Ken Perlin 1985年发明的经典梯度噪声。通过网格点的伪随机梯度向量插值生成平滑的自然噪声，是程序化内容生成的基石。";
        }
        else
        {
            AlgorithmTitle.Text = "Simplex 噪声";
            AlgorithmDesc.Text = "Perlin 噪声的改进版，使用单纯形网格而非正方形网格。各向同性更好，无明显方向性，计算更快，是现代游戏的首选。";
        }

        if (IsLoaded) RegenerateNoise();
    }

    private void ColorModeChanged(object sender, RoutedEventArgs e)
    {
        if (IsLoaded) RegenerateNoise();
    }

    private void RegenerateButton_Click(object sender, RoutedEventArgs e)
    {
        int seed = new Random().Next();
        _perlinNoise = new PerlinNoise(seed);
        _simplexNoise = new SimplexNoise(seed);
        RegenerateNoise();
        Log($"重新生成噪声，种子: {seed}");
    }

    private async void AnimateButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isAnimating)
        {
            _isAnimating = false;
            AnimateButton.Content = "▶ 3D 动画";
            return;
        }

        _isAnimating = true;
        AnimateButton.Content = "⏹ 停止";
        _animOffset = 0;

        Log("开始 3D 噪声动画");

        while (_isAnimating)
        {
            _animOffset += 0.02f;
            RegenerateNoiseAnimated();
            await Task.Delay(33);
        }
    }

    private void RegenerateNoiseAnimated()
    {
        float scale = (float)ScaleSlider.Value;
        int octaves = (int)OctavesSlider.Value;
        float persistence = (float)PersistenceSlider.Value;

        var noiseData = new float[_resolution, _resolution];
        float min = float.MaxValue, max = float.MinValue;

        for (int y = 0; y < _resolution; y++)
        {
            for (int x = 0; x < _resolution; x++)
            {
                float noise = TypeSimplex.IsChecked == true
                    ? _simplexNoise!.Noise3D(x * scale, y * scale, _animOffset)
                    : _perlinNoise!.Noise3D(x * scale, y * scale, _animOffset);

                noiseData[x, y] = noise;
                min = System.Math.Min(min, noise);
                max = System.Math.Max(max, noise);
            }
        }

        var bitmap = new WriteableBitmap(_resolution, _resolution, 96, 96, PixelFormats.Bgr32, null);
        var pixels = new byte[_resolution * _resolution * 4];

        for (int y = 0; y < _resolution; y++)
        {
            for (int x = 0; x < _resolution; x++)
            {
                float normalized = (noiseData[x, y] - min) / (max - min);
                var color = GetColorForValue(normalized);

                int idx = (y * _resolution + x) * 4;
                pixels[idx] = color.B;
                pixels[idx + 1] = color.G;
                pixels[idx + 2] = color.R;
                pixels[idx + 3] = 255;
            }
        }

        bitmap.WritePixels(new Int32Rect(0, 0, _resolution, _resolution), pixels, _resolution * 4, 0);
        NoiseImage.Source = bitmap;
    }

    #endregion
}
