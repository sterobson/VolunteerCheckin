using Azure;
using Azure.Data.Tables;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using VolunteerCheckin.Functions.Helpers;
using VolunteerCheckin.Functions.Models;
using VolunteerCheckin.Functions.Services;
using VolunteerCheckin.Functions.Repositories;

namespace VolunteerCheckin.Functions.Functions;

public class MarshalFunctions
{
    private readonly ILogger<MarshalFunctions> _logger;
    private readonly IMarshalRepository _marshalRepository;
    private readonly ILocationRepository _locationRepository;
    private readonly IAssignmentRepository _assignmentRepository;
    private readonly IEventRepository _eventRepository;
    private readonly IChecklistItemRepository _checklistItemRepository;
    private readonly INoteRepository _noteRepository;
    private readonly IEventContactRepository _contactRepository;
    private readonly ClaimsService _claimsService;
    private readonly ContactPermissionService _contactPermissionService;
    private readonly EmailService? _emailService;

    public MarshalFunctions(
        ILogger<MarshalFunctions> logger,
        IMarshalRepository marshalRepository,
        ILocationRepository locationRepository,
        IAssignmentRepository assignmentRepository,
        IEventRepository eventRepository,
        IChecklistItemRepository checklistItemRepository,
        INoteRepository noteRepository,
        IEventContactRepository contactRepository,
        ClaimsService claimsService,
        ContactPermissionService contactPermissionService,
        EmailService? emailService = null)
    {
        _logger = logger;
        _marshalRepository = marshalRepository;
        _locationRepository = locationRepository;
        _assignmentRepository = assignmentRepository;
        _eventRepository = eventRepository;
        _checklistItemRepository = checklistItemRepository;
        _noteRepository = noteRepository;
        _contactRepository = contactRepository;
        _claimsService = claimsService;
        _contactPermissionService = contactPermissionService;
        _emailService = emailService;
    }

#pragma warning disable MA0051
    [Function("CreateMarshal")]
    public async Task<IActionResult> CreateMarshal(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "marshals")] HttpRequest req)
    {
        try
        {
            string body = await new StreamReader(req.Body).ReadToEndAsync();
            CreateMarshalRequest? request = JsonSerializer.Deserialize<CreateMarshalRequest>(body, FunctionHelpers.JsonOptions);

            if (request == null)
            {
                return new BadRequestObjectResult(new { message = "Invalid request" });
            }

            // Require authentication (session token or sample code)
            string? sessionToken = FunctionHelpers.GetSessionToken(req);
            string? sampleCode = FunctionHelpers.GetSampleCodeFromHeader(req);
            if (string.IsNullOrWhiteSpace(sessionToken) && string.IsNullOrWhiteSpace(sampleCode))
            {
                return new UnauthorizedObjectResult(new { message = "Authentication required" });
            }

            UserClaims? claims = await _claimsService.GetClaimsWithSampleSupportAsync(sessionToken, sampleCode, request.EventId);
            if (claims == null)
            {
                return new UnauthorizedObjectResult(new { message = "Invalid or expired session" });
            }

            // Require EventAdmin role to create marshals
            if (!claims.IsEventAdmin && !claims.IsSystemAdmin)
            {
                return new ObjectResult(new { message = "Event admin permission required" }) { StatusCode = 403 };
            }

            // Sanitize inputs
            string sanitizedName = InputSanitizer.SanitizeName(request.Name);
            if (string.IsNullOrWhiteSpace(sanitizedName))
            {
                return new BadRequestObjectResult(new { message = "Name is required and cannot be empty" });
            }

            string? sanitizedEmail = InputSanitizer.SanitizeEmail(request.Email);
            string sanitizedPhone = InputSanitizer.SanitizePhone(request.PhoneNumber);
            string sanitizedNotes = InputSanitizer.SanitizeNotes(request.Notes);

            string marshalId = Guid.NewGuid().ToString();
            string personId = Guid.NewGuid().ToString(); // Each marshal gets their own PersonId
            List<string> roles = request.Roles ?? [];
            MarshalEntity marshalEntity = new()
            {
                PartitionKey = request.EventId,
                RowKey = marshalId,
                EventId = request.EventId,
                MarshalId = marshalId,
                PersonId = personId,
                MagicCode = AuthService.GenerateMagicCode(),
                Name = sanitizedName,
                Email = sanitizedEmail ?? string.Empty,
                PhoneNumber = sanitizedPhone,
                Notes = sanitizedNotes,
                RolesJson = JsonSerializer.Serialize(roles)
            };

            await _marshalRepository.AddAsync(marshalEntity);

            // Create pending checklist items scoped to this marshal
            if (request.PendingNewChecklistItems != null && request.PendingNewChecklistItems.Count > 0)
            {
                // Use authenticated claims for audit trail
                string adminEmail = claims.PersonEmail ?? claims.PersonName ?? "Unknown";
                int displayOrder = 0;

                foreach (PendingChecklistItem pendingItem in request.PendingNewChecklistItems)
                {
                    if (string.IsNullOrWhiteSpace(pendingItem.Text)) continue;

                    string itemId = Guid.NewGuid().ToString();
                    List<ScopeConfiguration> scopeConfig =
                    [
                        new ScopeConfiguration { Scope = "SpecificPeople", ItemType = "Marshal", Ids = [marshalId] }
                    ];

                    ChecklistItemEntity checklistEntity = new()
                    {
                        PartitionKey = request.EventId,
                        RowKey = itemId,
                        EventId = request.EventId,
                        ItemId = itemId,
                        Text = InputSanitizer.SanitizeDescription(pendingItem.Text),
                        ScopeConfigurationsJson = JsonSerializer.Serialize(scopeConfig),
                        DisplayOrder = displayOrder++,
                        IsRequired = false,
                        CreatedByAdminEmail = adminEmail,
                        CreatedDate = DateTime.UtcNow
                    };

                    await _checklistItemRepository.AddAsync(checklistEntity);
                    _logger.LogInformation("Checklist item {ItemId} created for new marshal {MarshalId}", itemId, marshalId);
                }
            }

            // Create pending notes scoped to this marshal
            if (request.PendingNewNotes != null && request.PendingNewNotes.Count > 0)
            {
                int displayOrder = 0;

                foreach (PendingNote pendingNote in request.PendingNewNotes)
                {
                    if (string.IsNullOrWhiteSpace(pendingNote.Title) && string.IsNullOrWhiteSpace(pendingNote.Content)) continue;

                    string noteId = Guid.NewGuid().ToString();
                    List<ScopeConfiguration> scopeConfig =
                    [
                        new ScopeConfiguration { Scope = "SpecificPeople", ItemType = "Marshal", Ids = [marshalId] }
                    ];

                    NoteEntity noteEntity = new()
                    {
                        PartitionKey = request.EventId,
                        RowKey = noteId,
                        EventId = request.EventId,
                        NoteId = noteId,
                        Title = InputSanitizer.SanitizeName(pendingNote.Title ?? "Untitled"),
                        Content = InputSanitizer.SanitizeDescription(pendingNote.Content ?? string.Empty),
                        ScopeConfigurationsJson = JsonSerializer.Serialize(scopeConfig),
                        DisplayOrder = displayOrder++,
                        Priority = Constants.NotePriorityNormal,
                        Category = string.Empty,
                        IsPinned = false,
                        CreatedByPersonId = claims.PersonId ?? string.Empty,
                        CreatedByName = claims.PersonName ?? "Admin",
                        CreatedAt = DateTime.UtcNow,
                        IsDeleted = false
                    };

                    await _noteRepository.AddAsync(noteEntity);
                    _logger.LogInformation("Note {NoteId} created for new marshal {MarshalId}", noteId, marshalId);
                }
            }

            MarshalResponse response = new(
                marshalEntity.MarshalId,
                marshalEntity.EventId,
                marshalEntity.Name,
                marshalEntity.Email,
                marshalEntity.PhoneNumber,
                marshalEntity.Notes,
                roles,
                [],
                false,
                marshalEntity.CreatedDate,
                marshalEntity.LastAccessedDate
            );

            _logger.LogInformation("Marshal created: {MarshalId}", marshalId);

            return new OkObjectResult(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating marshal");
            return new StatusCodeResult(500);
        }
    }
#pragma warning restore MA0051

#pragma warning disable MA0051
    [Function("GetMarshalsByEvent")]
    public async Task<IActionResult> GetMarshalsByEvent(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "events/{eventId}/marshals")] HttpRequest req,
        string eventId)
    {
        try
        {
            _logger.LogInformation("GetMarshalsByEvent called for event {EventId}", eventId);

            // Require authentication (session token or sample code)
            string? sessionToken = FunctionHelpers.GetSessionToken(req);
            string? sampleCode = FunctionHelpers.GetSampleCodeFromHeader(req);
            _logger.LogInformation("Session token present: {SessionTokenPresent}, Sample code present: {SampleCodePresent}",
                !string.IsNullOrWhiteSpace(sessionToken), !string.IsNullOrWhiteSpace(sampleCode));

            if (string.IsNullOrWhiteSpace(sessionToken) && string.IsNullOrWhiteSpace(sampleCode))
            {
                _logger.LogWarning("No session token or sample code provided");
                return new UnauthorizedObjectResult(new { message = "Authentication required" });
            }

            UserClaims? claims = await _claimsService.GetClaimsWithSampleSupportAsync(sessionToken, sampleCode, eventId);
            _logger.LogInformation("Claims resolved: {ClaimsResolved}, IsEventAdmin: {IsEventAdmin}, IsSystemAdmin: {IsSystemAdmin}", claims != null, claims?.IsEventAdmin, claims?.IsSystemAdmin);
            if (claims == null)
            {
                _logger.LogWarning("Claims returned null - invalid or expired session");
                return new UnauthorizedObjectResult(new { message = "Invalid or expired session" });
            }

            // Get contact permissions for this user
            ContactPermissions permissions = await _contactPermissionService.GetContactPermissionsAsync(claims, eventId);
            _logger.LogInformation("Permissions - CanViewAll: {CanViewAll}, CanModifyAll: {CanModifyAll}", permissions.CanViewAll, permissions.CanModifyAll);

            IEnumerable<MarshalEntity> marshalEntities = await _marshalRepository.GetByEventAsync(eventId);
            _logger.LogInformation("Found {MarshalCount} marshals for event {EventId}", marshalEntities.Count(), eventId);

            // Preload all assignments for the event (single DB call instead of N calls)
            IEnumerable<AssignmentEntity> allAssignments = await _assignmentRepository.GetByEventAsync(eventId);
            Dictionary<string, List<AssignmentEntity>> assignmentsByMarshal = allAssignments
                .GroupBy(a => a.MarshalId)
                .ToDictionary(g => g.Key, g => g.ToList());

            List<MarshalWithPermissionsResponse> marshals = [];

            foreach (MarshalEntity marshalEntity in marshalEntities)
            {
                // Get assignments from preloaded dictionary (O(1) lookup instead of DB call)
                List<AssignmentEntity> assignments = assignmentsByMarshal.GetValueOrDefault(marshalEntity.MarshalId, []);

                List<string> assignedLocationIds = [.. assignments.Select(a => a.LocationId)];
                bool isCheckedIn = assignments.Any(a => a.IsCheckedIn);

                bool canViewContact = _contactPermissionService.CanViewContactDetails(permissions, marshalEntity.MarshalId);
                bool canModify = _contactPermissionService.CanModifyMarshal(permissions, marshalEntity.MarshalId);

                List<string> marshalRoles = string.IsNullOrEmpty(marshalEntity.RolesJson)
                    ? []
                    : JsonSerializer.Deserialize<List<string>>(marshalEntity.RolesJson) ?? [];

                marshals.Add(new MarshalWithPermissionsResponse(
                    marshalEntity.MarshalId,
                    marshalEntity.EventId,
                    marshalEntity.Name,
                    canViewContact ? marshalEntity.Email : null,
                    canViewContact ? marshalEntity.PhoneNumber : null,
                    canViewContact ? marshalEntity.Notes : null,
                    marshalRoles,
                    assignedLocationIds,
                    isCheckedIn,
                    marshalEntity.CreatedDate,
                    marshalEntity.LastAccessedDate,
                    canViewContact,
                    canModify
                ));
            }

            return new OkObjectResult(marshals);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting marshals");
            return new StatusCodeResult(500);
        }
    }
