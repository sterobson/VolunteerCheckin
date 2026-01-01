using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using VolunteerCheckin.Functions.Repositories;

namespace VolunteerCheckin.Functions.Functions;

/// <summary>
/// Timer-triggered functions for scheduled maintenance tasks.
/// </summary>
public class MaintenanceFunctions
{
    private readonly ILogger<MaintenanceFunctions> _logger;
    private readonly IAuthTokenRepository _authTokenRepository;
    private readonly IAuthSessionRepository _authSessionRepository;

    public MaintenanceFunctions(
        ILogger<MaintenanceFunctions> logger,
        IAuthTokenRepository authTokenRepository,
        IAuthSessionRepository authSessionRepository)
    {
        _logger = logger;
        _authTokenRepository = authTokenRepository;
        _authSessionRepository = authSessionRepository;
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
}
