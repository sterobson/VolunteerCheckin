using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using VolunteerCheckin.Functions.Helpers;
using VolunteerCheckin.Functions.Models;
using VolunteerCheckin.Functions.Repositories;
using VolunteerCheckin.Functions.Services;

namespace VolunteerCheckin.Functions.Functions;

/// <summary>
/// Azure Functions for managing notes.
/// Notes are informational items displayed to marshals based on scope configurations.
/// Admins and area leads can create/edit notes; marshals can only view them.
/// </summary>
public class NoteFunctions
{
    private readonly ILogger<NoteFunctions> _logger;
    private readonly INoteRepository _noteRepository;
    private readonly ILocationRepository _locationRepository;
    private readonly IMarshalRepository _marshalRepository;
    private readonly IAssignmentRepository _assignmentRepository;
    private readonly IAreaRepository _areaRepository;
    private readonly ClaimsService _claimsService;

    public NoteFunctions(
        ILogger<NoteFunctions> logger,
        INoteRepository noteRepository,
        ILocationRepository locationRepository,
        IMarshalRepository marshalRepository,
        IAssignmentRepository assignmentRepository,
        IAreaRepository areaRepository,
        ClaimsService claimsService)
    {
        _logger = logger;
        _noteRepository = noteRepository;
        _locationRepository = locationRepository;
        _marshalRepository = marshalRepository;
        _assignmentRepository = assignmentRepository;
        _areaRepository = areaRepository;
        _claimsService = claimsService;
    }

    /// <summary>
    /// Create a new note for an event.
    /// Only event admins and area leads can create notes.
    /// </summary>
#pragma warning disable MA0051
    [Function("CreateNote")]
    public async Task<IActionResult> CreateNote(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "events/{eventId}/notes")] HttpRequest req,
        string eventId)
    {
        try
        {
            // Authenticate
            UserClaims? claims = await GetClaimsAsync(req, eventId);
            if (claims == null || !CanManageNotes(claims))
            {
                return new UnauthorizedObjectResult(new { message = Constants.ErrorNotAuthorizedToManageNotes });
            }

            // Parse request
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            CreateNoteRequest? request = JsonSerializer.Deserialize<CreateNoteRequest>(requestBody, FunctionHelpers.JsonOptions);

            if (request == null || string.IsNullOrWhiteSpace(request.Title))
            {
                return new BadRequestObjectResult(new { message = "Title is required" });
            }

            // Validate scope configurations
            if (request.ScopeConfigurations == null || !request.ScopeConfigurations.Any())
            {
                return new BadRequestObjectResult(new { message = "At least one scope configuration is required" });
            }

            // Validate that notes don't use "One per" scopes (no completion tracking)
            foreach (ScopeConfiguration scope in request.ScopeConfigurations)
            {
                if (scope.Scope is Constants.ChecklistScopeOnePerArea or
                    Constants.ChecklistScopeOnePerCheckpoint or
                    Constants.ChecklistScopeOneLeadPerArea)
                {
                    return new BadRequestObjectResult(new { message = "Notes cannot use 'One per' scopes as they don't have completion tracking" });
                }
            }

            string noteId = Guid.NewGuid().ToString();
            NoteEntity note = new NoteEntity
            {
                PartitionKey = eventId,
                RowKey = noteId,
                EventId = eventId,
                NoteId = noteId,
                Title = request.Title,
                Content = request.Content ?? string.Empty,
                ScopeConfigurationsJson = JsonSerializer.Serialize(request.ScopeConfigurations),
                DisplayOrder = request.DisplayOrder,
                Priority = request.Priority ?? Constants.NotePriorityNormal,
                Category = request.Category ?? string.Empty,
                IsPinned = request.IsPinned,
                CreatedByPersonId = claims.PersonId,
                CreatedByName = claims.PersonName,
                CreatedAt = DateTime.UtcNow,
                IsDeleted = false
            };

            await _noteRepository.AddAsync(note);

            _logger.LogInformation("Note {NoteId} created for event {EventId} by {PersonId}", noteId, eventId, claims.PersonId);

            return new OkObjectResult(ToNoteResponse(note));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating note for event {EventId}", eventId);
            return new StatusCodeResult(500);
        }
    }
