using Azure;
using Azure.Data.Tables;

namespace VolunteerCheckin.Functions.Models;

/// <summary>
/// Entity representing an incident reported by a marshal.
/// Contains denormalized snapshot data to preserve the state at the time of the incident,
/// since checkpoints can be renamed, marshals reassigned, etc.
/// </summary>
public class IncidentEntity : ITableEntity
{
    public string PartitionKey { get; set; } = string.Empty; // EventId
    public string RowKey { get; set; } = string.Empty; // IncidentId (GUID)
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string EventId { get; set; } = string.Empty;
    public string IncidentId { get; set; } = string.Empty;

    /// <summary>
    /// Title/summary of the incident (required)
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Detailed description of what happened (required)
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Severity level: "low", "medium", "high", "critical"
    /// </summary>
    public string Severity { get; set; } = "medium";

    /// <summary>
    /// When the incident occurred (user-provided, may differ from creation time)
    /// </summary>
    public DateTime IncidentTime { get; set; }

    /// <summary>
    /// When this report was created
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// GPS latitude where incident occurred (if known)
    /// </summary>
    public double? Latitude { get; set; }

    /// <summary>
    /// GPS longitude where incident occurred (if known)
    /// </summary>
    public double? Longitude { get; set; }

    /// <summary>
    /// JSON snapshot of the context at time of reporting.
    /// Contains denormalized data that won't change if checkpoint/marshals are modified later.
    /// Structure: IncidentContextSnapshot
    /// </summary>
    public string ContextSnapshotJson { get; set; } = "{}";

    /// <summary>
    /// ID of the person who reported this incident
    /// </summary>
    public string ReportedByPersonId { get; set; } = string.Empty;

    /// <summary>
    /// Name of the person who reported this (denormalized snapshot)
    /// </summary>
    public string ReportedByName { get; set; } = string.Empty;

    /// <summary>
    /// Marshal ID if the reporter was a marshal (for linking)
    /// </summary>
    public string ReportedByMarshalId { get; set; } = string.Empty;

    /// <summary>
    /// Current status: "open", "acknowledged", "in_progress", "resolved", "closed"
    /// </summary>
    public string Status { get; set; } = "open";

    /// <summary>
    /// JSON array of status updates/notes added by admins
    /// Structure: List&lt;IncidentUpdate&gt;
    /// </summary>
    public string UpdatesJson { get; set; } = "[]";

    /// <summary>
    /// ID of the area this incident belongs to (for area lead visibility)
    /// Determined by checkpoint area or incident location at time of report
    /// </summary>
    public string AreaId { get; set; } = string.Empty;

    /// <summary>
    /// Name of the area (denormalized snapshot)
    /// </summary>
    public string AreaName { get; set; } = string.Empty;
}

/// <summary>
/// Denormalized snapshot of context at time of incident report.
/// Stored as JSON to preserve the exact state even if checkpoint/marshals change later.
/// </summary>
public class IncidentContextSnapshot
{
    /// <summary>
    /// The checkpoint where the reporter was assigned (if any)
    /// </summary>
    public IncidentCheckpointSnapshot? Checkpoint { get; set; }

    /// <summary>
    /// All marshals assigned to the same checkpoint at the time
    /// </summary>
    public List<IncidentMarshalSnapshot> MarshalsPresentAtCheckpoint { get; set; } = [];
}

/// <summary>
/// Snapshot of checkpoint data at time of incident
/// </summary>
public class IncidentCheckpointSnapshot
{
    public string CheckpointId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public List<string> AreaIds { get; set; } = [];
    public List<string> AreaNames { get; set; } = [];
}

/// <summary>
/// Snapshot of marshal data at time of incident
/// </summary>
public class IncidentMarshalSnapshot
{
    public string MarshalId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool WasCheckedIn { get; set; }
    public DateTime? CheckInTime { get; set; }
    public string? CheckInMethod { get; set; }
}

/// <summary>
/// A status update or note added to an incident
/// </summary>
public class IncidentUpdate
{
    public string UpdateId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string AuthorPersonId { get; set; } = string.Empty;
    public string AuthorName { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
    public string? StatusChange { get; set; } // If status was changed, what it was changed to
}
