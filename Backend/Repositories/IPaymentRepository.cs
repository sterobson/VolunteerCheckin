using VolunteerCheckin.Functions.Models;

namespace VolunteerCheckin.Functions.Repositories;

/// <summary>
/// Repository interface for managing payment records
/// </summary>
public interface IPaymentRepository
{
    /// <summary>
    /// Add a new payment record
    /// </summary>
    Task AddAsync(PaymentEntity payment);

    /// <summary>
    /// Get all payments for an event
    /// </summary>
    Task<IEnumerable<PaymentEntity>> GetByEventAsync(string eventId);

    /// <summary>
    /// Get a payment by its Stripe Checkout Session ID
    /// </summary>
    Task<PaymentEntity?> GetByStripeSessionIdAsync(string eventId, string sessionId);

    /// <summary>
    /// Update an existing payment
    /// </summary>
    Task UpdateAsync(PaymentEntity payment);
}
