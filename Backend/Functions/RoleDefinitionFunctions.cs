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
/// Azure Functions for managing event role definitions.
/// Role definitions allow admins to create custom roles with notes/descriptions.
/// These roles can then be assigned to marshals and contacts.
/// </summary>
public class RoleDefinitionFunctions
{
    private readonly ILogger<RoleDefinitionFunctions> _logger;
    private readonly IEventRoleDefinitionRepository _roleDefinitionRepository;
    private readonly IMarshalRepository _marshalRepository;
    private readonly IEventContactRepository _contactRepository;
    private readonly IEventRoleRepository _eventRoleRepository;
    private readonly IAreaRepository _areaRepository;
    private readonly ClaimsService _claimsService;

    // Default roles that are auto-created for new events
    // (Name, IsBuiltIn, CanManageAreaCheckpoints)
    private static readonly List<(string Name, bool IsBuiltIn, bool CanManageAreaCheckpoints)> _defaultRoles =
    [
        ("Event director", true, false),
        ("Emergency contact", true, false),
        ("Safety officer", true, false),
        ("Medical lead", true, false),
        ("Logistics", true, false),
        ("Area lead", true, true)  // Area leads can manage checkpoints in their areas
    ];

    public RoleDefinitionFunctions(
        ILogger<RoleDefinitionFunctions> logger,
        IEventRoleDefinitionRepository roleDefinitionRepository,
        IMarshalRepository marshalRepository,
        IEventContactRepository contactRepository,
        IEventRoleRepository eventRoleRepository,
        IAreaRepository areaRepository,
        ClaimsService claimsService)
    {
        _logger = logger;
        _roleDefinitionRepository = roleDefinitionRepository;
        _marshalRepository = marshalRepository;
        _contactRepository = contactRepository;
        _eventRoleRepository = eventRoleRepository;
        _areaRepository = areaRepository;
        _claimsService = claimsService;
    }

    /// <summary>
    /// Get all role definitions for an event.
    /// On first call, auto-migrates existing roles from contacts to role definitions.
    /// </summary>
    [Function("GetRoleDefinitions")]
    public async Task<IActionResult> GetRoleDefinitions(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "events/{eventId}/role-definitions")] HttpRequest req,
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

            // Get existing role definitions
            IEnumerable<EventRoleDefinitionEntity> roleDefinitions = await _roleDefinitionRepository.GetByEventAsync(eventId);
            List<EventRoleDefinitionEntity> roleList = [.. roleDefinitions];

            // Auto-migrate if no role definitions exist yet
            if (roleList.Count == 0)
            {
                await MigrateExistingRolesAsync(eventId, claims.PersonId);
                roleDefinitions = await _roleDefinitionRepository.GetByEventAsync(eventId);
                roleList = [.. roleDefinitions];
            }

            // Calculate usage counts
            IEnumerable<MarshalEntity> marshals = await _marshalRepository.GetByEventAsync(eventId);
            IEnumerable<EventContactEntity> contacts = await _contactRepository.GetByEventAsync(eventId);

            List<RoleDefinitionResponse> responses = [];
            foreach (EventRoleDefinitionEntity role in roleList)
            {
                int usageCount = CountRoleUsage(role.RoleId, role.Name, marshals, contacts);
                responses.Add(ToRoleDefinitionResponse(role, usageCount));
            }

            return new OkObjectResult(responses);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting role definitions for event {EventId}", eventId);
            return new StatusCodeResult(500);
        }
    }

    /// <summary>
    /// Create a new role definition.
    /// </summary>
    [Function("CreateRoleDefinition")]
    public async Task<IActionResult> CreateRoleDefinition(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "events/{eventId}/role-definitions")] HttpRequest req,
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

            // Parse request
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            CreateRoleDefinitionRequest? request = JsonSerializer.Deserialize<CreateRoleDefinitionRequest>(requestBody, FunctionHelpers.JsonOptions);

            if (request == null || string.IsNullOrWhiteSpace(request.Name))
            {
                return new BadRequestObjectResult(new { message = "Name is required" });
            }

            // Check for duplicate name
            EventRoleDefinitionEntity? existing = await _roleDefinitionRepository.GetByNameAsync(eventId, request.Name);
            if (existing != null)
            {
                return new ConflictObjectResult(new { message = "A role with this name already exists" });
            }

            // Get max display order
            IEnumerable<EventRoleDefinitionEntity> allRoles = await _roleDefinitionRepository.GetByEventAsync(eventId);
            int maxDisplayOrder = allRoles.Any() ? allRoles.Max(r => r.DisplayOrder) : 0;

            string roleId = Guid.NewGuid().ToString();
            EventRoleDefinitionEntity roleDefinition = new EventRoleDefinitionEntity
            {
                PartitionKey = eventId,
                RowKey = roleId,
                EventId = eventId,
                RoleId = roleId,
                Name = request.Name,
                Notes = request.Notes ?? string.Empty,
                IsBuiltIn = false,
                CanManageAreaCheckpoints = request.CanManageAreaCheckpoints,
                DisplayOrder = maxDisplayOrder + 1,
                CreatedByPersonId = claims.PersonId,
                CreatedAt = DateTime.UtcNow,
                IsDeleted = false
            };

            await _roleDefinitionRepository.AddAsync(roleDefinition);

            _logger.LogInformation("Role definition {RoleId} created for event {EventId} by {PersonId}", roleId, eventId, claims.PersonId);

            return new OkObjectResult(ToRoleDefinitionResponse(roleDefinition, 0));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating role definition for event {EventId}", eventId);
            return new StatusCodeResult(500);
        }
    }

    /// <summary>
    /// Update an existing role definition.
    /// </summary>
