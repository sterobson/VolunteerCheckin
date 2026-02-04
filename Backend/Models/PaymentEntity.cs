using Azure;
using Azure.Data.Tables;

namespace VolunteerCheckin.Functions.Models;

/// <summary>
/// Represents a payment record in Azure Table Storage.
/// PartitionKey = EventId, RowKey = PaymentId (GUID).
/// </summary>
public class PaymentEntity : ITableEntity
{
    public string PartitionKey { get; set; } = string.Empty; // EventId
    public string RowKey { get; set; } = Guid.NewGuid().ToString(); // PaymentId
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string EventId { get; set; } = string.Empty;
    public string PersonId { get; set; } = string.Empty;
    public string StripeCheckoutSessionId { get; set; } = string.Empty;
    public string StripePaymentIntentId { get; set; } = string.Empty;
    public string PaymentType { get; set; } = string.Empty; // "EventCreation" or "MarshalUpgrade"
    public int MarshalTier { get; set; }
    public int AmountPence { get; set; }
    public string Currency { get; set; } = "gbp";
    public string Status { get; set; } = "Succeeded"; // "Succeeded", "Refunded"
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
