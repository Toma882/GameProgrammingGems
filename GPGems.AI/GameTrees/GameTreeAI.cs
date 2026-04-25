/* Copyright (C) Jan Svarovsky, 2000.
 * 从《游戏编程精粹 1》移植到 C#
 * 算法核心：极大极小值、NegaMax、Alpha-Beta剪枝
 */

namespace GPGems.AI.GameTrees;

/// <summary>
/// 表示棋盘上的一步移动
/// </summary>
/// <param name="From">起始位置</param>
/// <param name="To">目标位置（井字棋中与From相同）</param>
/// <param name="Player">执行移动的玩家（'X'或'O'）</param>
public record Move(int From, int To, char Player)
{
    /// <summary>此步的评估值（由AI计算得出）</summary>
    public int Value { get; set; }
    public override string ToString() => $"{Player}: ({From},{To}) = {Value}";
}

/// <summary>
/// 游戏状态接口
/// 所有可使用博弈树AI的游戏都需要实现此接口
/// </summary>
public interface IGameState
{
    /// <summary>判断游戏是否结束</summary>
    bool IsGameOver();
    /// <summary>评估当前局面，正数对最大化玩家有利，负数对最小化玩家有利</summary>
    int Evaluate();
    /// <summary>获取当前所有合法移动</summary>
    IEnumerable<Move> GetAvailableMoves();
    /// <summary>执行一步移动</summary>
    void MakeMove(Move move);
    /// <summary>撤销一步移动（用于回溯）</summary>
    void UnmakeMove(Move move);
    /// <summary>当前轮到哪个玩家</summary>
    char CurrentPlayer { get; }
    /// <summary>获取指定位置的棋盘状态</summary>
    char GetBoardCell(int index);
}

/// <summary>
/// 搜索树节点
/// 用于构建可视化搜索树，展示剪枝和最佳路径
/// </summary>
public class SearchNode
{
    /// <summary>此节点所在的深度（层数）</summary>
    public int Depth { get; set; }
    /// <summary>此节点的评估值</summary>
    public int Value { get; set; }
    /// <summary>导致此局面的移动</summary>
    public Move? Move { get; set; }
    /// <summary>子节点列表</summary>
    public List<SearchNode> Children { get; } = [];
    /// <summary>此节点是否被Alpha-Beta剪枝</summary>
    public bool IsPruned { get; set; }
    /// <summary>此节点是否在最佳路径上</summary>
    public bool IsBestPath { get; set; }
}

/// <summary>
/// 博弈树AI核心算法实现
/// 包含三种经典算法：极大极小值、NegaMax、Alpha-Beta剪枝
/// </summary>
public class GameTreeAI
{
    /// <summary>用于表示"无穷大"的估值</summary>
    public const int Infinity = 999999;
    private readonly IGameState _game;

    /// <summary>初始化AI，传入具体游戏状态</summary>
    public GameTreeAI(IGameState game) => _game = game;

    /// <summary>
    /// 极大化玩家搜索（Max策略）
    /// 在所有可能中选择对自己最有利的走法（最大值）
    /// </summary>
    /// <param name="ply">搜索深度</param>
    /// <returns>此局面的最佳评估值</returns>
    public int Maximize(int ply)
    {
        // 达到搜索深度或游戏结束，返回局面评估
        if (ply == 0 || _game.IsGameOver())
            return _game.Evaluate();

        int best = -Infinity;

        // 尝试每一步可能的走法
        foreach (var move in _game.GetAvailableMoves())
        {
            _game.MakeMove(move);
            // 轮到对手进行最小化搜索
            int newValue = Minimize(ply - 1);
            _game.UnmakeMove(move);
            // 取最大值作为最佳选择
            if (newValue > best) best = newValue;
        }
        return best;
    }

    /// <summary>
    /// 最小化玩家搜索（Min策略）
    /// 在所有可能中选择对对手最不利的走法（最小值）
    /// </summary>
    /// <param name="ply">搜索深度</param>
    /// <returns>此局面的最佳评估值</returns>
    public int Minimize(int ply)
    {
        if (ply == 0 || _game.IsGameOver())
            return _game.Evaluate();

        int best = Infinity;

        foreach (var move in _game.GetAvailableMoves())
        {
            _game.MakeMove(move);
            // 轮到对手进行最大化搜索
            int newValue = Maximize(ply - 1);
            _game.UnmakeMove(move);
            // 取最小值作为最佳选择
            if (newValue < best) best = newValue;
        }
        return best;
    }