#pragma warning disable MA0051
    [Function("UpdateRoleDefinition")]
    public async Task<IActionResult> UpdateRoleDefinition(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "events/{eventId}/role-definitions/{roleId}")] HttpRequest req,
        string eventId,
        string roleId)
    {
        try
        {
            // Authenticate
            UserClaims? claims = await GetClaimsAsync(req, eventId);
            if (claims == null || !claims.IsEventAdmin)
            {
                return new UnauthorizedObjectResult(new { message = Constants.ErrorNotAuthorized });
            }

            EventRoleDefinitionEntity? roleDefinition = await _roleDefinitionRepository.GetAsync(eventId, roleId);
            if (roleDefinition == null)
            {
                return new NotFoundObjectResult(new { message = "Role definition not found" });
            }

            // Parse request
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            UpdateRoleDefinitionRequest? request = JsonSerializer.Deserialize<UpdateRoleDefinitionRequest>(requestBody, FunctionHelpers.JsonOptions);

            if (request == null || string.IsNullOrWhiteSpace(request.Name))
            {
                return new BadRequestObjectResult(new { message = "Name is required" });
            }

            // Check for duplicate name (excluding current role)
            EventRoleDefinitionEntity? existing = await _roleDefinitionRepository.GetByNameAsync(eventId, request.Name);
            if (existing != null && existing.RoleId != roleId)
            {
                return new ConflictObjectResult(new { message = "A role with this name already exists" });
            }

            // Track if CanManageAreaCheckpoints changed
            bool permissionChanged = roleDefinition.CanManageAreaCheckpoints != request.CanManageAreaCheckpoints;

            // Update role definition
            roleDefinition.Name = request.Name;
            roleDefinition.Notes = request.Notes ?? string.Empty;
            roleDefinition.CanManageAreaCheckpoints = request.CanManageAreaCheckpoints;
            roleDefinition.UpdatedAt = DateTime.UtcNow;

            await _roleDefinitionRepository.UpdateAsync(roleDefinition);

            _logger.LogInformation("Role definition {RoleId} updated for event {EventId} by {PersonId}", roleId, eventId, claims.PersonId);

            // If CanManageAreaCheckpoints changed, re-sync permissions for all contacts with this role
            if (permissionChanged)
            {
                await ResyncAreaLeadPermissionsForRoleAsync(eventId, roleId, claims.PersonId);
            }

            // Calculate usage count
            IEnumerable<MarshalEntity> marshals = await _marshalRepository.GetByEventAsync(eventId);
            IEnumerable<EventContactEntity> contacts = await _contactRepository.GetByEventAsync(eventId);
            int usageCount = CountRoleUsage(roleId, roleDefinition.Name, marshals, contacts);

            return new OkObjectResult(ToRoleDefinitionResponse(roleDefinition, usageCount));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating role definition {RoleId} for event {EventId}", roleId, eventId);
            return new StatusCodeResult(500);
        }
    }
#pragma warning restore MA0051

    /// <summary>
    /// Delete a role definition.
    /// If the role is assigned to people, they will be unassigned first.
    /// </summary>
    [Function("DeleteRoleDefinition")]
