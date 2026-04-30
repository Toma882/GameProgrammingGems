/*
 * 字典树 Trie / Prefix Tree
 * 时间复杂度: O(L) 插入/查询, L=字符串长度
 * 空间复杂度: 共享前缀节省内存
 *
 * 经营游戏核心用途:
 *   - 自动补全: 玩家名/物品名搜索建议
 *   - 聊天敏感词过滤: 前缀匹配检测
 *   - 排行榜前缀查询: 玩家名/工会名快速搜索
 *   - 本地化文本索引: 多语言前缀查询
 */

using System;
using System.Collections.Generic;
using System.Text;

namespace GPGems.Core.DataStructures;

/// <summary>
/// Trie 节点
/// </summary>
internal class TrieNode
{
    public Dictionary<char, TrieNode> Children { get; }
    public bool IsEndOfWord { get; set; }
    public int Count { get; set; }  // 经过该节点的单词数量（前缀计数）

    public TrieNode()
    {
        Children = new Dictionary<char, TrieNode>();
        IsEndOfWord = false;
        Count = 0;
    }
}

/// <summary>
/// 字典树 - 前缀树
/// 支持前缀查询、自动补全、敏感词过滤
/// </summary>
public class Trie
{
    #region 字段与属性

    private readonly TrieNode _root;
    private int _totalWords;

    public int TotalWords => _totalWords;

    #endregion

    #region 构造函数

    public Trie()
    {
        _root = new TrieNode();
        _totalWords = 0;
    }

    #endregion

    #region 核心操作

    /// <summary>
    /// 插入单词
    /// </summary>
    public void Insert(string word)
    {
        if (string.IsNullOrEmpty(word))
            return;

        var current = _root;
        foreach (var c in word)
        {
            if (!current.Children.TryGetValue(c, out var node))
            {
                node = new TrieNode();
                current.Children[c] = node;
            }
            current = node;
            current.Count++;
        }

        if (!current.IsEndOfWord)
        {
            current.IsEndOfWord = true;
            _totalWords++;
        }
    }

    /// <summary>
    /// 批量插入单词
    /// </summary>
    public void InsertRange(IEnumerable<string> words)
    {
        foreach (var word in words)
        {
            Insert(word);
        }
    }

    /// <summary>
    /// 检查单词是否存在（精确匹配）
    /// </summary>
    public bool Contains(string word)
    {
        if (string.IsNullOrEmpty(word))
            return false;

        var node = FindNode(word);
        return node != null && node.IsEndOfWord;
    }

    /// <summary>
    /// 检查是否有任何单词以指定前缀开头
    /// </summary>
    public bool StartsWith(string prefix)
    {
        if (string.IsNullOrEmpty(prefix))
            return true;

        return FindNode(prefix) != null;
    }

    /// <summary>
    /// 删除单词
    /// </summary>
    public bool Remove(string word)
    {
        if (string.IsNullOrEmpty(word))
            return false;

        return Remove(_root, word, 0);
    }

    private bool Remove(TrieNode node, string word, int index)
    {
        if (index == word.Length)
        {
            if (!node.IsEndOfWord)
                return false;

            node.IsEndOfWord = false;
            _totalWords--;
            return node.Children.Count == 0;
        }

        char c = word[index];
        if (!node.Children.TryGetValue(c, out var childNode))
            return false;

        bool shouldDeleteChild = Remove(childNode, word, index + 1);

        if (shouldDeleteChild)
        {
            node.Children.Remove(c);
            return node.Children.Count == 0 && !node.IsEndOfWord;
        }

        childNode.Count--;
        return false;
    }

    /// <summary>
    /// 清空字典树
    /// </summary>
    public void Clear()
    {
        _root.Children.Clear();
        _root.Count = 0;
        _totalWords = 0;
    }

    #endregion

    #region 前缀查询与自动补全

