using Azure;
using Azure.Data.Tables;

namespace VolunteerCheckin.Functions.Models;

/// <summary>
/// V2 schema entity for saving locations. Only contains V2 properties.
/// Used when saving migrated entities to avoid persisting deprecated V1 columns.
/// </summary>
public class LocationEntityV2 : ITableEntity
{
    public string PartitionKey { get; set; } = string.Empty;
    public string RowKey { get; set; } = string.Empty;
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public int SchemaVersion { get; set; } = LocationEntity.CurrentSchemaVersion;
    public string PayloadJson { get; set; } = string.Empty;

    // Core fields that remain as direct columns
    public string EventId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int RequiredMarshals { get; set; } = 1;
    public int CheckedInCount { get; set; } = 0;
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public bool IsDynamic { get; set; } = false;

    /// <summary>
    /// Create a V2 entity from a full LocationEntity.
    /// </summary>
    public static LocationEntityV2 FromLocationEntity(LocationEntity entity)
    {
        return new LocationEntityV2
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
            Latitude = entity.Latitude,
            Longitude = entity.Longitude,
            RequiredMarshals = entity.RequiredMarshals,
            CheckedInCount = entity.CheckedInCount,
            StartTime = entity.StartTime,
            EndTime = entity.EndTime,
            IsDynamic = entity.IsDynamic
        };
    }
}