#pragma warning disable MA0051 // Method handles cleanup of related entities before deletion
    public async Task<IActionResult> DeleteRoleDefinition(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "events/{eventId}/role-definitions/{roleId}")] HttpRequest req,
        string eventId,
        string roleId)
    {
        try
        {
            // Authenticate
            UserClaims? claims = await GetClaimsAsync(req, eventId);
            if (claims == null || !claims.IsEventAdmin)
            {
                return new UnauthorizedObjectResult(new { message = Constants.ErrorNotAuthorized });
            }

            EventRoleDefinitionEntity? roleDefinition = await _roleDefinitionRepository.GetAsync(eventId, roleId);
            if (roleDefinition == null)
            {
                return new NotFoundObjectResult(new { message = "Role definition not found" });
            }

            // Remove the role from all marshals and contacts that have it
            IEnumerable<MarshalEntity> marshals = await _marshalRepository.GetByEventAsync(eventId);
            IEnumerable<EventContactEntity> contacts = await _contactRepository.GetByEventAsync(eventId);

            int unassignedCount = 0;

            // Track which marshals had the role removed (for linked contact deduplication)
            HashSet<string> unassignedMarshalIds = [];

            // Remove from marshals
            foreach (MarshalEntity marshal in marshals)
            {
                List<string> roles = GetRolesFromMarshal(marshal);
                if (roles.Contains(roleId) || roles.Contains(roleDefinition.Name, StringComparer.OrdinalIgnoreCase))
                {
                    roles.Remove(roleId);
                    roles.RemoveAll(r => r.Equals(roleDefinition.Name, StringComparison.OrdinalIgnoreCase));
                    marshal.RolesJson = JsonSerializer.Serialize(roles);
                    await _marshalRepository.UpdateAsync(marshal);
                    unassignedCount++;
                    unassignedMarshalIds.Add(marshal.MarshalId);
                }
            }

            // Remove from contacts (avoid double-counting linked pairs)
            foreach (EventContactEntity contact in contacts)
            {
                List<string> roles = GetRolesFromContact(contact);
                if (roles.Contains(roleId) || roles.Contains(roleDefinition.Name, StringComparer.OrdinalIgnoreCase))
                {
                    roles.Remove(roleId);
                    roles.RemoveAll(r => r.Equals(roleDefinition.Name, StringComparison.OrdinalIgnoreCase));
                    SetRolesOnContact(contact, roles);
                    contact.UpdatedAt = DateTime.UtcNow;
                    await _contactRepository.UpdateAsync(contact);

                    // Only count if not linked to a marshal we already counted
                    if (string.IsNullOrEmpty(contact.MarshalId) || !unassignedMarshalIds.Contains(contact.MarshalId))
                    {
                        unassignedCount++;
                    }
                }
            }

            await _roleDefinitionRepository.DeleteAsync(eventId, roleId);

            _logger.LogInformation("Role definition {RoleId} deleted for event {EventId} by {PersonId}, unassigned from {UnassignedCount} people",
                roleId, eventId, claims.PersonId, unassignedCount);

            return new NoContentResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting role definition {RoleId} for event {EventId}", roleId, eventId);
            return new StatusCodeResult(500);
        }
    }
#pragma warning restore MA0051

    /// <summary>
    /// Get unified list of people (marshals and contacts) for a role definition.
    /// Deduplicates linked marshal-contact pairs.
    /// </summary>
    [Function("GetPeopleForRole")]
    public async Task<IActionResult> GetPeopleForRole(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "events/{eventId}/role-definitions/{roleId}/people")] HttpRequest req,
        string eventId,
        string roleId)
    {
        try
        {
            // Authenticate
            UserClaims? claims = await GetClaimsAsync(req, eventId);
            if (claims == null || !claims.IsEventAdmin)
            {
                return new UnauthorizedObjectResult(new { message = Constants.ErrorNotAuthorized });
            }

            EventRoleDefinitionEntity? roleDefinition = await _roleDefinitionRepository.GetAsync(eventId, roleId);
            if (roleDefinition == null)
            {
                return new NotFoundObjectResult(new { message = "Role definition not found" });
            }

            // Get all marshals and contacts
            IEnumerable<MarshalEntity> marshals = await _marshalRepository.GetByEventAsync(eventId);
            IEnumerable<EventContactEntity> contacts = await _contactRepository.GetByEventAsync(eventId);

            // Build unified list
            List<PersonWithRoleResponse> people = BuildUnifiedPersonList(roleId, roleDefinition.Name, marshals, contacts);

            // Sort alphabetically by name
            people = [.. people.OrderBy(p => p.Name, StringComparer.Create(System.Globalization.CultureInfo.CurrentCulture, System.Globalization.CompareOptions.IgnoreCase))];

            return new OkObjectResult(people);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting people for role {RoleId} in event {EventId}", roleId, eventId);
            return new StatusCodeResult(500);
        }
    }

    /// <summary>
    /// Update role assignments for a role definition.
    /// Adds/removes the role from marshals and contacts.
    /// </summary>
    [Function("UpdateRoleAssignments")]