#pragma warning restore MA0051

#pragma warning disable MA0051
    [Function("GetMarshal")]
    public async Task<IActionResult> GetMarshal(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "marshals/{eventId}/{marshalId}")] HttpRequest req,
        string eventId,
        string marshalId)
    {
        try
        {
            // Require authentication (session token or sample code)
            string? sessionToken = FunctionHelpers.GetSessionToken(req);
            string? sampleCode = FunctionHelpers.GetSampleCodeFromHeader(req);
            if (string.IsNullOrWhiteSpace(sessionToken) && string.IsNullOrWhiteSpace(sampleCode))
            {
                return new UnauthorizedObjectResult(new { message = "Authentication required" });
            }

            UserClaims? claims = await _claimsService.GetClaimsWithSampleSupportAsync(sessionToken, sampleCode, eventId);
            if (claims == null)
            {
                return new UnauthorizedObjectResult(new { message = "Invalid or expired session" });
            }

            MarshalEntity? marshalEntity = await _marshalRepository.GetAsync(eventId, marshalId);

            if (marshalEntity == null)
            {
                return new NotFoundObjectResult(new { message = "Marshal not found" });
            }

            // Get contact permissions for this user
            ContactPermissions permissions = await _contactPermissionService.GetContactPermissionsAsync(claims, eventId);
            bool canViewContact = _contactPermissionService.CanViewContactDetails(permissions, marshalId);
            bool canModify = _contactPermissionService.CanModifyMarshal(permissions, marshalId);

            // Get assignments for this marshal
            IEnumerable<AssignmentEntity> assignments = await _assignmentRepository.GetByMarshalAsync(eventId, marshalId);

            List<string> assignedLocationIds = [];
            bool isCheckedIn = false;

            foreach (AssignmentEntity assignment in assignments)
            {
                assignedLocationIds.Add(assignment.LocationId);
                if (assignment.IsCheckedIn)
                {
                    isCheckedIn = true;
                }
            }

            List<string> marshalRoles = string.IsNullOrEmpty(marshalEntity.RolesJson)
                ? []
                : JsonSerializer.Deserialize<List<string>>(marshalEntity.RolesJson) ?? [];

            MarshalWithPermissionsResponse response = new(
                marshalEntity.MarshalId,
                marshalEntity.EventId,
                marshalEntity.Name,
                canViewContact ? marshalEntity.Email : null,
                canViewContact ? marshalEntity.PhoneNumber : null,
                canViewContact ? marshalEntity.Notes : null,
                marshalRoles,
                assignedLocationIds,
                isCheckedIn,
                marshalEntity.CreatedDate,
                marshalEntity.LastAccessedDate,
                canViewContact,
                canModify
            );

            return new OkObjectResult(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting marshal");
            return new StatusCodeResult(500);
        }
    }
#pragma warning restore MA0051

#pragma warning disable MA0051
    [Function("UpdateMarshal")]
    public async Task<IActionResult> UpdateMarshal(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "marshals/{eventId}/{marshalId}")] HttpRequest req,
        string eventId,
        string marshalId)
    {
        try
        {
            // Require authentication (session token or sample code)
            string? sessionToken = FunctionHelpers.GetSessionToken(req);
            string? sampleCode = FunctionHelpers.GetSampleCodeFromHeader(req);
            if (string.IsNullOrWhiteSpace(sessionToken) && string.IsNullOrWhiteSpace(sampleCode))
            {
                return new UnauthorizedObjectResult(new { message = "Authentication required" });
            }

            UserClaims? claims = await _claimsService.GetClaimsWithSampleSupportAsync(sessionToken, sampleCode, eventId);
            if (claims == null)
            {
                return new UnauthorizedObjectResult(new { message = "Invalid or expired session" });
            }

            // Check modification permissions
            ContactPermissions permissions = await _contactPermissionService.GetContactPermissionsAsync(claims, eventId);
            if (!_contactPermissionService.CanModifyMarshal(permissions, marshalId))
            {
                return new ObjectResult(new { message = "You do not have permission to modify this marshal" }) { StatusCode = 403 };
            }

            string body = await new StreamReader(req.Body).ReadToEndAsync();
            UpdateMarshalRequest? request = JsonSerializer.Deserialize<UpdateMarshalRequest>(body, FunctionHelpers.JsonOptions);

            if (request == null)
            {
                return new BadRequestObjectResult(new { message = "Invalid request" });
            }

            MarshalEntity? marshalEntity = await _marshalRepository.GetAsync(eventId, marshalId);

            if (marshalEntity == null)
            {
                return new NotFoundObjectResult(new { message = "Marshal not found" });
            }

            // Sanitize inputs
            string sanitizedName = InputSanitizer.SanitizeName(request.Name);
            if (string.IsNullOrWhiteSpace(sanitizedName))
            {
                return new BadRequestObjectResult(new { message = "Name is required and cannot be empty" });
            }

            string? sanitizedEmail = InputSanitizer.SanitizeEmail(request.Email);
            string sanitizedPhone = InputSanitizer.SanitizePhone(request.PhoneNumber);
            string sanitizedNotes = InputSanitizer.SanitizeNotes(request.Notes);

            // Marshals can modify their own details, but only admins can modify notes
            marshalEntity.Name = sanitizedName;
            marshalEntity.Email = sanitizedEmail ?? string.Empty;
            marshalEntity.PhoneNumber = sanitizedPhone;

            // Only admins can modify notes and roles (notes may contain sensitive admin-only info)
            List<string>? newRoles = null;
            if (claims.IsEventAdmin || claims.IsSystemAdmin)
            {
                marshalEntity.Notes = sanitizedNotes;
                newRoles = request.Roles ?? [];
                marshalEntity.RolesJson = JsonSerializer.Serialize(newRoles);
            }

            await _marshalRepository.UpdateAsync(marshalEntity);

            // Sync roles to linked contact if roles were updated
            if (newRoles != null)
            {
                await SyncRolesToLinkedContactAsync(eventId, marshalId, newRoles);
            }

            // Update denormalized name in all assignments
            IEnumerable<AssignmentEntity> assignments = await _assignmentRepository.GetByMarshalAsync(eventId, marshalId);
            foreach (AssignmentEntity assignment in assignments)
            {
                assignment.MarshalName = sanitizedName;
                await _assignmentRepository.UpdateAsync(assignment);
            }

            // Get updated assignments
            List<string> assignedLocationIds = [];
            bool isCheckedIn = false;

            IEnumerable<AssignmentEntity> updatedAssignments = await _assignmentRepository.GetByMarshalAsync(eventId, marshalId);
            foreach (AssignmentEntity assignment in updatedAssignments)
            {
                assignedLocationIds.Add(assignment.LocationId);
                if (assignment.IsCheckedIn)
                {
                    isCheckedIn = true;
                }
            }

            // Return with appropriate visibility
            bool canViewContact = _contactPermissionService.CanViewContactDetails(permissions, marshalId);

            List<string> marshalRoles = string.IsNullOrEmpty(marshalEntity.RolesJson)
                ? []
                : JsonSerializer.Deserialize<List<string>>(marshalEntity.RolesJson) ?? [];

            MarshalWithPermissionsResponse response = new(
                marshalEntity.MarshalId,
                marshalEntity.EventId,
                marshalEntity.Name,
                canViewContact ? marshalEntity.Email : null,
                canViewContact ? marshalEntity.PhoneNumber : null,
                canViewContact ? marshalEntity.Notes : null,
                marshalRoles,
                assignedLocationIds,
                isCheckedIn,
                marshalEntity.CreatedDate,
                marshalEntity.LastAccessedDate,
                canViewContact,
                true // They can modify since we passed the permission check
            );

            _logger.LogInformation("Marshal updated: {MarshalId}", marshalId);

            return new OkObjectResult(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating marshal");
            return new StatusCodeResult(500);
        }
    }
#pragma warning restore MA0051

#pragma warning disable MA0051
    [Function("DeleteMarshal")]
    public async Task<IActionResult> DeleteMarshal(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "marshals/{eventId}/{marshalId}")] HttpRequest req,
        string eventId,
        string marshalId)
    {
        try
        {
            // Require authentication (session token or sample code)
            string? sessionToken = FunctionHelpers.GetSessionToken(req);
            string? sampleCode = FunctionHelpers.GetSampleCodeFromHeader(req);
            if (string.IsNullOrWhiteSpace(sessionToken) && string.IsNullOrWhiteSpace(sampleCode))
            {
                return new UnauthorizedObjectResult(new { message = "Authentication required" });
            }

            UserClaims? claims = await _claimsService.GetClaimsWithSampleSupportAsync(sessionToken, sampleCode, eventId);
            if (claims == null)
            {
                return new UnauthorizedObjectResult(new { message = "Invalid or expired session" });
            }

            // Require EventAdmin role to delete marshals
            if (!claims.IsEventAdmin && !claims.IsSystemAdmin)
            {
                return new ObjectResult(new { message = "Event admin permission required" }) { StatusCode = 403 };
            }

            // Delete all assignments for this marshal
            IEnumerable<AssignmentEntity> assignments = await _assignmentRepository.GetByMarshalAsync(eventId, marshalId);
            foreach (AssignmentEntity assignment in assignments)
            {
                await _assignmentRepository.DeleteAsync(eventId, assignment.RowKey);
            }

            // Delete the marshal
            await _marshalRepository.DeleteAsync(eventId, marshalId);

            _logger.LogInformation("Marshal deleted: {MarshalId}", marshalId);

            return new NoContentResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting marshal");
            return new StatusCodeResult(500);
        }
    }
