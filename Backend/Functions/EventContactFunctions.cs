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
    private readonly IEventRoleRepository _eventRoleRepository;
    private readonly ClaimsService _claimsService;

    // Built-in contact roles
    private static readonly List<string> BuiltInRoles =
    [
        Constants.ContactRoleEmergency,
        Constants.ContactRoleEventDirector,
        Constants.ContactRoleMedicalLead,
        Constants.ContactRoleSafetyOfficer,
        Constants.ContactRoleLogistics,
        Constants.ContactRoleAreaLead
    ];

    public EventContactFunctions(
        ILogger<EventContactFunctions> logger,
        IEventContactRepository contactRepository,
        ILocationRepository locationRepository,
        IMarshalRepository marshalRepository,
        IAssignmentRepository assignmentRepository,
        IAreaRepository areaRepository,
        IEventRoleRepository eventRoleRepository,
        ClaimsService claimsService)
    {
        _logger = logger;
        _contactRepository = contactRepository;
        _locationRepository = locationRepository;
        _marshalRepository = marshalRepository;
        _assignmentRepository = assignmentRepository;
        _areaRepository = areaRepository;
        _eventRoleRepository = eventRoleRepository;
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

            // Sync area lead role if this contact is linked to a marshal
            await SyncAreaLeadRoleAsync(eventId, request.MarshalId, request.Role, scopeConfigs, claims.PersonId);

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

            // Store old marshal ID for potential role cleanup
            string? oldMarshalId = contact.MarshalId;
            string oldRole = contact.Role;

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

            // Handle area lead role changes
            List<ScopeConfiguration> scopeConfigs = request.ScopeConfigurations ?? [];

            // If marshal changed, remove role from old marshal first
            if (oldMarshalId != request.MarshalId && !string.IsNullOrEmpty(oldMarshalId) && oldRole == Constants.ContactRoleAreaLead)
            {
                await RemoveAreaLeadRoleAsync(eventId, oldMarshalId);
            }

            // Sync area lead role for the current marshal
            await SyncAreaLeadRoleAsync(eventId, request.MarshalId, request.Role, scopeConfigs, claims.PersonId);

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

            // Remove area lead role if this was an AreaLead contact linked to a marshal
            if (contact.Role == Constants.ContactRoleAreaLead && !string.IsNullOrEmpty(contact.MarshalId))
            {
                await RemoveAreaLeadRoleAsync(eventId, contact.MarshalId);
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
            // Build marshal context and checkpoint lookup together (avoids duplicate location fetch)
            (ScopeEvaluator.MarshalContext? marshalContext, Dictionary<string, LocationEntity> checkpointLookup) =
                await BuildMarshalContextWithCheckpointsAsync(eventId, marshalId);

            if (marshalContext == null)
            {
                return new NotFoundObjectResult(new { message = Constants.ErrorMarshalNotFound });
            }

            // Get all contacts for the event
            IEnumerable<EventContactEntity> allContacts = await _contactRepository.GetByEventAsync(eventId);

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

    /// <summary>
    /// Builds marshal context and checkpoint lookup together to avoid duplicate location fetches.
    /// </summary>
    private async Task<(ScopeEvaluator.MarshalContext?, Dictionary<string, LocationEntity>)> BuildMarshalContextWithCheckpointsAsync(string eventId, string marshalId)
    {
        MarshalEntity? marshal = await _marshalRepository.GetAsync(eventId, marshalId);
        if (marshal == null)
        {
            return (null, new Dictionary<string, LocationEntity>());
        }

        // Get marshal's assignments
        IEnumerable<AssignmentEntity> assignments = await _assignmentRepository.GetByMarshalAsync(eventId, marshalId);
        List<string> assignedLocationIds = assignments.Select(a => a.LocationId).ToList();

        // Get checkpoints ONCE (will be reused for both context building and scope evaluation)
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

        ScopeEvaluator.MarshalContext context = new(
            MarshalId: marshalId,
            AssignedAreaIds: assignedAreaIds.ToList(),
            AssignedLocationIds: assignedLocationIds,
            AreaLeadForAreaIds: areaLeadForAreaIds
        );

        return (context, checkpointLookup);
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

    /// <summary>
    /// Synchronizes the EventAreaLead role for a marshal based on their contact role.
    /// If the contact has the AreaLead role and is linked to a marshal, the marshal gets promoted.
    /// If the contact role changes or is deleted, the marshal is demoted.
    /// </summary>
    private async Task SyncAreaLeadRoleAsync(
        string eventId,
        string? marshalId,
        string contactRole,
        List<ScopeConfiguration> scopeConfigs,
        string grantedByPersonId)
    {
        if (string.IsNullOrEmpty(marshalId))
        {
            return; // No marshal linked, nothing to do
        }

        // Get the marshal to find their PersonId
        MarshalEntity? marshal = await _marshalRepository.GetAsync(eventId, marshalId);
        if (marshal == null || string.IsNullOrEmpty(marshal.PersonId))
        {
            return;
        }

        string personId = marshal.PersonId;

        // Find existing area lead role for this person/event
        IEnumerable<EventRoleEntity> existingRoles = await _eventRoleRepository.GetByPersonAndEventAsync(personId, eventId);
        EventRoleEntity? existingAreaLeadRole = existingRoles.FirstOrDefault(r => r.Role == Constants.RoleEventAreaLead);

        if (contactRole == Constants.ContactRoleAreaLead)
        {
            // Extract area IDs from scope configurations
            List<string> areaIds = [];
            foreach (ScopeConfiguration config in scopeConfigs)
            {
                if (config.ItemType == "Area" && config.Ids != null)
                {
                    // If ALL_AREAS, get all area IDs for the event
                    if (config.Ids.Contains(Constants.AllAreas))
                    {
                        IEnumerable<AreaEntity> allAreas = await _areaRepository.GetByEventAsync(eventId);
                        areaIds.AddRange(allAreas.Select(a => a.RowKey));
                    }
                    else
                    {
                        areaIds.AddRange(config.Ids);
                    }
                }
            }

            // Remove duplicates
            areaIds = areaIds.Distinct().ToList();

            if (areaIds.Count == 0)
            {
                // No areas specified, cannot be an area lead without areas
                // Remove existing role if any
                if (existingAreaLeadRole != null)
                {
                    await _eventRoleRepository.DeleteAsync(personId, existingAreaLeadRole.RowKey);
                    _logger.LogInformation("Removed EventAreaLead role for person {PersonId} in event {EventId} (no areas specified)", personId, eventId);
                }
                return;
            }

            if (existingAreaLeadRole != null)
            {
                // Update existing role with new area IDs
                existingAreaLeadRole.AreaIdsJson = JsonSerializer.Serialize(areaIds);
                await _eventRoleRepository.UpdateAsync(existingAreaLeadRole);
                _logger.LogInformation("Updated EventAreaLead role for person {PersonId} in event {EventId} with areas: {AreaIds}", personId, eventId, string.Join(", ", areaIds));
            }
            else
            {
                // Create new area lead role
                string roleId = Guid.NewGuid().ToString();
                EventRoleEntity newRole = new EventRoleEntity
                {
                    PartitionKey = personId,
                    RowKey = EventRoleEntity.CreateRowKey(eventId, roleId),
                    PersonId = personId,
                    EventId = eventId,
                    Role = Constants.RoleEventAreaLead,
                    AreaIdsJson = JsonSerializer.Serialize(areaIds),
                    GrantedByPersonId = grantedByPersonId,
                    GrantedAt = DateTime.UtcNow
                };
                await _eventRoleRepository.AddAsync(newRole);
                _logger.LogInformation("Granted EventAreaLead role to person {PersonId} in event {EventId} for areas: {AreaIds}", personId, eventId, string.Join(", ", areaIds));
            }
        }
        else
        {
            // Contact role is not AreaLead, remove any existing area lead role
            if (existingAreaLeadRole != null)
            {
                await _eventRoleRepository.DeleteAsync(personId, existingAreaLeadRole.RowKey);
                _logger.LogInformation("Removed EventAreaLead role for person {PersonId} in event {EventId} (contact role changed)", personId, eventId);
            }
        }
    }

    /// <summary>
    /// Removes area lead role when a contact is deleted.
    /// </summary>
    private async Task RemoveAreaLeadRoleAsync(string eventId, string? marshalId)
    {
        if (string.IsNullOrEmpty(marshalId))
        {
            return;
        }

        MarshalEntity? marshal = await _marshalRepository.GetAsync(eventId, marshalId);
        if (marshal == null || string.IsNullOrEmpty(marshal.PersonId))
        {
            return;
        }

        string personId = marshal.PersonId;

        // Find and delete existing area lead role
        IEnumerable<EventRoleEntity> existingRoles = await _eventRoleRepository.GetByPersonAndEventAsync(personId, eventId);
        EventRoleEntity? existingAreaLeadRole = existingRoles.FirstOrDefault(r => r.Role == Constants.RoleEventAreaLead);

        if (existingAreaLeadRole != null)
        {
            await _eventRoleRepository.DeleteAsync(personId, existingAreaLeadRole.RowKey);
            _logger.LogInformation("Removed EventAreaLead role for person {PersonId} in event {EventId} (contact deleted)", personId, eventId);
        }
    }

    #endregion
}
