using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using VolunteerCheckin.Functions.Models;
using VolunteerCheckin.Functions.Repositories;
using VolunteerCheckin.Functions.Services;

namespace VolunteerCheckin.Functions.Functions;

/// <summary>
/// Azure Functions for incident reporting.
/// Marshals can report incidents; admins and area leads can manage them.
/// </summary>
public class IncidentFunctions
{
    private readonly ILogger<IncidentFunctions> _logger;
    private readonly IIncidentRepository _incidentRepository;
    private readonly ILocationRepository _locationRepository;
    private readonly IMarshalRepository _marshalRepository;
    private readonly IAssignmentRepository _assignmentRepository;
    private readonly IAreaRepository _areaRepository;
    private readonly ClaimsService _claimsService;

    private static readonly string[] ValidSeverities = ["low", "medium", "high", "critical"];
    private static readonly string[] ValidStatuses = ["open", "acknowledged", "in_progress", "resolved", "closed"];

    public IncidentFunctions(
        ILogger<IncidentFunctions> logger,
        IIncidentRepository incidentRepository,
        ILocationRepository locationRepository,
        IMarshalRepository marshalRepository,
        IAssignmentRepository assignmentRepository,
        IAreaRepository areaRepository,
        ClaimsService claimsService)
    {
        _logger = logger;
        _incidentRepository = incidentRepository;
        _locationRepository = locationRepository;
        _marshalRepository = marshalRepository;
        _assignmentRepository = assignmentRepository;
        _areaRepository = areaRepository;
        _claimsService = claimsService;
    }

