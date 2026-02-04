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
    private readonly IEventRoleDefinitionRepository _roleDefinitionRepository;
    private readonly ClaimsService _claimsService;

    // Built-in contact roles
    private static readonly List<string> _builtInRoles =
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
        IEventRoleDefinitionRepository roleDefinitionRepository,
        ClaimsService claimsService)
    {
        _logger = logger;
        _contactRepository = contactRepository;
        _locationRepository = locationRepository;
        _marshalRepository = marshalRepository;
        _assignmentRepository = assignmentRepository;
        _areaRepository = areaRepository;
        _eventRoleRepository = eventRoleRepository;
        _roleDefinitionRepository = roleDefinitionRepository;
        _claimsService = claimsService;
    }

    /// <summary>
    /// Create a new contact for an event.
    /// Only event admins can create contacts.
    /// </summary>
#pragma warning disable MA0051
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
            CreateEventContactRequest? request = JsonSerializer.Deserialize<CreateEventContactRequest>(requestBody, FunctionHelpers.JsonOptions);

            if (request == null || string.IsNullOrWhiteSpace(request.Name) || request.Roles == null || request.Roles.Count == 0)
            {
                return new BadRequestObjectResult(new { message = "Name and at least one role are required" });
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
                Name = request.Name,
                Phone = request.Phone,
                Email = request.Email,
                Notes = request.Notes,
                MarshalId = request.MarshalId,
                ScopeConfigurationsJson = JsonSerializer.Serialize(scopeConfigs),
                DisplayOrder = request.DisplayOrder,
                IsPinned = request.IsPinned,
                ShowInEmergencyInfo = request.ShowInEmergencyInfo,
                CreatedByPersonId = claims.PersonId,
                CreatedAt = DateTime.UtcNow,
                IsDeleted = false
            };
            SetRolesOnEntity(contact, request.Roles);

            await _contactRepository.AddAsync(contact);

            // Sync roles to the linked marshal if applicable
            await SyncRolesToLinkedMarshalAsync(eventId, request.MarshalId, request.Roles);

            // Sync phone/email to the linked marshal if applicable
            await SyncContactDetailsToLinkedMarshalAsync(eventId, request.MarshalId, request.Phone, request.Email);

            // Sync area lead role if this contact is linked to a marshal and has AreaLead role
            await SyncAreaLeadRoleAsync(eventId, request.MarshalId, request.Roles, scopeConfigs, claims.PersonId);

            _logger.LogInformation("Contact {ContactId} created for event {EventId} by {PersonId}", contactId, eventId, claims.PersonId);

            return new OkObjectResult(ToContactResponse(contact, linkedMarshal?.Name));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating contact for event {EventId}", eventId);
            return new StatusCodeResult(500);
        }
    }
