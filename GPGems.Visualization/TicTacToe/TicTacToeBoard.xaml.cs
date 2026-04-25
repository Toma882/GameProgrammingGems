using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using GPGems.AI.Games;

namespace GPGems.Visualization.TicTacToe;

/// <summary>
/// 井字棋棋盘控件
/// </summary>
public partial class TicTacToeBoard : UserControl
{
    private readonly Button[] _cells = new Button[9];
    private readonly Brush _xBrush = Brushes.DarkBlue;
    private readonly Brush _oBrush = Brushes.DarkRed;

    /// <summary>用户点击格子时触发</summary>
    public event EventHandler<int>? CellClicked;

    public TicTacToeBoard()
    {
        InitializeComponent();
        InitializeCells();
    }

    private void InitializeCells()
    {
        for (int i = 0; i < 9; i++)
        {
            int index = i;
            var button = new Button
            {
                FontSize = 48,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(2),
                Background = Brushes.White,
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1)
            };
            button.Click += (s, e) => CellClicked?.Invoke(this, index);
            _cells[i] = button;
            BoardGrid.Children.Add(button);
        }
    }

    /// <summary>更新棋盘显示</summary>
    public void UpdateBoard(GPGems.AI.Games.TicTacToe game)
    {
        for (int i = 0; i < 9; i++)
        {
            char cell = game.GetBoardCell(i);
            _cells[i].Content = cell == ' ' ? null : cell.ToString();
            _cells[i].Foreground = cell == 'X' ? _xBrush : _oBrush;
            _cells[i].IsEnabled = cell == ' ';
        }
    }

    /// <summary>设置胜负高亮</summary>
    public void SetWinHighlight(int[] winLine)
    {
        foreach (var cell in _cells)
            cell.Background = Brushes.White;

        if (winLine != null)
        {
            foreach (int i in winLine)
                _cells[i].Background = Brushes.LightGreen;
        }
    }

    /// <summary>重置棋盘</summary>
    public void Reset()
    {
        foreach (var cell in _cells)
        {
            cell.Content = null;
            cell.Background = Brushes.White;
            cell.IsEnabled = true;
        }
    }
}
