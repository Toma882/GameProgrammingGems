# 在GameProgrammingGems目录下创建
cd "d:\Project_Git@SVN\GameProgrammingGems"
dotnet new wpf -n GameTreeVisualizer
cd GameTreeVisualizer

# 添加NuGet包
dotnet add package ScottPlot.WPF
dotnet add package CommunityToolkit.Mvvm  # MVVM框架，可选但推荐


## 方案C：Avalonia + ScottPlot（跨平台）
- 如果你需要Mac/Linux也能运行：


dotnet new avalonia.app -n GameTreeAvalonia
cd GameTreeAvalonia
dotnet add package ScottPlot.Avalonia