#pragma warning restore MA0051

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

            List<EventContactEntity> contacts = (await _contactRepository.GetByEventAsync(eventId)).ToList();

            // Lazy migration: normalize DisplayOrder if any contacts have DisplayOrder = 0
            await NormalizeContactDisplayOrdersAsync(contacts, eventId);

            // Get marshal names for linked contacts
            IEnumerable<MarshalEntity> marshals = await _marshalRepository.GetByEventAsync(eventId);
            Dictionary<string, string> marshalNameLookup = marshals.ToDictionary(m => m.MarshalId, m => m.Name);

            List<EventContactResponse> responses = [.. contacts.Select(c =>
                ToContactResponse(c, c.MarshalId != null && marshalNameLookup.TryGetValue(c.MarshalId, out string? name) ? name : null)
            )];

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
#pragma warning disable MA0051
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
            List<string> oldRoles = GetRolesFromEntity(contact);

            // Parse request
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            UpdateEventContactRequest? request = JsonSerializer.Deserialize<UpdateEventContactRequest>(requestBody, FunctionHelpers.JsonOptions);

            if (request == null || string.IsNullOrWhiteSpace(request.Name) || request.Roles == null || request.Roles.Count == 0)
            {
                return new BadRequestObjectResult(new { message = "Name and at least one role are required" });
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
            SetRolesOnEntity(contact, request.Roles);
            contact.Name = request.Name;
            contact.Phone = request.Phone;
            contact.Email = request.Email;
            contact.Notes = request.Notes;
            contact.MarshalId = request.MarshalId;
            contact.ScopeConfigurationsJson = JsonSerializer.Serialize(request.ScopeConfigurations ?? []);
            contact.DisplayOrder = request.DisplayOrder;
            contact.IsPinned = request.IsPinned;
            contact.ShowInEmergencyInfo = request.ShowInEmergencyInfo;
            contact.UpdatedAt = DateTime.UtcNow;

            await _contactRepository.UpdateAsync(contact);

            // Sync roles to the linked marshal if applicable
            await SyncRolesToLinkedMarshalAsync(eventId, request.MarshalId, request.Roles);

            // Sync phone/email to the linked marshal if applicable
            await SyncContactDetailsToLinkedMarshalAsync(eventId, request.MarshalId, request.Phone, request.Email);

            // Handle area lead role changes
            List<ScopeConfiguration> scopeConfigs = request.ScopeConfigurations ?? [];

            // If marshal changed, remove role from old marshal first
            if (oldMarshalId != request.MarshalId && !string.IsNullOrEmpty(oldMarshalId) && oldRoles.Contains(Constants.ContactRoleAreaLead))
            {
                await RemoveAreaLeadRoleAsync(eventId, oldMarshalId);
            }

            // Sync area lead role for the current marshal
            await SyncAreaLeadRoleAsync(eventId, request.MarshalId, request.Roles, scopeConfigs, claims.PersonId);

            _logger.LogInformation("Contact {ContactId} updated for event {EventId} by {PersonId}", contactId, eventId, claims.PersonId);

            return new OkObjectResult(ToContactResponse(contact, linkedMarshal?.Name));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating contact {ContactId} for event {EventId}", contactId, eventId);
            return new StatusCodeResult(500);
        }
    }
#pragma warning restore MA0051

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
            List<string> roles = GetRolesFromEntity(contact);
            if (roles.Contains(Constants.ContactRoleAreaLead) && !string.IsNullOrEmpty(contact.MarshalId))
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
#pragma warning disable MA0051 // Method is too long - complex endpoint with scope evaluation
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

            // Load role definitions to resolve role IDs to names
            IEnumerable<EventRoleDefinitionEntity> roleDefinitions = await _roleDefinitionRepository.GetByEventAsync(eventId);
            Dictionary<string, string> roleIdToName = roleDefinitions.ToDictionary(r => r.RoleId, r => r.Name);

            // Filter contacts based on scope
            List<EventContactForMarshalResponse> relevantContacts = [];
            foreach (EventContactEntity contact in allContacts)
            {
                List<ScopeConfiguration> configs = JsonSerializer.Deserialize<List<ScopeConfiguration>>(
                    contact.ScopeConfigurationsJson, FunctionHelpers.JsonOptions) ?? [];

                ScopeEvaluator.ScopeMatchResult result = ScopeEvaluator.EvaluateScopeConfigurations(
                    configs, marshalContext, checkpointLookup);

                if (result.IsRelevant)
                {
                    List<string> rawRoles = GetRolesFromEntity(contact);
                    List<string> resolvedRoles = ResolveRoleNames(rawRoles, roleIdToName);

                    relevantContacts.Add(new EventContactForMarshalResponse(
                        ContactId: contact.ContactId,
                        Roles: resolvedRoles,
                        Name: contact.Name,
                        Phone: contact.Phone,
                        Email: contact.Email,
                        Notes: contact.Notes,
                        DisplayOrder: contact.DisplayOrder,
                        IsPinned: contact.IsPinned,
                        ShowInEmergencyInfo: contact.ShowInEmergencyInfo,
                        MatchedScope: result.WinningConfig?.Scope ?? string.Empty
                    ));
                }
            }

            // Sort: pinned first, then by display order, then by name
            relevantContacts = [.. relevantContacts
                .OrderByDescending(c => c.IsPinned)
                .ThenBy(c => c.DisplayOrder)
                .ThenBy(c => c.Name)];

            return new OkObjectResult(relevantContacts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting contacts for marshal {MarshalId} in event {EventId}", marshalId, eventId);
            return new StatusCodeResult(500);
        }
    }
