using System.Windows;
using System.Windows.Controls;
using GPGems.AI.GameTrees;
using GPGems.AI.Games;
using GPGems.Visualization.GameTree;

namespace GPGems.DemoApp;

/// <summary>
/// 博弈树 AI 演示窗口
/// </summary>
public partial class GameTreeWindow : Window
{
    private GPGems.AI.Games.TicTacToe _game = null!;
    private GameTreeAI _ai = null!;
    private bool _isAiTurn;
    private int _searchDepth = 4;
    private readonly Stack<(Move move, SearchNode? searchNode)> _history = new();

    public GameTreeWindow()
    {
        InitializeComponent();
        NewGame();
    }

    /// <summary>开始新游戏</summary>
    private void NewGame()
    {
        _game = new GPGems.AI.Games.TicTacToe();
        _ai = new GameTreeAI(_game);
        _history.Clear();
        GameBoard.Reset();
        UpdateStatus();

        // 绘制空的搜索树
        var emptyNode = new SearchNode { Value = 0 };
        GameTreeRenderer.DrawSearchTree(SearchTreePlot.Plot, emptyNode, 2);
        SearchTreePlot.Refresh();

        // 如果 AI 先手
        if (AiStartsCheckBox.IsChecked == true)
        {
            _isAiTurn = true;
            MakeAiMove();
        }
        else
        {
            _isAiTurn = false;
        }
    }

    /// <summary>玩家点击棋盘格子</summary>
    private void GameBoard_CellClicked(object? sender, int index)
    {
        if (_game.IsGameOver() || _isAiTurn)
            return;

        // 玩家走棋
        var playerMove = new Move(index, index, 'O');
        _game.MakeMove(playerMove);
        _history.Push((playerMove, null));
        GameBoard.UpdateBoard(_game);
        UpdateStatus();

        if (!_game.IsGameOver())
        {
            // AI 走棋
            _isAiTurn = true;
            MakeAiMove();
        }
        else
        {
            CheckGameEnd();
        }
    }

    /// <summary>AI 计算并走棋</summary>
    private void MakeAiMove()
    {
        // 获取 AI 最佳走法，同时获取搜索树
        var bestMove = _ai.GetBestMove(_searchDepth, out var rootNode);

        if (bestMove != null)
        {
            _game.MakeMove(bestMove);
            _history.Push((bestMove, rootNode));
            GameBoard.UpdateBoard(_game);
            UpdateStatus();

            // 绘制搜索树
            if (rootNode != null)
            {
                GameTreeRenderer.DrawSearchTree(SearchTreePlot.Plot, rootNode, _searchDepth);
                SearchTreePlot.Refresh();

                // 更新统计
                var (total, pruned, _) = GameTreeRenderer.CountNodes(rootNode);
                double rate = total > 0 ? (double)pruned / total * 100 : 0;
                TotalNodesText.Text = $"总节点数: {total}";
                PrunedNodesText.Text = $"剪枝节点数: {pruned}";
                PruneRateText.Text = $"剪枝率: {rate:F1}%";
            }
        }

        _isAiTurn = false;
        CheckGameEnd();
    }

    /// <summary>检查游戏是否结束</summary>
    private void CheckGameEnd()
    {
        char winner = _game.GetWinner();
        if (winner != ' ')
        {
            StatusText.Text = winner == 'X' ? "🎉 AI 获胜！" : "🎉 你获胜！";
            HighlightWinLine();
        }
        else if (!_game.GetAvailableMoves().Any())
        {
            StatusText.Text = "🤝 平局！";
        }
    }

    /// <summary>高亮获胜连线</summary>
    private void HighlightWinLine()
    {
        int[][] winLines = { new[] { 0, 1, 2 }, new[] { 3, 4, 5 }, new[] { 6, 7, 8 },
                            new[] { 0, 3, 6 }, new[] { 1, 4, 7 }, new[] { 2, 5, 8 },
                            new[] { 0, 4, 8 }, new[] { 2, 4, 6 } };

        foreach (var line in winLines)
        {
            char c = _game.GetBoardCell(line[0]);
            if (c != ' ' && c == _game.GetBoardCell(line[1]) && c == _game.GetBoardCell(line[2]))
            {
                GameBoard.SetWinHighlight(line);
                break;
            }
        }
    }

    /// <summary>更新状态显示</summary>
    private void UpdateStatus()
    {
        CurrentPlayerText.Text = $"当前玩家: {_game.CurrentPlayer}";

        if (_game.IsGameOver())
        {
            StatusText.Text = "游戏结束";
        }
        else if (_isAiTurn)
        {
            StatusText.Text = "AI 思考中...";
        }
        else
        {
            StatusText.Text = "轮到你了 (O)";
        }
    }

    /// <summary>新游戏按钮</summary>
    private void NewGameBtn_Click(object sender, RoutedEventArgs e)
    {
        NewGame();
    }

    /// <summary>悔棋功能</summary>
    private void UndoBtn_Click(object sender, RoutedEventArgs e)
    {
        // 需要撤销两步：一步玩家，一步 AI
        if (_history.Count >= 2)
        {
            var (aiMove, _) = _history.Pop();
            _game.UnmakeMove(aiMove);
            var (playerMove, _) = _history.Pop();
            _game.UnmakeMove(playerMove);

            GameBoard.UpdateBoard(_game);
            GameBoard.SetWinHighlight(null!);
            UpdateStatus();

            StatusText.Text = "已悔棋";
        }
        else if (_history.Count >= 1 && AiStartsCheckBox.IsChecked == true)
        {
            var (aiMove, _) = _history.Pop();
            _game.UnmakeMove(aiMove);
            GameBoard.UpdateBoard(_game);
            GameBoard.SetWinHighlight(null!);
            UpdateStatus();

            _isAiTurn = false;
        }
    }

    /// <summary>搜索深度改变</summary>
    private void DepthSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        _searchDepth = (int)e.NewValue;
    }
}
