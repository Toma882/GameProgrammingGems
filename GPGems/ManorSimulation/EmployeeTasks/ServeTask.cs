using System.Numerics;

namespace GPGames.ManorSimulation.EmployeeTasks;

/// <summary>
/// 服务任务
/// </summary>
public class ServeTask : EmployeeTaskBase
{
    public string ServiceType { get; set; } = string.Empty;
    public int CustomerId { get; set; }

    public ServeTask(Vector2 position, float duration = 4f)
    {
        Type = "Serve";
        TargetPosition = position;
        Duration = duration;
    }
}
