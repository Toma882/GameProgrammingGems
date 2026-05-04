using System.Numerics;

namespace GPGems.ManorSimulation;

/// <summary>
/// 喂养任务
/// </summary>
public class FeedTask : EmployeeTaskBase
{
    public string AnimalType { get; set; } = string.Empty;

    public FeedTask()
    {
        Type = "Feed";
    }

    public FeedTask(Vector2 position, float duration = 3f) : this()
    {
        TargetPosition = position;
        Duration = duration;
    }
}
