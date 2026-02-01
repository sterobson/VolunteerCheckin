using Azure;
using Azure.Data.Tables;

namespace VolunteerCheckin.Functions.Models;

/// <summary>
/// V2 schema entity for saving areas. Only contains V2 properties.
/// Used when saving migrated entities to avoid persisting deprecated V1 columns.
/// </summary>
public class AreaEntityV2 : ITableEntity
{
    public string PartitionKey { get; set; } = string.Empty;
    public string RowKey { get; set; } = string.Empty;
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public int SchemaVersion { get; set; } = AreaEntity.CurrentSchemaVersion;
    public string PayloadJson { get; set; } = string.Empty;

    // Core fields that remain as direct columns
    public string EventId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Color { get; set; } = "#667eea";
    public bool IsDefault { get; set; } = false;
    public int DisplayOrder { get; set; } = 0;
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Create a V2 entity from a full AreaEntity.
    /// </summary>
    public static AreaEntityV2 FromAreaEntity(AreaEntity entity)
    {
        return new AreaEntityV2
        {
            PartitionKey = entity.PartitionKey,
            RowKey = entity.RowKey,
            Timestamp = entity.Timestamp,
            ETag = entity.ETag,
            SchemaVersion = entity.SchemaVersion,
            PayloadJson = entity.PayloadJson,
            EventId = entity.EventId,
            Name = entity.Name,
            Description = entity.Description,
            Color = entity.Color,
            IsDefault = entity.IsDefault,
            DisplayOrder = entity.DisplayOrder,
            CreatedDate = entity.CreatedDate
        };
    }
}