#pragma warning disable MA0051 // Method handles multiple related operations atomically
    public async Task<IActionResult> UpdateRoleAssignments(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "events/{eventId}/role-definitions/{roleId}/people")] HttpRequest req,
        string eventId,
        string roleId)
    {
        try
        {
            // Authenticate
            UserClaims? claims = await GetClaimsAsync(req, eventId);
            if (claims == null || !claims.IsEventAdmin)
            {
                return new UnauthorizedObjectResult(new { message = Constants.ErrorNotAuthorized });
            }

            EventRoleDefinitionEntity? roleDefinition = await _roleDefinitionRepository.GetAsync(eventId, roleId);
            if (roleDefinition == null)
            {
                return new NotFoundObjectResult(new { message = "Role definition not found" });
            }

            // Parse request
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            UpdateRoleAssignmentsRequest? request = JsonSerializer.Deserialize<UpdateRoleAssignmentsRequest>(requestBody, FunctionHelpers.JsonOptions);

            if (request == null)
            {
                return new BadRequestObjectResult(new { message = "Invalid request" });
            }

            // Process marshal additions
            foreach (string marshalId in request.MarshalIdsToAdd ?? [])
            {
                MarshalEntity? marshal = await _marshalRepository.GetAsync(eventId, marshalId);
                if (marshal != null)
                {
                    List<string> roles = GetRolesFromMarshal(marshal);
                    if (!roles.Contains(roleId))
                    {
                        roles.Add(roleId);
                        marshal.RolesJson = JsonSerializer.Serialize(roles);
                        await _marshalRepository.UpdateAsync(marshal);

                        // Sync to linked contact if exists
                        await SyncRoleToLinkedContactAsync(eventId, marshalId, roleId, true);
                    }
                }
            }

            // Process marshal removals
            foreach (string marshalId in request.MarshalIdsToRemove ?? [])
            {
                MarshalEntity? marshal = await _marshalRepository.GetAsync(eventId, marshalId);
                if (marshal != null)
                {
                    List<string> roles = GetRolesFromMarshal(marshal);
                    if (roles.Contains(roleId))
                    {
                        roles.Remove(roleId);
                        marshal.RolesJson = JsonSerializer.Serialize(roles);
                        await _marshalRepository.UpdateAsync(marshal);

                        // Sync to linked contact if exists
                        await SyncRoleToLinkedContactAsync(eventId, marshalId, roleId, false);
                    }
                }
            }

            // Process contact additions
            foreach (string contactId in request.ContactIdsToAdd ?? [])
            {
                EventContactEntity? contact = await _contactRepository.GetAsync(eventId, contactId);
                if (contact != null)
                {
                    List<string> roles = GetRolesFromContact(contact);
                    if (!roles.Contains(roleId))
                    {
                        roles.Add(roleId);
                        SetRolesOnContact(contact, roles);
                        contact.UpdatedAt = DateTime.UtcNow;
                        await _contactRepository.UpdateAsync(contact);

                        // Sync to linked marshal if exists
                        await SyncRoleToLinkedMarshalAsync(eventId, contact.MarshalId, roleId, true);
                    }
                }
            }

            // Process contact removals
            foreach (string contactId in request.ContactIdsToRemove ?? [])
            {
                EventContactEntity? contact = await _contactRepository.GetAsync(eventId, contactId);
                if (contact != null)
                {
                    List<string> roles = GetRolesFromContact(contact);
                    if (roles.Contains(roleId))
                    {
                        roles.Remove(roleId);
                        SetRolesOnContact(contact, roles);
                        contact.UpdatedAt = DateTime.UtcNow;
                        await _contactRepository.UpdateAsync(contact);

                        // Sync to linked marshal if exists
                        await SyncRoleToLinkedMarshalAsync(eventId, contact.MarshalId, roleId, false);
                    }
                }
            }

            _logger.LogInformation("Role assignments updated for role {RoleId} in event {EventId} by {PersonId}", roleId, eventId, claims.PersonId);

            // Return updated people list
            IEnumerable<MarshalEntity> marshals = await _marshalRepository.GetByEventAsync(eventId);
            IEnumerable<EventContactEntity> contacts = await _contactRepository.GetByEventAsync(eventId);
            List<PersonWithRoleResponse> people = BuildUnifiedPersonList(roleId, roleDefinition.Name, marshals, contacts);
            people = [.. people.OrderBy(p => p.Name, StringComparer.Create(System.Globalization.CultureInfo.CurrentCulture, System.Globalization.CompareOptions.IgnoreCase))];

            return new OkObjectResult(people);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating role assignments for role {RoleId} in event {EventId}", roleId, eventId);
            return new StatusCodeResult(500);
        }
    }
#pragma warning restore MA0051

    /// <summary>
    /// Reorder role definitions by updating their display order.
    /// </summary>
    [Function("ReorderRoleDefinitions")]
    public async Task<IActionResult> ReorderRoleDefinitions(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "events/{eventId}/role-definitions/reorder")] HttpRequest req,
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

            // Parse request
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            ReorderRoleDefinitionsRequest? request = JsonSerializer.Deserialize<ReorderRoleDefinitionsRequest>(requestBody, FunctionHelpers.JsonOptions);

            if (request == null || request.Items == null || request.Items.Count == 0)
            {
                return new BadRequestObjectResult(new { message = "Items array is required" });
            }

            // Build display order map
            Dictionary<string, int> displayOrders = request.Items.ToDictionary(i => i.Id, i => i.DisplayOrder);

            // Update all role definitions
            await _roleDefinitionRepository.UpdateDisplayOrdersAsync(eventId, displayOrders);

            _logger.LogInformation("Role definitions reordered for event {EventId} by {PersonId}", eventId, claims.PersonId);

            return new NoContentResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reordering role definitions for event {EventId}", eventId);
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
    /// Auto-migrates existing roles from contacts to role definitions.
    /// Creates role definitions for built-in roles and any custom roles found in contacts,
    /// then migrates existing contacts/marshals to use roleIds instead of role names.
    /// </summary>