#pragma warning restore MA0051

    /// <summary>
    /// Get all notes for an event (admin view with full details).
    /// Only event admins can see all notes.
    /// </summary>
    [Function("GetNotes")]
    public async Task<IActionResult> GetNotes(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "events/{eventId}/notes")] HttpRequest req,
        string eventId)
    {
        try
        {
            // Authenticate
            UserClaims? claims = await GetClaimsAsync(req, eventId);
            if (claims == null || !claims.IsEventAdmin)
            {
                return new UnauthorizedObjectResult(new { message = Constants.ErrorNotAuthorized });
            }

            IEnumerable<NoteEntity> notes = await _noteRepository.GetByEventAsync(eventId);

            // Build a lookup of current marshal names by PersonId
            IEnumerable<MarshalEntity> marshals = await _marshalRepository.GetByEventAsync(eventId);
            Dictionary<string, string> personNameLookup = marshals
                .Where(m => !string.IsNullOrEmpty(m.PersonId))
                .ToDictionary(m => m.PersonId, m => m.Name);

            List<NoteResponse> responses = [.. notes.Select(n => ToNoteResponse(n, personNameLookup))];

            return new OkObjectResult(responses);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting notes for event {EventId}", eventId);
            return new StatusCodeResult(500);
        }
    }

    /// <summary>
    /// Get a specific note by ID.
    /// Event admins can view any note.
    /// </summary>
    [Function("GetNote")]
    public async Task<IActionResult> GetNote(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "events/{eventId}/notes/{noteId}")] HttpRequest req,
        string eventId,
        string noteId)
    {
        try
        {
            // Authenticate
            UserClaims? claims = await GetClaimsAsync(req, eventId);
            if (claims == null || !claims.IsEventAdmin)
            {
                return new UnauthorizedObjectResult(new { message = Constants.ErrorNotAuthorized });
            }

            NoteEntity? note = await _noteRepository.GetAsync(eventId, noteId);
            if (note == null)
            {
                return new NotFoundObjectResult(new { message = Constants.ErrorNoteNotFound });
            }

            // Build a lookup of current marshal names by PersonId
            IEnumerable<MarshalEntity> marshals = await _marshalRepository.GetByEventAsync(eventId);
            Dictionary<string, string> personNameLookup = marshals
                .Where(m => !string.IsNullOrEmpty(m.PersonId))
                .ToDictionary(m => m.PersonId, m => m.Name);

            return new OkObjectResult(ToNoteResponse(note, personNameLookup));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting note {NoteId} for event {EventId}", noteId, eventId);
            return new StatusCodeResult(500);
        }
    }

    /// <summary>
    /// Update an existing note.
    /// Only event admins and area leads can update notes.
    /// </summary>
#pragma warning disable MA0051
    [Function("UpdateNote")]
    public async Task<IActionResult> UpdateNote(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "events/{eventId}/notes/{noteId}")] HttpRequest req,
        string eventId,
        string noteId)
    {
        try
        {
            // Authenticate
            UserClaims? claims = await GetClaimsAsync(req, eventId);
            if (claims == null || !CanManageNotes(claims))
            {
                return new UnauthorizedObjectResult(new { message = Constants.ErrorNotAuthorizedToManageNotes });
            }

            NoteEntity? note = await _noteRepository.GetAsync(eventId, noteId);
            if (note == null)
            {
                return new NotFoundObjectResult(new { message = Constants.ErrorNoteNotFound });
            }

            // Parse request
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            UpdateNoteRequest? request = JsonSerializer.Deserialize<UpdateNoteRequest>(requestBody, FunctionHelpers.JsonOptions);

            if (request == null || string.IsNullOrWhiteSpace(request.Title))
            {
                return new BadRequestObjectResult(new { message = "Title is required" });
            }

            // Validate that notes don't use "One per" scopes
            if (request.ScopeConfigurations != null)
            {
                foreach (ScopeConfiguration scope in request.ScopeConfigurations)
                {
                    if (scope.Scope is Constants.ChecklistScopeOnePerArea or
                        Constants.ChecklistScopeOnePerCheckpoint or
                        Constants.ChecklistScopeOneLeadPerArea)
                    {
                        return new BadRequestObjectResult(new { message = "Notes cannot use 'One per' scopes as they don't have completion tracking" });
                    }
                }
            }

            // Update note
            note.Title = request.Title;
            note.Content = request.Content ?? string.Empty;
            note.ScopeConfigurationsJson = JsonSerializer.Serialize(request.ScopeConfigurations ?? []);
            note.DisplayOrder = request.DisplayOrder;
            note.Priority = request.Priority ?? Constants.NotePriorityNormal;
            note.Category = request.Category ?? string.Empty;
            note.IsPinned = request.IsPinned;
            note.UpdatedByPersonId = claims.PersonId;
            note.UpdatedByName = claims.PersonName;
            note.UpdatedAt = DateTime.UtcNow;

            await _noteRepository.UpdateAsync(note);

            _logger.LogInformation("Note {NoteId} updated for event {EventId} by {PersonId}", noteId, eventId, claims.PersonId);

            // Note already has current names stored (UpdatedByName = claims.PersonName), no need to fetch all marshals
            return new OkObjectResult(ToNoteResponse(note));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating note {NoteId} for event {EventId}", noteId, eventId);
            return new StatusCodeResult(500);
        }
    }
