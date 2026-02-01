using Azure;
using Azure.Data.Tables;

namespace VolunteerCheckin.Functions.Models;

public class LayerEntity : ITableEntity
{
    public string PartitionKey { get; set; } = string.Empty; // EventId
    public string RowKey { get; set; } = Guid.NewGuid().ToString(); // LayerId
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string EventId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int DisplayOrder { get; set; } = 0;

    // Route data (optionally associated with this layer)
    public string GpxRouteJson { get; set; } = "[]";
    public string RouteColor { get; set; } = string.Empty; // Hex color, default #3388ff (Leaflet blue)
    public string RouteStyle { get; set; } = string.Empty; // line, dash, dash-long, dash-short, dot, dot-sparse, dash-dot, dash-dot-dot, etc.
    public int? RouteWeight { get; set; } // Line thickness in pixels (default: 4)

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets the layer ID (alias for RowKey)
    /// </summary>
    public string LayerId => RowKey;
}