#pragma warning disable MA0051 // One-time migration logic with sequential steps
    private async Task MigrateExistingRolesAsync(string eventId, string personId)
    {
        _logger.LogInformation("Migrating existing roles for event {EventId}", eventId);

        // Build a map of role name -> roleId for migration
        Dictionary<string, string> roleNameToId = new(StringComparer.OrdinalIgnoreCase);

        // Legacy role name mappings (old Constants values -> new display names)
        Dictionary<string, string> legacyNameMappings = new(StringComparer.OrdinalIgnoreCase)
        {
            { "EmergencyContact", "Emergency contact" },
            { "EventDirector", "Event director" },
            { "MedicalLead", "Medical lead" },
            { "SafetyOfficer", "Safety officer" },
            { "Logistics", "Logistics" },
            { "AreaLead", "Area lead" }
        };

        // Create default roles
        int displayOrder = 0;
        foreach ((string name, bool isBuiltIn, bool canManageAreaCheckpoints) in _defaultRoles)
        {
            string roleId = Guid.NewGuid().ToString();
            roleNameToId[name] = roleId;

            // Also map legacy names to the same roleId
            foreach (KeyValuePair<string, string> mapping in legacyNameMappings)
            {
                if (mapping.Value.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    roleNameToId[mapping.Key] = roleId;
                }
            }

            EventRoleDefinitionEntity roleDefinition = new EventRoleDefinitionEntity
            {
                PartitionKey = eventId,
                RowKey = roleId,
                EventId = eventId,
                RoleId = roleId,
                Name = name,
                Notes = string.Empty,
                IsBuiltIn = isBuiltIn,
                CanManageAreaCheckpoints = canManageAreaCheckpoints,
                DisplayOrder = displayOrder++,
                CreatedByPersonId = personId,
                CreatedAt = DateTime.UtcNow,
                IsDeleted = false
            };
            await _roleDefinitionRepository.AddAsync(roleDefinition);
        }

        // Find custom roles from existing contacts
        IEnumerable<EventContactEntity> contacts = await _contactRepository.GetByEventAsync(eventId);
        HashSet<string> defaultAndLegacyNames = [.. _defaultRoles.Select(r => r.Name), .. legacyNameMappings.Keys];
        HashSet<string> customRoles = [];

        foreach (EventContactEntity contact in contacts)
        {
            List<string> roles = GetRolesFromContact(contact);
            foreach (string role in roles)
            {
                if (!string.IsNullOrEmpty(role) && !defaultAndLegacyNames.Contains(role))
                {
                    customRoles.Add(role);
                }
            }
        }

        // Create custom role definitions
        foreach (string roleName in customRoles.OrderBy(r => r))
        {
            string roleId = Guid.NewGuid().ToString();
            roleNameToId[roleName] = roleId;

            EventRoleDefinitionEntity roleDefinition = new EventRoleDefinitionEntity
            {
                PartitionKey = eventId,
                RowKey = roleId,
                EventId = eventId,
                RoleId = roleId,
                Name = roleName,
                Notes = string.Empty,
                IsBuiltIn = false,
                DisplayOrder = displayOrder++,
                CreatedByPersonId = personId,
                CreatedAt = DateTime.UtcNow,
                IsDeleted = false
            };
            await _roleDefinitionRepository.AddAsync(roleDefinition);
        }

        // Migrate contacts: replace role names with roleIds
        int contactsMigrated = 0;
        foreach (EventContactEntity contact in contacts)
        {
            List<string> oldRoles = GetRolesFromContact(contact);
            if (oldRoles.Count == 0) continue;

            List<string> newRoles = [];
            bool needsMigration = false;

            foreach (string roleName in oldRoles)
            {
                // Check if this is already a GUID (roleId) - skip if so
                if (Guid.TryParse(roleName, out _))
                {
                    newRoles.Add(roleName);
                }
                else if (roleNameToId.TryGetValue(roleName, out string? roleId))
                {
                    newRoles.Add(roleId);
                    needsMigration = true;
                }
                // else: unknown role name, skip it
            }

            if (needsMigration && newRoles.Count > 0)
            {
                SetRolesOnContact(contact, newRoles);
                // Use unconditional update for migration - avoids conflicts with concurrent requests
                await _contactRepository.UpdateUnconditionalAsync(contact);
                contactsMigrated++;
            }
        }

        // Migrate marshals: replace role names with roleIds
        IEnumerable<MarshalEntity> marshals = await _marshalRepository.GetByEventAsync(eventId);
        int marshalsMigrated = 0;
        foreach (MarshalEntity marshal in marshals)
        {
            List<string> oldRoles = GetRolesFromMarshal(marshal);
            if (oldRoles.Count == 0) continue;

            List<string> newRoles = [];
            bool needsMigration = false;

            foreach (string roleName in oldRoles)
            {
                // Check if this is already a GUID (roleId) - skip if so
                if (Guid.TryParse(roleName, out _))
                {
                    newRoles.Add(roleName);
                }
                else if (roleNameToId.TryGetValue(roleName, out string? roleId))
                {
                    newRoles.Add(roleId);
                    needsMigration = true;
                }
                // else: unknown role name, skip it
            }

            if (needsMigration && newRoles.Count > 0)
            {
                marshal.RolesJson = JsonSerializer.Serialize(newRoles);
                // Use unconditional update for migration - avoids conflicts with concurrent requests
                await _marshalRepository.UpdateUnconditionalAsync(marshal);
                marshalsMigrated++;
            }
        }

        _logger.LogInformation("Migration complete for event {EventId}: {DefaultCount} default roles, {CustomCount} custom roles, {ContactsMigrated} contacts migrated, {MarshalsMigrated} marshals migrated",
            eventId, _defaultRoles.Count, customRoles.Count, contactsMigrated, marshalsMigrated);
    }
