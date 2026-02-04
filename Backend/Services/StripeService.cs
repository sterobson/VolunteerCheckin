using Microsoft.Extensions.Logging;
using Stripe;
using Stripe.Checkout;

namespace VolunteerCheckin.Functions.Services;

/// <summary>
/// Wraps Stripe API interactions for payment processing.
/// </summary>
public class StripeService
{
    private readonly ILogger<StripeService> _logger;
    private readonly string? _secretKey;
    private readonly string? _webhookSecret;

    public StripeService(ILogger<StripeService> logger)
    {
        _logger = logger;
        _secretKey = Environment.GetEnvironmentVariable("STRIPE_SECRET_KEY");
        _webhookSecret = Environment.GetEnvironmentVariable("STRIPE_WEBHOOK_SECRET");

        if (!string.IsNullOrWhiteSpace(_secretKey))
        {
            StripeConfiguration.ApiKey = _secretKey;
        }
    }

    /// <summary>
    /// Whether Stripe is configured (has a secret key).
    /// </summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_secretKey);

    /// <summary>
    /// Create a Stripe Checkout session for a new event purchase.
    /// </summary>
    public async Task<Session> CreateCheckoutSessionAsync(
        int amountPence,
        int marshalTier,
        string sessionId,
        string successUrl,
        string cancelUrl,
        string customerEmail,
        string eventName)
    {
        SessionCreateOptions options = new()
        {
            PaymentMethodTypes = ["card"],
            Mode = "payment",
            SuccessUrl = successUrl,
            CancelUrl = cancelUrl,
            CustomerEmail = customerEmail,
            ClientReferenceId = sessionId,
            LineItems =
            [
                new SessionLineItemOptions
                {
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = "gbp",
                        UnitAmount = amountPence,
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = $"Event: {eventName}",
                            Description = $"Includes up to {marshalTier} marshals"
                        }
                    },
                    Quantity = 1
                }
            ],
            Metadata = new Dictionary<string, string>
            {
                ["pending_session_id"] = sessionId,
                ["marshal_tier"] = marshalTier.ToString(),
                ["payment_type"] = Constants.PaymentTypeEventCreation
            }
        };

        SessionService service = new();
        Session session = await service.CreateAsync(options);

        _logger.LogInformation("Stripe Checkout session created: {SessionId} for amount {Amount}p",
            session.Id, amountPence);

        return session;
    }

    /// <summary>
    /// Create a Stripe Checkout session for upgrading a marshal tier.
    /// </summary>
    public async Task<Session> CreateUpgradeCheckoutSessionAsync(
        string eventId,
        int upgradePricePence,
        int currentTier,
        int newTier,
        string successUrl,
        string cancelUrl,
        string customerEmail,
        string eventName)
    {
        SessionCreateOptions options = new()
        {
            PaymentMethodTypes = ["card"],
            Mode = "payment",
            SuccessUrl = successUrl,
            CancelUrl = cancelUrl,
            CustomerEmail = customerEmail,
            ClientReferenceId = eventId,
            LineItems =
            [
                new SessionLineItemOptions
                {
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = "gbp",
                        UnitAmount = upgradePricePence,
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = $"Upgrade: {eventName}",
                            Description = $"Upgrade from {currentTier} to {newTier} marshals"
                        }
                    },
                    Quantity = 1
                }
            ],
            Metadata = new Dictionary<string, string>
            {
                ["event_id"] = eventId,
                ["current_tier"] = currentTier.ToString(),
                ["new_tier"] = newTier.ToString(),
                ["payment_type"] = Constants.PaymentTypeMarshalUpgrade
            }
        };

        SessionService service = new();
        Session session = await service.CreateAsync(options);

        _logger.LogInformation("Stripe upgrade session created: {SessionId} for event {EventId}, {CurrentTier} -> {NewTier}",
            session.Id, eventId, currentTier, newTier);

        return session;
    }

    /// <summary>
    /// Verify and construct a Stripe webhook event from the request body and signature.
    /// </summary>
    public Event ConstructWebhookEvent(string json, string signature)
    {
        if (string.IsNullOrWhiteSpace(_webhookSecret))
        {
            throw new InvalidOperationException("STRIPE_WEBHOOK_SECRET is not configured");
        }

        return EventUtility.ConstructEvent(json, signature, _webhookSecret);
    }

    /// <summary>
    /// Get a Checkout session by ID (for verifying payment status).
    /// </summary>
    public async Task<Session> GetSessionAsync(string sessionId)
    {
        SessionService service = new();
        return await service.GetAsync(sessionId);
    }
}
