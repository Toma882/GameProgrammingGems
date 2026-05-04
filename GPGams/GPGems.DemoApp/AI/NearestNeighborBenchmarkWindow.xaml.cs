using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using GPGems.Core.Algorithms;

namespace GPGems.DemoApp.AI;

/// <summary>
/// 最近邻算法性能对比演示窗口
/// </summary>
public partial class NearestNeighborBenchmarkWindow : Window
{
    private readonly Random _random = new(42);

    public NearestNeighborBenchmarkWindow()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            PointCountSlider.ValueChanged += (_, _) => PointCountText.Text = ((int)PointCountSlider.Value).ToString();
            DimensionSlider.ValueChanged += (_, _) => DimensionText.Text = ((int)DimensionSlider.Value).ToString();
            KValueSlider.ValueChanged += (_, _) => KValueText.Text = ((int)KValueSlider.Value).ToString();
            QueryCountSlider.ValueChanged += (_, _) => QueryCountText.Text = ((int)QueryCountSlider.Value).ToString();
        };
    }

    private void RunBenchmark_Click(object sender, RoutedEventArgs e)
    {
        RunBenchmarkBtn.IsEnabled = false;
        LogText.Text = string.Empty;
        ResultsPanel.Items.Clear();

        int pointCount = (int)PointCountSlider.Value;
        int dimensions = (int)DimensionSlider.Value;
        int k = (int)KValueSlider.Value;
        int queryCount = (int)QueryCountSlider.Value;

        Log($"正在生成 {pointCount} 个 {dimensions} 维数据点...");

        // 生成测试数据
        var dataPoints = GenerateRandomPoints(pointCount, dimensions);
        var testQueries = GenerateRandomPoints(queryCount, dimensions).Select(p => p.Point).ToArray();

        Log($"数据生成完成，准备运行基准测试...");
        Log($"测试配置: k={k}, 查询次数={queryCount}\n");

        // 构建要测试的算法列表
        var algorithms = new List<INearestNeighborIndex<int>>();
        if (UseBruteForce.IsChecked == true) algorithms.Add(new BruteForceSearch<int>(dimensions));
        if (UseKDTree.IsChecked == true) algorithms.Add(new KDTreeIndex<int>(dimensions));
        if (UseBallTree.IsChecked == true) algorithms.Add(new BallTreeIndex<int>(dimensions));
        if (UseLSH.IsChecked == true) algorithms.Add(new LocalitySensitiveHashing<int>(dimensions));

        if (algorithms.Count == 0)
        {
            Log("请至少选择一个算法！");
            RunBenchmarkBtn.IsEnabled = true;
            return;
        }

        // 运行基准测试
        var result = NearestNeighborBenchmark.Run(algorithms.ToArray(), dataPoints, testQueries, k);

        // 显示结果
        Log("=== 基准测试结果 ===");
        Log(result.ToString());

        // 计算相对加速比
        var brute = result.Results.FirstOrDefault(r => r.AlgorithmName.Contains("BruteForce"));
        if (brute != null)
        {
            Log("\n=== 相对加速比（相对于暴力搜索） ===");
            foreach (var r in result.Results)
            {
                if (r.AlgorithmName != brute.AlgorithmName)
                {
                    double speedup = brute.AvgQueryTimeUs / r.AvgQueryTimeUs;
                    Log($"{r.AlgorithmName,-20} {speedup,8:F1}x 加速");
                }
            }
        }

        // 在UI上显示结果卡片
        DisplayResultCards(result);

        RunBenchmarkBtn.IsEnabled = true;
    }

    private async void RunAnimation_Click(object sender, RoutedEventArgs e)
    {
        RunAnimationBtn.IsEnabled = false;
        LogText.Text = string.Empty;
        ResultsPanel.Items.Clear();

        int k = (int)KValueSlider.Value;
        int queryCount = (int)QueryCountSlider.Value;

        Log("开始维度扫描动画...\n");

        // 扫描不同维度下的性能
        for (int dim = 2; dim <= 64; dim *= 2)
        {
            Log($"--- 维度 = {dim} ---");

            var dataPoints = GenerateRandomPoints(5000, dim);
            var testQueries = GenerateRandomPoints(100, dim).Select(p => p.Point).ToArray();

            var algorithms = new INearestNeighborIndex<int>[]
            {
                new BruteForceSearch<int>(dim),
                new BallTreeIndex<int>(dim),
            };

            var result = NearestNeighborBenchmark.Run(algorithms, dataPoints, testQueries, k);

            var brute = result.Results[0];
            var ball = result.Results[1];
            double speedup = brute.AvgQueryTimeUs / ball.AvgQueryTimeUs;

            Log($"Ball树: {ball.AvgQueryTimeUs:F1}μs / 查询, 加速比 {speedup:F1}x\n");

            await Task.Delay(500);
        }

        Log("\n维度扫描完成！高维下Ball树优势更明显。");
        RunAnimationBtn.IsEnabled = true;
    }

    private List<(float[] Point, int Value)> GenerateRandomPoints(int count, int dimensions)
    {
        var result = new List<(float[], int)>(count);
        for (int i = 0; i < count; i++)
        {
            var point = new float[dimensions];
            for (int d = 0; d < dimensions; d++)
            {
                point[d] = (float)(_random.NextDouble() * 100);
            }
            result.Add((point, i));
        }
        return result;
    }

    private void DisplayResultCards(NearestNeighborBenchmark.BenchmarkResult result)
    {
        var colors = new[]
        {
            Color.FromRgb(231, 76, 60),    // 暴力搜索 - 红
            Color.FromRgb(52, 152, 219),   // KD树 - 蓝
            Color.FromRgb(46, 204, 113),   // Ball树 - 绿
            Color.FromRgb(241, 196, 15)    // LSH - 黄
        };

        int colorIndex = 0;
        foreach (var r in result.Results)
        {
            var color = colors[colorIndex++ % colors.Length];
            var card = CreateResultCard(r, color);
            ResultsPanel.Items.Add(card);
        }
    }

    private Border CreateResultCard(NearestNeighborBenchmark.AlgorithmResult result, Color color)
    {
        var card = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(22, 33, 62)),
            BorderBrush = new SolidColorBrush(color),
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(15),
            Margin = new Thickness(5),
            Width = 280
        };

        var panel = new StackPanel();

        panel.Children.Add(new TextBlock
        {
            Text = result.AlgorithmName,
            Foreground = new SolidColorBrush(color),
            FontSize = 14,
            FontWeight = FontWeights.Bold
        });

        panel.Children.Add(new TextBlock
        {
            Text = $"构建时间: {result.BuildTimeMs:F2} ms",
            Foreground = Brushes.White,
            FontSize = 11,
            Margin = new Thickness(0, 5, 0, 0)
        });

        panel.Children.Add(new TextBlock
        {
            Text = $"平均查询: {result.AvgQueryTimeUs:F1} μs",
            Foreground = Brushes.White,
            FontSize = 16,
            FontWeight = FontWeights.Bold
        });

        panel.Children.Add(new TextBlock
        {
            Text = result.Stats,
            Foreground = new SolidColorBrush(Color.FromRgb(160, 160, 160)),
            FontSize = 10,
            Margin = new Thickness(0, 8, 0, 0)
        });

        card.Child = panel;
        return card;
    }

    private void Log(string message)
    {
        string timestamp = DateTime.Now.ToString("HH:mm:ss");
        LogText.Text += $"[{timestamp}] {message}\n";
    }
}
