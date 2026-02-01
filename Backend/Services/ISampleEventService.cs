namespace VolunteerCheckin.Functions.Services;

/// <summary>
/// Interface for sample event service, used by ClaimsService for validation.
/// </summary>
public interface ISampleEventService
{
    /// <summary>
    /// Look up a sample event by its admin code.
    /// Returns the event ID if found and not expired, otherwise null.
    /// </summary>
    Task<string?> GetEventIdByAdminCodeAsync(string adminCode);

    /// <summary>
    /// Recover a sample event by device fingerprint.
    /// Returns the event details if found and not expired, null otherwise.
    /// </summary>
    Task<SampleEventRecoveryResult?> GetSampleEventByDeviceFingerprintAsync(string deviceFingerprint);
}
