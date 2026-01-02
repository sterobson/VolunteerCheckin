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
/// Azure Functions for managing event contacts.
/// Contacts are displayed to marshals based on scope configurations (like notes).
/// Admins can create/edit contacts; marshals can view relevant contacts.
/// </summary>
public class EventContactFunctions
{
    private readonly ILogger<EventContactFunctions> _logger;
    private readonly IEventContactRepository _contactRepository;
    private readonly ILocationRepository _locationRepository;
    private readonly IMarshalRepository _marshalRepository;
    private readonly IAssignmentRepository _assignmentRepository;
    private readonly IAreaRepository _areaRepository;
    private readonly ClaimsService _claimsService;

    // Built-in contact roles
    private static readonly List<string> BuiltInRoles =
    [
        Constants.ContactRoleEmergency,
        Constants.ContactRoleEventDirector,
        Constants.ContactRoleMedicalLead,
        Constants.ContactRoleSafetyOfficer,
        Constants.ContactRoleLogistics
    ];

    public EventContactFunctions(
        ILogger<EventContactFunctions> logger,
        IEventContactRepository contactRepository,
        ILocationRepository locationRepository,
        IMarshalRepository marshalRepository,
        IAssignmentRepository assignmentRepository,
        IAreaRepository areaRepository,
        ClaimsService claimsService)
    {
        _logger = logger;
        _contactRepository = contactRepository;
        _locationRepository = locationRepository;
        _marshalRepository = marshalRepository;
        _assignmentRepository = assignmentRepository;
        _areaRepository = areaRepository;
        _claimsService = claimsService;
    }