#pragma warning restore MA0051

    /// <summary>
    /// Delete a note.
    /// Only event admins can delete notes.
    /// </summary>
    [Function("DeleteNote")]
    public async Task<IActionResult> DeleteNote(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "events/{eventId}/notes/{noteId}")] HttpRequest req,
        string eventId,
        string noteId)
    {
        try
        {
            // Authenticate - only event admins can delete
            UserClaims? claims = await GetClaimsAsync(req, eventId);
            if (claims == null || !claims.IsEventAdmin)
            {
                return new UnauthorizedObjectResult(new { message = Constants.ErrorNotAuthorizedToManageNotes });
            }

            NoteEntity? note = await _noteRepository.GetAsync(eventId, noteId);
            if (note == null)
            {
                return new NotFoundObjectResult(new { message = Constants.ErrorNoteNotFound });
            }

            await _noteRepository.DeleteAsync(eventId, noteId);

            _logger.LogInformation("Note {NoteId} deleted for event {EventId} by {PersonId}", noteId, eventId, claims.PersonId);

            return new NoContentResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting note {NoteId} for event {EventId}", noteId, eventId);
            return new StatusCodeResult(500);
        }
    }

    /// <summary>
    /// Get notes relevant to a specific marshal.
    /// Uses scope evaluation to filter notes based on marshal's assignments.
    /// </summary>
