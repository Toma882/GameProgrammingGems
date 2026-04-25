/*
 * GPGems.AI - Utility System Consideration
 * 考虑因素：从黑板获取值，通过曲线转换为效用得分
 */

using GPGems.AI.Decision.Blackboards;
namespace GPGems.AI.Decision.Utility
{
    /// <summary>
    /// 考虑因素接口
    /// </summary>
    public interface IConsideration
    {
        /// <summary>考虑因素名称</summary>
        string Name { get; }

        /// <summary>权重</summary>
        float Weight { get; set; }

        /// <summary>计算效用得分 (0-1)</summary>
        float Evaluate(Blackboard blackboard);
    }

    /// <summary>
    /// 比较类型
    /// </summary>
    public enum ComparisonType
    {
        /// <summary>大于</summary>
        Greater,
        /// <summary>大于等于</summary>
        GreaterOrEqual,
        /// <summary>小于</summary>
        Less,
        /// <summary>小于等于</summary>
        LessOrEqual
    }

    /// <summary>
    /// 考虑因素基类
    /// </summary>
    public abstract class Consideration : IConsideration
    {
        public string Name { get; }
        public float Weight { get; set; } = 1f;

        protected Consideration(string name)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }

        public abstract float Evaluate(Blackboard blackboard);
    }

    /// <summary>
    /// 黑板值考虑因素
    /// </summary>
    public class BlackboardConsideration : Consideration
    {
        private readonly string _key;
        private readonly UtilityCurve _curve;

        public BlackboardConsideration(string name, string key, UtilityCurve curve)
            : base(name)
        {
            _key = key ?? throw new ArgumentNullException(nameof(key));
            _curve = curve ?? throw new ArgumentNullException(nameof(curve));
        }

        public override float Evaluate(Blackboard blackboard)
        {
            if (!blackboard.TryGet<float>(_key, out var value))
                return 0f;

            return _curve.Evaluate(value);
        }
    }

    /// <summary>
    /// 函数考虑因素
    /// </summary>
    public class FuncConsideration : Consideration
    {
        private readonly Func<Blackboard, float> _evaluator;

        public FuncConsideration(string name, Func<Blackboard, float> evaluator, float weight = 1f)
            : base(name)
        {
            _evaluator = evaluator ?? throw new ArgumentNullException(nameof(evaluator));
            Weight = weight;
        }

        public override float Evaluate(Blackboard blackboard)
        {
            return Math.Clamp(_evaluator(blackboard), 0f, 1f);
        }
    }

    /// <summary>
    /// 布尔考虑因素
    /// </summary>
    public class BoolConsideration : Consideration
    {
        private readonly string _key;
        private readonly bool _expected;
        private readonly float _trueValue;
        private readonly float _falseValue;

        public BoolConsideration(string name, string key, bool expected = true,
            float trueValue = 1f, float falseValue = 0f)
            : base(name)
        {
            _key = key;
            _expected = expected;
            _trueValue = trueValue;
            _falseValue = falseValue;
        }

        public override float Evaluate(Blackboard blackboard)
        {
            if (!blackboard.TryGet<bool>(_key, out var value))
                return _falseValue;

            return value == _expected ? _trueValue : _falseValue;
        }
    }

    /// <summary>
    /// 比较考虑因素
    /// </summary>
    public class CompareConsideration : Consideration
    {
        private readonly string _key;
        private readonly float _threshold;
        private readonly ComparisonType _type;
        private readonly float _matchValue;
        private readonly float _mismatchValue;

        public CompareConsideration(string name, string key, float threshold, ComparisonType type,
            float matchValue = 1f, float mismatchValue = 0f)
            : base(name)
        {
            _key = key;
            _threshold = threshold;
            _type = type;
            _matchValue = matchValue;
            _mismatchValue = mismatchValue;
        }

        public override float Evaluate(Blackboard blackboard)
        {
            if (!blackboard.TryGet<float>(_key, out var value))
                return _mismatchValue;

            var match = _type switch
            {
                ComparisonType.Greater => value > _threshold,
                ComparisonType.GreaterOrEqual => value >= _threshold,
                ComparisonType.Less => value < _threshold,
                ComparisonType.LessOrEqual => value <= _threshold,
                _ => false
            };

            return match ? _matchValue : _mismatchValue;
        }
    }
}
