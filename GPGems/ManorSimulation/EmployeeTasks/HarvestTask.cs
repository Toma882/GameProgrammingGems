using System.Numerics;

namespace GPGames.ManorSimulation.EmployeeTasks;

/// <summary>
/// 收获任务
/// </summary>
public class HarvestTask : EmployeeTaskBase
{
    public string ResourceType { get; set; } = string.Empty;
    public int Amount { get; set; }

    public HarvestTask(Vector2 position, float duration = 5f)
    {
        Type = "Harvest";
        TargetPosition = position;
        Duration = duration;
    }
}