#pragma warning restore MA0051

    private static RoleDefinitionResponse ToRoleDefinitionResponse(EventRoleDefinitionEntity role, int usageCount)
    {
        return new RoleDefinitionResponse(
            RoleId: role.RoleId,
            EventId: role.EventId,
            Name: role.Name,
            Notes: role.Notes,
            IsBuiltIn: role.IsBuiltIn,
            CanManageAreaCheckpoints: role.CanManageAreaCheckpoints,
            DisplayOrder: role.DisplayOrder,
            UsageCount: usageCount,
            CreatedAt: role.CreatedAt,
            UpdatedAt: role.UpdatedAt
        );
    }

    private static int CountRoleUsage(string roleId, string roleName, IEnumerable<MarshalEntity> marshals, IEnumerable<EventContactEntity> contacts)
    {
        int count = 0;

        // Helper to check if roles list contains this role (by ID or name for backwards compatibility)
        bool HasRole(List<string> roles) =>
            roles.Contains(roleId) ||
            roles.Contains(roleName, StringComparer.OrdinalIgnoreCase);

        // Count marshals with this role
        foreach (MarshalEntity marshal in marshals)
        {
            List<string> roles = GetRolesFromMarshal(marshal);
            if (HasRole(roles))
            {
                count++;
            }
        }

        // Count contacts with this role (but not if they're linked to a marshal we already counted)
        HashSet<string> countedMarshalIds = [.. marshals.Where(m => HasRole(GetRolesFromMarshal(m))).Select(m => m.MarshalId)];
        foreach (EventContactEntity contact in contacts)
        {
            // Skip if this contact is linked to a marshal we already counted
            if (!string.IsNullOrEmpty(contact.MarshalId) && countedMarshalIds.Contains(contact.MarshalId))
            {
                continue;
            }

            List<string> roles = GetRolesFromContact(contact);
            if (HasRole(roles))
            {
                count++;
            }
        }

        return count;
    }

#pragma warning disable MA0051 // Builds unified view from multiple data sources
    private static List<PersonWithRoleResponse> BuildUnifiedPersonList(string roleId, string roleName, IEnumerable<MarshalEntity> marshals, IEnumerable<EventContactEntity> contacts)
    {
        List<PersonWithRoleResponse> people = [];
        HashSet<string> processedMarshalIds = [];

        // Helper to check if roles list contains this role (by ID or name for backwards compatibility)
        bool HasRole(List<string> roles) =>
            roles.Contains(roleId) ||
            roles.Contains(roleName, StringComparer.OrdinalIgnoreCase);

        // Process marshals first
        foreach (MarshalEntity marshal in marshals)
        {
            processedMarshalIds.Add(marshal.MarshalId);
            List<string> roles = GetRolesFromMarshal(marshal);
            bool hasRole = HasRole(roles);

            // Check if marshal has linked contacts
            EventContactEntity? linkedContact = contacts.FirstOrDefault(c => c.MarshalId == marshal.MarshalId);

            if (linkedContact != null)
            {
                // Linked - show as "Linked" type
                List<string> contactRoles = GetRolesFromContact(linkedContact);
                bool contactHasRole = HasRole(contactRoles);

                people.Add(new PersonWithRoleResponse(
                    Id: marshal.MarshalId,
                    Name: marshal.Name,
                    PersonType: "Linked",
                    MarshalId: marshal.MarshalId,
                    ContactId: linkedContact.ContactId,
                    HasRole: hasRole || contactHasRole
                ));
            }
            else
            {
                // Marshal only
                people.Add(new PersonWithRoleResponse(
                    Id: marshal.MarshalId,
                    Name: marshal.Name,
                    PersonType: "Marshal",
                    MarshalId: marshal.MarshalId,
                    ContactId: null,
                    HasRole: hasRole
                ));
            }
        }

        // Add contacts that are not linked to marshals
        foreach (EventContactEntity contact in contacts)
        {
            if (!string.IsNullOrEmpty(contact.MarshalId) && processedMarshalIds.Contains(contact.MarshalId))
            {
                continue; // Already processed as linked
            }

            List<string> roles = GetRolesFromContact(contact);
            bool hasRole = HasRole(roles);

            people.Add(new PersonWithRoleResponse(
                Id: contact.ContactId,
                Name: contact.Name,
                PersonType: "Contact",
                MarshalId: null,
                ContactId: contact.ContactId,
                HasRole: hasRole
            ));
        }

        return people;
    }