#pragma warning restore MA0051

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

                    // Load role definitions to resolve role IDs to names
                    IEnumerable<EventRoleDefinitionEntity> roleDefinitions = await _roleDefinitionRepository.GetByEventAsync(eventId);
                    Dictionary<string, string> roleIdToName = roleDefinitions.ToDictionary(r => r.RoleId, r => r.Name);

                    List<EventContactForMarshalResponse> allContactsResponse = [.. allContacts
                        .OrderByDescending(c => c.IsPinned)
                        .ThenBy(c => c.DisplayOrder)
                        .ThenBy(c => c.Name)
                        .Select(c =>
                        new EventContactForMarshalResponse(
                            ContactId: c.ContactId,
                            Roles: ResolveRoleNames(GetRolesFromEntity(c), roleIdToName),
                            Name: c.Name,
                            Phone: c.Phone,
                            Email: c.Email,
                            Notes: c.Notes,
                            DisplayOrder: c.DisplayOrder,
                            IsPinned: c.IsPinned,
                            ShowInEmergencyInfo: c.ShowInEmergencyInfo,
                            MatchedScope: "Admin"
                        )
                    )];
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
            List<string> customRoles = [.. contacts
                .SelectMany(c => GetRolesFromEntity(c))
                .Where(r => !string.IsNullOrEmpty(r) && !_builtInRoles.Contains(r))
                .Distinct()
                .OrderBy(r => r)];

            return new OkObjectResult(new ContactRolesResponse(_builtInRoles, customRoles));
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
        string? sessionToken = FunctionHelpers.GetSessionToken(req);

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
        List<string> assignedLocationIds = [.. assignments.Select(a => a.LocationId)];

        // Get checkpoints ONCE (will be reused for both context building and scope evaluation)
        IEnumerable<LocationEntity> checkpoints = await _locationRepository.GetByEventAsync(eventId);
        Dictionary<string, LocationEntity> checkpointLookup = checkpoints.ToDictionary(c => c.RowKey);

        // Get areas from assigned checkpoints
        HashSet<string> assignedAreaIds = [];
        foreach (string locationId in assignedLocationIds)
        {
            if (checkpointLookup.TryGetValue(locationId, out LocationEntity? checkpoint))
            {
                List<string> areaIds = checkpoint.GetPayload().AreaIds;
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
            List<AreaContact> contacts = area.GetPayload().Contacts;
            if (contacts.Any(c => c.MarshalId == marshalId && c.Role == "Leader"))
            {
                areaLeadForAreaIds.Add(area.RowKey);
            }
        }

        ScopeEvaluator.MarshalContext context = new(
            MarshalId: marshalId,
            AssignedAreaIds: [.. assignedAreaIds],
            AssignedLocationIds: assignedLocationIds,
            AreaLeadForAreaIds: areaLeadForAreaIds
        );

        return (context, checkpointLookup);
    }

    private static EventContactResponse ToContactResponse(EventContactEntity contact, string? marshalName)
    {
        List<ScopeConfiguration> configs = JsonSerializer.Deserialize<List<ScopeConfiguration>>(
            contact.ScopeConfigurationsJson, FunctionHelpers.JsonOptions) ?? [];

        return new EventContactResponse(
            ContactId: contact.ContactId,
            EventId: contact.EventId,
            Roles: GetRolesFromEntity(contact),
            Name: contact.Name,
            Phone: contact.Phone,
            Email: contact.Email,
            Notes: contact.Notes,
            MarshalId: contact.MarshalId,
            MarshalName: marshalName,
            ScopeConfigurations: configs,
            DisplayOrder: contact.DisplayOrder,
            IsPinned: contact.IsPinned,
            ShowInEmergencyInfo: contact.ShowInEmergencyInfo,
            CreatedAt: contact.CreatedAt,
            UpdatedAt: contact.UpdatedAt
        );
    }

    /// <summary>
    /// Gets roles from entity, handling migration from single Role to Roles array.
    /// Reads from RolesJson if available, falls back to single Role field.
    /// </summary>
    private static List<string> GetRolesFromEntity(EventContactEntity contact)
    {
        // Try to read from RolesJson first (new format)
        if (!string.IsNullOrEmpty(contact.RolesJson) && contact.RolesJson != "[]")
        {
            List<string>? roles = JsonSerializer.Deserialize<List<string>>(contact.RolesJson, FunctionHelpers.JsonOptions);
            if (roles != null && roles.Count > 0)
            {
                return roles;
            }
        }

        // Fall back to single Role field (migration support)
        if (!string.IsNullOrEmpty(contact.Role))
        {
            return [contact.Role];
        }

        return [];
    }

    /// <summary>
    /// Resolves role IDs to role names using role definitions lookup.
    /// Built-in roles (non-GUIDs) are returned as-is.
    /// </summary>
    private static List<string> ResolveRoleNames(List<string> roleIds, Dictionary<string, string> roleIdToName)
    {
        List<string> resolved = [];
        foreach (string roleId in roleIds)
        {
            // If it's a GUID and we have a mapping, resolve it
            if (Guid.TryParse(roleId, out _) && roleIdToName.TryGetValue(roleId, out string? roleName))
            {
                resolved.Add(roleName);
            }
            else
            {
                // Built-in role or unknown - return as-is
                resolved.Add(roleId);
            }
        }
        return resolved;
    }

    /// <summary>
    /// Sets roles on entity, updating both RolesJson and legacy Role field.
    /// </summary>
    private static void SetRolesOnEntity(EventContactEntity contact, List<string> roles)
    {
        contact.RolesJson = JsonSerializer.Serialize(roles);
        // Keep legacy Role field populated with first role for backwards compatibility
        contact.Role = roles.Count > 0 ? roles[0] : string.Empty;
    }

    /// <summary>
    /// Synchronizes the EventAreaLead role for a marshal based on their contact roles.
    /// If the contact has a role with CanManageAreaCheckpoints=true and is linked to a marshal, the marshal gets promoted.
    /// If the contact roles change or is deleted, the marshal is demoted.
    /// </summary>
