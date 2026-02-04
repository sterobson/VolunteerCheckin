using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Stripe;
using Stripe.Checkout;
using System.Text.Json;
using VolunteerCheckin.Functions.Helpers;
using VolunteerCheckin.Functions.Models;
using VolunteerCheckin.Functions.Repositories;
using VolunteerCheckin.Functions.Services;

namespace VolunteerCheckin.Functions.Functions;

public class PaymentFunctions
{
    private readonly ILogger<PaymentFunctions> _logger;
    private readonly StripeService _stripeService;
    private readonly IPaymentRepository _paymentRepository;
    private readonly IPendingEventRepository _pendingEventRepository;
    private readonly IEventRepository _eventRepository;
    private readonly IPersonRepository _personRepository;
    private readonly IEventRoleRepository _eventRoleRepository;
    private readonly IMarshalRepository _marshalRepository;
    private readonly ClaimsService _claimsService;

    public PaymentFunctions(
        ILogger<PaymentFunctions> logger,
        StripeService stripeService,
        IPaymentRepository paymentRepository,
        IPendingEventRepository pendingEventRepository,
        IEventRepository eventRepository,
        IPersonRepository personRepository,
        IEventRoleRepository eventRoleRepository,
        IMarshalRepository marshalRepository,
        ClaimsService claimsService)
    {
        _logger = logger;
        _stripeService = stripeService;
        _paymentRepository = paymentRepository;
        _pendingEventRepository = pendingEventRepository;
        _eventRepository = eventRepository;
        _personRepository = personRepository;
        _eventRoleRepository = eventRoleRepository;
        _marshalRepository = marshalRepository;
        _claimsService = claimsService;
    }

    /// <summary>
    /// Create a Stripe Checkout session for purchasing a new event.
    /// Stores event data as a PendingEventEntity until payment is confirmed.
    /// </summary>
#pragma warning disable MA0051
    [Function("CreateCheckoutSession")]
    public async Task<IActionResult> CreateCheckoutSession(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "payments/create-checkout-session")] HttpRequest req)
    {
        try
        {
            if (!_stripeService.IsConfigured)
            {
                return new StatusCodeResult(503);
            }

            string? sessionToken = FunctionHelpers.GetSessionToken(req);
            if (string.IsNullOrWhiteSpace(sessionToken))
            {
                return new UnauthorizedObjectResult(new { message = "Authentication required" });
            }

            UserClaims? claims = await _claimsService.GetClaimsAsync(sessionToken);
            if (claims == null)
            {
                return new UnauthorizedObjectResult(new { message = "Invalid or expired session" });
            }

            (CreateCheckoutSessionRequest? request, IActionResult? error) =
                await FunctionHelpers.TryDeserializeRequestAsync<CreateCheckoutSessionRequest>(req);
            if (error != null) return error;

            if (request!.MarshalTier < PricingService.MinimumMarshalTier)
            {
                return new BadRequestObjectResult(new { message = Constants.ErrorInvalidMarshalTier });
            }

            int amountPence = PricingService.CalculatePricePence(request.MarshalTier);

            // Serialize the event data for storage
            string eventDataJson = JsonSerializer.Serialize(request.EventData, FunctionHelpers.JsonOptions);

            // Build success/cancel URLs
            string frontendUrl = FunctionHelpers.GetFrontendUrl(req);
            bool useHashRouting = FunctionHelpers.UsesHashRouting(req);
            string routePrefix = useHashRouting ? "/#" : "";
            string successUrl = $"{frontendUrl}{routePrefix}/payment/success?session_id={{CHECKOUT_SESSION_ID}}";
            string cancelUrl = $"{frontendUrl}{routePrefix}/myevents";

            // Create pending event entity first
            string pendingId = Guid.NewGuid().ToString();
            PendingEventEntity pendingEvent = new()
            {
                RowKey = pendingId, // Temporary - will be updated with Stripe session ID
                PersonId = claims.PersonId,
                EventDataJson = eventDataJson,
                MarshalTier = request.MarshalTier,
                AmountPence = amountPence,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(Constants.PendingEventExpiryHours),
                Status = Constants.PendingStatusPending
            };

            // Create Stripe Checkout session
            Session stripeSession = await _stripeService.CreateCheckoutSessionAsync(
                amountPence,
                request.MarshalTier,
                pendingId,
                successUrl,
                cancelUrl,
                claims.PersonEmail,
                request.EventData.Name);

            // Update pending event with the Stripe session ID as RowKey
            pendingEvent.RowKey = stripeSession.Id;
            await _pendingEventRepository.AddAsync(pendingEvent);

            _logger.LogInformation("Checkout session created for person {PersonId}, tier {Tier}, amount {Amount}p",
                claims.PersonId, request.MarshalTier, amountPence);

            return new OkObjectResult(new CreateCheckoutSessionResponse(stripeSession.Url, stripeSession.Id));
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe error creating checkout session");
            return new ObjectResult(new { message = "Payment service error" }) { StatusCode = 502 };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating checkout session");
            return new StatusCodeResult(500);
        }
    }
