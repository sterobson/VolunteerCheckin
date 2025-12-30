namespace VolunteerCheckin.Functions.Models;

/// <summary>
/// Defines a single scope configuration for a checklist item.
/// Multiple scope configurations can be combined to create complex visibility and completion rules.
/// The "Most Specific Wins" principle determines which configuration applies when multiple match.
/// </summary>
public class ScopeConfiguration
{
    /// <summary>
    /// The scope type that determines completion semantics (personal vs shared).
    /// Values: "Everyone", "EveryoneInAreas", "EveryoneAtCheckpoints", "SpecificPeople",
    ///         "OnePerCheckpoint", "OnePerArea", "AreaLead"
    /// </summary>
    public string Scope { get; set; } = string.Empty;

    /// <summary>
    /// The type of entity being filtered.
    /// Values: "Marshal" (most specific), "Checkpoint", "Area", null (Everyone - least specific)
    /// </summary>
    public string? ItemType { get; set; }

    /// <summary>
    /// The list of IDs to match against (Marshal IDs, Checkpoint IDs, or Area IDs).
    /// Empty or null for "Everyone" scope.
    /// </summary>
    public List<string> Ids { get; set; } = [];

    /// <summary>
    /// Gets the specificity level for priority ranking.
    /// Lower number = higher priority (more specific)
    /// 1 = Marshal (most specific)
    /// 2 = Checkpoint
    /// 3 = Area
    /// 4 = Everyone (least specific)
    /// </summary>
    public int GetSpecificity()
    {
        return ItemType switch
        {
            "Marshal" => 1,
            "Checkpoint" => 2,
            "Area" => 3,
            null => 4,
            _ => 4
        };
    }
}
