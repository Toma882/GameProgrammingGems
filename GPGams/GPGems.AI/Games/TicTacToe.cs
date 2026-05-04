using GPGems.AI.GameTrees;

namespace GPGems.AI.Games;

/// <summary>
/// 井字棋游戏实现
/// 作为博弈树AI的示例游戏，实现 IGameState 接口
/// </summary>
public class TicTacToe : IGameState
{
    /// <summary>3x3棋盘，共9格</summary>
    private readonly char[] _board = new char[9];
    /// <summary>当前轮到的玩家</summary>
    private char _currentPlayer = 'X';
    /// <summary>历史记录栈，用于撤销移动</summary>
    private readonly Stack<char> _history = new();

    /// <summary>获取当前玩家</summary>
    public char CurrentPlayer => _currentPlayer;

    /// <summary>初始化一个空棋盘</summary>
    public TicTacToe()
    {
        Array.Fill(_board, ' ');
    }

    /// <summary>获取指定位置的棋子</summary>
    public char GetBoardCell(int index) => _board[index];

    /// <summary>判断游戏是否结束（有玩家获胜或棋盘已满）</summary>
    public bool IsGameOver() => GetWinner() != ' ' || GetAvailableMoves().Any() == false;

    /// <summary>
    /// 评估当前局面
    /// X获胜返回+10，O获胜返回-10，平局或未分胜负返回0
    /// 注意：这里没有加入深度加权，是简单版本
    /// </summary>
    public int Evaluate()
    {
        char winner = GetWinner();
        if (winner == 'X') return 10;
        if (winner == 'O') return -10;
        return 0;
    }

    /// <summary>获取所有合法移动（所有空格）</summary>
    public IEnumerable<Move> GetAvailableMoves()
    {
        for (int i = 0; i < 9; i++)
        {
            if (_board[i] == ' ')
                yield return new Move(i, i, _currentPlayer);
        }
    }

    /// <summary>执行一步移动</summary>
    public void MakeMove(Move move)
    {
        _board[move.From] = move.Player;
        _history.Push(move.Player);
        // 切换玩家
        _currentPlayer = _currentPlayer == 'X' ? 'O' : 'X';
    }

    /// <summary>撤销一步移动（用于搜索回溯）</summary>
    public void UnmakeMove(Move move)
    {
        _board[move.From] = ' ';
        _history.Pop();
        _currentPlayer = _currentPlayer == 'X' ? 'O' : 'X';
    }

    /// <summary>检查是否有玩家获胜</summary>
    /// <returns>获胜玩家 'X'/'O'，没有获胜者返回空格</returns>
    public char GetWinner()
    {
        // 8条获胜线：3横 + 3竖 + 2斜
        int[,] winLines = { { 0, 1, 2 }, { 3, 4, 5 }, { 6, 7, 8 },
                            { 0, 3, 6 }, { 1, 4, 7 }, { 2, 5, 8 },
                            { 0, 4, 8 }, { 2, 4, 6 } };

        for (int i = 0; i < 8; i++)
        {
            int a = winLines[i, 0], b = winLines[i, 1], c = winLines[i, 2];
            // 检查三个格子是否相同且非空格
            if (_board[a] != ' ' && _board[a] == _board[b] && _board[b] == _board[c])
                return _board[a];
        }
        return ' ';
    }

    /// <summary>输出棋盘的文本表示</summary>
    public override string ToString()
    {
        return $"""
             {_board[0]} | {_board[1]} | {_board[2]}
            ---+---+---
             {_board[3]} | {_board[4]} | {_board[5]}
            ---+---+---
             {_board[6]} | {_board[7]} | {_board[8]}
            """;
    }
}