    /// <summary>
    /// Create a new incident report.
    /// Any authenticated marshal can create an incident.
    /// </summary>
    [Function("CreateIncident")]
    public async Task<IActionResult> CreateIncident(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "events/{eventId}/incidents")] HttpRequest req,
        string eventId)
    {
        try
        {
            // Authenticate
            UserClaims? claims = await GetClaimsAsync(req, eventId);
            if (claims == null)
            {
                return new UnauthorizedObjectResult(new { message = Constants.ErrorNotAuthorized });
            }

            // Parse request
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            CreateIncidentRequest? request = JsonSerializer.Deserialize<CreateIncidentRequest>(requestBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (request == null || string.IsNullOrWhiteSpace(request.Description))
            {
                return new BadRequestObjectResult(new { message = "Description is required" });
            }

            // Validate severity
            string severity = request.Severity?.ToLowerInvariant() ?? "medium";
            if (!ValidSeverities.Contains(severity))
            {
                return new BadRequestObjectResult(new { message = $"Invalid severity. Valid values: {string.Join(", ", ValidSeverities)}" });
            }

            // Build context snapshot
            IncidentContextSnapshot contextSnapshot = await BuildContextSnapshotAsync(eventId, claims.MarshalId, request.CheckpointId, request.SkipCheckpointAutoAssign);

            // Determine area from checkpoint or location
            string areaId = string.Empty;
            string areaName = string.Empty;

            if (contextSnapshot.Checkpoint != null && contextSnapshot.Checkpoint.AreaIds.Count > 0)
            {
                areaId = contextSnapshot.Checkpoint.AreaIds[0];
                areaName = contextSnapshot.Checkpoint.AreaNames.Count > 0 ? contextSnapshot.Checkpoint.AreaNames[0] : string.Empty;
            }
            else if (request.Latitude.HasValue && request.Longitude.HasValue)
            {
                // Try to determine area from GPS location
                (areaId, areaName) = await DetermineAreaFromLocationAsync(eventId, request.Latitude.Value, request.Longitude.Value);
            }

            string incidentId = Guid.NewGuid().ToString();
            DateTime now = DateTime.UtcNow;

            IncidentEntity incident = new IncidentEntity
            {
                PartitionKey = eventId,
                RowKey = incidentId,
                EventId = eventId,
                IncidentId = incidentId,
                Title = request.Title ?? string.Empty,
                Description = request.Description,
                Severity = severity,
                IncidentTime = request.IncidentTime ?? now,
                CreatedAt = now,
                Latitude = request.Latitude,
                Longitude = request.Longitude,
                ContextSnapshotJson = JsonSerializer.Serialize(contextSnapshot),
                ReportedByPersonId = claims.PersonId,
                ReportedByName = claims.PersonName,
                ReportedByMarshalId = claims.MarshalId ?? string.Empty,
                Status = "open",
                UpdatesJson = "[]",
                AreaId = areaId,
                AreaName = areaName
            };

            await _incidentRepository.AddAsync(incident);

            _logger.LogInformation("Incident {IncidentId} created for event {EventId} by {PersonId}", incidentId, eventId, claims.PersonId);

            return new ObjectResult(ToIncidentResponse(incident)) { StatusCode = 201 };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating incident for event {EventId}", eventId);
            return new StatusCodeResult(500);
        }
    }

    /// <summary>
    /// Get all incidents for an event.
    /// - Admins see all incidents
    /// - Area leads see incidents in their areas (by checkpoint area, reporter area, or GPS location)
    /// - Marshals see only their own reported incidents
    /// </summary>
    [Function("GetIncidents")]
    public async Task<IActionResult> GetIncidents(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "events/{eventId}/incidents")] HttpRequest req,
        string eventId)
    {
        try
        {
            // Authenticate
            UserClaims? claims = await GetClaimsAsync(req, eventId);
            if (claims == null)
            {
                return new UnauthorizedObjectResult(new { message = Constants.ErrorNotAuthorized });
            }

            IEnumerable<IncidentEntity> incidents = await _incidentRepository.GetByEventAsync(eventId);

            // Filter based on user's role
            if (claims.IsEventAdmin)
            {
                // Admins see everything
            }
            else
            {
                // Get area IDs where user is an area lead
                List<string> leadAreaIds = claims.EventRoles
                    .Where(r => r.Role == Constants.RoleEventAreaLead)
                    .SelectMany(r => r.AreaIds)
                    .ToList();

                // Filter incidents based on marshal/lead rules
                incidents = await FilterIncidentsForUserAsync(eventId, incidents, claims.MarshalId, leadAreaIds, claims.PersonId);
            }

            // Apply filters
            string? statusFilter = req.Query["status"];
            string? severityFilter = req.Query["severity"];

            if (!string.IsNullOrEmpty(statusFilter))
            {
                incidents = incidents.Where(i => i.Status == statusFilter.ToLowerInvariant());
            }

            if (!string.IsNullOrEmpty(severityFilter))
            {
                incidents = incidents.Where(i => i.Severity == severityFilter.ToLowerInvariant());
            }

            List<IncidentResponse> responses = incidents.Select(ToIncidentResponse).ToList();

            return new OkObjectResult(new IncidentsListResponse(responses));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting incidents for event {EventId}", eventId);
            return new StatusCodeResult(500);
        }
    }

    /// <summary>
    /// Get incidents for a specific area (area lead view).
    /// Area leads can only see incidents in their areas.
    /// </summary>
    [Function("GetIncidentsForArea")]
    public async Task<IActionResult> GetIncidentsForArea(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "events/{eventId}/areas/{areaId}/incidents")] HttpRequest req,
        string eventId,
        string areaId)
    {
        try
        {
            // Authenticate
            UserClaims? claims = await GetClaimsAsync(req, eventId);
            if (claims == null)
            {
                return new UnauthorizedObjectResult(new { message = Constants.ErrorNotAuthorized });
            }

            // Check if user is admin or area lead for this area
            bool isAdmin = claims.IsEventAdmin;
            bool isAreaLead = claims.IsAreaLead(areaId);

            if (!isAdmin && !isAreaLead)
            {
                return new UnauthorizedObjectResult(new { message = "You are not authorized to view incidents for this area" });
            }

            IEnumerable<IncidentEntity> incidents = await _incidentRepository.GetByAreaAsync(eventId, areaId);

            List<IncidentResponse> responses = incidents.Select(ToIncidentResponse).ToList();

            return new OkObjectResult(new IncidentsListResponse(responses));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting incidents for area {AreaId} in event {EventId}", areaId, eventId);
            return new StatusCodeResult(500);
        }
    }

    /// <summary>
    /// Get a specific incident by ID.
    /// </summary>
    [Function("GetIncident")]
    public async Task<IActionResult> GetIncident(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "events/{eventId}/incidents/{incidentId}")] HttpRequest req,
        string eventId,
        string incidentId)
    {
        try
        {
            // Authenticate
            UserClaims? claims = await GetClaimsAsync(req, eventId);
            if (claims == null)
            {
                return new UnauthorizedObjectResult(new { message = Constants.ErrorNotAuthorized });
            }

            IncidentEntity? incident = await _incidentRepository.GetAsync(eventId, incidentId);
            if (incident == null)
            {
                return new NotFoundObjectResult(new { message = "Incident not found" });
            }

            // Check authorization - admin, area lead for the incident's area, or the reporter
            bool isAdmin = claims.IsEventAdmin;
            bool isAreaLead = !string.IsNullOrEmpty(incident.AreaId) && claims.IsAreaLead(incident.AreaId);
            bool isReporter = claims.PersonId == incident.ReportedByPersonId;

            if (!isAdmin && !isAreaLead && !isReporter)
            {
                return new UnauthorizedObjectResult(new { message = Constants.ErrorNotAuthorized });
            }

            return new OkObjectResult(ToIncidentResponse(incident));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting incident {IncidentId} for event {EventId}", incidentId, eventId);
            return new StatusCodeResult(500);
        }
    }

    /// <summary>
    /// Update an incident's status.
    /// Only event admins and area leads can update status.
    /// </summary>
    [Function("UpdateIncidentStatus")]
    public async Task<IActionResult> UpdateIncidentStatus(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "events/{eventId}/incidents/{incidentId}")] HttpRequest req,
        string eventId,
        string incidentId)
    {
        try
        {
            // Authenticate
            UserClaims? claims = await GetClaimsAsync(req, eventId);
            if (claims == null)
            {
                return new UnauthorizedObjectResult(new { message = Constants.ErrorNotAuthorized });
            }

            IncidentEntity? incident = await _incidentRepository.GetAsync(eventId, incidentId);
            if (incident == null)
            {
                return new NotFoundObjectResult(new { message = "Incident not found" });
            }

            // Check authorization
            bool isAdmin = claims.IsEventAdmin;
            bool isAreaLead = !string.IsNullOrEmpty(incident.AreaId) && claims.IsAreaLead(incident.AreaId);

            if (!isAdmin && !isAreaLead)
            {
                return new UnauthorizedObjectResult(new { message = "You are not authorized to update this incident" });
            }

            // Parse request
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            UpdateIncidentStatusRequest? request = JsonSerializer.Deserialize<UpdateIncidentStatusRequest>(requestBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (request == null || string.IsNullOrWhiteSpace(request.Status))
            {
                return new BadRequestObjectResult(new { message = "Status is required" });
            }

            string status = request.Status.ToLowerInvariant();
            if (!ValidStatuses.Contains(status))
            {
                return new BadRequestObjectResult(new { message = $"Invalid status. Valid values: {string.Join(", ", ValidStatuses)}" });
            }

            // Create update entry
            List<IncidentUpdate> updates = JsonSerializer.Deserialize<List<IncidentUpdate>>(incident.UpdatesJson) ?? [];

            IncidentUpdate update = new IncidentUpdate
            {
                UpdateId = Guid.NewGuid().ToString(),
                Timestamp = DateTime.UtcNow,
                AuthorPersonId = claims.PersonId,
                AuthorName = claims.PersonName,
                Note = request.Note ?? $"Status changed to {status}",
                StatusChange = status
            };

            updates.Add(update);

            // Update incident
            incident.Status = status;
            incident.UpdatesJson = JsonSerializer.Serialize(updates);

            await _incidentRepository.UpdateAsync(incident);

            _logger.LogInformation("Incident {IncidentId} status updated to {Status} by {PersonId}", incidentId, status, claims.PersonId);

            return new OkObjectResult(ToIncidentResponse(incident));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating incident {IncidentId} for event {EventId}", incidentId, eventId);
            return new StatusCodeResult(500);
        }
    }

    /// <summary>
    /// Add a note to an incident.
    /// Any authenticated user who can see the incident can add notes.
    /// </summary>
    [Function("AddIncidentNote")]
    public async Task<IActionResult> AddIncidentNote(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "events/{eventId}/incidents/{incidentId}/notes")] HttpRequest req,
        string eventId,
        string incidentId)
    {
        try
        {
            // Authenticate - any authenticated user can add notes to incidents they can see
            // (visibility is already restricted by the GetIncidents endpoint)
            UserClaims? claims = await GetClaimsAsync(req, eventId);
            if (claims == null)
            {
                return new UnauthorizedObjectResult(new { message = Constants.ErrorNotAuthorized });
            }

            IncidentEntity? incident = await _incidentRepository.GetAsync(eventId, incidentId);
            if (incident == null)
            {
                return new NotFoundObjectResult(new { message = "Incident not found" });
            }

            // Parse request
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            AddIncidentNoteRequest? request = JsonSerializer.Deserialize<AddIncidentNoteRequest>(requestBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (request == null || string.IsNullOrWhiteSpace(request.Note))
            {
                return new BadRequestObjectResult(new { message = "Note is required" });
            }

            // Create update entry
            List<IncidentUpdate> updates = JsonSerializer.Deserialize<List<IncidentUpdate>>(incident.UpdatesJson) ?? [];

            IncidentUpdate update = new IncidentUpdate
            {
                UpdateId = Guid.NewGuid().ToString(),
                Timestamp = DateTime.UtcNow,
                AuthorPersonId = claims.PersonId,
                AuthorName = claims.PersonName,
                Note = request.Note,
                StatusChange = null
            };

            updates.Add(update);

            incident.UpdatesJson = JsonSerializer.Serialize(updates);

            await _incidentRepository.UpdateAsync(incident);

            _logger.LogInformation("Note added to incident {IncidentId} by {PersonId}", incidentId, claims.PersonId);

            return new OkObjectResult(ToIncidentResponse(incident));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding note to incident {IncidentId} for event {EventId}", incidentId, eventId);
            return new StatusCodeResult(500);
        }
    }

    #region Helper Methods

    private async Task<UserClaims?> GetClaimsAsync(HttpRequest req, string eventId)
    {
        string? sessionToken = req.Headers["Authorization"].FirstOrDefault()?.Replace("Bearer ", "");
        if (string.IsNullOrWhiteSpace(sessionToken))
        {
            sessionToken = req.Cookies["session_token"];
        }

        if (string.IsNullOrWhiteSpace(sessionToken))
        {
            return null;
        }

        return await _claimsService.GetClaimsAsync(sessionToken, eventId);
    }

    /// <summary>
    /// Filter incidents based on user's role:
    ///
    /// For marshals:
    /// - Incidents I reported
    /// - Incidents reported by anyone at my checkpoints
    /// - Incidents where any of my checkpoints are tagged
    ///
    /// For area leads (same as marshals, plus):
    /// - Incidents at checkpoints in my area
    /// - Incidents reported by people at checkpoints in my area
    /// </summary>
    private async Task<IEnumerable<IncidentEntity>> FilterIncidentsForUserAsync(
        string eventId,
        IEnumerable<IncidentEntity> incidents,
        string? marshalId,
        List<string> leadAreaIds,
        string personId)
    {
        // Get my checkpoint assignments
        List<string> myCheckpointIds = [];
        HashSet<string> marshalsAtMyCheckpoints = [];

        if (!string.IsNullOrEmpty(marshalId))
        {
            IEnumerable<AssignmentEntity> myAssignments = await _assignmentRepository.GetByMarshalAsync(eventId, marshalId);
            myCheckpointIds = myAssignments.Select(a => a.LocationId).ToList();

            // Get all marshals assigned to my checkpoints
            foreach (string checkpointId in myCheckpointIds)
            {
                IEnumerable<AssignmentEntity> checkpointAssignments = await _assignmentRepository.GetByLocationAsync(eventId, checkpointId);
                foreach (AssignmentEntity assignment in checkpointAssignments)
                {
                    marshalsAtMyCheckpoints.Add(assignment.MarshalId);
                }
            }
        }

        // For area leads: get additional context
        Dictionary<string, List<string>> marshalCheckpointLookup = [];
        HashSet<string> checkpointsInMyAreas = [];
        Dictionary<string, AreaEntity> areaLookup = [];

        if (leadAreaIds.Count > 0)
        {
            // Get all checkpoints to check which areas they belong to
            IEnumerable<LocationEntity> checkpoints = await _locationRepository.GetByEventAsync(eventId);

            // Find all checkpoints in my areas
            foreach (LocationEntity checkpoint in checkpoints)
            {
                List<string> areaIds = JsonSerializer.Deserialize<List<string>>(checkpoint.AreaIdsJson) ?? [];
                if (areaIds.Any(areaId => leadAreaIds.Contains(areaId)))
                {
                    checkpointsInMyAreas.Add(checkpoint.RowKey);
                }
            }

            // Get all assignments to find marshals at checkpoints in my areas
            IEnumerable<AssignmentEntity> allAssignments = await _assignmentRepository.GetByEventAsync(eventId);
            marshalCheckpointLookup = allAssignments
                .GroupBy(a => a.MarshalId)
                .ToDictionary(g => g.Key, g => g.Select(a => a.LocationId).ToList());

            // Get areas for GPS polygon checking
            IEnumerable<AreaEntity> areas = await _areaRepository.GetByEventAsync(eventId);
            areaLookup = areas.ToDictionary(a => a.RowKey);
        }

        List<IncidentEntity> filteredIncidents = [];

        foreach (IncidentEntity incident in incidents)
        {
            // === MARSHAL RULES ===

            // Rule 1: Incidents I reported
            if (incident.ReportedByPersonId == personId)
            {
                filteredIncidents.Add(incident);
                continue;
            }

            // Rule 2: Incidents reported by anyone at my checkpoints
            if (!string.IsNullOrEmpty(incident.ReportedByMarshalId) &&
                marshalsAtMyCheckpoints.Contains(incident.ReportedByMarshalId))
            {
                filteredIncidents.Add(incident);
                continue;
            }

            // Rule 3: Incidents where any of my checkpoints are tagged
            IncidentContextSnapshot? context = JsonSerializer.Deserialize<IncidentContextSnapshot>(incident.ContextSnapshotJson);
            if (context?.Checkpoint != null && myCheckpointIds.Contains(context.Checkpoint.CheckpointId))
            {
                filteredIncidents.Add(incident);
                continue;
            }

            // === AREA LEAD RULES (only if user is an area lead) ===
            if (leadAreaIds.Count > 0)
            {
                // Rule 4: Incidents at checkpoints in my area
                if (context?.Checkpoint != null && checkpointsInMyAreas.Contains(context.Checkpoint.CheckpointId))
                {
                    filteredIncidents.Add(incident);
                    continue;
                }

                // Rule 5: Incidents reported by people at checkpoints in my area
                if (!string.IsNullOrEmpty(incident.ReportedByMarshalId) &&
                    marshalCheckpointLookup.TryGetValue(incident.ReportedByMarshalId, out List<string>? reporterCheckpoints) &&
                    reporterCheckpoints.Any(cpId => checkpointsInMyAreas.Contains(cpId)))
                {
                    filteredIncidents.Add(incident);
                    continue;
                }

                // Rule 6: Incidents geographically inside my area (GPS location within polygon)
                if (incident.Latitude.HasValue && incident.Longitude.HasValue)
                {
                    bool inMyArea = false;
                    foreach (string leadAreaId in leadAreaIds)
                    {
                        if (areaLookup.TryGetValue(leadAreaId, out AreaEntity? area))
                        {
                            List<RoutePoint> polygon = JsonSerializer.Deserialize<List<RoutePoint>>(area.PolygonJson) ?? [];
                            if (polygon.Count >= 3 && IsPointInPolygon(incident.Latitude.Value, incident.Longitude.Value, polygon))
                            {
                                inMyArea = true;
                                break;
                            }
                        }
                    }
                    if (inMyArea)
                    {
                        filteredIncidents.Add(incident);
                        continue;
                    }
                }
            }
        }

        return filteredIncidents;
    }

    private async Task<IncidentContextSnapshot> BuildContextSnapshotAsync(string eventId, string? marshalId, string? checkpointId, bool skipCheckpointAutoAssign = false)
    {
        IncidentContextSnapshot snapshot = new IncidentContextSnapshot();

        // If no checkpoint specified but marshal has assignments, use their first assignment
        // BUT only if user didn't explicitly choose "no checkpoint"
        if (string.IsNullOrEmpty(checkpointId) && !string.IsNullOrEmpty(marshalId) && !skipCheckpointAutoAssign)
        {
            IEnumerable<AssignmentEntity> assignments = await _assignmentRepository.GetByMarshalAsync(eventId, marshalId);
            AssignmentEntity? firstAssignment = assignments.FirstOrDefault();
            if (firstAssignment != null)
            {
                checkpointId = firstAssignment.LocationId;
            }
        }

        if (!string.IsNullOrEmpty(checkpointId))
        {
            // Get checkpoint details
            LocationEntity? checkpoint = await _locationRepository.GetAsync(eventId, checkpointId);
            if (checkpoint != null)
            {
                // Get area names
                List<string> areaIds = JsonSerializer.Deserialize<List<string>>(checkpoint.AreaIdsJson) ?? [];
                List<string> areaNames = [];

                if (areaIds.Count > 0)
                {
                    IEnumerable<AreaEntity> areas = await _areaRepository.GetByEventAsync(eventId);
                    Dictionary<string, AreaEntity> areaLookup = areas.ToDictionary(a => a.RowKey);

                    foreach (string areaId in areaIds)
                    {
                        if (areaLookup.TryGetValue(areaId, out AreaEntity? area))
                        {
                            areaNames.Add(area.Name);
                        }
                    }
                }

                snapshot.Checkpoint = new IncidentCheckpointSnapshot
                {
                    CheckpointId = checkpoint.RowKey,
                    Name = checkpoint.Name,
                    Description = checkpoint.Description,
                    Latitude = checkpoint.Latitude,
                    Longitude = checkpoint.Longitude,
                    AreaIds = areaIds,
                    AreaNames = areaNames
                };

                // Get all marshals assigned to this checkpoint
                IEnumerable<AssignmentEntity> checkpointAssignments = await _assignmentRepository.GetByLocationAsync(eventId, checkpointId);
                IEnumerable<MarshalEntity> allMarshals = await _marshalRepository.GetByEventAsync(eventId);
                Dictionary<string, MarshalEntity> marshalLookup = allMarshals.ToDictionary(m => m.RowKey);

                foreach (AssignmentEntity assignment in checkpointAssignments)
                {
                    if (marshalLookup.TryGetValue(assignment.MarshalId, out MarshalEntity? marshal))
                    {
                        snapshot.MarshalsPresentAtCheckpoint.Add(new IncidentMarshalSnapshot
                        {
                            MarshalId = marshal.RowKey,
                            Name = marshal.Name,
                            WasCheckedIn = assignment.IsCheckedIn,
                            CheckInTime = assignment.CheckInTime,
                            CheckInMethod = assignment.CheckInMethod
                        });
                    }
                }
            }
        }

        return snapshot;
    }

    private async Task<(string areaId, string areaName)> DetermineAreaFromLocationAsync(string eventId, double latitude, double longitude)
    {
        // Get all areas with polygons
        IEnumerable<AreaEntity> areas = await _areaRepository.GetByEventAsync(eventId);

        foreach (AreaEntity area in areas)
        {
            List<RoutePoint> polygon = JsonSerializer.Deserialize<List<RoutePoint>>(area.PolygonJson) ?? [];
            if (polygon.Count >= 3 && IsPointInPolygon(latitude, longitude, polygon))
            {
                return (area.RowKey, area.Name);
            }
        }

        return (string.Empty, string.Empty);
    }

    private static bool IsPointInPolygon(double lat, double lng, List<RoutePoint> polygon)
    {
        bool inside = false;
        int j = polygon.Count - 1;

        for (int i = 0; i < polygon.Count; i++)
        {
            if (((polygon[i].Lng < lng && polygon[j].Lng >= lng) ||
                 (polygon[j].Lng < lng && polygon[i].Lng >= lng)) &&
                polygon[i].Lat + (lng - polygon[i].Lng) / (polygon[j].Lng - polygon[i].Lng) * (polygon[j].Lat - polygon[i].Lat) < lat)
            {
                inside = !inside;
            }
            j = i;
        }

        return inside;
    }

    private static IncidentResponse ToIncidentResponse(IncidentEntity incident)
    {
        IncidentContextSnapshot context = JsonSerializer.Deserialize<IncidentContextSnapshot>(incident.ContextSnapshotJson)
            ?? new IncidentContextSnapshot();

        List<IncidentUpdate> updates = JsonSerializer.Deserialize<List<IncidentUpdate>>(incident.UpdatesJson) ?? [];

        IncidentCheckpointInfo? checkpointInfo = null;
        if (context.Checkpoint != null)
        {
            checkpointInfo = new IncidentCheckpointInfo(
                CheckpointId: context.Checkpoint.CheckpointId,
                Name: context.Checkpoint.Name,
                Description: context.Checkpoint.Description,
                Latitude: context.Checkpoint.Latitude,
                Longitude: context.Checkpoint.Longitude,
                AreaIds: context.Checkpoint.AreaIds,
                AreaNames: context.Checkpoint.AreaNames
            );
        }

        List<IncidentMarshalInfo> marshalInfos = context.MarshalsPresentAtCheckpoint
            .Select(m => new IncidentMarshalInfo(
                MarshalId: m.MarshalId,
                Name: m.Name,
                WasCheckedIn: m.WasCheckedIn,
                CheckInTime: m.CheckInTime,
                CheckInMethod: m.CheckInMethod
            ))
            .ToList();

        List<IncidentUpdateInfo> updateInfos = updates
            .Select(u => new IncidentUpdateInfo(
                UpdateId: u.UpdateId,
                Timestamp: u.Timestamp,
                AuthorPersonId: u.AuthorPersonId,
                AuthorName: u.AuthorName,
                Note: u.Note,
                StatusChange: u.StatusChange
            ))
            .ToList();

        IncidentAreaInfo? areaInfo = null;
        if (!string.IsNullOrEmpty(incident.AreaId))
        {
            areaInfo = new IncidentAreaInfo(
                AreaId: incident.AreaId,
                AreaName: incident.AreaName
            );
        }

        return new IncidentResponse(
            IncidentId: incident.IncidentId,
            EventId: incident.EventId,
            Title: incident.Title,
            Description: incident.Description,
            Severity: incident.Severity,
            IncidentTime: incident.IncidentTime,
            CreatedAt: incident.CreatedAt,
            Latitude: incident.Latitude,
            Longitude: incident.Longitude,
            Status: incident.Status,
            ReportedBy: new IncidentReporterInfo(
                PersonId: incident.ReportedByPersonId,
                Name: incident.ReportedByName,
                MarshalId: string.IsNullOrEmpty(incident.ReportedByMarshalId) ? null : incident.ReportedByMarshalId
            ),
            Area: areaInfo,
            Context: new IncidentContextInfo(
                Checkpoint: checkpointInfo,
                MarshalsPresentAtCheckpoint: marshalInfos
            ),
            Updates: updateInfos
        );
    }

    #endregion
}
