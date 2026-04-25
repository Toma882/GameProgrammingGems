using GPGems.AI.GameTrees;
using ScottPlot;
using ScottPlot.Plottables;

namespace GPGems.Visualization.GameTree;

/// <summary>
/// 博弈树可视化渲染器
/// 使用 ScottPlot 绘制搜索树，高亮剪枝节点和最佳路径
/// </summary>
public class GameTreeRenderer
{
    /// <summary>将搜索树绘制到 ScottPlot 图表</summary>
    public static void DrawSearchTree(Plot plot, SearchNode root, int maxDepth = 5)
    {
        plot.Clear();
        plot.Title("Alpha-Beta 搜索树");
        plot.Axes.SetLimitsX(-1, 1);
        plot.Axes.SetLimitsY(-0.5, maxDepth + 0.5);
        plot.HideGrid();
        plot.Layout.Frameless();

        // 递归计算位置并绘制
        var nodePositions = new Dictionary<SearchNode, (double x, double y)>();
        CalculateNodePositions(root, 0, -1, 1, nodePositions, maxDepth);
        DrawNodesAndEdges(plot, root, nodePositions, maxDepth);
    }

    /// <summary>递归计算每个节点的坐标位置</summary>
    private static void CalculateNodePositions(
        SearchNode node,
        int depth,
        double left,
        double right,
        Dictionary<SearchNode, (double x, double y)> positions,
        int maxDepth)
    {
        if (depth > maxDepth || !node.Children.Any())
        {
            double x = (left + right) / 2;
            double posY = maxDepth - depth;
            positions[node] = (x, posY);
            return;
        }

        double width = right - left;
        double childWidth = width / node.Children.Count;

        for (int i = 0; i < node.Children.Count; i++)
        {
            var child = node.Children[i];
            double childLeft = left + i * childWidth;
            double childRight = childLeft + childWidth;
            CalculateNodePositions(child, depth + 1, childLeft, childRight, positions, maxDepth);
        }

        // 父节点位于子节点中心上方
        var childrenPositions = node.Children.Select(c => positions[c]).ToList();
        double avgX = childrenPositions.Average(p => p.x);
        double y = maxDepth - depth;
        positions[node] = (avgX, y);
    }

    /// <summary>绘制所有节点和连线</summary>
    private static void DrawNodesAndEdges(
        Plot plot,
        SearchNode root,
        Dictionary<SearchNode, (double x, double y)> positions,
        int maxDepth)
    {
        // 先画所有连线
        DrawEdges(plot, root, positions, maxDepth);

        // 再画所有节点（在连线之上）
        foreach (var (node, (x, y)) in positions)
        {
            DrawNode(plot, node, x, y);
        }
    }

    /// <summary>递归绘制连线</summary>
    private static void DrawEdges(
        Plot plot,
        SearchNode node,
        Dictionary<SearchNode, (double x, double y)> positions,
        int maxDepth)
    {
        var (x1, y1) = positions[node];

        foreach (var child in node.Children)
        {
            var (x2, y2) = positions[child];

            // 线条颜色
            ScottPlot.Color lineColor = child.IsPruned ? Colors.Red :
                                       child.IsBestPath ? Colors.Green :
                                       Colors.Gray;

            var line = plot.Add.Line(x1, y1, x2, y2);
            line.Color = lineColor;
            line.LineWidth = child.IsBestPath ? 3 : 1.5f;

            DrawEdges(plot, child, positions, maxDepth);
        }
    }

    /// <summary>绘制单个节点</summary>
    private static void DrawNode(Plot plot, SearchNode node, double x, double y)
    {
        // 节点颜色
        ScottPlot.Color fillColor = node.IsPruned ? Color.FromHex("#ffcccc") :
                                  node.IsBestPath ? Color.FromHex("#ccffcc") :
                                  Color.FromHex("#e8e8e8");

        ScottPlot.Color borderColor = node.IsPruned ? Colors.Red :
                                    node.IsBestPath ? Colors.Green :
                                    Colors.Gray;

        // 绘制圆形节点
        var circle = plot.Add.Circle(x, y, 0.04);
        circle.FillColor = fillColor;
        circle.LineColor = borderColor;
        circle.LineWidth = node.IsBestPath ? 3 : 1.5f;

        // 显示节点值
        string label = node.Move != null ? $"{node.Value}" : "Root";
        var text = plot.Add.Text(label, x, y);
        text.LabelFontSize = 10;
        text.LabelBold = true;
    }

    /// <summary>获取搜索统计信息</summary>
    public static (int totalNodes, int prunedNodes, int bestPathNodes) CountNodes(SearchNode node)
    {
        int total = 1;
        int pruned = node.IsPruned ? 1 : 0;
        int best = node.IsBestPath ? 1 : 0;

        foreach (var child in node.Children)
        {
            var (cTotal, cPruned, cBest) = CountNodes(child);
            total += cTotal;
            pruned += cPruned;
            best += cBest;
        }

        return (total, pruned, best);
    }
}
