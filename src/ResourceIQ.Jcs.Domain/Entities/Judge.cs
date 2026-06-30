namespace ResourceIQ.Jcs.Domain.Entities;

/// <summary>
/// A judge (FR-04). A judge is assigned to one or more rooms (غرف) via <see cref="JudgeRoom"/>;
/// the room determines the court. A judge may serve in multiple rooms (confirmed business rule).
/// </summary>
public class Judge
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public ICollection<JudgeRoom> Rooms { get; set; } = new List<JudgeRoom>();
}
