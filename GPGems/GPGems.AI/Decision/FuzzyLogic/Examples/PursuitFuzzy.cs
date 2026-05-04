/*
 * GPGems.AI - Fuzzy Logic Example: Pursuit Controller
 * 追逃行为模糊控制示例（GPG1 AI\08McCuskey）
 *
 * 输入变量：
 *   - Distance: 与目标的距离 (0-100)
 *   - Angle: 与目标的角度差 (-180 to 180)
 *   - Speed: 当前速度 (0-20)
 *
 * 输出变量：
 *   - Steering: 转向角度 (-45 to 45)
 *   - Throttle: 油门/加速度 (0-1)
 */

namespace GPGems.AI.Decision.FuzzyLogic.Examples;

/// <summary>
/// 追逃模糊控制器
/// </summary>
public class PursuitFuzzy
{
    private readonly FuzzyEngine _engine;

    public PursuitFuzzy()
    {
        _engine = CreatePursuitEngine();
    }

    /// <summary>
    /// 创建追逃模糊推理引擎
    /// </summary>
    private FuzzyEngine CreatePursuitEngine()
    {
        var engine = new FuzzyEngine("PursuitController");

        // 输入变量：距离
        var distance = new FuzzyVariable("Distance", 0f, 100f);
        distance.AddSet(FuzzySet.CreateLeftShoulder("Close", 0f, 30f));
        distance.AddSet(FuzzySet.CreateTriangle("Medium", 20f, 50f, 80f));
        distance.AddSet(FuzzySet.CreateRightShoulder("Far", 70f, 100f));
        engine.AddInputVariable(distance);

        // 输入变量：角度差
        var angle = new FuzzyVariable("Angle", -180f, 180f);
        angle.AddSet(FuzzySet.CreateLeftShoulder("LeftLarge", -180f, -60f));
        angle.AddSet(FuzzySet.CreateTriangle("LeftSmall", -90f, -30f, 0f));
        angle.AddSet(FuzzySet.CreateTriangle("OnTarget", -20f, 0f, 20f));
        angle.AddSet(FuzzySet.CreateTriangle("RightSmall", 0f, 30f, 90f));
        angle.AddSet(FuzzySet.CreateRightShoulder("RightLarge", 60f, 180f));
        engine.AddInputVariable(angle);

        // 输入变量：当前速度
        var speed = new FuzzyVariable("Speed", 0f, 20f);
        speed.AddSet(FuzzySet.CreateLeftShoulder("Slow", 0f, 8f));
        speed.AddSet(FuzzySet.CreateTriangle("Medium", 5f, 10f, 15f));
        speed.AddSet(FuzzySet.CreateRightShoulder("Fast", 12f, 20f));
        engine.AddInputVariable(speed);

        // 输出变量：转向
        var steering = new FuzzyVariable("Steering", -45f, 45f);
        steering.AddSet(FuzzySet.CreateLeftShoulder("HardLeft", -45f, -30f));
        steering.AddSet(FuzzySet.CreateTriangle("SoftLeft", -35f, -20f, 0f));
        steering.AddSet(FuzzySet.CreateTriangle("Straight", -10f, 0f, 10f));
        steering.AddSet(FuzzySet.CreateTriangle("SoftRight", 0f, 20f, 35f));
        steering.AddSet(FuzzySet.CreateRightShoulder("HardRight", 30f, 45f));
        engine.AddOutputVariable(steering);

        // 输出变量：油门
        var throttle = new FuzzyVariable("Throttle", 0f, 1f);
        throttle.AddSet(FuzzySet.CreateLeftShoulder("Brake", 0f, 0.3f));
        throttle.AddSet(FuzzySet.CreateTriangle("Maintain", 0.2f, 0.5f, 0.8f));
        throttle.AddSet(FuzzySet.CreateRightShoulder("Accelerate", 0.7f, 1f));
        engine.AddOutputVariable(throttle);

        // 添加规则（15条典型追逃规则）

        // 规则 1-5: 角度控制规则
        engine.AddRule(new FuzzyRule("R1: Turn Hard Left")
            .If("Angle", "LeftLarge")
            .Then("Steering", "HardLeft")
            .Then("Throttle", "Maintain"));

        engine.AddRule(new FuzzyRule("R2: Turn Soft Left")
            .If("Angle", "LeftSmall")
            .Then("Steering", "SoftLeft")
            .Then("Throttle", "Maintain"));

        engine.AddRule(new FuzzyRule("R3: Go Straight")
            .If("Angle", "OnTarget")
            .Then("Steering", "Straight")
            .Then("Throttle", "Accelerate"));

        engine.AddRule(new FuzzyRule("R4: Turn Soft Right")
            .If("Angle", "RightSmall")
            .Then("Steering", "SoftRight")
            .Then("Throttle", "Maintain"));

        engine.AddRule(new FuzzyRule("R5: Turn Hard Right")
            .If("Angle", "RightLarge")
            .Then("Steering", "HardRight")
            .Then("Throttle", "Maintain"));

        // 规则 6-10: 距离 + 角度组合规则
        engine.AddRule(new FuzzyRule("R6: Close + OnTarget = Accelerate")
            .If("Distance", "Close")
            .If("Angle", "OnTarget")
            .Then("Throttle", "Accelerate"));

        engine.AddRule(new FuzzyRule("R7: Close + OffTarget = Maintain")
            .If("Distance", "Close")
            .If("Angle", "LeftSmall")
            .Then("Throttle", "Maintain"));

        engine.AddRule(new FuzzyRule("R8: Medium Distance = Full Speed")
            .If("Distance", "Medium")
            .Then("Throttle", "Accelerate")
            .WithWeight(0.8f));

        engine.AddRule(new FuzzyRule("R9: Far Distance = Accelerate Hard")
            .If("Distance", "Far")
            .Then("Throttle", "Accelerate")
            .WithWeight(1.2f));

        engine.AddRule(new FuzzyRule("R10: Very Close + Large Angle = Brake")
            .If("Distance", "Close")
            .If("Angle", "LeftLarge")
            .Then("Throttle", "Brake"));

        // 规则 11-15: 速度控制规则
        engine.AddRule(new FuzzyRule("R11: Fast + Close = Brake")
            .If("Speed", "Fast")
            .If("Distance", "Close")
            .Then("Throttle", "Brake"));

        engine.AddRule(new FuzzyRule("R12: Slow + Far = Accelerate")
            .If("Speed", "Slow")
            .If("Distance", "Far")
            .Then("Throttle", "Accelerate"));

        engine.AddRule(new FuzzyRule("R13: Fast + Large Turn = Brake")
            .If("Speed", "Fast")
            .If("Angle", "LeftLarge")
            .Then("Throttle", "Brake"));

        engine.AddRule(new FuzzyRule("R14: OnTarget + Slow = Accelerate")
            .If("Angle", "OnTarget")
            .If("Speed", "Slow")
            .Then("Throttle", "Accelerate"));

        engine.AddRule(new FuzzyRule("R15: Medium Speed = Maintain")
            .If("Speed", "Medium")
            .Then("Throttle", "Maintain")
            .WithWeight(0.5f));

        return engine;
    }