    /// <summary>
    /// 获取所有以指定前缀开头的单词
    /// </summary>
    /// <param name="prefix">前缀</param>
    /// <param name="limit">最大返回数量</param>
    public List<string> GetWordsWithPrefix(string prefix, int limit = int.MaxValue)
    {
        var result = new List<string>();
        if (limit <= 0) return result;

        var prefixNode = FindNode(prefix);
        if (prefixNode == null) return result;

        CollectWords(prefixNode, new StringBuilder(prefix), result, limit);
        return result;
    }

    /// <summary>
    /// 获取最热门的单词（按前缀计数排序）
    /// </summary>
    /// <param name="prefix">前缀</param>
    /// <param name="count">返回数量</param>
    public List<string> GetSuggestions(string prefix, int count = 5)
    {
        return GetWordsWithPrefix(prefix, count);
    }

    /// <summary>
    /// 获取指定前缀的单词数量
    /// </summary>
    public int CountWordsWithPrefix(string prefix)
    {
        var node = FindNode(prefix);
        return node?.Count ?? 0;
    }

    #endregion

    #region 敏感词过滤

    /// <summary>
    /// 检查文本是否包含任何敏感词（子串匹配）
    /// </summary>
    public bool ContainsBadWord(string text)
    {
        for (int i = 0; i < text.Length; i++)
        {
            var current = _root;
            for (int j = i; j < text.Length; j++)
            {
                if (!current.Children.TryGetValue(text[j], out current))
                    break;

                if (current.IsEndOfWord)
                    return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 找出文本中包含的所有敏感词
    /// </summary>
    public List<string> FindAllBadWords(string text)
    {
        var result = new List<string>();
        var found = new HashSet<string>();

        for (int i = 0; i < text.Length; i++)
        {
            var current = _root;
            var sb = new StringBuilder();

            for (int j = i; j < text.Length; j++)
            {
                if (!current.Children.TryGetValue(text[j], out current))
                    break;

                sb.Append(text[j]);
                if (current.IsEndOfWord)
                {
                    found.Add(sb.ToString());
                }
            }
        }

        result.AddRange(found);
        return result;
    }

    /// <summary>
    /// 替换文本中的敏感词
    /// </summary>
    /// <param name="text">原始文本</param>
    /// <param name="replacement">替换字符, 如 '*'</param>
    public string CensorText(string text, char replacement = '*')
    {
        if (string.IsNullOrEmpty(text))
            return text;

        var result = new char[text.Length];
        Array.Copy(text.ToCharArray(), result, text.Length);

        for (int i = 0; i < text.Length; i++)
        {
            var current = _root;
            int matchLength = 0;

            for (int j = i; j < text.Length; j++)
            {
                if (!current.Children.TryGetValue(text[j], out current))
                    break;

                matchLength++;
                if (current.IsEndOfWord)
                {
                    // 替换匹配的字符
                    for (int k = i; k <= j; k++)
                    {
                        result[k] = replacement;
                    }
                }
            }
        }

        return new string(result);
    }

    #endregion

    #region 辅助方法

    /// <summary>
    /// 查找指定前缀对应的节点
    /// </summary>
    private TrieNode? FindNode(string prefix)
    {
        var current = _root;
        foreach (var c in prefix)
        {
            if (!current.Children.TryGetValue(c, out current))
                return null;
        }
        return current;
    }

    /// <summary>
    /// 收集所有单词（DFS）
    /// </summary>
    private void CollectWords(TrieNode node, StringBuilder current, List<string> result, int limit)
    {
        if (result.Count >= limit)
            return;

        if (node.IsEndOfWord)
        {
            result.Add(current.ToString());
        }

        foreach (var kvp in node.Children)
        {
            current.Append(kvp.Key);
            CollectWords(kvp.Value, current, result, limit);
            current.Length--;
        }
    }

    #endregion

    #region 遍历与导出

    /// <summary>
    /// 获取所有单词
    /// </summary>
    public List<string> GetAllWords()
    {
        var result = new List<string>();
        CollectWords(_root, new StringBuilder(), result, int.MaxValue);
        return result;
    }

    #endregion
}
