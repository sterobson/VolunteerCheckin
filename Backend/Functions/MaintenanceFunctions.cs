using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using VolunteerCheckin.Functions.Models;
using VolunteerCheckin.Functions.Repositories;
using VolunteerCheckin.Functions.Services;

namespace VolunteerCheckin.Functions.Functions;

/// <summary>
/// Timer-triggered functions for scheduled maintenance tasks.
/// </summary>
public class MaintenanceFunctions
{
    private readonly ILogger<MaintenanceFunctions> _logger;
    private readonly IAuthTokenRepository _authTokenRepository;
    private readonly IAuthSessionRepository _authSessionRepository;
    private readonly SampleEventService _sampleEventService;
    private readonly IEventDeletionRepository _eventDeletionRepository;
    private readonly IEventService _eventService;

    public MaintenanceFunctions(
        ILogger<MaintenanceFunctions> logger,
        IAuthTokenRepository authTokenRepository,
        IAuthSessionRepository authSessionRepository,
        SampleEventService sampleEventService,
        IEventDeletionRepository eventDeletionRepository,
        IEventService eventService)
    {
        _logger = logger;
        _authTokenRepository = authTokenRepository;
        _authSessionRepository = authSessionRepository;
        _sampleEventService = sampleEventService;
        _eventDeletionRepository = eventDeletionRepository;
        _eventService = eventService;
    }

    /// <summary>
    /// Clean up expired authentication tokens and sessions.
    /// Runs every hour at minute 0.
    /// </summary>
    [Function("CleanupExpiredTokens")]
    public async Task CleanupExpiredTokens(
        [TimerTrigger("0 0 * * * *")] Microsoft.Azure.Functions.Worker.TimerInfo timerInfo)
    {
        _logger.LogInformation("Starting cleanup of expired authentication tokens and sessions");

        try
        {
            // Clean up expired magic link tokens
            await _authTokenRepository.DeleteExpiredTokensAsync();
            _logger.LogInformation("Expired auth tokens cleaned up");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up expired auth tokens");
        }

        try
        {
            // Clean up expired sessions
            await _authSessionRepository.DeleteExpiredSessionsAsync();
            _logger.LogInformation("Expired auth sessions cleaned up");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up expired auth sessions");
        }

        _logger.LogInformation("Cleanup of expired authentication tokens and sessions completed");
    }

    /// <summary>
    /// Unified cleanup function that:
    /// 1. Marks expired sample events for deletion
    /// 2. Processes all pending event deletions
    /// Runs every minute.
    /// </summary>
    [Function("ProcessEventCleanup")]
    public async Task ProcessEventCleanup(
        [TimerTrigger("0 * * * * *")] Microsoft.Azure.Functions.Worker.TimerInfo timerInfo)
    {
        _logger.LogInformation("Starting event cleanup process");

        // Step 1: Mark expired sample events for deletion
        await MarkExpiredSampleEventsForDeletionAsync();

        // Step 2: Process all pending deletions
        await ProcessPendingDeletionsAsync();

        _logger.LogInformation("Event cleanup process completed");
    }

    /// <summary>
    /// Find expired sample events and create deletion records for them.
    /// </summary>
    private async Task MarkExpiredSampleEventsForDeletionAsync()
    {
        try
        {
            // Get expired sample events from EventEntity table
            List<EventEntity> expiredEvents = await _sampleEventService.GetExpiredSampleEventsAsync();

            // Also get expired entries from SampleEventAdmin table (catches orphaned entries)
            List<SampleEventAdminEntity> expiredAdminEntries = await _sampleEventService.GetExpiredSampleEventAdminEntriesAsync();

            // Combine event IDs from both sources (de-duplicated)
            HashSet<string> expiredEventIds = new(expiredEvents.Select(e => e.RowKey));
            foreach (SampleEventAdminEntity adminEntry in expiredAdminEntries)
            {
                expiredEventIds.Add(adminEntry.EventId);
            }

            if (expiredEventIds.Count == 0)
            {
                return;
            }

            _logger.LogInformation("Found {Count} expired sample events to mark for deletion", expiredEventIds.Count);

            foreach (string eventId in expiredEventIds)
            {
                try
                {
                    // Check if a deletion record already exists
                    EventDeletionEntity? existingDeletion = await _eventDeletionRepository.GetByEventIdAsync(eventId);
                    if (existingDeletion != null)
                    {
                        continue; // Already has a deletion record
                    }

                    // Get event name if available
                    EventEntity? eventEntity = expiredEvents.FirstOrDefault(e => e.RowKey == eventId);
                    string eventName = eventEntity?.Name ?? "Expired Sample Event";

                    // Create deletion record
                    EventDeletionEntity deletion = new()
                    {
                        EventId = eventId,
                        EventName = eventName,
                        RequestedByEmail = "system@cleanup",
                        RequestedByName = "System Cleanup",
                        RequestedAt = DateTime.UtcNow,
                        Status = EventDeletionStatus.Pending
                    };

                    await _eventDeletionRepository.AddAsync(deletion);
                    _logger.LogInformation("Created deletion record for expired sample event {EventId}", eventId);

                    // Immediately delete the Event and SampleEventAdmin records to prevent access
                    await _eventService.DeleteEventRecordImmediateAsync(eventId);
                    _logger.LogInformation("Deleted Event and SampleEventAdmin records for {EventId}", eventId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error marking expired sample event {EventId} for deletion", eventId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding expired sample events");
        }
    }

    /// <summary>
    /// Process all pending event deletion records.
    /// </summary>
    private async Task ProcessPendingDeletionsAsync()
    {
        try
        {
            IEnumerable<EventDeletionEntity> pendingDeletions = await _eventDeletionRepository.GetPendingAsync();
            List<EventDeletionEntity> deletionsList = pendingDeletions.ToList();

            if (deletionsList.Count == 0)
            {
                return;
            }

            _logger.LogInformation("Found {Count} pending event deletions to process", deletionsList.Count);

            foreach (EventDeletionEntity deletion in deletionsList)
            {
                try
                {
                    // Mark as in progress
                    deletion.Status = EventDeletionStatus.InProgress;
                    await _eventDeletionRepository.UpdateAsync(deletion);

                    _logger.LogInformation(
                        "Processing deletion of event {EventId} ({EventName}) requested by {Email}",
                        deletion.EventId, deletion.EventName, deletion.RequestedByEmail);

                    // Delete all event data (Event record may already be deleted)
                    await _eventService.DeleteEventWithAllDataAsync(deletion.EventId);

                    // Mark as completed
                    deletion.Status = EventDeletionStatus.Completed;
                    deletion.CompletedAt = DateTime.UtcNow;
                    await _eventDeletionRepository.UpdateAsync(deletion);

                    _logger.LogInformation(
                        "Successfully deleted event {EventId} ({EventName})",
                        deletion.EventId, deletion.EventName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Error deleting event {EventId} ({EventName})",
                        deletion.EventId, deletion.EventName);

                    // Mark as failed
                    deletion.Status = EventDeletionStatus.Failed;
                    deletion.ErrorMessage = ex.Message;
                    try
                    {
                        await _eventDeletionRepository.UpdateAsync(deletion);
                    }
                    catch (Exception updateEx)
                    {
                        _logger.LogError(updateEx, "Failed to update deletion status to failed");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during event deletion processing");
        }
    }
}
