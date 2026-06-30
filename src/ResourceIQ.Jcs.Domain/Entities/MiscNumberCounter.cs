namespace ResourceIQ.Jcs.Domain.Entities;

/// <summary>
/// Running counter for رقم المتفرق (the extra sequential number on متفرق copies). Scoped by a
/// string key + year, and reset each year (like the copy number). The scope key is:
///   • "R:{roomId}" for جزائية (criminal) courts — sequential per room;
///   • "C:{courtId}" for all other courts — sequential per court.
/// Incremented atomically inside the create transaction; decremented when the latest copy is
/// deleted (FR-16) so no gap appears. Composite key (ScopeKey, Year).
/// </summary>
public class MiscNumberCounter
{
    public string ScopeKey { get; set; } = string.Empty;
    public int Year { get; set; }
    public int LastNumber { get; set; }
}
