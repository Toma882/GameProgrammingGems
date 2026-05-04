using System.Numerics;

namespace GPGems.ManorSimulation;

/// <summary>
/// 收获任务
/// </summary>
public class HarvestTask : EmployeeTaskBase
{
    public string ResourceType { get; set; } = string.Empty;
    public int Amount { get; set; }

    public HarvestTask()
    {
        Type = "Harvest";
    }

    public HarvestTask(Vector2 position, float duration = 5f) : this()
    {
        TargetPosition = position;
        Duration = duration;
    }
}