#pragma warning disable MA0051
    private async Task SyncAreaLeadRoleAsync(
        string eventId,
        string? marshalId,
        List<string> contactRoles,
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

        // Check if any of the contact's roles have CanManageAreaCheckpoints=true
        bool hasAreaManageRole = await HasRoleWithAreaManagePermissionAsync(eventId, contactRoles);

        if (hasAreaManageRole)
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
            areaIds = [.. areaIds.Distinct()];

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
            // Contact doesn't have a role with area manage permission, remove any existing area lead role
            if (existingAreaLeadRole != null)
            {
                await _eventRoleRepository.DeleteAsync(personId, existingAreaLeadRole.RowKey);
                _logger.LogInformation("Removed EventAreaLead role for person {PersonId} in event {EventId} (contact role changed)", personId, eventId);
            }
        }
    }
#pragma warning restore MA0051

    /// <summary>
    /// Checks if any of the given role IDs correspond to a role definition with CanManageAreaCheckpoints=true.
    /// Also supports legacy role names for backwards compatibility.
    /// </summary>
    private async Task<bool> HasRoleWithAreaManagePermissionAsync(string eventId, List<string> roleIds)
    {
        if (roleIds.Count == 0)
        {
            return false;
        }

        // Get all role definitions for this event
        IEnumerable<EventRoleDefinitionEntity> roleDefinitions = await _roleDefinitionRepository.GetByEventAsync(eventId);
        List<EventRoleDefinitionEntity> roleDefList = [.. roleDefinitions];

        // Check each role ID
        foreach (string roleId in roleIds)
        {
            // Check by role ID (GUID)
            EventRoleDefinitionEntity? matchByGuid = roleDefList.FirstOrDefault(rd => rd.RoleId == roleId);
            if (matchByGuid != null && matchByGuid.CanManageAreaCheckpoints)
            {
                return true;
            }

            // For backwards compatibility, also check if this is a legacy role name
            // (e.g., "AreaLead" string from before migration)
            if (!Guid.TryParse(roleId, out _))
            {
                EventRoleDefinitionEntity? matchByName = roleDefList.FirstOrDefault(rd =>
                    rd.Name.Equals(roleId, StringComparison.OrdinalIgnoreCase) ||
                    roleId.Equals(Constants.ContactRoleAreaLead, StringComparison.OrdinalIgnoreCase));
                if (matchByName != null && matchByName.CanManageAreaCheckpoints)
                {
                    return true;
                }
            }
        }

        return false;
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

    /// <summary>
    /// Syncs roles from a contact to its linked marshal.
    /// When a contact has roles and is linked to a marshal, the marshal's roles are updated to match.
    /// </summary>
    private async Task SyncRolesToLinkedMarshalAsync(string eventId, string? marshalId, List<string> roles)
    {
        if (string.IsNullOrEmpty(marshalId))
        {
            return; // No marshal linked, nothing to do
        }

        MarshalEntity? marshal = await _marshalRepository.GetAsync(eventId, marshalId);
        if (marshal == null)
        {
            return;
        }

        // Update the marshal's roles to match the contact's roles
        marshal.RolesJson = JsonSerializer.Serialize(roles);
        await _marshalRepository.UpdateAsync(marshal);

        _logger.LogInformation("Synced roles from contact to linked marshal {MarshalId} in event {EventId}: {Roles}",
            marshalId, eventId, string.Join(", ", roles));
    }

    /// <summary>
    /// Syncs phone and email from a contact to its linked marshal.
    /// When a contact has phone/email and is linked to a marshal, the marshal's phone/email are updated to match.
    /// </summary>
    private async Task SyncContactDetailsToLinkedMarshalAsync(string eventId, string? marshalId, string? phone, string? email)
    {
        if (string.IsNullOrEmpty(marshalId))
        {
            return; // No marshal linked, nothing to do
        }

        MarshalEntity? marshal = await _marshalRepository.GetAsync(eventId, marshalId);
        if (marshal == null)
        {
            return;
        }

        // Update the marshal's phone and email to match the contact's
        marshal.PhoneNumber = phone ?? string.Empty;
        marshal.Email = email ?? string.Empty;
        await _marshalRepository.UpdateAsync(marshal);

        _logger.LogInformation("Synced contact details to linked marshal {MarshalId} in event {EventId}", marshalId, eventId);
    }

    #endregion

    /// <summary>
    /// Reorder contacts by updating their display order.
    /// Only event admins can reorder contacts.
    /// </summary>
    [Function("ReorderContacts")]
    public async Task<IActionResult> ReorderContacts(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "events/{eventId}/contacts/reorder")] HttpRequest req,
        string eventId)
    {
        try
        {
            // Authenticate - only admins can reorder
            UserClaims? claims = await GetClaimsAsync(req, eventId);
            if (claims == null || !claims.IsEventAdmin)
            {
                return new UnauthorizedObjectResult(new { message = "Only event admins can reorder contacts" });
            }

            // Parse request
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            ReorderContactsRequest? request = JsonSerializer.Deserialize<ReorderContactsRequest>(requestBody, FunctionHelpers.JsonOptions);

            if (request == null || request.Items == null || request.Items.Count == 0)
            {
                return new BadRequestObjectResult(new { message = "Items array is required" });
            }

            // Build display order map
            Dictionary<string, int> displayOrders = request.Items.ToDictionary(i => i.Id, i => i.DisplayOrder);

            // Update all contacts
            await _contactRepository.UpdateDisplayOrdersAsync(eventId, displayOrders);

            _logger.LogInformation("Contacts reordered for event {EventId} by {PersonId}", eventId, claims.PersonId);

            return new NoContentResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reordering contacts for event {EventId}", eventId);
            return FunctionHelpers.CreateErrorResponse(ex, "Failed to reorder contacts");
        }
    }

    /// <summary>
    /// Lazy migration: if any contacts have DisplayOrder = 0, normalize all display orders.
    /// Sorts by pinned first, then primary, then by name, and assigns sequential orders.
    /// </summary>
    private async Task NormalizeContactDisplayOrdersAsync(List<EventContactEntity> contacts, string eventId)
    {
        // Check if any contacts need migration (DisplayOrder = 0)
        bool needsMigration = contacts.Any(c => c.DisplayOrder == 0);
        if (!needsMigration || contacts.Count == 0)
        {
            return;
        }

        _logger.LogInformation("Normalizing DisplayOrder for {Count} contacts in event {EventId}",
            contacts.Count, eventId);

        // Sort: pinned first, then primary, then alphabetically by name
        List<EventContactEntity> sortedContacts = [.. contacts
            .OrderByDescending(c => c.IsPinned)
            .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)];

        // Assign sequential display orders
        Dictionary<string, int> changes = [];
        for (int i = 0; i < sortedContacts.Count; i++)
        {
            int newOrder = i + 1;
            if (sortedContacts[i].DisplayOrder != newOrder)
            {
                sortedContacts[i].DisplayOrder = newOrder;
                changes[sortedContacts[i].ContactId] = newOrder;
            }
        }

        // Batch update if there are changes
        if (changes.Count > 0)
        {
            try
            {
                await _contactRepository.UpdateDisplayOrdersAsync(eventId, changes);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to persist DisplayOrder migration for contacts");
                // Continue anyway - the in-memory list is already updated
            }
        }
    }
}
