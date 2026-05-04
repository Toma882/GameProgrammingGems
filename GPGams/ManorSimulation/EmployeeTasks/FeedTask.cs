using System.Numerics;

namespace GPGames.ManorSimulation;

/// <summary>
/// 喂养任务
/// </summary>
public class FeedTask : EmployeeTaskBase
{
    public string AnimalType { get; set; } = string.Empty;

    public FeedTask(Vector2 position, float duration = 3f)
    {
        Type = "Feed";
        TargetPosition = position;
        Duration = duration;
    }
}
