using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace VolunteerCheckin.Functions.Functions;

/// <summary>
/// Health check endpoint for the API.
/// </summary>
public class HealthFunctions
{
    /// <summary>
    /// Simple health check endpoint.
    /// GET /api/health or HEAD /api/health
    /// </summary>
    [Function("Health")]
    public IActionResult Health(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "head", Route = "health")] HttpRequest req)
    {
        return new OkObjectResult(new { status = "healthy" });
    }
}