#pragma warning restore MA0051

    /// <summary>
    /// Stripe webhook handler. Processes checkout.session.completed events.
    /// Anonymous endpoint - verified via Stripe signature.
    /// </summary>
#pragma warning disable MA0051
    [Function("StripeWebhook")]
    public async Task<IActionResult> StripeWebhook(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "payments/webhook")] HttpRequest req)
    {
        try
        {
            string json = await new StreamReader(req.Body).ReadToEndAsync();
            string? signature = req.Headers["Stripe-Signature"];

            if (string.IsNullOrWhiteSpace(signature))
            {
                return new BadRequestObjectResult(new { message = "Missing Stripe-Signature header" });
            }

            Event stripeEvent;
            try
            {
                stripeEvent = _stripeService.ConstructWebhookEvent(json, signature);
            }
            catch (StripeException ex)
            {
                _logger.LogWarning(ex, "Stripe webhook signature verification failed");
                return new BadRequestObjectResult(new { message = "Invalid signature" });
            }

            if (stripeEvent.Type == EventTypes.CheckoutSessionCompleted)
            {
                Session session = (Session)stripeEvent.Data.Object;
                await HandleCheckoutSessionCompletedAsync(session);
            }

            return new OkResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Stripe webhook");
            return new StatusCodeResult(500);
        }
    }
