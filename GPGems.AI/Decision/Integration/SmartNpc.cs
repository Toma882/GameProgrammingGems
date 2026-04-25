/*
 * GPGems.AI - 融合决策演示：NPC 真实日常作息
 *
 * 架构：FSM 管大场景 + 阶段管子行为 + 专业算法管决策 + 黑板管数据
 *
 * FSM 大场景（5 个）：
 *   Morning(6-9) → Working(9-17) → Evening(17-23) → Sleeping(23-6) → Morning...
 *   周末: Weekend 独立模式
 *
 * 每个大场景内部按时间推进不同阶段（phase），细粒度行为由阶段驱动。
 * 模糊逻辑、效用系统、GOAP 在阶段内部各司其职。
 */

using GPGems.AI.Decision.BehaviorTree;
using GPGems.AI.Decision.Blackboards;
using GPGems.AI.Decision.FSM;
using GPGems.AI.Decision.FuzzyLogic;
using GPGems.AI.Decision.GOAP;
using GPGems.AI.Decision.Utility;
using GPGems.Core.Messages;

namespace GPGems.AI.Decision.Integration;

using BehaviorTree = GPGems.AI.Decision.BehaviorTree.BehaviorTree;

/// <summary>大场景状态（FSM 粒度）</summary>
public enum DailyState
{
    Morning,    // 起床→洗漱→早餐→通勤
    Working,    // 工作→午休→工作
    Evening,    // 下班→晚餐→休闲→睡前准备
    Sleeping,   // 睡觉
    Weekend     // 周末全天
}

/// <summary>NPC 个性特质</summary>
public record PersonalityTraits
{
    public float Conscientiousness { get; init; } = 0.5f;
    public float Extraversion { get; init; } = 0.5f;
    public float Neuroticism { get; init; } = 0.3f;
    public float Routineness { get; init; } = 0.6f;
}

/// <summary>
/// 智能 NPC：真实日常作息
/// </summary>
public class SmartNpc
{
    // ===== 公开属性 =====
    public string Name { get; }
    public Blackboard Blackboard { get; }
    public DailyState CurrentState => _currentState;
    public string CurrentPhase => _currentPhase;
    public BehaviorTree? CurrentBehaviorTree => _currentBehaviorTree;
    public FuzzyEngine FuzzyEngine { get; }
    public UtilityReasoner UtilityReasoner { get; }
    public GoapAgent GoapAgent { get; }
    public StateMachine Fsm { get; }
    public PersonalityTraits Personality { get; }

    // ===== 内部状态 =====
    private DailyState _currentState;
    private string _currentPhase = "";
    private BehaviorTree? _currentBehaviorTree;
    private readonly Dictionary<DailyState, BehaviorTree> _behaviorTrees = new();
    private readonly MessageRouter _router;
    private float _stateElapsed;
    private float _phaseElapsed;
    private bool _isWeekend => Blackboard.GetOrDefault("is_weekend", false);

    public SmartNpc(string name, PersonalityTraits? personality = null)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Personality = personality ?? new PersonalityTraits();
        Blackboard = new Blackboard($"{Name}_Global");
        _router = new MessageRouter($"{Name}_Router");

        FuzzyEngine = CreateFuzzyEngine();
        UtilityReasoner = CreateUtilityReasoner();
        GoapAgent = CreateGoapAgent();

        Fsm = CreateFsm();
        foreach (var state in Enum.GetValues<DailyState>())
            _behaviorTrees[state] = CreateBehaviorTreeForState(state);

        _currentState = DailyState.Morning;
        _currentPhase = "wake_up";
        _currentBehaviorTree = _behaviorTrees[_currentState];
        _currentBehaviorTree.Start();
        _stateElapsed = 0f;
        _phaseElapsed = 0f;