#pragma warning disable MA0051
    [Function("GetNotesForMarshal")]
    public async Task<IActionResult> GetNotesForMarshal(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "events/{eventId}/marshals/{marshalId}/notes")] HttpRequest req,
        string eventId,
        string marshalId)
    {
        try
        {
            // Build marshal context and checkpoint lookup together (avoids duplicate location fetch)
            (ScopeEvaluator.MarshalContext? marshalContext, Dictionary<string, LocationEntity> checkpointLookup,
             Dictionary<string, string> personNameLookup) = await BuildMarshalContextWithLookupsAsync(eventId, marshalId);

            if (marshalContext == null)
            {
                return new NotFoundObjectResult(new { message = Constants.ErrorMarshalNotFound });
            }

            // Get all notes for the event
            IEnumerable<NoteEntity> allNotes = await _noteRepository.GetByEventAsync(eventId);

            // Filter notes based on scope
            List<NoteForMarshalResponse> relevantNotes = [];
            foreach (NoteEntity note in allNotes)
            {
                List<ScopeConfiguration> configs = JsonSerializer.Deserialize<List<ScopeConfiguration>>(
                    note.ScopeConfigurationsJson, FunctionHelpers.JsonOptions) ?? [];

                ScopeEvaluator.ScopeMatchResult result = ScopeEvaluator.EvaluateScopeConfigurations(
                    configs, marshalContext, checkpointLookup);

                if (result.IsRelevant)
                {
                    // Look up current name
                    string createdByName = note.CreatedByName;
                    if (!string.IsNullOrEmpty(note.CreatedByPersonId) &&
                        personNameLookup.TryGetValue(note.CreatedByPersonId, out string? currentName) &&
                        !string.IsNullOrEmpty(currentName))
                    {
                        createdByName = currentName;
                    }

                    relevantNotes.Add(new NoteForMarshalResponse(
                        NoteId: note.NoteId,
                        EventId: note.EventId,
                        Title: note.Title,
                        Content: note.Content,
                        Priority: note.Priority,
                        Category: string.IsNullOrEmpty(note.Category) ? null : note.Category,
                        IsPinned: note.IsPinned,
                        CreatedAt: note.CreatedAt,
                        CreatedByName: createdByName,
                        MatchedScope: result.WinningConfig?.Scope ?? string.Empty
                    ));
                }
            }

            // Sort: pinned first, then by priority (Urgent > High > Normal > Low), then by display order
            relevantNotes = [.. relevantNotes
                .OrderByDescending(n => n.IsPinned)
                .ThenBy(n => GetPrioritySortOrder(n.Priority))
                .ThenBy(n => allNotes.First(note => note.NoteId == n.NoteId).DisplayOrder)
                .ThenByDescending(n => n.CreatedAt)];

            return new OkObjectResult(relevantNotes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting notes for marshal {MarshalId} in event {EventId}", marshalId, eventId);
            return new StatusCodeResult(500);
        }
    }
#pragma warning restore MA0051

    /// <summary>
    /// Get notes for the current authenticated marshal.
    /// </summary>
#pragma warning disable MA0051
    [Function("GetMyNotes")]
    public async Task<IActionResult> GetMyNotes(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "events/{eventId}/my-notes")] HttpRequest req,
        string eventId)
    {
        try
        {
            // Authenticate and get marshal ID
            UserClaims? claims = await GetClaimsAsync(req, eventId);
            if (claims == null)
            {
                return new UnauthorizedObjectResult(new { message = Constants.ErrorNotAuthorized });
            }

            string? marshalId = claims.MarshalId;
            if (string.IsNullOrEmpty(marshalId))
            {
                // If user is admin without marshal assignment, return all notes
                if (claims.IsEventAdmin)
                {
                    IEnumerable<NoteEntity> allNotes = await _noteRepository.GetByEventAsync(eventId);

                    // Build a lookup of current marshal names by PersonId
                    IEnumerable<MarshalEntity> marshals = await _marshalRepository.GetByEventAsync(eventId);
                    Dictionary<string, string> personNameLookup = marshals
                        .Where(m => !string.IsNullOrEmpty(m.PersonId))
                        .ToDictionary(m => m.PersonId, m => m.Name);

                    List<NoteForMarshalResponse> allNotesResponse = [.. allNotes.Select(n =>
                    {
                        // Look up current name
                        string createdByName = n.CreatedByName;
                        if (!string.IsNullOrEmpty(n.CreatedByPersonId) &&
                            personNameLookup.TryGetValue(n.CreatedByPersonId, out string? currentName) &&
                            !string.IsNullOrEmpty(currentName))
                        {
                            createdByName = currentName;
                        }

                        return new NoteForMarshalResponse(
                            NoteId: n.NoteId,
                            EventId: n.EventId,
                            Title: n.Title,
                            Content: n.Content,
                            Priority: n.Priority,
                            Category: string.IsNullOrEmpty(n.Category) ? null : n.Category,
                            IsPinned: n.IsPinned,
                            CreatedAt: n.CreatedAt,
                            CreatedByName: createdByName,
                            MatchedScope: "Admin"
                        );
                    })];
                    return new OkObjectResult(allNotesResponse);
                }

                return new BadRequestObjectResult(new { message = "No marshal assignment found" });
            }

            // Delegate to GetNotesForMarshal
            return await GetNotesForMarshal(req, eventId, marshalId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting notes for current user in event {EventId}", eventId);
            return new StatusCodeResult(500);
        }
    }
#pragma warning restore MA0051

    #region Helper Methods

    private async Task<UserClaims?> GetClaimsAsync(HttpRequest req, string eventId)
    {
        string? sessionToken = req.Headers.Authorization.FirstOrDefault()?.Replace("Bearer ", "", StringComparison.OrdinalIgnoreCase);
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

    private static bool CanManageNotes(UserClaims claims)
    {
        // Event admins can manage all notes
        if (claims.IsEventAdmin)
        {
            return true;
        }

        // Area leads can create/edit notes (scoped to their areas in practice)
        if (claims.EventRoles.Any(r => r.Role == Constants.RoleEventAreaLead))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Builds marshal context, checkpoint lookup, and person name lookup together to avoid duplicate fetches.
    /// </summary>
    private async Task<(ScopeEvaluator.MarshalContext?, Dictionary<string, LocationEntity>, Dictionary<string, string>)>
        BuildMarshalContextWithLookupsAsync(string eventId, string marshalId)
    {
        MarshalEntity? marshal = await _marshalRepository.GetAsync(eventId, marshalId);
        if (marshal == null)
        {
            return (null, new Dictionary<string, LocationEntity>(), new Dictionary<string, string>());
        }

        // Get marshal's assignments
        IEnumerable<AssignmentEntity> assignments = await _assignmentRepository.GetByMarshalAsync(eventId, marshalId);
        List<string> assignedLocationIds = [.. assignments.Select(a => a.LocationId)];

        // Get checkpoints ONCE (will be reused for both context building and scope evaluation)
        IEnumerable<LocationEntity> checkpoints = await _locationRepository.GetByEventAsync(eventId);
        Dictionary<string, LocationEntity> checkpointLookup = checkpoints.ToDictionary(c => c.RowKey);

        // Get all marshals ONCE (for person name lookup)
        IEnumerable<MarshalEntity> allMarshals = await _marshalRepository.GetByEventAsync(eventId);
        Dictionary<string, string> personNameLookup = allMarshals
            .Where(m => !string.IsNullOrEmpty(m.PersonId))
            .ToDictionary(m => m.PersonId, m => m.Name);

        // Get areas from assigned checkpoints
        HashSet<string> assignedAreaIds = [];
        foreach (string locationId in assignedLocationIds)
        {
            if (checkpointLookup.TryGetValue(locationId, out LocationEntity? checkpoint))
            {
                List<string> areaIds = JsonSerializer.Deserialize<List<string>>(checkpoint.AreaIdsJson) ?? [];
                foreach (string areaId in areaIds)
                {
                    assignedAreaIds.Add(areaId);
                }
            }
        }

        // Get areas where marshal is a lead
        List<string> areaLeadForAreaIds = [];
        IEnumerable<AreaEntity> areas = await _areaRepository.GetByEventAsync(eventId);
        foreach (AreaEntity area in areas)
        {
            List<AreaContact> contacts = JsonSerializer.Deserialize<List<AreaContact>>(area.ContactsJson) ?? [];
            if (contacts.Any(c => c.MarshalId == marshalId && c.Role == "Leader"))
            {
                areaLeadForAreaIds.Add(area.RowKey);
            }
        }

        ScopeEvaluator.MarshalContext context = new(
            MarshalId: marshalId,
            AssignedAreaIds: assignedAreaIds.ToList(),
            AssignedLocationIds: assignedLocationIds,
            AreaLeadForAreaIds: areaLeadForAreaIds
        );

        return (context, checkpointLookup, personNameLookup);
    }

    private static NoteResponse ToNoteResponse(NoteEntity note, Dictionary<string, string>? personNameLookup = null)
    {
        List<ScopeConfiguration> configs = JsonSerializer.Deserialize<List<ScopeConfiguration>>(
            note.ScopeConfigurationsJson, FunctionHelpers.JsonOptions) ?? [];

        // Look up current name if lookup is provided, otherwise fall back to stored name
        string createdByName = note.CreatedByName;
        if (personNameLookup != null && !string.IsNullOrEmpty(note.CreatedByPersonId) &&
            personNameLookup.TryGetValue(note.CreatedByPersonId, out string? currentName) &&
            !string.IsNullOrEmpty(currentName))
        {
            createdByName = currentName;
        }

        string? updatedByName = note.UpdatedByName;
        if (personNameLookup != null && !string.IsNullOrEmpty(note.UpdatedByPersonId) &&
            personNameLookup.TryGetValue(note.UpdatedByPersonId, out string? currentUpdaterName) &&
            !string.IsNullOrEmpty(currentUpdaterName))
        {
            updatedByName = currentUpdaterName;
        }

        return new NoteResponse(
            NoteId: note.NoteId,
            EventId: note.EventId,
            Title: note.Title,
            Content: note.Content,
            ScopeConfigurations: configs,
            DisplayOrder: note.DisplayOrder,
            Priority: note.Priority,
            Category: string.IsNullOrEmpty(note.Category) ? null : note.Category,
            IsPinned: note.IsPinned,
            CreatedByPersonId: note.CreatedByPersonId,
            CreatedByName: createdByName,
            CreatedAt: note.CreatedAt,
            UpdatedByPersonId: note.UpdatedByPersonId,
            UpdatedByName: updatedByName,
            UpdatedAt: note.UpdatedAt
        );
    }

    private static int GetPrioritySortOrder(string priority)
    {
        return priority switch
        {
            Constants.NotePriorityEmergency => 0,
            Constants.NotePriorityUrgent => 1,
            Constants.NotePriorityHigh => 2,
            Constants.NotePriorityNormal => 3,
            Constants.NotePriorityLow => 4,
            _ => 3 // Default to Normal
        };
    }

    #endregion
}
