using Azure;
using Azure.Data.Tables;

namespace VolunteerCheckin.Functions.Models;

/// <summary>
/// V2 schema entity for saving events. Only contains V2 properties.
/// Used when saving migrated entities to avoid persisting deprecated V1 columns.
/// </summary>
public class EventEntityV2 : ITableEntity
{
    public string PartitionKey { get; set; } = Constants.EventPartitionKey;
    public string RowKey { get; set; } = string.Empty;
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public int SchemaVersion { get; set; } = EventEntity.CurrentSchemaVersion;
    public string PayloadJson { get; set; } = string.Empty;

    // Core fields that remain as direct columns
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }
    public string TimeZoneId { get; set; } = "UTC";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedDate { get; set; }

    // Sample event properties (queryable - not in payload)
    public bool IsSampleEvent { get; set; } = false;
    public DateTime? ExpiresAt { get; set; } = null;

    /// <summary>
    /// Create a V2 entity from a full EventEntity.
    /// </summary>
    public static EventEntityV2 FromEventEntity(EventEntity entity)
    {
        return new EventEntityV2
        {
            PartitionKey = entity.PartitionKey,
            RowKey = entity.RowKey,
            Timestamp = entity.Timestamp,
            ETag = entity.ETag,
            SchemaVersion = entity.SchemaVersion,
            PayloadJson = entity.PayloadJson,
            Name = entity.Name,
            Description = entity.Description,
            EventDate = entity.EventDate,
            TimeZoneId = entity.TimeZoneId,
            IsActive = entity.IsActive,
            CreatedDate = entity.CreatedDate,
            IsSampleEvent = entity.IsSampleEvent,
            ExpiresAt = entity.ExpiresAt
        };
    }
}