#pragma warning restore MA0051

    private static List<string> GetRolesFromMarshal(MarshalEntity marshal)
    {
        if (string.IsNullOrEmpty(marshal.RolesJson) || marshal.RolesJson == "[]")
        {
            return [];
        }

        return JsonSerializer.Deserialize<List<string>>(marshal.RolesJson, FunctionHelpers.JsonOptions) ?? [];
    }

    private static List<string> GetRolesFromContact(EventContactEntity contact)
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

    private static void SetRolesOnContact(EventContactEntity contact, List<string> roles)
    {
        contact.RolesJson = JsonSerializer.Serialize(roles);
        // Keep legacy Role field populated with first role for backwards compatibility
        contact.Role = roles.Count > 0 ? roles[0] : string.Empty;
    }

    /// <summary>
    /// Syncs a role change from a marshal to their linked contact.
    /// </summary>
    private async Task SyncRoleToLinkedContactAsync(string eventId, string marshalId, string roleId, bool addRole)
    {
        IEnumerable<EventContactEntity> linkedContacts = await _contactRepository.GetByMarshalAsync(eventId, marshalId);
        foreach (EventContactEntity contact in linkedContacts)
        {
            List<string> roles = GetRolesFromContact(contact);
            bool changed = false;

            if (addRole && !roles.Contains(roleId))
            {
                roles.Add(roleId);
                changed = true;
            }
            else if (!addRole && roles.Contains(roleId))
            {
                roles.Remove(roleId);
                changed = true;
            }

            if (changed)
            {
                SetRolesOnContact(contact, roles);
                contact.UpdatedAt = DateTime.UtcNow;
                await _contactRepository.UpdateAsync(contact);
            }
        }
    }

    /// <summary>
    /// Syncs a role change from a contact to their linked marshal.
    /// </summary>
    private async Task SyncRoleToLinkedMarshalAsync(string eventId, string? marshalId, string roleId, bool addRole)
    {
        if (string.IsNullOrEmpty(marshalId))
        {
            return;
        }

        MarshalEntity? marshal = await _marshalRepository.GetAsync(eventId, marshalId);
        if (marshal == null)
        {
            return;
        }

        List<string> roles = GetRolesFromMarshal(marshal);
        bool changed = false;

        if (addRole && !roles.Contains(roleId))
        {
            roles.Add(roleId);
            changed = true;
        }
        else if (!addRole && roles.Contains(roleId))
        {
            roles.Remove(roleId);
            changed = true;
        }

        if (changed)
        {
            marshal.RolesJson = JsonSerializer.Serialize(roles);
            await _marshalRepository.UpdateAsync(marshal);
        }
    }

    /// <summary>
    /// Re-syncs area lead permissions for all contacts that have the specified role.
    /// Called when a role definition's CanManageAreaCheckpoints flag changes.
    /// </summary>
