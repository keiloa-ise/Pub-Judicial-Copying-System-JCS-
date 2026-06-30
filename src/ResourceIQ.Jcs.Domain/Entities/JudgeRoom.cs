namespace ResourceIQ.Jcs.Domain.Entities;

/// <summary>Join row assigning a <see cref="Judge"/> to a <see cref="Room"/> (FR-04). A judge may
/// serve in more than one room; the room's court is its court.</summary>
public class JudgeRoom
{
    public Guid JudgeId { get; set; }
    public Judge? Judge { get; set; }

    public Guid RoomId { get; set; }
    public Room? Room { get; set; }
}
