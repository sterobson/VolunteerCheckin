namespace VolunteerCheckin.Functions.Services;

/// <summary>
/// Interface for event-level operations that span multiple repositories.
/// </summary>
public interface IEventService
{
    /// <summary>
    /// Delete an event and all related data.
    /// </summary>
    Task DeleteEventWithAllDataAsync(string eventId);

    /// <summary>
    /// Immediately delete the Event and SampleEventAdmin records.
    /// Used when a deletion is requested to prevent further access.
    /// The remaining data will be cleaned up by a background job.
    /// </summary>
    Task DeleteEventRecordImmediateAsync(string eventId);
}