#pragma warning disable MA0051 // Complex permission sync logic requires sequential processing
    private async Task ResyncAreaLeadPermissionsForRoleAsync(string eventId, string roleId, string grantedByPersonId)
    {
        // Get all contacts for this event
        IEnumerable<EventContactEntity> contacts = await _contactRepository.GetByEventAsync(eventId);

        // Get all role definitions to check which have CanManageAreaCheckpoints
        IEnumerable<EventRoleDefinitionEntity> allRoleDefinitions = await _roleDefinitionRepository.GetByEventAsync(eventId);
        HashSet<string> areaManageRoleIds = [.. allRoleDefinitions.Where(rd => rd.CanManageAreaCheckpoints).Select(rd => rd.RoleId)];

        int syncedCount = 0;

        foreach (EventContactEntity contact in contacts)
        {
            // Only process contacts linked to marshals
            if (string.IsNullOrEmpty(contact.MarshalId))
            {
                continue;
            }

            // Check if this contact has the updated role
            List<string> contactRoles = GetRolesFromContact(contact);
            if (!contactRoles.Contains(roleId))
            {
                continue;
            }

            // Get the marshal to find their PersonId
            MarshalEntity? marshal = await _marshalRepository.GetAsync(eventId, contact.MarshalId);
            if (marshal == null || string.IsNullOrEmpty(marshal.PersonId))
            {
                continue;
            }

            string personId = marshal.PersonId;

            // Check if this contact has ANY role with CanManageAreaCheckpoints
            bool hasAnyAreaManageRole = contactRoles.Any(r => areaManageRoleIds.Contains(r));

            // Find existing area lead role for this person/event
            IEnumerable<EventRoleEntity> existingRoles = await _eventRoleRepository.GetByPersonAndEventAsync(personId, eventId);
            EventRoleEntity? existingAreaLeadRole = existingRoles.FirstOrDefault(r => r.Role == Constants.RoleEventAreaLead);

            if (hasAnyAreaManageRole)
            {
                // Parse scope configurations from contact
                List<ScopeConfiguration> scopeConfigs = [];
                if (!string.IsNullOrEmpty(contact.ScopeConfigurationsJson))
                {
                    scopeConfigs = JsonSerializer.Deserialize<List<ScopeConfiguration>>(contact.ScopeConfigurationsJson, FunctionHelpers.JsonOptions) ?? [];
                }

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
                    // No areas specified, remove existing role if any
                    if (existingAreaLeadRole != null)
                    {
                        await _eventRoleRepository.DeleteAsync(personId, existingAreaLeadRole.RowKey);
                        _logger.LogInformation("Removed EventAreaLead role for person {PersonId} during role resync (no areas)", personId);
                    }
                }
                else if (existingAreaLeadRole != null)
                {
                    // Update existing role with current area IDs
                    existingAreaLeadRole.AreaIdsJson = JsonSerializer.Serialize(areaIds);
                    await _eventRoleRepository.UpdateAsync(existingAreaLeadRole);
                    _logger.LogInformation("Updated EventAreaLead role for person {PersonId} during role resync", personId);
                }
                else
                {
                    // Create new area lead role
                    string newRoleId = Guid.NewGuid().ToString();
                    EventRoleEntity newRole = new EventRoleEntity
                    {
                        PartitionKey = personId,
                        RowKey = EventRoleEntity.CreateRowKey(eventId, newRoleId),
                        PersonId = personId,
                        EventId = eventId,
                        Role = Constants.RoleEventAreaLead,
                        AreaIdsJson = JsonSerializer.Serialize(areaIds),
                        GrantedByPersonId = grantedByPersonId,
                        GrantedAt = DateTime.UtcNow
                    };
                    await _eventRoleRepository.AddAsync(newRole);
                    _logger.LogInformation("Granted EventAreaLead role to person {PersonId} during role resync", personId);
                }

                syncedCount++;
            }
            else
            {
                // Contact no longer has any area manage role, remove existing role if any
                if (existingAreaLeadRole != null)
                {
                    await _eventRoleRepository.DeleteAsync(personId, existingAreaLeadRole.RowKey);
                    _logger.LogInformation("Removed EventAreaLead role for person {PersonId} during role resync (permission removed)", personId);
                    syncedCount++;
                }
            }
        }

        _logger.LogInformation("Resynced area lead permissions for {Count} contacts after role {RoleId} permission change in event {EventId}",
            syncedCount, roleId, eventId);
    }
#pragma warning restore MA0051

    #endregion

    /// <summary>
    /// Get the current marshal's assigned roles with their notes.
    /// Accessible by any authenticated marshal.
    /// </summary>
    [Function("GetMyRoles")]
    public async Task<IActionResult> GetMyRoles(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "events/{eventId}/my-roles")] HttpRequest req,
        string eventId)
    {
        try
        {
            // Authenticate - requires being a marshal
            UserClaims? claims = await GetClaimsAsync(req, eventId);
            if (claims == null || !claims.CanActAsMarshal || claims.MarshalId == null)
            {
                return new UnauthorizedObjectResult(new { message = Constants.ErrorNotAuthorized });
            }

            // Get the marshal's record
            MarshalEntity? marshal = await _marshalRepository.GetAsync(eventId, claims.MarshalId);
            if (marshal == null)
            {
                return new NotFoundObjectResult(new { message = "Marshal not found" });
            }

            // Get the role IDs assigned to this marshal
            List<string> roleIds = GetRolesFromMarshal(marshal);
            if (roleIds.Count == 0)
            {
                return new OkObjectResult(new List<MarshalRoleResponse>());
            }

            // Get all role definitions for this event
            IEnumerable<EventRoleDefinitionEntity> allRoles = await _roleDefinitionRepository.GetByEventAsync(eventId);
            Dictionary<string, EventRoleDefinitionEntity> roleMap = allRoles.ToDictionary(r => r.RoleId);

            // Build the response with only roles that have notes
            List<MarshalRoleResponse> responses = [];
            foreach (string roleId in roleIds)
            {
                if (roleMap.TryGetValue(roleId, out EventRoleDefinitionEntity? role) && !string.IsNullOrWhiteSpace(role.Notes))
                {
                    responses.Add(new MarshalRoleResponse(
                        RoleId: role.RoleId,
                        Name: role.Name,
                        Notes: role.Notes
                    ));
                }
            }

            // Sort by display order
            responses = [.. responses.OrderBy(r => roleMap.TryGetValue(r.RoleId, out EventRoleDefinitionEntity? role) ? role.DisplayOrder : 0)];

            return new OkObjectResult(responses);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting roles for marshal in event {EventId}", eventId);
            return new StatusCodeResult(500);
        }
    }
}