#pragma warning restore MA0051

    /// <summary>
    /// Verify a Stripe Checkout session status (fallback for when webhook is delayed).
    /// </summary>
    [Function("VerifySession")]
    public async Task<IActionResult> VerifySession(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "payments/verify-session/{sessionId}")] HttpRequest req,
        string sessionId)
    {
        try
        {
            string? sessionToken = FunctionHelpers.GetSessionToken(req);
            if (string.IsNullOrWhiteSpace(sessionToken))
            {
                return new UnauthorizedObjectResult(new { message = "Authentication required" });
            }

            UserClaims? claims = await _claimsService.GetClaimsAsync(sessionToken);
            if (claims == null)
            {
                return new UnauthorizedObjectResult(new { message = "Invalid or expired session" });
            }

            // Check if the pending event has already been processed
            PendingEventEntity? pendingEvent = await _pendingEventRepository.GetBySessionIdAsync(sessionId);
            if (pendingEvent == null)
            {
                return new OkObjectResult(new VerifySessionResponse(false, null, "Session not found"));
            }

            if (pendingEvent.Status == Constants.PendingStatusCompleted)
            {
                // Already processed - find the event by looking at payments
                return new OkObjectResult(new VerifySessionResponse(true, null, "Payment already processed"));
            }

            // Check with Stripe directly
            Session stripeSession = await _stripeService.GetSessionAsync(sessionId);
            if (stripeSession.PaymentStatus == "paid")
            {
                // Process the payment now (idempotent)
                string? eventId = await HandleCheckoutSessionCompletedAsync(stripeSession);
                return new OkObjectResult(new VerifySessionResponse(true, eventId, "Payment verified"));
            }

            return new OkObjectResult(new VerifySessionResponse(false, null, "Payment not yet completed"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying session {SessionId}", sessionId);
            return new StatusCodeResult(500);
        }
    }

    /// <summary>
    /// Create a Stripe Checkout session for upgrading a marshal tier.
    /// </summary>
#pragma warning disable MA0051
    [Function("CreateUpgradeSession")]
    public async Task<IActionResult> CreateUpgradeSession(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "payments/create-upgrade-session")] HttpRequest req)
    {
        try
        {
            if (!_stripeService.IsConfigured)
            {
                return new StatusCodeResult(503);
            }

            string? sessionToken = FunctionHelpers.GetSessionToken(req);
            if (string.IsNullOrWhiteSpace(sessionToken))
            {
                return new UnauthorizedObjectResult(new { message = "Authentication required" });
            }

            (CreateUpgradeSessionRequest? request, IActionResult? error) =
                await FunctionHelpers.TryDeserializeRequestAsync<CreateUpgradeSessionRequest>(req);
            if (error != null) return error;

            UserClaims? claims = await _claimsService.GetClaimsAsync(sessionToken, request!.EventId);
            if (claims == null)
            {
                return new UnauthorizedObjectResult(new { message = Constants.ErrorNotAuthorized });
            }

            if (!claims.IsEventOwner)
            {
                return new UnauthorizedObjectResult(new { message = "Only event owners can upgrade the marshal tier" });
            }

            EventEntity? eventEntity = await _eventRepository.GetAsync(request.EventId);
            if (eventEntity == null)
            {
                return new NotFoundObjectResult(new { message = Constants.ErrorEventNotFound });
            }

            if (eventEntity.IsFreeEvent)
            {
                return new BadRequestObjectResult(new { message = "This event does not require payment" });
            }

            if (request.NewMarshalTier <= eventEntity.PaidMarshalTier)
            {
                return new BadRequestObjectResult(new { message = Constants.ErrorCannotDowngradeTier });
            }

            if (request.NewMarshalTier < PricingService.MinimumMarshalTier)
            {
                return new BadRequestObjectResult(new { message = Constants.ErrorInvalidMarshalTier });
            }

            int upgradePricePence = PricingService.CalculateUpgradePricePence(
                eventEntity.PaidMarshalTier, request.NewMarshalTier);

            if (upgradePricePence <= 0)
            {
                return new BadRequestObjectResult(new { message = Constants.ErrorCannotDowngradeTier });
            }

            // Store pending upgrade info
            PendingEventEntity pendingUpgrade = new()
            {
                RowKey = Guid.NewGuid().ToString(), // Temporary
                PersonId = claims.PersonId,
                EventDataJson = string.Empty,
                MarshalTier = request.NewMarshalTier,
                AmountPence = upgradePricePence,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(Constants.PendingEventExpiryHours),
                Status = Constants.PendingStatusPending,
                UpgradeEventId = request.EventId
            };

            string frontendUrl = FunctionHelpers.GetFrontendUrl(req);
            bool useHashRouting = FunctionHelpers.UsesHashRouting(req);
            string routePrefix = useHashRouting ? "/#" : "";
            string successUrl = $"{frontendUrl}{routePrefix}/payment/success?session_id={{CHECKOUT_SESSION_ID}}&upgrade=true&eventId={request.EventId}";
            string cancelUrl = $"{frontendUrl}{routePrefix}/admin/event/{request.EventId}";

            Session stripeSession = await _stripeService.CreateUpgradeCheckoutSessionAsync(
                request.EventId,
                upgradePricePence,
                eventEntity.PaidMarshalTier,
                request.NewMarshalTier,
                successUrl,
                cancelUrl,
                claims.PersonEmail,
                eventEntity.Name);

            pendingUpgrade.RowKey = stripeSession.Id;
            await _pendingEventRepository.AddAsync(pendingUpgrade);

            _logger.LogInformation("Upgrade session created for event {EventId}, {CurrentTier} -> {NewTier}, amount {Amount}p",
                request.EventId, eventEntity.PaidMarshalTier, request.NewMarshalTier, upgradePricePence);

            return new OkObjectResult(new CreateCheckoutSessionResponse(stripeSession.Url, stripeSession.Id));
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe error creating upgrade session");
            return new ObjectResult(new { message = "Payment service error" }) { StatusCode = 502 };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating upgrade session");
            return new StatusCodeResult(500);
        }
    }