    /// <summary>
    /// 计算控制输出
    /// </summary>
    /// <param name="distance">与目标的距离</param>
    /// <param name="angle">与目标的角度差</param>
    /// <param name="currentSpeed">当前速度</param>
    /// <returns>(转向角度, 油门值</returns>
    public (float Steering, float Throttle) CalculateControl(float distance, float angle, float currentSpeed)
    {
        var inputs = new Dictionary<string, float>
        {
            ["Distance"] = distance,
            ["Angle"] = angle,
            ["Speed"] = currentSpeed
        };

        var outputs = _engine.Process(inputs);

        return (
            Steering: outputs.TryGetValue("Steering", out var s) ? s : 0f,
            Throttle: outputs.TryGetValue("Throttle", out var t) ? t : 0.5f
        );
    }

    /// <summary>
    /// 获取调试信息
    /// </summary>
    public string GetDebugInfo(float distance, float angle, float currentSpeed)
    {
        var inputs = new Dictionary<string, float>
        {
            ["Distance"] = distance,
            ["Angle"] = angle,
            ["Speed"] = currentSpeed
        };

        return _engine.GetDebugInfo(inputs);
    }
}

/// <summary>
/// 简单的追逃模拟器
/// </summary>
public class PursuitSimulator
{
    public float PursuerX { get; private set; }
    public float PursuerY { get; private set; }
    public float PursuerAngle { get; private set; }
    public float PursuerSpeed { get; private set; }

    public float TargetX { get; private set; }
    public float TargetY { get; private set; }
    public float TargetAngle { get; private set; }
    public float TargetSpeed { get; private set; }

    private readonly PursuitFuzzy _controller = new();
    private readonly Random _random = new();

    public PursuitSimulator()
    {
        Reset();
    }

    public void Reset()
    {
        PursuerX = 0f;
        PursuerY = 0f;
        PursuerAngle = 0f;
        PursuerSpeed = 0f;

        TargetX = 80f;
        TargetY = 50f;
        TargetAngle = 180f;
        TargetSpeed = 5f;
    }

    public void Update(float deltaTime = 1f)
    {
        // 计算相对距离和角度
        var dx = TargetX - PursuerX;
        var dy = TargetY - PursuerY;
        var distance = MathF.Sqrt(dx * dx + dy * dy);
        var targetAngle = MathF.Atan2(dy, dx) * 180f / MathF.PI;
        var angleDiff = NormalizeAngle(targetAngle - PursuerAngle);

        // 模糊控制决策
        var (steering, throttle) = _controller.CalculateControl(distance, angleDiff, PursuerSpeed);

        // 更新追击者状态
        PursuerAngle = NormalizeAngle(PursuerAngle + steering * deltaTime);
        PursuerSpeed = Math.Clamp(PursuerSpeed + (throttle - 0.5f) * 2f * deltaTime, 0f, 20f);

        var rad = PursuerAngle * MathF.PI / 180f;
        PursuerX += MathF.Cos(rad) * PursuerSpeed * deltaTime;
        PursuerY += MathF.Sin(rad) * PursuerSpeed * deltaTime;

        // 目标做随机游走
        TargetAngle += (_random.NextSingle() * 20f - 10f) * deltaTime;
        TargetAngle = NormalizeAngle(TargetAngle);

        var targetRad = TargetAngle * MathF.PI / 180f;
        TargetX += MathF.Cos(targetRad) * TargetSpeed * deltaTime;
        TargetY += MathF.Sin(targetRad) * TargetSpeed * deltaTime;

        // 边界限制
        TargetX = Math.Clamp(TargetX, -100f, 200f);
        TargetY = Math.Clamp(TargetY, -100f, 200f);
    }

    private static float NormalizeAngle(float angle)
    {
        while (angle > 180f) angle -= 360f;
        while (angle < -180f) angle += 360f;
        return angle;
    }

    public string GetStatus()
    {
        var dx = TargetX - PursuerX;
        var dy = TargetY - PursuerY;
        var distance = MathF.Sqrt(dx * dx + dy * dy);

        return $"Distance: {distance:F1} | Pursuer: ({PursuerX:F1}, {PursuerY:F1}) @ {PursuerSpeed:F1} m/s | Target: ({TargetX:F1}, {TargetY:F1})";
    }
}