    /// <summary>
    /// NegaMax算法 - 极大极小值的简化版本
    /// 利用"最大化对手的最小值 = 最小化对手的最大值的负数"这一性质
    /// 用单一函数替代Maximize和Minimize两个函数
    /// </summary>
    /// <param name="ply">搜索深度</param>
    /// <returns>此局面的最佳评估值</returns>
    public int NegaMax(int ply)
    {
        if (ply == 0 || _game.IsGameOver())
            return _game.Evaluate();

        int best = -Infinity;

        foreach (var move in _game.GetAvailableMoves())
        {
            _game.MakeMove(move);
            // 递归搜索并取负值（转换玩家视角）
            int newValue = -NegaMax(ply - 1);
            _game.UnmakeMove(move);
            if (newValue > best) best = newValue;
        }
        return best;
    }

    /// <summary>
    /// Alpha-Beta剪枝算法
    /// 在NegaMax基础上增加剪枝，可以大幅减少搜索节点数
    /// 是博弈树搜索的工业标准算法
    /// </summary>
    /// <param name="ply">搜索深度</param>
    /// <param name="alpha">当前玩家已知的最佳下限</param>
    /// <param name="beta">当前玩家已知的最佳上限</param>
    /// <returns>此局面的最佳评估值</returns>
    public int AlphaBeta(int ply, int alpha, int beta)
    {
        if (ply == 0 || _game.IsGameOver())
            return _game.Evaluate();

        foreach (var move in _game.GetAvailableMoves())
        {
            _game.MakeMove(move);
            // 注意：交换alpha/beta并取负值，转换玩家视角
            int newValue = -AlphaBeta(ply - 1, -beta, -alpha);
            _game.UnmakeMove(move);

            // Beta剪枝：发现必然更差的分支，直接放弃
            if (newValue >= beta) return beta;
            // 更新alpha下限
            if (newValue > alpha) alpha = newValue;
        }
        return alpha;
    }

    /// <summary>
    /// 获取AI的最佳走法（带完整搜索树信息用于可视化）
    /// </summary>
    /// <param name="maxPly">搜索深度</param>
    /// <param name="rootNode">输出：搜索树根节点（用于可视化）</param>
    /// <returns>AI选择的最佳移动</returns>
    public Move? GetBestMove(int maxPly, out SearchNode? rootNode)
    {
        rootNode = new SearchNode { Depth = maxPly, Value = -Infinity };
        Move? bestMove = null;
        int bestValue = -Infinity;

        // 对第一层所有可能走法进行搜索
        foreach (var move in _game.GetAvailableMoves())
        {
            var childNode = new SearchNode { Depth = maxPly - 1, Move = move };
            rootNode.Children.Add(childNode);

            _game.MakeMove(move);
            // 进行Alpha-Beta搜索，同时构建搜索树节点
            int value = -AlphaBetaWithNode(maxPly - 1, -Infinity, Infinity, childNode);
            _game.UnmakeMove(move);

            move.Value = value;
            childNode.Value = value;

            // 记录最佳走法
            if (value > bestValue)
            {
                bestValue = value;
                bestMove = move;
                childNode.IsBestPath = true;
            }
        }

        rootNode.Value = bestValue;
        return bestMove;
    }

    /// <summary>
    /// 带节点记录的Alpha-Beta搜索
    /// 与标准Alpha-Beta相同，但额外记录搜索树结构用于可视化
    /// </summary>
    private int AlphaBetaWithNode(int ply, int alpha, int beta, SearchNode node)
    {
        if (ply == 0 || _game.IsGameOver())
        {
            int eval = _game.Evaluate();
            node.Value = eval;
            return eval;
        }

        foreach (var move in _game.GetAvailableMoves())
        {
            var childNode = new SearchNode { Depth = ply - 1, Move = move };
            node.Children.Add(childNode);

            _game.MakeMove(move);
            int newValue = -AlphaBetaWithNode(ply - 1, -beta, -alpha, childNode);
            _game.UnmakeMove(move);

            move.Value = newValue;
            childNode.Value = newValue;

            if (newValue >= beta)
            {
                childNode.IsPruned = true; // 标记此节点被剪枝
                return beta;
            }
            if (newValue > alpha)
                alpha = newValue;
        }
        return alpha;
    }
}
