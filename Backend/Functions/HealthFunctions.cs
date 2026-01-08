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
#pragma warning disable CA1822, RCS1163, MA0038 // Azure Functions convention uses instance methods; parameter required for HTTP trigger binding
    public IActionResult Health(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "head", Route = "health")] HttpRequest req)
#pragma warning restore CA1822, RCS1163, MA0038
    {
        return new OkObjectResult(new { status = "healthy" });
    }
}