        InitializeBlackboard();
    }

    // ===================================================================
    //  黑板初始化
    // ===================================================================
    private void InitializeBlackboard()
    {
        Blackboard.Set("hour_of_day", 7f);
        Blackboard.Set("day_progress", 7f / 24f);
        Blackboard.Set("day_of_week", 1f);
        Blackboard.Set("is_weekend", false);

        Blackboard.Set("energy", 60f);
        Blackboard.Set("hunger", 30f);
        Blackboard.Set("bladder", 20f);
        Blackboard.Set("hygiene", 70f);
        Blackboard.Set("fatigue", 20f);

        Blackboard.Set("stress", 10f);
        Blackboard.Set("mood", 70f);
        Blackboard.Set("social", 40f);
        Blackboard.Set("fun", 30f);

        Blackboard.Set("productivity", 70f);
        Blackboard.Set("task_count", 5f);
        Blackboard.Set("work_intensity", 50f);
        Blackboard.Set("has_meeting_today", false);
        Blackboard.Set("boss_nearby", false);

        Blackboard.Set("weather", 0f);
        Blackboard.Set("noise_level", 30f);
        Blackboard.Set("commute_progress", 0f);

        Blackboard.Set("selected_task", "None");
        Blackboard.Set("current_thought", "新的一天开始了...");

        Blackboard.Set("state_elapsed", 0f);
        Blackboard.Set("daily_phase", "wake_up");
        Blackboard.Set("phase_elapsed", 0f);

        Blackboard.Set("sleep_quality", 0.8f);
        Blackboard.Set("wake_up_time", 7f);
        Blackboard.Set("bed_time", 23f);
    }

    // ===================================================================
    //  模糊推理引擎
    // ===================================================================
    private FuzzyEngine CreateFuzzyEngine()
    {
        var engine = new FuzzyEngine("DailyMood");

        var energy = new FuzzyVariable("energy", 0f, 100f)
            .AddSet(new FuzzySet("Exhausted", 0f, 0f, 10f, 25f))
            .AddSet(new FuzzySet("Tired", 15f, 30f, 30f, 50f))
            .AddSet(new FuzzySet("Okay", 35f, 50f, 55f, 70f))
            .AddSet(new FuzzySet("Energetic", 55f, 70f, 80f, 90f))
            .AddSet(new FuzzySet("Full", 75f, 88f, 100f, 100f));
        engine.AddInputVariable(energy);

        var stress = new FuzzyVariable("stress", 0f, 100f)
            .AddSet(new FuzzySet("Relaxed", 0f, 0f, 15f, 30f))
            .AddSet(new FuzzySet("Mild", 20f, 35f, 40f, 55f))
            .AddSet(new FuzzySet("Stressed", 40f, 55f, 65f, 80f))
            .AddSet(new FuzzySet("Overwhelmed", 65f, 80f, 100f, 100f));
        engine.AddInputVariable(stress);

        var mood = new FuzzyVariable("mood", 0f, 100f)
            .AddSet(new FuzzySet("Terrible", 0f, 0f, 10f, 25f))
            .AddSet(new FuzzySet("Bad", 15f, 28f, 30f, 45f))
            .AddSet(new FuzzySet("Neutral", 30f, 48f, 55f, 70f))
            .AddSet(new FuzzySet("Good", 55f, 68f, 75f, 88f))
            .AddSet(new FuzzySet("Great", 75f, 88f, 100f, 100f));
        engine.AddOutputVariable(mood);

        var fatigue = new FuzzyVariable("fatigue", 0f, 100f)
            .AddSet(new FuzzySet("Rested", 0f, 0f, 15f, 30f))
            .AddSet(new FuzzySet("Slight", 20f, 30f, 40f, 55f))
            .AddSet(new FuzzySet("Tired", 40f, 55f, 65f, 80f))
            .AddSet(new FuzzySet("Exhausted", 65f, 80f, 100f, 100f));
        engine.AddInputVariable(fatigue);

        engine.AddRule(new FuzzyRule("高精力+低压→心情很好")
            .If("energy", "Full").If("stress", "Relaxed").Then("mood", "Great"));
        engine.AddRule(new FuzzyRule("中精力+低压→心情好")
            .If("energy", "Energetic").If("stress", "Relaxed").Then("mood", "Good"));
        engine.AddRule(new FuzzyRule("中精力+中压→心情一般")
            .If("energy", "Okay").If("stress", "Mild").Then("mood", "Neutral"));
        engine.AddRule(new FuzzyRule("低精力+高压→心情差")
            .If("energy", "Tired").If("stress", "Stressed").Then("mood", "Bad"));
        engine.AddRule(new FuzzyRule("低精力+高压+疲劳→心情极差")
            .If("energy", "Exhausted").If("stress", "Overwhelmed").Then("mood", "Terrible"));
        engine.AddRule(new FuzzyRule("高疲劳→心情差")
            .If("fatigue", "Exhausted").Then("mood", "Bad"));
        engine.AddRule(new FuzzyRule("好精力+低疲劳→心情好")
            .If("energy", "Full").If("fatigue", "Rested").Then("mood", "Great"));

        var workEngine = new FuzzyEngine("WorkIntensity");

        var wEnergy = new FuzzyVariable("energy", 0f, 100f)
            .AddSet(new FuzzySet("Low", 0f, 0f, 20f, 40f))
            .AddSet(new FuzzySet("Medium", 30f, 50f, 50f, 70f))
            .AddSet(new FuzzySet("High", 60f, 80f, 100f, 100f));
        workEngine.AddInputVariable(wEnergy);

        var wStress = new FuzzyVariable("stress", 0f, 100f)
            .AddSet(new FuzzySet("Low", 0f, 0f, 20f, 40f))
            .AddSet(new FuzzySet("Medium", 30f, 50f, 50f, 70f))
            .AddSet(new FuzzySet("High", 60f, 80f, 100f, 100f));
        workEngine.AddInputVariable(wStress);

        var wIntensity = new FuzzyVariable("work_intensity", 0f, 100f)
            .AddSet(new FuzzySet("Slacking", 0f, 0f, 15f, 30f))
            .AddSet(new FuzzySet("Normal", 25f, 40f, 50f, 65f))
            .AddSet(new FuzzySet("Focused", 55f, 70f, 80f, 90f))
            .AddSet(new FuzzySet("Grinding", 80f, 90f, 100f, 100f));
        workEngine.AddOutputVariable(wIntensity);

        workEngine.AddRule(new FuzzyRule("高精力+低压→专注")
            .If("energy", "High").If("stress", "Low").Then("work_intensity", "Focused"));
        workEngine.AddRule(new FuzzyRule("中精力+中压→正常")
            .If("energy", "Medium").If("stress", "Medium").Then("work_intensity", "Normal"));
        workEngine.AddRule(new FuzzyRule("低精力→摸鱼")
            .If("energy", "Low").Then("work_intensity", "Slacking"));
        workEngine.AddRule(new FuzzyRule("高压→摸鱼")
            .If("stress", "High").Then("work_intensity", "Slacking"));

        Blackboard.Set("_fuzzy_mood_engine", engine);
        Blackboard.Set("_fuzzy_work_engine", workEngine);
        return engine;
    }

    // ===================================================================
    //  效用系统
    // ===================================================================
    private UtilityReasoner CreateUtilityReasoner()
    {
        var reasoner = new UtilityReasoner("DailyDecisions", Blackboard);

        var writeCode = new UtilityAction("写代码", bb =>
        {
            bb.Set("energy", Math.Max(0f, bb.GetOrDefault("energy", 50f) - 1.5f * Personality.Conscientiousness));
            bb.Set("stress", Math.Min(100f, bb.GetOrDefault("stress", 10f) + 5f));
            bb.Set("task_count", Math.Max(0f, bb.GetOrDefault("task_count", 0f) - 0.7f));
            bb.Set("productivity", Math.Min(100f, bb.GetOrDefault("productivity", 70f) + 3f));
            return 1f;
        });
        writeCode.AddConsideration("Energy", "energy", new LinearCurve { MinX = 0, MaxX = 100 });
        writeCode.AddConsideration("Tasks", "task_count", new StepCurve { Threshold = 0.5f });
        writeCode.BaseScore = 0.7f * Personality.Conscientiousness;
        reasoner.AddAction(writeCode);

        var slackOff = new UtilityAction("摸鱼", bb =>
        {
            bb.Set("stress", Math.Max(0f, bb.GetOrDefault("stress", 10f) - 12f));
            bb.Set("energy", Math.Min(100f, bb.GetOrDefault("energy", 50f) + 4f));
            bb.Set("fun", Math.Min(100f, bb.GetOrDefault("fun", 30f) + 8f));
            return 1f;
        });
        slackOff.AddConsideration("Stress", "stress", new LinearCurve { MinX = 0, MaxX = 100 });
        slackOff.BaseScore = 0.2f * (1.3f - Personality.Conscientiousness);
        reasoner.AddAction(slackOff);

        var takeBreak = new UtilityAction("休息一下", bb =>
        {
            bb.Set("energy", Math.Min(100f, bb.GetOrDefault("energy", 50f) + 6f));
            bb.Set("stress", Math.Max(0f, bb.GetOrDefault("stress", 10f) - 8f));
            return 1f;
        });
        takeBreak.AddConsideration("Fatigue", "fatigue", new LinearCurve { MinX = 0, MaxX = 100 });
        takeBreak.BaseScore = 0.45f;
        reasoner.AddAction(takeBreak);

        var goToilet = new UtilityAction("上厕所", bb =>
        {
            bb.Set("bladder", 10f);
            bb.Set("stress", Math.Max(0f, bb.GetOrDefault("stress", 10f) - 3f));
            return 1f;
        });
        goToilet.AddConsideration("Bladder", "bladder", new StepCurve { Threshold = 0.5f });
        goToilet.BaseScore = 0.1f;
        reasoner.AddAction(goToilet);

        var playGame = new UtilityAction("打游戏", bb =>
        {
            bb.Set("energy", Math.Max(0f, bb.GetOrDefault("energy", 50f) - 10f));
            bb.Set("fun", Math.Min(100f, bb.GetOrDefault("fun", 30f) + 20f));
            bb.Set("stress", Math.Max(0f, bb.GetOrDefault("stress", 10f) - 5f));
            return 1f;
        });
        playGame.AddConsideration("FunNeed", "fun", new LinearCurve { MinX = 0, MaxX = 100 });
        playGame.AddConsideration("EnergyNeed", "energy", new StepCurve { Threshold = 0.5f });
        playGame.BaseScore = 0.6f;
        reasoner.AddAction(playGame);

        var watchVideo = new UtilityAction("看视频", bb =>
        {
            bb.Set("fun", Math.Min(100f, bb.GetOrDefault("fun", 30f) + 10f));
            bb.Set("energy", Math.Min(100f, bb.GetOrDefault("energy", 50f) + 3f));
            return 1f;
        });
        watchVideo.AddConsideration("FunNeed", "fun", new LinearCurve { MinX = 0, MaxX = 100 });
        watchVideo.BaseScore = 0.5f;
        reasoner.AddAction(watchVideo);

        var socialMedia = new UtilityAction("刷手机", bb =>
        {
            bb.Set("fun", Math.Min(100f, bb.GetOrDefault("fun", 30f) + 5f));
            bb.Set("social", Math.Min(100f, bb.GetOrDefault("social", 40f) + 3f));
            bb.Set("fatigue", Math.Min(100f, bb.GetOrDefault("fatigue", 20f) + 2f));
            return 1f;
        });
        socialMedia.AddConsideration("FunNeed", "fun", new LinearCurve { MinX = 0, MaxX = 100 });
        socialMedia.BaseScore = 0.4f;
        reasoner.AddAction(socialMedia);

        var readBook = new UtilityAction("看书学习", bb =>
        {
            bb.Set("energy", Math.Max(0f, bb.GetOrDefault("energy", 50f) - 5f));
            bb.Set("fun", Math.Min(100f, bb.GetOrDefault("fun", 30f) + 8f));
            bb.Set("productivity", Math.Min(100f, bb.GetOrDefault("productivity", 70f) + 5f));
            return 1f;
        });
        readBook.AddConsideration("Energy", "energy", new LinearCurve { MinX = 0, MaxX = 100 });
        readBook.AddConsideration("Consc", _ => Personality.Conscientiousness, 0.5f);
        readBook.BaseScore = 0.3f;
        reasoner.AddAction(readBook);

        var chat = new UtilityAction("找人聊天", bb =>
        {
            bb.Set("social", Math.Min(100f, bb.GetOrDefault("social", 40f) + 15f));
            var extra = Personality.Extraversion;
            bb.Set("energy", extra > 0.5f
                ? Math.Min(100f, bb.GetOrDefault("energy", 50f) + 3f)
                : Math.Max(0f, bb.GetOrDefault("energy", 50f) - 4f));
            bb.Set("stress", Math.Max(0f, bb.GetOrDefault("stress", 10f) - 5f));
            return 1f;
        });
        chat.AddConsideration("SocialNeed", "social", new LinearCurve { MinX = 0, MaxX = 100 });
        chat.BaseScore = 0.5f * Personality.Extraversion;
        reasoner.AddAction(chat);

        var eatMeal = new UtilityAction("吃饭", bb =>
        {
            bb.Set("hunger", Math.Min(100f, bb.GetOrDefault("hunger", 30f) + 35f));
            bb.Set("energy", Math.Min(100f, bb.GetOrDefault("energy", 50f) + 10f));
            bb.Set("mood", Math.Min(100f, bb.GetOrDefault("mood", 70f) + 5f));
            return 1f;
        });
        eatMeal.AddConsideration("Hunger", "hunger", new LinearCurve { MinX = 0, MaxX = 100 });
        eatMeal.BaseScore = 0.8f;
        reasoner.AddAction(eatMeal);

        var drinkWater = new UtilityAction("喝水", bb =>
        {
            bb.Set("energy", Math.Min(100f, bb.GetOrDefault("energy", 50f) + 2f));
            bb.Set("stress", Math.Max(0f, bb.GetOrDefault("stress", 10f) - 2f));
            return 1f;
        });
        drinkWater.BaseScore = 0.3f;
        reasoner.AddAction(drinkWater);

        var shower = new UtilityAction("洗澡", bb =>
        {
            bb.Set("hygiene", 85f);
            bb.Set("energy", Math.Min(100f, bb.GetOrDefault("energy", 50f) + 5f));
            bb.Set("mood", Math.Min(100f, bb.GetOrDefault("mood", 70f) + 8f));
            return 1f;
        });
        shower.AddConsideration("Hygiene", "hygiene", new LinearCurve { MinX = 0, MaxX = 100 });
        shower.BaseScore = 0.7f;
        reasoner.AddAction(shower);

        return reasoner;
    }

    // ===================================================================
    //  GOAP 代理
    // ===================================================================
    private GoapAgent CreateGoapAgent()
    {
        var agent = new GoapAgent("TaskPlanner");

        agent.AddAction(new GoapAction("收集资料") { Cost = 2f }
            .AddPrecondition("has_information", false)
            .AddEffect("has_information", true));

        agent.AddAction(new GoapAction("分析数据") { Cost = 3f }
            .AddPrecondition("has_information", true)
            .AddPrecondition("has_analysis", false)
            .AddEffect("has_analysis", true));

        agent.AddAction(new GoapAction("编写报告") { Cost = 2f }
            .AddPrecondition("has_analysis", true)
            .AddPrecondition("has_report", false)
            .AddEffect("has_report", true));

        agent.AddAction(new GoapAction("提交报告") { Cost = 1f }
            .AddPrecondition("has_report", true)
            .AddPrecondition("task_complete", false)
            .AddEffect("task_complete", true));

        agent.AddAction(new GoapAction("审阅文档") { Cost = 1.5f }
            .AddPrecondition("has_document", true)
            .AddPrecondition("document_reviewed", false)
            .AddEffect("document_reviewed", true));

        agent.AddAction(new GoapAction("准备会议") { Cost = 2.5f }
            .AddPrecondition("meeting_prepared", false)
            .AddEffect("meeting_prepared", true));

        agent.AddGoal(new GoapGoal("完成任务") { Priority = 10f }
            .AddCondition("task_complete", true));

        agent.AddGoal(new GoapGoal("准备好会议") { Priority = 8f }
            .AddCondition("meeting_prepared", true));

        return agent;
    }

    // ===================================================================
    //  FSM — 仅 5 个大场景
    // ===================================================================
    private StateMachine CreateFsm()
    {
        var fsm = new StateMachine("DailyRoutine", Blackboard);
        fsm.Router = _router;
        _router.RegisterReceiver(fsm);

        var morning = new DelegateState("Morning") { UpdateAction = OnMorningState };
        var working = new DelegateState("Working") { UpdateAction = OnWorkingState };
        var evening = new DelegateState("Evening") { UpdateAction = OnEveningState };
        var sleeping = new DelegateState("Sleeping") { UpdateAction = OnSleepingState };
        var weekend = new DelegateState("Weekend") { UpdateAction = OnWeekendState };

        // 工作日流转
        fsm.AddTransitions(fsm.From(morning).To(working,
            bb => bb.GetOrDefault("hour_of_day", 7f) >= 9f));
        fsm.AddTransitions(fsm.From(working).To(evening,
            bb => bb.GetOrDefault("hour_of_day", 7f) >= 17f));
        fsm.AddTransitions(fsm.From(evening).To(sleeping,
            bb => bb.GetOrDefault("hour_of_day", 7f) >= bb.GetOrDefault("bed_time", 23f)));
        fsm.AddTransitions(fsm.From(sleeping).To(morning,
            bb => bb.GetOrDefault("hour_of_day", 7f) >= bb.GetOrDefault("wake_up_time", 7f)));

        // 周末分流（从早上/睡觉进入周末）
        fsm.AddTransitions(fsm.From(morning).To(weekend,
            bb => bb.GetOrDefault("is_weekend", false)));
        fsm.AddTransitions(fsm.From(sleeping).To(weekend,
            bb => bb.GetOrDefault("is_weekend", false)));
        fsm.AddTransitions(fsm.From(weekend).To(sleeping,
            bb => bb.GetOrDefault("hour_of_day", 7f) >= bb.GetOrDefault("bed_time", 23f)));

        fsm.SetInitialState(morning);
        fsm.Start();
        return fsm;
    }

    // ===================================================================
    //  大场景内部阶段驱动
    // ===================================================================
    private void SetPhase(string phase)
    {
        _currentPhase = phase;
        _phaseElapsed = 0f;
        Blackboard.Set("daily_phase", phase);
        Blackboard.Set("phase_elapsed", 0f);
    }

    // ---------- Morning 阶段 ----------
    private void OnMorningState(Blackboard bb)
    {
        var hour = bb.GetOrDefault("hour_of_day", 7f);

        // 6:00-7:00 起床
        if (hour < 7f) { UpdatePhase("wake_up", OnWakeUp, bb); }
        // 7:00-7:30 洗漱
        else if (hour < 7.5f) { UpdatePhase("hygiene", OnHygiene, bb); }
        // 7:30-8:00 早餐
        else if (hour < 8f) { UpdatePhase("breakfast", OnBreakfast, bb); }
        // 8:00-9:00 通勤
        else { UpdatePhase("commute", OnCommute, bb); }
    }

    // ---------- Working 阶段 ----------
    private void OnWorkingState(Blackboard bb)
    {
        var hour = bb.GetOrDefault("hour_of_day", 9f);

        // 9:00-12:00 上午工作
        if (hour < 12f) { UpdatePhase("focus", OnFocusWork, bb); }
        // 12:00-13:00 午休
        else if (hour < 13f) { UpdatePhase("lunch", OnLunch, bb); }
        // 13:00-17:00 下午工作
        else { UpdatePhase("afternoon", OnAfternoonWork, bb); }
    }

    // ---------- Evening 阶段 ----------
    private void OnEveningState(Blackboard bb)
    {
        var hour = bb.GetOrDefault("hour_of_day", 17f);

        // 17:00-18:00 下班通勤
        if (hour < 18f) { UpdatePhase("commute_home", OnCommuteHome, bb); }
        // 18:00-19:30 晚餐
        else if (hour < 19.5f) { UpdatePhase("dinner", OnDinner, bb); }
        // 19:30-22:00 休闲
        else if (hour < 22f) { UpdatePhase("leisure", OnLeisure, bb); }
        // 22:00-23:00 睡前准备
        else { UpdatePhase("sleep_prep", OnSleepPrep, bb); }
    }

    // ---------- Sleeping ----------
    private void OnSleepingState(Blackboard bb)
    {
        UpdatePhase("sleeping", OnSleep, bb);
    }

    // ---------- Weekend ----------
    private void OnWeekendState(Blackboard bb)
    {
        var hour = bb.GetOrDefault("hour_of_day", 8f);

        if (hour < 10f) { UpdatePhase("weekend_morning", OnWeekendMorning, bb); }
        else if (hour < 18f) { UpdatePhase("weekend_afternoon", OnWeekendAfternoon, bb); }
        else { UpdatePhase("weekend_evening", OnWeekendEvening, bb); }
    }

    // ===================================================================
    //  阶段更新辅助
    // ===================================================================
    private void UpdatePhase(string phase, Action<Blackboard> action, Blackboard bb)
    {
        if (_currentPhase != phase)
        {
            var prev = _currentPhase;
            SetPhase(phase);
            Console.WriteLine($"  [{Name}] · {GetPhaseDisplayName(phase)}");
        }
        _phaseElapsed += 0.1f;
        bb.Set("phase_elapsed", _phaseElapsed);
        action(bb);
    }

    // ===================================================================
    //  各阶段具体逻辑
    // ===================================================================
    private void OnWakeUp(Blackboard bb)
    {
        var sq = bb.GetOrDefault("sleep_quality", 0.8f);
        bb.Set("energy", Math.Min(100f, bb.GetOrDefault("energy", 50f) + 3f * sq));
        bb.Set("fatigue", Math.Max(0f, bb.GetOrDefault("fatigue", 20f) - 5f * sq));
        bb.Set("current_thought", sq > 0.7f ? "睡得好舒服！" : "没睡好...");
    }

    private void OnHygiene(Blackboard bb)
    {
        bb.Set("hygiene", Math.Min(100f, bb.GetOrDefault("hygiene", 70f) + 15f));
        bb.Set("current_thought", "洗漱清醒一下");
    }

    private void OnBreakfast(Blackboard bb)
    {
        bb.Set("hunger", Math.Min(100f, bb.GetOrDefault("hunger", 30f) + 25f));
        bb.Set("current_thought", "吃早餐");
    }

    private void OnCommute(Blackboard bb)
    {
        var p = Math.Min(100f, bb.GetOrDefault("commute_progress", 0f) + 20f);
        bb.Set("commute_progress", p);
        if (Personality.Extraversion > 0.6f)
            bb.Set("social", Math.Min(100f, bb.GetOrDefault("social", 40f) + 2f));
        else
            bb.Set("energy", Math.Max(0f, bb.GetOrDefault("energy", 50f) - 2f));
        bb.Set("current_thought", p < 80f ? "通勤路上..." : "快到公司了");
    }

    private void OnFocusWork(Blackboard bb)
    {
        var engine = Blackboard.Get<FuzzyEngine>("_fuzzy_work_engine");
        if (engine != null)
        {
            var inputs = new Dictionary<string, float>
            {
                ["energy"] = bb.GetOrDefault("energy", 50f),
                ["stress"] = bb.GetOrDefault("stress", 10f)
            };
            bb.Set("work_intensity", engine.Process(inputs)["work_intensity"]);
        }

        var intensity = bb.GetOrDefault("work_intensity", 50f) / 100f;
        bb.Set("energy", Math.Max(0f, bb.GetOrDefault("energy", 50f) - 0.4f * intensity));
        bb.Set("stress", Math.Min(100f, bb.GetOrDefault("stress", 10f) + 0.4f * intensity));
        bb.Set("fatigue", Math.Min(100f, bb.GetOrDefault("fatigue", 20f) + 0.3f * intensity));
        bb.Set("bladder", Math.Min(100f, bb.GetOrDefault("bladder", 20f) + 8f));

        // 摸鱼倾向
        if (Random.Shared.NextDouble() < 0.08 * (1f - Personality.Conscientiousness))
        {
            Console.WriteLine($"  [{Name}]   😅 偷偷刷了下手机");
            bb.Set("stress", Math.Max(0f, bb.GetOrDefault("stress", 10f) - 3f));
            bb.Set("fun", Math.Min(100f, bb.GetOrDefault("fun", 30f) + 3f));
        }

        UtilityReasoner.Update();
    }

    private void OnLunch(Blackboard bb)
    {
        bb.Set("hunger", Math.Min(100f, bb.GetOrDefault("hunger", 30f) + 30f));
        bb.Set("energy", Math.Min(100f, bb.GetOrDefault("energy", 50f) + 15f));
        bb.Set("stress", Math.Max(0f, bb.GetOrDefault("stress", 10f) - 8f));

        if (Personality.Extraversion > 0.5f && Random.Shared.NextDouble() < 0.5f)
        {
            bb.Set("social", Math.Min(100f, bb.GetOrDefault("social", 40f) + 8f));
            Console.WriteLine($"  [{Name}]   🗣️ 和同事吃饭聊天");
        }
    }

    private void OnAfternoonWork(Blackboard bb)
    {
        var engine = Blackboard.Get<FuzzyEngine>("_fuzzy_work_engine");
        if (engine != null)
        {
            var inputs = new Dictionary<string, float>
            {
                ["energy"] = bb.GetOrDefault("energy", 50f),
                ["stress"] = bb.GetOrDefault("stress", 10f)
            };
            bb.Set("work_intensity", engine.Process(inputs)["work_intensity"]);
        }

        var intensity = bb.GetOrDefault("work_intensity", 50f) / 100f;
        bb.Set("energy", Math.Max(0f, bb.GetOrDefault("energy", 50f) - 0.6f * intensity));
        bb.Set("stress", Math.Min(100f, bb.GetOrDefault("stress", 10f) + 0.6f * intensity));
        bb.Set("fatigue", Math.Min(100f, bb.GetOrDefault("fatigue", 20f) + 0.5f * intensity));
        bb.Set("bladder", Math.Min(100f, bb.GetOrDefault("bladder", 20f) + 6f));

        var hour = bb.GetOrDefault("hour_of_day", 13f);
        if (hour >= 16f)
            bb.Set("current_thought", "快下班了...");
        else
            bb.Set("current_thought", "下午工作");

        // 下午低精力时喝咖啡
        if (bb.GetOrDefault("energy", 50f) < 20f && _phaseElapsed > 0.5f)
        {
            Console.WriteLine($"  [{Name}]   ☕ 泡杯咖啡提神");
            bb.Set("energy", Math.Min(100f, bb.GetOrDefault("energy", 50f) + 15f));
        }

        UtilityReasoner.Update();
    }

    private void OnCommuteHome(Blackboard bb)
    {
        var p = Math.Min(100f, bb.GetOrDefault("commute_progress", 0f) + 25f);
        bb.Set("commute_progress", p);
        bb.Set("stress", Math.Max(0f, bb.GetOrDefault("stress", 10f) - 3f));
        bb.Set("current_thought", p < 80f ? "下班回家~" : "到家了");
    }

    private void OnDinner(Blackboard bb)
    {
        bb.Set("hunger", Math.Min(100f, bb.GetOrDefault("hunger", 30f) + 30f));
        bb.Set("stress", Math.Max(0f, bb.GetOrDefault("stress", 10f) - 5f));
        bb.Set("energy", Math.Min(100f, bb.GetOrDefault("energy", 50f) + 3f));

        if (bb.GetOrDefault("hygiene", 70f) < 40f || _phaseElapsed > 1f)
        {
            bb.Set("hygiene", Math.Min(100f, bb.GetOrDefault("hygiene", 70f) + 20f));
        }
        bb.Set("current_thought", "晚饭吃什么呢...");
    }

    private void OnLeisure(Blackboard bb)
    {
        UtilityReasoner.Update();
        bb.Set("stress", Math.Max(0f, bb.GetOrDefault("stress", 10f) - 3f));
        bb.Set("social", Math.Min(100f, bb.GetOrDefault("social", 40f) + 1f));
        bb.Set("fatigue", Math.Max(0f, bb.GetOrDefault("fatigue", 20f) - 2f));
    }

    private void OnSleepPrep(Blackboard bb)
    {
        bb.Set("hygiene", Math.Min(100f, bb.GetOrDefault("hygiene", 70f) + 10f));
        bb.Set("energy", Math.Min(100f, bb.GetOrDefault("energy", 50f) + 2f));
        bb.Set("current_thought", "该睡了...");
    }

    private void OnSleep(Blackboard bb)
    {
        var recovery = 5f + (100f - bb.GetOrDefault("stress", 10f)) / 100f * 3f;
        bb.Set("energy", Math.Min(100f, bb.GetOrDefault("energy", 50f) + recovery));
        bb.Set("fatigue", Math.Max(0f, bb.GetOrDefault("fatigue", 20f) - 6f));
        bb.Set("stress", Math.Max(0f, bb.GetOrDefault("stress", 10f) - 4f));

        var sq = bb.GetOrDefault("sleep_quality", 0.8f);
        if (bb.GetOrDefault("stress", 10f) > 60f) sq = Math.Max(0.3f, sq - 0.01f);
        if (bb.GetOrDefault("fatigue", 20f) > 70f) sq = Math.Max(0.4f, sq + 0.005f);
        bb.Set("sleep_quality", sq);

        var hour = bb.GetOrDefault("hour_of_day", 0f);
        bb.Set("current_thought", hour < 3f ? "😴 Zzz..." : hour < 6f ? "😴 深度睡眠" : "🌅 快醒了");
    }

    private void OnWeekendMorning(Blackboard bb)
    {
        bb.Set("energy", Math.Min(100f, bb.GetOrDefault("energy", 50f) + 4f));
        bb.Set("fatigue", Math.Max(0f, bb.GetOrDefault("fatigue", 20f) - 4f));
        bb.Set("stress", Math.Max(0f, bb.GetOrDefault("stress", 10f) - 3f));

        if (bb.GetOrDefault("hunger", 30f) < 40f)
            bb.Set("hunger", Math.Min(100f, bb.GetOrDefault("hunger", 30f) + 25f));

        bb.Set("current_thought", "周末啦~");
    }

    private void OnWeekendAfternoon(Blackboard bb)
    {
        UtilityReasoner.Update();
        bb.Set("stress", Math.Max(0f, bb.GetOrDefault("stress", 10f) - 4f));
        bb.Set("social", Math.Min(100f, bb.GetOrDefault("social", 40f) + 2f));

        if (_phaseElapsed > 10f && Random.Shared.NextDouble() < 0.03)
        {
            Console.WriteLine($"  [{Name}]   🚶 出门散步");
            bb.Set("mood", Math.Min(100f, bb.GetOrDefault("mood", 70f) + 5f));
            bb.Set("fun", Math.Min(100f, bb.GetOrDefault("fun", 30f) + 5f));
        }
    }

    private void OnWeekendEvening(Blackboard bb)
    {
        UtilityReasoner.Update();
        bb.Set("stress", Math.Max(0f, bb.GetOrDefault("stress", 10f) - 3f));
        bb.Set("fun", Math.Min(100f, bb.GetOrDefault("fun", 30f) + 3f));
        var bt = bb.GetOrDefault("bed_time", 23f);
        bb.Set("bed_time", Math.Min(26f, bt + 0.01f));
    }

    // ===================================================================
    //  行为树
    // ===================================================================
    private BehaviorTree CreateBehaviorTreeForState(DailyState state)
    {
        return state switch
        {
            DailyState.Working => CreateWorkBehaviorTree(),
            DailyState.Evening => CreateEveningBehaviorTree(),
            _ => CreateDefaultBehaviorTree(state)
        };
    }

    private BehaviorTree CreateWorkBehaviorTree()
    {
        return BehaviorTreeBuilder.Create("WorkFlow", Blackboard)
            .Selector("Work")
                .Sequence("BossMode")
                    .Condition("boss_nearby", true)
                    .Action(bb =>
                    {
                        Console.WriteLine($"  [{Name}] 👀 老板在！认真工作中...");
                        bb.Set("productivity", 90f);
                        bb.Set("work_intensity", 80f);
                        return NodeStatus.Success;
                    })
                .End()
                .Sequence("Normal")
                    .Action(bb =>
                    {
                        var engine = Blackboard.Get<FuzzyEngine>("_fuzzy_work_engine");
                        if (engine != null)
                        {
                            var inputs = new Dictionary<string, float>
                            {
                                ["energy"] = bb.GetOrDefault("energy", 50f),
                                ["stress"] = bb.GetOrDefault("stress", 10f)
                            };
                            bb.Set("work_intensity", engine.Process(inputs)["work_intensity"]);
                        }
                        return NodeStatus.Success;
                    })
                    .Action(bb => { UtilityReasoner.Update(); return NodeStatus.Success; })
                    .Selector("Exec")
                        .Sequence("Goap")
                            .Condition(bb => bb.GetOrDefault("selected_task", "") == "写代码")
                            .Action(bb => { GoapAgent.Update(); return NodeStatus.Success; })
                        .End()
                        .Success("Ok")
                    .End()
                .End()
            .End()
            .Build();
    }

    private BehaviorTree CreateEveningBehaviorTree()
    {
        return BehaviorTreeBuilder.Create("EveningFlow", Blackboard)
            .Selector("Evening")
                .Sequence("Play")
                    .Condition(bb => bb.GetOrDefault("energy", 0f) > 30f)
                    .Condition(bb => bb.GetOrDefault("fun", 30f) < 60f)
                    .Action(bb =>
                    {
                        Console.WriteLine($"  [{Name}] 🎮 打游戏中...");
                        bb.Set("fun", Math.Min(100f, bb.GetOrDefault("fun", 30f) + 15f));
                        bb.Set("energy", Math.Max(0f, bb.GetOrDefault("energy", 50f) - 8f));
                        return NodeStatus.Success;
                    })
                .End()
                .Sequence("Watch")
                    .Condition(bb => bb.GetOrDefault("fun", 30f) < 70f)
                    .Action(bb =>
                    {
                        Console.WriteLine($"  [{Name}] 📺 看剧...");
                        bb.Set("fun", Math.Min(100f, bb.GetOrDefault("fun", 30f) + 8f));
                        bb.Set("energy", Math.Min(100f, bb.GetOrDefault("energy", 50f) + 2f));
                        return NodeStatus.Success;
                    })
                .End()
                .Sequence("Socialize")
                    .Condition(bb => bb.GetOrDefault("social", 40f) < 50f)
                    .Condition(bb => Personality.Extraversion > 0.4f)
                    .Action(bb =>
                    {
                        Console.WriteLine($"  [{Name}] 💬 和朋友聊天...");
                        bb.Set("social", Math.Min(100f, bb.GetOrDefault("social", 40f) + 12f));
                        return NodeStatus.Success;
                    })
                .End()
                .Sequence("Read")
                    .Condition(bb => bb.GetOrDefault("energy", 0f) > 20f)
                    .Condition(bb => Personality.Conscientiousness > 0.4f)
                    .Action(bb =>
                    {
                        Console.WriteLine($"  [{Name}] 📖 看点书...");
                        bb.Set("fun", Math.Min(100f, bb.GetOrDefault("fun", 30f) + 5f));
                        bb.Set("productivity", Math.Min(100f, bb.GetOrDefault("productivity", 70f) + 3f));
                        return NodeStatus.Success;
                    })
                .End()
            .End()
            .Build();
    }

    private BehaviorTree CreateDefaultBehaviorTree(DailyState state)
    {
        return BehaviorTreeBuilder.Create(state.ToString(), Blackboard)
            .Sequence("Default")
                .Action(bb => NodeStatus.Success)
            .End()
            .Build();
    }

    // ===================================================================
    //  主循环
    // ===================================================================
    public void Update(float deltaTime = 1f)
    {
        // ---- 时间 ----
        var hour = Blackboard.GetOrDefault("hour_of_day", 7f);
        hour += deltaTime * 0.1f;
        if (hour >= 24f)
        {
            hour -= 24f;
            var dow = Blackboard.GetOrDefault("day_of_week", 1f) + 1f;
            if (dow >= 7f) dow = 0f;
            Blackboard.Set("day_of_week", dow);
            Blackboard.Set("is_weekend", dow is 0f or 6f);
            Blackboard.Set("commute_progress", 0f);
            Blackboard.Set("bed_time", 23f);

            GoapAgent.UpdateState("task_complete", false);
            GoapAgent.UpdateState("has_information", false);
            GoapAgent.UpdateState("has_analysis", false);
            GoapAgent.UpdateState("has_report", false);
            GoapAgent.UpdateState("meeting_prepared", false);
            GoapAgent.UpdateState("document_reviewed", false);
        }
        Blackboard.Set("hour_of_day", hour);
        Blackboard.Set("day_progress", hour / 24f);

        // ---- 生理衰减 ----
        Blackboard.Set("energy", Math.Max(0f, Blackboard.GetOrDefault("energy", 50f) - deltaTime * 0.05f));
        Blackboard.Set("hunger", Math.Max(0f, Blackboard.GetOrDefault("hunger", 30f) - deltaTime * 0.6f));
        Blackboard.Set("bladder", Math.Min(100f, Blackboard.GetOrDefault("bladder", 20f) + deltaTime * 0.4f));
        Blackboard.Set("fatigue", Math.Min(100f, Blackboard.GetOrDefault("fatigue", 20f) + deltaTime * 0.3f));
        Blackboard.Set("stress", Math.Min(100f,
            Blackboard.GetOrDefault("stress", 10f) + deltaTime * 0.2f * (1f + Personality.Neuroticism)));

        // ---- 随机事件 ----
        if (Random.Shared.NextDouble() < 0.02)
            Blackboard.Set("boss_nearby", !Blackboard.GetOrDefault("boss_nearby", false));

        // ---- 状态计时 ----
        _stateElapsed += deltaTime * 0.1f;
        Blackboard.Set("state_elapsed", _stateElapsed);

        // ---- 模糊心情 ----
        var moodEngine = Blackboard.Get<FuzzyEngine>("_fuzzy_mood_engine");
        if (moodEngine != null)
        {
            var inputs = new Dictionary<string, float>
            {
                ["energy"] = Blackboard.GetOrDefault("energy", 50f),
                ["stress"] = Blackboard.GetOrDefault("stress", 10f),
                ["fatigue"] = Blackboard.GetOrDefault("fatigue", 20f)
            };
            Blackboard.Set("mood", moodEngine.Process(inputs)["mood"]);
        }

        // ---- FSM 更新 ----
        Fsm.Update();

        // ---- FSM → DailyState 映射 ----
        var fsmName = Fsm.CurrentState?.Name ?? "Morning";
        var newState = fsmName switch
        {
            "Working" => DailyState.Working,
            "Evening" => DailyState.Evening,
            "Sleeping" => DailyState.Sleeping,
            "Weekend" => DailyState.Weekend,
            _ => DailyState.Morning
        };

        if (newState != _currentState)
        {
            _currentBehaviorTree?.Stop();
            _currentState = newState;
            _currentPhase = "";
            _currentBehaviorTree = _behaviorTrees[_currentState];
            _currentBehaviorTree.Start();
            _stateElapsed = 0f;
            _phaseElapsed = 0f;
            Console.WriteLine($"\n=== [{Name}] === {GetStateDisplayName(_currentState)} ===");
        }

        // ---- 行为树 ----
        _currentBehaviorTree?.Update();
    }

    // ===================================================================
    //  辅助
    // ===================================================================
    private static string GetPhaseDisplayName(string phase) => phase switch
    {
        "wake_up" => "🌅 起床",
        "hygiene" => "🪥 洗漱",
        "breakfast" => "🥐 早餐",
        "commute" => "🚇 通勤",
        "focus" => "💼 上午工作",
        "lunch" => "🍚 午休",
        "afternoon" => "💻 下午工作",
        "commute_home" => "🚇 下班回家",
        "dinner" => "🍳 晚餐",
        "leisure" => "🎮 自由时间",
        "sleep_prep" => "🪥 睡前准备",
        "sleeping" => "😴 睡觉",
        "weekend_morning" => "🌤️ 周末上午",
        "weekend_afternoon" => "☀️ 周末下午",
        "weekend_evening" => "🌙 周末晚上",
        _ => phase
    };

    private static string GetStateDisplayName(DailyState state) => state switch
    {
        DailyState.Morning => "🌅 早上",
        DailyState.Working => "💼 工作",
        DailyState.Evening => "🌆 晚间",
        DailyState.Sleeping => "😴 睡觉",
        DailyState.Weekend => "🎉 周末",
        _ => state.ToString()
    };

    public string GetDebugInfo()
    {
        var lines = new List<string>
        {
            $"=== {Name} 的日常 ===",
            $"",
            $"场景: {GetStateDisplayName(_currentState)} ({_stateElapsed:F1}h)",
            $"阶段: {GetPhaseDisplayName(_currentPhase)} ({_phaseElapsed:F1}h)",
            $"时间: 星期{GetWeekDayName(Blackboard.GetOrDefault("day_of_week", 1f))} " +
                $"{Blackboard.GetOrDefault("hour_of_day", 7f):F1}:00",
            $"",
            $"生理: 精力{Blackboard.GetOrDefault("energy", 50f):F0} " +
                $"饱腹{Blackboard.GetOrDefault("hunger", 30f):F0} " +
                $"压力{Blackboard.GetOrDefault("stress", 10f):F0} " +
                $"疲劳{Blackboard.GetOrDefault("fatigue", 20f):F0} " +
                $"膀胱{Blackboard.GetOrDefault("bladder", 20f):F0}",
            $"心理: 心情{Blackboard.GetOrDefault("mood", 70f):F0} " +
                $"社交{Blackboard.GetOrDefault("social", 40f):F0} " +
                $"娱乐{Blackboard.GetOrDefault("fun", 30f):F0}",
            $"工作: 强度{Blackboard.GetOrDefault("work_intensity", 0f):F0} " +
                $"效率{Blackboard.GetOrDefault("productivity", 70f):F0} " +
                $"任务{Blackboard.GetOrDefault("task_count", 0f):F0}",
            $"卫生{Blackboard.GetOrDefault("hygiene", 70f):F0} " +
                $"睡眠质量{Blackboard.GetOrDefault("sleep_quality", 0.8f):F2}",
            $"想法: \"{Blackboard.GetOrDefault("current_thought", "...")}\""
        };
        return string.Join(Environment.NewLine, lines);
    }

    private static string GetWeekDayName(float day) => day switch
    {
        0f => "日", 1f => "一", 2f => "二", 3f => "三",
        4f => "四", 5f => "五", 6f => "六", _ => "?"
    };
}
