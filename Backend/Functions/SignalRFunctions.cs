using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.SignalRService;
using Microsoft.Extensions.Logging;

namespace VolunteerCheckin.Functions.Functions;

public class SignalRFunctions
{
    private readonly ILogger<SignalRFunctions> _logger;

    public SignalRFunctions(ILogger<SignalRFunctions> logger)
    {
        _logger = logger;
    }

    [Function("negotiate")]
    public SignalRConnectionInfo Negotiate(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "negotiate")] HttpRequest req,
        [SignalRConnectionInfoInput(HubName = "checkinHub")] SignalRConnectionInfo connectionInfo)
    {
        _logger.LogInformation("SignalR connection negotiation");
        return connectionInfo;
    }
}