#pragma warning restore MA0051

    /// <summary>
    /// Get payment info for an event (billing section).
    /// </summary>
    [Function("GetEventPayments")]
    public async Task<IActionResult> GetEventPayments(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "payments/event/{eventId}")] HttpRequest req,
        string eventId)
    {
        try
        {
            string? sessionToken = FunctionHelpers.GetSessionToken(req);
            if (string.IsNullOrWhiteSpace(sessionToken))
            {
                return new UnauthorizedObjectResult(new { message = "Authentication required" });
            }

            UserClaims? claims = await _claimsService.GetClaimsAsync(sessionToken, eventId);
            if (claims == null || !claims.IsEventOwner)
            {
                return new UnauthorizedObjectResult(new { message = Constants.ErrorNotAuthorized });
            }

            EventEntity? eventEntity = await _eventRepository.GetAsync(eventId);
            if (eventEntity == null)
            {
                return new NotFoundObjectResult(new { message = Constants.ErrorEventNotFound });
            }

            IEnumerable<PaymentEntity> payments = await _paymentRepository.GetByEventAsync(eventId);
            IEnumerable<MarshalEntity> marshals = await _marshalRepository.GetByEventAsync(eventId);

            List<PaymentHistoryItem> history = payments.Select(p => new PaymentHistoryItem(
                p.RowKey,
                p.PaymentType,
                p.MarshalTier,
                p.AmountPence,
                p.Status,
                p.CreatedAt
            )).ToList();

            EventPaymentInfoResponse response = new(
                eventEntity.PaidMarshalTier,
                marshals.Count(),
                eventEntity.IsFreeEvent,
                history
            );

            return new OkObjectResult(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting event payments");
            return new StatusCodeResult(500);
        }
    }

    /// <summary>
    /// Calculate pricing for display purposes (anonymous endpoint).
    /// </summary>
    [Function("CalculatePricing")]
    public IActionResult CalculatePricing(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "pricing/calculate")] HttpRequest req)
    {
        if (!int.TryParse(req.Query["marshalCount"], out int marshalCount))
        {
            marshalCount = PricingService.MinimumMarshalTier;
        }

        PricingBreakdown breakdown = PricingService.GetBreakdown(marshalCount);

        PricingCalculationResponse response = new(
            breakdown.MarshalCount,
            breakdown.TotalPricePence,
            breakdown.BasePricePence,
            breakdown.AdditionalMarshalsPricePence,
            breakdown.IncludedMarshals,
            breakdown.AdditionalMarshals
        );

        return new OkObjectResult(response);
    }

    /// <summary>
    /// Cleanup expired pending events. Runs hourly.
    /// </summary>
    [Function("CleanupExpiredPendingEvents")]
    public async Task CleanupExpiredPendingEvents(
        [TimerTrigger("0 0 * * * *")] TimerInfo timer)
    {
        try
        {
            IEnumerable<PendingEventEntity> expired = await _pendingEventRepository.GetExpiredAsync();
            int count = 0;
            foreach (PendingEventEntity entity in expired)
            {
                await _pendingEventRepository.DeleteAsync(entity.RowKey);
                count++;
            }

            if (count > 0)
            {
                _logger.LogInformation("Cleaned up {Count} expired pending events", count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up expired pending events");
        }
    }

    /// <summary>
    /// Handle a completed checkout session - create the event or apply the upgrade.
    /// Returns the event ID if a new event was created.
    /// </summary>
    private async Task<string?> HandleCheckoutSessionCompletedAsync(Session session)
    {
        PendingEventEntity? pendingEvent = await _pendingEventRepository.GetBySessionIdAsync(session.Id);
        if (pendingEvent == null)
        {
            _logger.LogWarning("No pending event found for session {SessionId}", session.Id);
            return null;
        }

        // Idempotency check - skip if already processed
        if (pendingEvent.Status == Constants.PendingStatusCompleted)
        {
            _logger.LogInformation("Session {SessionId} already processed, skipping", session.Id);
            return null;
        }

        string? eventId;
        if (!string.IsNullOrEmpty(pendingEvent.UpgradeEventId))
        {
            // This is an upgrade payment
            eventId = await HandleUpgradePaymentAsync(session, pendingEvent);
        }
        else
        {
            // This is a new event creation payment
            eventId = await HandleNewEventPaymentAsync(session, pendingEvent);
        }

        // Mark pending event as completed
        pendingEvent.Status = Constants.PendingStatusCompleted;
        await _pendingEventRepository.UpdateAsync(pendingEvent);

        return eventId;
    }

    private async Task<string?> HandleNewEventPaymentAsync(Session session, PendingEventEntity pendingEvent)
    {
        // Deserialize the stored event data
        CreateEventRequest? eventRequest = JsonSerializer.Deserialize<CreateEventRequest>(
            pendingEvent.EventDataJson, FunctionHelpers.JsonOptions);

        if (eventRequest == null)
        {
            _logger.LogError("Failed to deserialize event data for session {SessionId}", session.Id);
            return null;
        }

        // Create the event (same logic as EventFunctions.CreateEvent)
        DateTime eventDateUtc = FunctionHelpers.ConvertToUtc(eventRequest.EventDate, eventRequest.TimeZoneId);

        EventEntity eventEntity = new()
        {
            RowKey = Guid.NewGuid().ToString(),
            Name = eventRequest.Name,
            Description = eventRequest.Description,
            EventDate = eventDateUtc,
            TimeZoneId = eventRequest.TimeZoneId,
            PaidMarshalTier = pendingEvent.MarshalTier,
            IsFreeEvent = false
        };

        // Build and set the v2 payload
        EventPayload payload = new()
        {
            Terminology = new TerminologyPayload
            {
                Person = eventRequest.PeopleTerm ?? "Marshals",
                Location = eventRequest.CheckpointTerm ?? "Checkpoints",
                Area = eventRequest.AreaTerm ?? "Areas",
                Task = eventRequest.ChecklistTerm ?? "Tasks",
                Course = eventRequest.CourseTerm ?? "Course"
            },
            Styling = new StylingPayload
            {
                Locations = new LocationStylingPayload
                {
                    DefaultType = eventRequest.DefaultCheckpointStyleType ?? "default",
                    DefaultColor = eventRequest.DefaultCheckpointStyleColor ?? string.Empty,
                    DefaultBackgroundShape = eventRequest.DefaultCheckpointStyleBackgroundShape ?? string.Empty,
                    DefaultBackgroundColor = eventRequest.DefaultCheckpointStyleBackgroundColor ?? string.Empty,
                    DefaultBorderColor = eventRequest.DefaultCheckpointStyleBorderColor ?? string.Empty,
                    DefaultIconColor = eventRequest.DefaultCheckpointStyleIconColor ?? string.Empty,
                    DefaultSize = eventRequest.DefaultCheckpointStyleSize ?? string.Empty,
                    DefaultMapRotation = eventRequest.DefaultCheckpointStyleMapRotation ?? string.Empty
                },
                Branding = new BrandingPayload
                {
                    AccentColour = eventRequest.BrandingAccentColor ?? string.Empty,
                    HeaderGradientStart = eventRequest.BrandingHeaderGradientStart ?? string.Empty,
                    HeaderGradientEnd = eventRequest.BrandingHeaderGradientEnd ?? string.Empty,
                    PageGradientStart = eventRequest.BrandingPageGradientStart ?? string.Empty,
                    PageGradientEnd = eventRequest.BrandingPageGradientEnd ?? string.Empty,
                    LogoUrl = eventRequest.BrandingLogoUrl ?? string.Empty,
                    LogoPosition = eventRequest.BrandingLogoPosition ?? string.Empty
                }
            }
        };
        eventEntity.SetPayload(payload);

        await _eventRepository.AddAsync(eventEntity);

        // Grant EventOwner role to the creator
        string roleId = Guid.NewGuid().ToString();
        EventRoleEntity eventRole = new()
        {
            PartitionKey = pendingEvent.PersonId,
            RowKey = EventRoleEntity.CreateRowKey(eventEntity.RowKey, roleId),
            PersonId = pendingEvent.PersonId,
            EventId = eventEntity.RowKey,
            Role = Constants.RoleEventOwner,
            AreaIdsJson = "[]",
            GrantedByPersonId = pendingEvent.PersonId,
            GrantedAt = DateTime.UtcNow
        };
        await _eventRoleRepository.AddAsync(eventRole);

        // Create payment record
        PaymentEntity payment = new()
        {
            PartitionKey = eventEntity.RowKey,
            EventId = eventEntity.RowKey,
            PersonId = pendingEvent.PersonId,
            StripeCheckoutSessionId = session.Id,
            StripePaymentIntentId = session.PaymentIntentId ?? string.Empty,
            PaymentType = Constants.PaymentTypeEventCreation,
            MarshalTier = pendingEvent.MarshalTier,
            AmountPence = pendingEvent.AmountPence,
            Currency = "gbp",
            Status = Constants.PaymentStatusSucceeded,
            CreatedAt = DateTime.UtcNow
        };
        await _paymentRepository.AddAsync(payment);

        _logger.LogInformation("Event {EventId} created via payment, session {SessionId}, owner {PersonId}",
            eventEntity.RowKey, session.Id, pendingEvent.PersonId);

        return eventEntity.RowKey;
    }

    private async Task<string?> HandleUpgradePaymentAsync(Session session, PendingEventEntity pendingEvent)
    {
        string eventId = pendingEvent.UpgradeEventId!;

        EventEntity? eventEntity = await _eventRepository.GetAsync(eventId);
        if (eventEntity == null)
        {
            _logger.LogError("Event {EventId} not found for upgrade session {SessionId}", eventId, session.Id);
            return null;
        }

        // Update the marshal tier
        eventEntity.PaidMarshalTier = pendingEvent.MarshalTier;
        await _eventRepository.UpdateAsync(eventEntity);

        // Create payment record
        PaymentEntity payment = new()
        {
            PartitionKey = eventId,
            EventId = eventId,
            PersonId = pendingEvent.PersonId,
            StripeCheckoutSessionId = session.Id,
            StripePaymentIntentId = session.PaymentIntentId ?? string.Empty,
            PaymentType = Constants.PaymentTypeMarshalUpgrade,
            MarshalTier = pendingEvent.MarshalTier,
            AmountPence = pendingEvent.AmountPence,
            Currency = "gbp",
            Status = Constants.PaymentStatusSucceeded,
            CreatedAt = DateTime.UtcNow
        };
        await _paymentRepository.AddAsync(payment);

        _logger.LogInformation("Event {EventId} upgraded to tier {Tier} via payment, session {SessionId}",
            eventId, pendingEvent.MarshalTier, session.Id);

        return eventId;
    }
}
