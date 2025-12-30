using Azure;
using Azure.Data.Tables;

namespace VolunteerCheckin.Functions.Models;

public class AdminUserEntity : ITableEntity
{
    public string PartitionKey { get; set; } = Constants.AdminPartitionKey;
    // RowKey = email address
    public string RowKey { get; set; } = string.Empty;
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
}
