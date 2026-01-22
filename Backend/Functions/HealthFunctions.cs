using System;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace VolunteerCheckin.Functions.Functions;

/// <summary>
/// Health check and version endpoints for the API.
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

    /// <summary>
    /// Version endpoint returning the deployment version.
    /// GET /api/version
    /// The version is set via DEPLOYMENT_VERSION environment variable during deployment.
    /// Format: yyyy.MM.dd.HH.mm-gitshorthash (e.g., 2026.01.22.16.41-abc1234)
    /// Returns "local" if not deployed to Azure.
    /// </summary>
    [Function("Version")]
#pragma warning disable CA1822, RCS1163, MA0038 // Azure Functions convention uses instance methods; parameter required for HTTP trigger binding
    public IActionResult Version(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "version")] HttpRequest req)
#pragma warning restore CA1822, RCS1163, MA0038
    {
        string version = Environment.GetEnvironmentVariable("DEPLOYMENT_VERSION") ?? "local";
        return new OkObjectResult(new { version });
    }
}
