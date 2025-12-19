using Azure;
using Azure.Data.Tables;

namespace VolunteerCheckin.Functions.Models;

public class MagicLinkEntity : ITableEntity
{
    public string PartitionKey { get; set; } = "MAGICLINK";
    // RowKey = token
    public string RowKey { get; set; } = string.Empty;
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string Email { get; set; } = string.Empty;
    public DateTime ExpiryDate { get; set; }
    public bool IsUsed { get; set; } = false;
}