    /// <summary>
    /// Create a new contact for an event.
    /// Only event admins can create contacts.
    /// </summary>
    [Function("CreateEventContact")]
    public async Task<IActionResult> CreateContact(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "events/{eventId}/contacts")] HttpRequest req,
        string eventId)
    {
        try
        {
            // Authenticate
            UserClaims? claims = await GetClaimsAsync(req, eventId);
            if (claims == null || !claims.IsEventAdmin)
            {
                return new UnauthorizedObjectResult(new { message = Constants.ErrorNotAuthorizedToManageContacts });
            }

            // Parse request
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            CreateEventContactRequest? request = JsonSerializer.Deserialize<CreateEventContactRequest>(requestBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (request == null || string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Role))
            {
                return new BadRequestObjectResult(new { message = "Name and Role are required" });
            }

            // If linked to a marshal, verify the marshal exists
            MarshalEntity? linkedMarshal = null;
            if (!string.IsNullOrEmpty(request.MarshalId))
            {
                linkedMarshal = await _marshalRepository.GetAsync(eventId, request.MarshalId);
                if (linkedMarshal == null)
                {
                    return new BadRequestObjectResult(new { message = Constants.ErrorMarshalNotFound });
                }
            }

            // Default scope: visible to everyone if no scope specified
            List<ScopeConfiguration> scopeConfigs = request.ScopeConfigurations ??
            [
                new ScopeConfiguration
                {
                    Scope = Constants.ChecklistScopeEveryoneInAreas,
                    ItemType = "Area",
                    Ids = [Constants.AllAreas]
                }
            ];

            // Validate scope configurations (same as notes - no completion-based scopes)
            foreach (ScopeConfiguration scope in scopeConfigs)
            {
                if (scope.Scope is Constants.ChecklistScopeOnePerArea or
                    Constants.ChecklistScopeOnePerCheckpoint or
                    Constants.ChecklistScopeOneLeadPerArea)
                {
                    return new BadRequestObjectResult(new { message = "Contacts cannot use 'One per' scopes" });
                }
            }

            string contactId = Guid.NewGuid().ToString();
            EventContactEntity contact = new EventContactEntity
            {
                PartitionKey = eventId,
                RowKey = contactId,
                EventId = eventId,
                ContactId = contactId,
                Role = request.Role,
                Name = request.Name,
                Phone = request.Phone,
                Email = request.Email,
                Notes = request.Notes,
                MarshalId = request.MarshalId,
                ScopeConfigurationsJson = JsonSerializer.Serialize(scopeConfigs),
                DisplayOrder = request.DisplayOrder,
                IsPrimary = request.IsPrimary,
                CreatedByPersonId = claims.PersonId,
                CreatedAt = DateTime.UtcNow,
                IsDeleted = false
            };

            await _contactRepository.AddAsync(contact);

            _logger.LogInformation("Contact {ContactId} created for event {EventId} by {PersonId}", contactId, eventId, claims.PersonId);

            return new OkObjectResult(ToContactResponse(contact, linkedMarshal?.Name));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating contact for event {EventId}", eventId);
            return new StatusCodeResult(500);
        }
    }

    /// <summary>
    /// Get all contacts for an event (admin view with full details).
    /// Only event admins can see all contacts.
    /// </summary>
    [Function("GetEventContacts")]
    public async Task<IActionResult> GetContacts(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "events/{eventId}/contacts")] HttpRequest req,
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

            IEnumerable<EventContactEntity> contacts = await _contactRepository.GetByEventAsync(eventId);

            // Get marshal names for linked contacts
            IEnumerable<MarshalEntity> marshals = await _marshalRepository.GetByEventAsync(eventId);
            Dictionary<string, string> marshalNameLookup = marshals.ToDictionary(m => m.MarshalId, m => m.Name);

            List<EventContactResponse> responses = contacts.Select(c =>
                ToContactResponse(c, c.MarshalId != null && marshalNameLookup.TryGetValue(c.MarshalId, out string? name) ? name : null)
            ).ToList();

            return new OkObjectResult(responses);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting contacts for event {EventId}", eventId);
            return new StatusCodeResult(500);
        }
    }

    /// <summary>
    /// Get a specific contact by ID.
    /// Event admins can view any contact.
    /// </summary>
    [Function("GetEventContact")]
    public async Task<IActionResult> GetContact(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "events/{eventId}/contacts/{contactId}")] HttpRequest req,
        string eventId,
        string contactId)
    {
        try
        {
            // Authenticate
            UserClaims? claims = await GetClaimsAsync(req, eventId);
            if (claims == null || !claims.IsEventAdmin)
            {
                return new UnauthorizedObjectResult(new { message = Constants.ErrorNotAuthorized });
            }

            EventContactEntity? contact = await _contactRepository.GetAsync(eventId, contactId);
            if (contact == null)
            {
                return new NotFoundObjectResult(new { message = Constants.ErrorContactNotFound });
            }

            // Get marshal name if linked
            string? marshalName = null;
            if (!string.IsNullOrEmpty(contact.MarshalId))
            {
                MarshalEntity? marshal = await _marshalRepository.GetAsync(eventId, contact.MarshalId);
                marshalName = marshal?.Name;
            }

            return new OkObjectResult(ToContactResponse(contact, marshalName));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting contact {ContactId} for event {EventId}", contactId, eventId);
            return new StatusCodeResult(500);
        }
    }

    /// <summary>
    /// Update an existing contact.
    /// Only event admins can update contacts.
    /// </summary>
    [Function("UpdateEventContact")]
    public async Task<IActionResult> UpdateContact(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "events/{eventId}/contacts/{contactId}")] HttpRequest req,
        string eventId,
        string contactId)
    {
        try
        {
            // Authenticate
            UserClaims? claims = await GetClaimsAsync(req, eventId);
            if (claims == null || !claims.IsEventAdmin)
            {
                return new UnauthorizedObjectResult(new { message = Constants.ErrorNotAuthorizedToManageContacts });
            }

            EventContactEntity? contact = await _contactRepository.GetAsync(eventId, contactId);
            if (contact == null)
            {
                return new NotFoundObjectResult(new { message = Constants.ErrorContactNotFound });
            }

            // Parse request
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            UpdateEventContactRequest? request = JsonSerializer.Deserialize<UpdateEventContactRequest>(requestBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (request == null || string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Role))
            {
                return new BadRequestObjectResult(new { message = "Name and Role are required" });
            }

            // If linked to a marshal, verify the marshal exists
            MarshalEntity? linkedMarshal = null;
            if (!string.IsNullOrEmpty(request.MarshalId))
            {
                linkedMarshal = await _marshalRepository.GetAsync(eventId, request.MarshalId);
                if (linkedMarshal == null)
                {
                    return new BadRequestObjectResult(new { message = Constants.ErrorMarshalNotFound });
                }
            }

            // Validate scope configurations
            if (request.ScopeConfigurations != null)
            {
                foreach (ScopeConfiguration scope in request.ScopeConfigurations)
                {
                    if (scope.Scope is Constants.ChecklistScopeOnePerArea or
                        Constants.ChecklistScopeOnePerCheckpoint or
                        Constants.ChecklistScopeOneLeadPerArea)
                    {
                        return new BadRequestObjectResult(new { message = "Contacts cannot use 'One per' scopes" });
                    }
                }
            }

            // Update contact
            contact.Role = request.Role;
            contact.Name = request.Name;
            contact.Phone = request.Phone;
            contact.Email = request.Email;
            contact.Notes = request.Notes;
            contact.MarshalId = request.MarshalId;
            contact.ScopeConfigurationsJson = JsonSerializer.Serialize(request.ScopeConfigurations ?? []);
            contact.DisplayOrder = request.DisplayOrder;
            contact.IsPrimary = request.IsPrimary;
            contact.UpdatedAt = DateTime.UtcNow;

            await _contactRepository.UpdateAsync(contact);

            _logger.LogInformation("Contact {ContactId} updated for event {EventId} by {PersonId}", contactId, eventId, claims.PersonId);

            return new OkObjectResult(ToContactResponse(contact, linkedMarshal?.Name));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating contact {ContactId} for event {EventId}", contactId, eventId);
            return new StatusCodeResult(500);
        }
    }

    /// <summary>
    /// Delete a contact.
    /// Only event admins can delete contacts.
    /// </summary>
    [Function("DeleteEventContact")]
    public async Task<IActionResult> DeleteContact(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "events/{eventId}/contacts/{contactId}")] HttpRequest req,
        string eventId,
        string contactId)
    {
        try
        {
            // Authenticate
            UserClaims? claims = await GetClaimsAsync(req, eventId);
            if (claims == null || !claims.IsEventAdmin)
            {
                return new UnauthorizedObjectResult(new { message = Constants.ErrorNotAuthorizedToManageContacts });
            }

            EventContactEntity? contact = await _contactRepository.GetAsync(eventId, contactId);
            if (contact == null)
            {
                return new NotFoundObjectResult(new { message = Constants.ErrorContactNotFound });
            }

            await _contactRepository.DeleteAsync(eventId, contactId);

            _logger.LogInformation("Contact {ContactId} deleted for event {EventId} by {PersonId}", contactId, eventId, claims.PersonId);

            return new NoContentResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting contact {ContactId} for event {EventId}", contactId, eventId);
            return new StatusCodeResult(500);
        }
    }

    /// <summary>
    /// Get contacts relevant to a specific marshal.
    /// Uses scope evaluation to filter contacts based on marshal's assignments.
    /// </summary>
    [Function("GetContactsForMarshal")]
    public async Task<IActionResult> GetContactsForMarshal(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "events/{eventId}/marshals/{marshalId}/contacts")] HttpRequest req,
        string eventId,
        string marshalId)
    {
        try
        {
            // Build marshal context for scope evaluation
            ScopeEvaluator.MarshalContext? marshalContext = await BuildMarshalContextAsync(eventId, marshalId);
            if (marshalContext == null)
            {
                return new NotFoundObjectResult(new { message = Constants.ErrorMarshalNotFound });
            }

            // Get all contacts for the event
            IEnumerable<EventContactEntity> allContacts = await _contactRepository.GetByEventAsync(eventId);

            // Build checkpoint lookup for scope evaluation
            Dictionary<string, LocationEntity> checkpointLookup = await BuildCheckpointLookupAsync(eventId);

            // Filter contacts based on scope
            List<EventContactForMarshalResponse> relevantContacts = [];
            foreach (EventContactEntity contact in allContacts)
            {
                List<ScopeConfiguration> configs = JsonSerializer.Deserialize<List<ScopeConfiguration>>(
                    contact.ScopeConfigurationsJson) ?? [];

                ScopeEvaluator.ScopeMatchResult result = ScopeEvaluator.EvaluateScopeConfigurations(
                    configs, marshalContext, checkpointLookup);

                if (result.IsRelevant)
                {
                    relevantContacts.Add(new EventContactForMarshalResponse(
                        ContactId: contact.ContactId,
                        Role: contact.Role,
                        Name: contact.Name,
                        Phone: contact.Phone,
                        Email: contact.Email,
                        Notes: contact.Notes,
                        IsPrimary: contact.IsPrimary,
                        MatchedScope: result.WinningConfig?.Scope ?? string.Empty
                    ));
                }
            }

            // Sort: primary first, then by role, then by display order
            relevantContacts = relevantContacts
                .OrderByDescending(c => c.IsPrimary)
                .ThenBy(c => c.Role)
                .ThenBy(c => allContacts.First(contact => contact.ContactId == c.ContactId).DisplayOrder)
                .ToList();

            return new OkObjectResult(relevantContacts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting contacts for marshal {MarshalId} in event {EventId}", marshalId, eventId);
            return new StatusCodeResult(500);
        }
    }

    /// <summary>
    /// Get contacts for the current authenticated marshal.
    /// </summary>
    [Function("GetMyContacts")]
    public async Task<IActionResult> GetMyContacts(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "events/{eventId}/my-contacts")] HttpRequest req,
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
                // If user is admin without marshal assignment, return all contacts
                if (claims.IsEventAdmin)
                {
                    IEnumerable<EventContactEntity> allContacts = await _contactRepository.GetByEventAsync(eventId);
                    List<EventContactForMarshalResponse> allContactsResponse = allContacts.Select(c =>
                        new EventContactForMarshalResponse(
                            ContactId: c.ContactId,
                            Role: c.Role,
                            Name: c.Name,
                            Phone: c.Phone,
                            Email: c.Email,
                            Notes: c.Notes,
                            IsPrimary: c.IsPrimary,
                            MatchedScope: "Admin"
                        )
                    ).ToList();
                    return new OkObjectResult(allContactsResponse);
                }

                return new BadRequestObjectResult(new { message = "No marshal assignment found" });
            }

            // Delegate to GetContactsForMarshal
            return await GetContactsForMarshal(req, eventId, marshalId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting contacts for current user in event {EventId}", eventId);
            return new StatusCodeResult(500);
        }
    }

    /// <summary>
    /// Get available contact roles (built-in + custom roles used in this event).
    /// </summary>
    [Function("GetContactRoles")]
    public async Task<IActionResult> GetContactRoles(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "events/{eventId}/contact-roles")] HttpRequest req,
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

            // Get all contacts for the event to find custom roles
            IEnumerable<EventContactEntity> contacts = await _contactRepository.GetByEventAsync(eventId);
            List<string> customRoles = contacts
                .Select(c => c.Role)
                .Where(r => !BuiltInRoles.Contains(r))
                .Distinct()
                .OrderBy(r => r)
                .ToList();

            return new OkObjectResult(new ContactRolesResponse(BuiltInRoles, customRoles));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting contact roles for event {EventId}", eventId);
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

    private async Task<ScopeEvaluator.MarshalContext?> BuildMarshalContextAsync(string eventId, string marshalId)
    {
        MarshalEntity? marshal = await _marshalRepository.GetAsync(eventId, marshalId);
        if (marshal == null)
        {
            return null;
        }

        // Get marshal's assignments
        IEnumerable<AssignmentEntity> assignments = await _assignmentRepository.GetByMarshalAsync(eventId, marshalId);
        List<string> assignedLocationIds = assignments.Select(a => a.LocationId).ToList();

        // Get checkpoints to determine areas
        IEnumerable<LocationEntity> checkpoints = await _locationRepository.GetByEventAsync(eventId);
        Dictionary<string, LocationEntity> checkpointLookup = checkpoints.ToDictionary(c => c.RowKey);

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

        return new ScopeEvaluator.MarshalContext(
            MarshalId: marshalId,
            AssignedAreaIds: assignedAreaIds.ToList(),
            AssignedLocationIds: assignedLocationIds,
            AreaLeadForAreaIds: areaLeadForAreaIds
        );
    }

    private async Task<Dictionary<string, LocationEntity>> BuildCheckpointLookupAsync(string eventId)
    {
        IEnumerable<LocationEntity> checkpoints = await _locationRepository.GetByEventAsync(eventId);
        return checkpoints.ToDictionary(c => c.RowKey);
    }

    private static EventContactResponse ToContactResponse(EventContactEntity contact, string? marshalName)
    {
        List<ScopeConfiguration> configs = JsonSerializer.Deserialize<List<ScopeConfiguration>>(
            contact.ScopeConfigurationsJson) ?? [];

        return new EventContactResponse(
            ContactId: contact.ContactId,
            EventId: contact.EventId,
            Role: contact.Role,
            Name: contact.Name,
            Phone: contact.Phone,
            Email: contact.Email,
            Notes: contact.Notes,
            MarshalId: contact.MarshalId,
            MarshalName: marshalName,
            ScopeConfigurations: configs,
            DisplayOrder: contact.DisplayOrder,
            IsPrimary: contact.IsPrimary,
            CreatedAt: contact.CreatedAt,
            UpdatedAt: contact.UpdatedAt
        );
    }

    #endregion
}
