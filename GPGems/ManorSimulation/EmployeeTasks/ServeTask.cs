using System.Numerics;

namespace GPGems.ManorSimulation;

/// <summary>
/// 服务任务
/// </summary>
public class ServeTask : EmployeeTaskBase
{
    public string ServiceType { get; set; } = string.Empty;
    public int CustomerId { get; set; }

    public ServeTask()
    {
        Type = "Serve";
    }

    public ServeTask(Vector2 position, float duration = 4f) : this()
    {
        TargetPosition = position;
        Duration = duration;
    }
}
