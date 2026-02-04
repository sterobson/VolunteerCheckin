using Azure;
using Azure.Data.Tables;
using VolunteerCheckin.Functions.Models;
using VolunteerCheckin.Functions.Services;

namespace VolunteerCheckin.Functions.Repositories;

/// <summary>
/// Azure Table Storage implementation of IPaymentRepository
/// </summary>
public class TableStoragePaymentRepository : IPaymentRepository
{
    private readonly TableClient _table;

    public TableStoragePaymentRepository(TableStorageService tableStorage)
    {
        _table = tableStorage.GetPaymentsTable();
    }

    public async Task AddAsync(PaymentEntity payment)
    {
        await _table.AddEntityAsync(payment);
    }

    public async Task<IEnumerable<PaymentEntity>> GetByEventAsync(string eventId)
    {
        List<PaymentEntity> payments = [];
        await foreach (PaymentEntity payment in _table.QueryAsync<PaymentEntity>(p => p.PartitionKey == eventId))
        {
            payments.Add(payment);
        }
        return payments.OrderByDescending(p => p.CreatedAt);
    }

    public async Task<PaymentEntity?> GetByStripeSessionIdAsync(string eventId, string sessionId)
    {
        List<PaymentEntity> payments = [];
        await foreach (PaymentEntity payment in _table.QueryAsync<PaymentEntity>(p => p.PartitionKey == eventId && p.StripeCheckoutSessionId == sessionId))
        {
            payments.Add(payment);
        }
        return payments.FirstOrDefault();
    }

    public async Task UpdateAsync(PaymentEntity payment)
    {
        await _table.UpdateEntityAsync(payment, payment.ETag, TableUpdateMode.Replace);
    }
}