#pragma warning restore MA0051

#pragma warning disable MA0051 // Authentication and magic link generation with validation
    [Function("GetMarshalMagicLink")]
    public async Task<IActionResult> GetMarshalMagicLink(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "marshals/{eventId}/{marshalId}/magic-link")] HttpRequest req,
        string eventId,
        string marshalId)
    {
        try
        {
            // Require authentication (session token or sample code)
            string? sessionToken = FunctionHelpers.GetSessionToken(req);
            string? sampleCode = FunctionHelpers.GetSampleCodeFromHeader(req);
            if (string.IsNullOrWhiteSpace(sessionToken) && string.IsNullOrWhiteSpace(sampleCode))
            {
                return new UnauthorizedObjectResult(new { message = "Authentication required" });
            }

            UserClaims? claims = await _claimsService.GetClaimsWithSampleSupportAsync(sessionToken, sampleCode, eventId);
            if (claims == null)
            {
                return new UnauthorizedObjectResult(new { message = "Invalid or expired session" });
            }

            // Check permissions: admin, area admin, or area lead for marshal's areas
            bool hasAreaAdminRole = claims.HasRole(Constants.RoleEventAreaAdmin);
            bool isAdminOrAreaAdmin = claims.IsEventAdmin || claims.IsSystemAdmin || hasAreaAdminRole;

            if (!isAdminOrAreaAdmin)
            {
                // Check if user is an area lead with access to this marshal
                bool canAccess = await CanAreaLeadAccessMarshalMagicLinkAsync(claims, eventId, marshalId);
                if (!canAccess)
                {
                    return new ObjectResult(new { message = "You can only access magic links for marshals in your areas" }) { StatusCode = 403 };
                }
            }

            MarshalEntity? marshalEntity = await _marshalRepository.GetAsync(eventId, marshalId);
            if (marshalEntity == null)
            {
                return new NotFoundObjectResult(new { message = "Marshal not found" });
            }

            // Generate magic code if not exists
            if (string.IsNullOrWhiteSpace(marshalEntity.MagicCode))
            {
                marshalEntity.MagicCode = AuthService.GenerateMagicCode();
                await _marshalRepository.UpdateAsync(marshalEntity);
            }

            // Get frontend URL - prefer explicit value from query parameter, fall back to header detection
            string? frontendUrlParam = req.Query["frontendUrl"].FirstOrDefault();
            string frontendUrl = !string.IsNullOrWhiteSpace(frontendUrlParam)
                ? frontendUrlParam.TrimEnd('/')
                : FunctionHelpers.GetFrontendUrl(req);

            // Use routing mode from query param if provided, otherwise detect from referer header
            string? useHashRoutingParam = req.Query["useHashRouting"].FirstOrDefault();
            bool useHashRouting = useHashRoutingParam != null
                ? useHashRoutingParam.Equals("true", StringComparison.OrdinalIgnoreCase)
                : FunctionHelpers.UsesHashRouting(req);
            string routePrefix = useHashRouting ? "/#" : "";
            string magicLink = $"{frontendUrl}{routePrefix}/event/{eventId}?code={marshalEntity.MagicCode}";

            return new OkObjectResult(new MarshalMagicLinkResponse(
                marshalEntity.MagicCode,
                magicLink,
                !string.IsNullOrWhiteSpace(marshalEntity.Email)
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting marshal magic link");
            return new StatusCodeResult(500);
        }
    }
#pragma warning restore MA0051

#pragma warning disable MA0051
    [Function("SendMarshalMagicLink")]
    public async Task<IActionResult> SendMarshalMagicLink(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "marshals/{eventId}/{marshalId}/send-magic-link")] HttpRequest req,
        string eventId,
        string marshalId)
    {
        try
        {
            // Require authentication (session token or sample code)
            string? sessionToken = FunctionHelpers.GetSessionToken(req);
            string? sampleCode = FunctionHelpers.GetSampleCodeFromHeader(req);
            if (string.IsNullOrWhiteSpace(sessionToken) && string.IsNullOrWhiteSpace(sampleCode))
            {
                return new UnauthorizedObjectResult(new { message = "Authentication required" });
            }

            UserClaims? claims = await _claimsService.GetClaimsWithSampleSupportAsync(sessionToken, sampleCode, eventId);
            if (claims == null)
            {
                return new UnauthorizedObjectResult(new { message = "Invalid or expired session" });
            }

            // Require at least area admin permission
            bool hasAreaAdminRole = claims.HasRole(Constants.RoleEventAreaAdmin);
            if (!claims.IsEventAdmin && !claims.IsSystemAdmin && !hasAreaAdminRole)
            {
                return new ObjectResult(new { message = "Admin permission required" }) { StatusCode = 403 };
            }

            MarshalEntity? marshalEntity = await _marshalRepository.GetAsync(eventId, marshalId);
            if (marshalEntity == null)
            {
                return new NotFoundObjectResult(new { message = "Marshal not found" });
            }

            // Parse request body
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            SendMarshalMagicLinkRequest? request = string.IsNullOrWhiteSpace(requestBody)
                ? null
                : JsonSerializer.Deserialize<SendMarshalMagicLinkRequest>(requestBody, FunctionHelpers.JsonOptions);

            // Determine target email - use custom email if provided, otherwise marshal's stored email
            string? targetEmail = !string.IsNullOrWhiteSpace(request?.Email)
                ? request.Email
                : marshalEntity.Email;

            if (string.IsNullOrWhiteSpace(targetEmail))
            {
                return new BadRequestObjectResult(new { message = "No email address provided" });
            }

            if (_emailService == null)
            {
                return new ObjectResult(new { message = "Email service not configured" }) { StatusCode = 503 };
            }

            // Generate magic code if not exists
            if (string.IsNullOrWhiteSpace(marshalEntity.MagicCode))
            {
                marshalEntity.MagicCode = AuthService.GenerateMagicCode();
                await _marshalRepository.UpdateAsync(marshalEntity);
            }

            // Get event details
            EventEntity? eventEntity = await _eventRepository.GetAsync(eventId);
            string eventName = eventEntity?.Name ?? "Event";

            // Get frontend URL - prefer explicit value from request body, fall back to header detection
            string frontendUrl = !string.IsNullOrWhiteSpace(request?.FrontendUrl)
                ? request.FrontendUrl.TrimEnd('/')
                : FunctionHelpers.GetFrontendUrl(req);

            // Use routing mode from request body if provided, otherwise detect from referer header
            bool useHashRouting = request?.UseHashRouting ?? FunctionHelpers.UsesHashRouting(req);
            string routePrefix = useHashRouting ? "/#" : "";
            string magicLink = $"{frontendUrl}{routePrefix}/event/{eventId}?code={marshalEntity.MagicCode}";

            // Check if we should include event/checkpoint details
            if (request?.IncludeDetails == true)
            {
                // Get marshal's assignments
                IEnumerable<AssignmentEntity> assignments = await _assignmentRepository.GetByMarshalAsync(eventId, marshalId);
                List<CheckpointEmailInfo> checkpointInfos = [];

                // Get all notes for the event to filter checkpoint-specific ones
                IEnumerable<NoteEntity> allNotes = await _noteRepository.GetByEventAsync(eventId);

                foreach (AssignmentEntity assignment in assignments)
                {
                    LocationEntity? location = await _locationRepository.GetAsync(eventId, assignment.LocationId);
                    if (location != null)
                    {
                        // Get notes specifically scoped to this checkpoint
                        List<NoteEmailInfo> checkpointNotes = GetNotesForCheckpoint(allNotes, assignment.LocationId);

                        // Only include lat/long if they're valid (not the default 0,0 point)
                        bool hasValidCoordinates = Math.Abs(location.Latitude) > 0.0001 || Math.Abs(location.Longitude) > 0.0001;
                        double? latitude = hasValidCoordinates ? location.Latitude : null;
                        double? longitude = hasValidCoordinates ? location.Longitude : null;

                        checkpointInfos.Add(new CheckpointEmailInfo(
                            location.Name,
                            location.Description,
                            location.StartTime,
                            latitude,
                            longitude,
                            checkpointNotes
                        ));
                    }
                }

                // Sort checkpoints by arrival time, then name
                checkpointInfos = [.. checkpointInfos.OrderBy(c => c.ArrivalTime ?? DateTime.MaxValue).ThenBy(c => c.Name, StringComparer.Create(System.Globalization.CultureInfo.CurrentCulture, System.Globalization.CompareOptions.IgnoreCase))];

                // Get checkpoint terminology
                string checkpointTerm = eventEntity?.CheckpointTerm ?? "Checkpoint";
                // Convert plural term to singular
                string checkpointTermSingular = checkpointTerm switch
                {
                    "Checkpoints" => "Checkpoint",
                    "Stations" => "Station",
                    "Locations" => "Location",
                    "Feed stations" => "Feed station",
                    "Aid stations" => "Aid station",
                    "Water stations" => "Water station",
                    _ => checkpointTerm.TrimEnd('s')
                };

                await _emailService.SendMarshalMagicLinkWithDetailsEmailAsync(
                    targetEmail,
                    marshalEntity.Name,
                    eventName,
                    magicLink,
                    eventEntity?.EventDate,
                    checkpointTermSingular,
                    checkpointInfos
                );
            }
            else
            {
                // Send simple email without details
                await _emailService.SendMarshalMagicLinkEmailAsync(
                    targetEmail,
                    marshalEntity.Name,
                    eventName,
                    magicLink
                );
            }

            _logger.LogInformation("Sent magic link email to marshal {MarshalId} at {Email}", marshalId, targetEmail);

            return new OkObjectResult(new { message = "Magic link sent successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending marshal magic link");
            return new StatusCodeResult(500);
        }
    }
#pragma warning restore MA0051

#pragma warning disable MA0051
    [Function("ImportMarshals")]
    public async Task<IActionResult> ImportMarshals(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "marshals/import/{eventId}")] HttpRequest req,
        string eventId)
    {
        try
        {
            // Require authentication (session token or sample code)
            string? sessionToken = FunctionHelpers.GetSessionToken(req);
            string? sampleCode = FunctionHelpers.GetSampleCodeFromHeader(req);
            if (string.IsNullOrWhiteSpace(sessionToken) && string.IsNullOrWhiteSpace(sampleCode))
            {
                return new UnauthorizedObjectResult(new { message = "Authentication required" });
            }

            UserClaims? claims = await _claimsService.GetClaimsWithSampleSupportAsync(sessionToken, sampleCode, eventId);
            if (claims == null)
            {
                return new UnauthorizedObjectResult(new { message = "Invalid or expired session" });
            }

            // Require EventAdmin role to import marshals
            if (!claims.IsEventAdmin && !claims.IsSystemAdmin)
            {
                return new ObjectResult(new { message = "Event admin permission required" }) { StatusCode = 403 };
            }

            // Get the uploaded file
            if (req.Form.Files.Count == 0)
            {
                return new BadRequestObjectResult(new { message = "No file uploaded" });
            }

            IFormFile csvFile = req.Form.Files[0];
            if (!csvFile.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            {
                return new BadRequestObjectResult(new { message = "File must be a .csv file" });
            }

            // Parse the CSV file
            CsvParserService.MarshalCsvParseResult parseResult;
            await using (Stream stream = csvFile.OpenReadStream())
            {
                parseResult = CsvParserService.ParseMarshalsCsv(stream);
            }

            if (parseResult.Marshals.Count == 0 && parseResult.Errors.Count > 0)
            {
                return new BadRequestObjectResult(new ImportMarshalsResponse(0, 0, parseResult.Errors));
            }

            // Load existing marshals to check for updates
            IEnumerable<MarshalEntity> existingMarshalsList = await _marshalRepository.GetByEventAsync(eventId);
            Dictionary<string, MarshalEntity> existingMarshals = existingMarshalsList
                .ToDictionary(m => m.Name.ToLower(), m => m);

            // Preload all locations for efficient lookup during import
            IEnumerable<LocationEntity> existingLocationsList = await _locationRepository.GetByEventAsync(eventId);
            Dictionary<string, LocationEntity> locationCache = existingLocationsList
                .ToDictionary(l => l.Name.ToLower(), l => l);

            int marshalsCreated = 0;
            int assignmentsCreated = 0;

            foreach (CsvParserService.MarshalCsvRow row in parseResult.Marshals)
            {
                try
                {
                    bool isUpdate = existingMarshals.TryGetValue(row.Name.ToLower(), out MarshalEntity? existing);

                    MarshalEntity marshalEntity;
                    if (isUpdate && existing != null)
                    {
                        marshalEntity = await UpdateExistingMarshal(existing, row, eventId);
                    }
                    else
                    {
                        marshalEntity = await CreateNewMarshal(row, eventId);
                        marshalsCreated++;
                    }

                    // Create assignment if checkpoint is specified
                    if (!string.IsNullOrWhiteSpace(row.Checkpoint))
                    {
                        bool assignmentCreated = await CreateAssignmentForCheckpoint(
                            marshalEntity, row.Checkpoint, eventId, locationCache, parseResult.Errors);

                        if (assignmentCreated)
                        {
                            assignmentsCreated++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    parseResult.Errors.Add($"Failed to process marshal '{row.Name}': {ex.Message}");
                }
            }

            _logger.LogInformation("Imported {MarshalsCreated} new marshals and created {AssignmentsCreated} assignments for event {EventId}", marshalsCreated, assignmentsCreated, eventId);

            return new OkObjectResult(new ImportMarshalsResponse(marshalsCreated, assignmentsCreated, parseResult.Errors));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing marshals");
            return new StatusCodeResult(500);
        }
    }
#pragma warning restore MA0051

    private async Task<MarshalEntity> UpdateExistingMarshal(
        MarshalEntity marshalEntity,
        CsvParserService.MarshalCsvRow row,
        string eventId)
    {
        // Update existing marshal - only update fields that are provided
        if (!string.IsNullOrWhiteSpace(row.Email))
        {
            marshalEntity.Email = row.Email;
        }

        if (!string.IsNullOrWhiteSpace(row.Phone))
        {
            marshalEntity.PhoneNumber = row.Phone;
        }

        await _marshalRepository.UpdateAsync(marshalEntity);

        // Update denormalized name in all assignments
        IEnumerable<AssignmentEntity> assignments = await _assignmentRepository.GetByMarshalAsync(eventId, marshalEntity.MarshalId);
        foreach (AssignmentEntity assignment in assignments)
        {
            assignment.MarshalName = marshalEntity.Name;
            await _assignmentRepository.UpdateAsync(assignment);
        }

        return marshalEntity;
    }

    private async Task<MarshalEntity> CreateNewMarshal(
        CsvParserService.MarshalCsvRow row,
        string eventId)
    {
        string newMarshalId = Guid.NewGuid().ToString();
        MarshalEntity marshalEntity = new()
        {
            PartitionKey = eventId,
            RowKey = newMarshalId,
            EventId = eventId,
            MarshalId = newMarshalId,
            MagicCode = AuthService.GenerateMagicCode(),
            Name = row.Name,
            Email = row.Email,
            PhoneNumber = row.Phone,
            Notes = string.Empty
        };

        await _marshalRepository.AddAsync(marshalEntity);
        _logger.LogInformation("Created new marshal from CSV: {MarshalId} - {MarshalName}", newMarshalId, row.Name);

        return marshalEntity;
    }

    private async Task<bool> CreateAssignmentForCheckpoint(
        MarshalEntity marshalEntity,
        string checkpointName,
        string eventId,
        Dictionary<string, LocationEntity> locationCache,
        List<string> errors)
    {
        // Find the location by name using cached lookup (O(1))
        if (!locationCache.TryGetValue(checkpointName.ToLower(), out LocationEntity? location))
        {
            errors.Add($"Checkpoint '{checkpointName}' not found for marshal '{marshalEntity.Name}'");
            return false;
        }

        // Check if assignment already exists
        bool assignmentExists = await _assignmentRepository.ExistsAsync(eventId, marshalEntity.MarshalId, location.RowKey);

        if (assignmentExists)
        {
            return false;
        }

        AssignmentEntity assignmentEntity = new()
        {
            PartitionKey = eventId,
            RowKey = Guid.NewGuid().ToString(),
            EventId = eventId,
            LocationId = location.RowKey,
            MarshalId = marshalEntity.MarshalId,
            MarshalName = marshalEntity.Name
        };

        await _assignmentRepository.AddAsync(assignmentEntity);
        return true;
    }

    /// <summary>
    /// Checks if an area lead has access to a marshal's magic link.
    /// Area leads can only access magic links for marshals assigned to checkpoints in their areas.
    /// </summary>
    private async Task<bool> CanAreaLeadAccessMarshalMagicLinkAsync(UserClaims claims, string eventId, string marshalId)
    {
        // Get area lead's area IDs
        List<string> areaLeadAreaIds = claims.EventRoles
            .Where(r => r.Role == Constants.RoleEventAreaLead)
            .SelectMany(r => r.AreaIds)
            .ToList();

        if (areaLeadAreaIds.Count == 0)
        {
            return false;
        }

        // Get marshal's assignments to find their checkpoint areas
        IEnumerable<AssignmentEntity> assignments = await _assignmentRepository.GetByMarshalAsync(eventId, marshalId);
        List<string> locationIds = assignments.Select(a => a.LocationId).Distinct().ToList();

        // Get locations to find area IDs
        HashSet<string> marshalAreaIds = [];
        foreach (string locId in locationIds)
        {
            LocationEntity? loc = await _locationRepository.GetAsync(eventId, locId);
            if (loc != null)
            {
                List<string> areaIds = loc.GetPayload().AreaIds;
                foreach (string areaId in areaIds)
                {
                    marshalAreaIds.Add(areaId);
                }
            }
        }

        // Check if marshal is in any of the area lead's areas
        return marshalAreaIds.Any(areaId => areaLeadAreaIds.Contains(areaId));
    }

    /// <summary>
    /// Syncs roles from a marshal to their linked contact.
    /// When a marshal has roles updated and is linked to a contact, the contact's roles are updated to match.
    /// </summary>
    private async Task SyncRolesToLinkedContactAsync(string eventId, string marshalId, List<string> roles)
    {
        // Find any contact linked to this marshal
        IEnumerable<EventContactEntity> contacts = await _contactRepository.GetByEventAsync(eventId);
        EventContactEntity? linkedContact = contacts.FirstOrDefault(c => c.MarshalId == marshalId && !c.IsDeleted);

        if (linkedContact == null)
        {
            return; // No linked contact, nothing to do
        }

        // Update the contact's roles to match the marshal's roles
        linkedContact.RolesJson = JsonSerializer.Serialize(roles);
        linkedContact.Role = roles.Count > 0 ? roles[0] : string.Empty; // Keep legacy field for backwards compatibility
        linkedContact.UpdatedAt = DateTime.UtcNow;
        await _contactRepository.UpdateAsync(linkedContact);

        _logger.LogInformation("Synced roles from marshal {MarshalId} to linked contact {ContactId} in event {EventId}: {Roles}",
            marshalId, linkedContact.ContactId, eventId, string.Join(", ", roles));
    }

    /// <summary>
    /// Gets notes that are specifically scoped to a checkpoint (not area-based scopes).
    /// Matches the frontend logic in MarshalView.vue getNotesForCheckpoint().
    /// </summary>
    private static List<NoteEmailInfo> GetNotesForCheckpoint(IEnumerable<NoteEntity> allNotes, string locationId)
    {
        List<NoteEmailInfo> matchedNotes = [];
        HashSet<string> seenNoteIds = [];

        foreach (NoteEntity note in allNotes)
        {
            if (seenNoteIds.Contains(note.NoteId))
            {
                continue;
            }

            List<ScopeConfiguration> scopeConfigurations = JsonSerializer.Deserialize<List<ScopeConfiguration>>(
                note.ScopeConfigurationsJson, FunctionHelpers.JsonOptions) ?? [];

            bool matched = false;

            foreach (ScopeConfiguration config in scopeConfigurations)
            {
                // Only match checkpoint-specific scopes (not area-based)
                if (config.ItemType == "Checkpoint" &&
                    (config.Ids.Contains(locationId) || config.Ids.Contains("ALL_CHECKPOINTS")))
                {
                    matched = true;
                    break;
                }
            }

            if (matched)
            {
                seenNoteIds.Add(note.NoteId);
                matchedNotes.Add(new NoteEmailInfo(
                    note.Title,
                    note.Content,
                    note.Priority,
                    note.IsPinned
                ));
            }
        }

        // Sort by: Pinned first → Priority (Emergency > Urgent > High > Normal > Low) → DisplayOrder → CreatedAt (newest first)
        Dictionary<string, int> priorityOrder = new()
        {
            { "Emergency", 0 },
            { "Urgent", 1 },
            { "High", 2 },
            { "Normal", 3 },
            { "Low", 4 }
        };

        return [.. matchedNotes
            .OrderByDescending(n => n.IsPinned)
            .ThenBy(n => priorityOrder.GetValueOrDefault(n.Priority, 3))
            .ThenBy(n => allNotes.First(note => note.Title == n.Title && note.Content == n.Content).DisplayOrder)
            .ThenByDescending(n => allNotes.First(note => note.Title == n.Title && note.Content == n.Content).CreatedAt)];
    }
}
