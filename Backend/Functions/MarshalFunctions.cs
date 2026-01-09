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

            // Require authentication
            string? sessionToken = FunctionHelpers.GetSessionToken(req);
            if (string.IsNullOrWhiteSpace(sessionToken))
            {
                return new UnauthorizedObjectResult(new { message = "Authentication required" });
            }

            UserClaims? claims = await _claimsService.GetClaimsAsync(sessionToken, request.EventId);
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
                Notes = sanitizedNotes
            };

            await _marshalRepository.AddAsync(marshalEntity);

            // Create pending checklist items scoped to this marshal
            if (request.PendingNewChecklistItems != null && request.PendingNewChecklistItems.Count > 0)
            {
                string adminEmail = req.Headers[Constants.AdminEmailHeader].ToString();
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

            // Require authentication
            string? sessionToken = FunctionHelpers.GetSessionToken(req);
            _logger.LogInformation("Session token present: {SessionTokenPresent}", !string.IsNullOrWhiteSpace(sessionToken));
            if (string.IsNullOrWhiteSpace(sessionToken))
            {
                _logger.LogWarning("No session token provided");
                return new UnauthorizedObjectResult(new { message = "Authentication required" });
            }

            UserClaims? claims = await _claimsService.GetClaimsAsync(sessionToken, eventId);
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

                marshals.Add(new MarshalWithPermissionsResponse(
                    marshalEntity.MarshalId,
                    marshalEntity.EventId,
                    marshalEntity.Name,
                    canViewContact ? marshalEntity.Email : null,
                    canViewContact ? marshalEntity.PhoneNumber : null,
                    canViewContact ? marshalEntity.Notes : null,
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
            // Require authentication
            string? sessionToken = FunctionHelpers.GetSessionToken(req);
            if (string.IsNullOrWhiteSpace(sessionToken))
            {
                return new UnauthorizedObjectResult(new { message = "Authentication required" });
            }

            UserClaims? claims = await _claimsService.GetClaimsAsync(sessionToken, eventId);
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

            MarshalWithPermissionsResponse response = new(
                marshalEntity.MarshalId,
                marshalEntity.EventId,
                marshalEntity.Name,
                canViewContact ? marshalEntity.Email : null,
                canViewContact ? marshalEntity.PhoneNumber : null,
                canViewContact ? marshalEntity.Notes : null,
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
            // Require authentication
            string? sessionToken = FunctionHelpers.GetSessionToken(req);
            if (string.IsNullOrWhiteSpace(sessionToken))
            {
                return new UnauthorizedObjectResult(new { message = "Authentication required" });
            }

            UserClaims? claims = await _claimsService.GetClaimsAsync(sessionToken, eventId);
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

            // Only admins can modify notes (notes may contain sensitive admin-only info)
            if (claims.IsEventAdmin || claims.IsSystemAdmin)
            {
                marshalEntity.Notes = sanitizedNotes;
            }

            await _marshalRepository.UpdateAsync(marshalEntity);

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

            MarshalWithPermissionsResponse response = new(
                marshalEntity.MarshalId,
                marshalEntity.EventId,
                marshalEntity.Name,
                canViewContact ? marshalEntity.Email : null,
                canViewContact ? marshalEntity.PhoneNumber : null,
                canViewContact ? marshalEntity.Notes : null,
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
            // Require authentication
            string? sessionToken = FunctionHelpers.GetSessionToken(req);
            if (string.IsNullOrWhiteSpace(sessionToken))
            {
                return new UnauthorizedObjectResult(new { message = "Authentication required" });
            }

            UserClaims? claims = await _claimsService.GetClaimsAsync(sessionToken, eventId);
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

    [Function("GetMarshalMagicLink")]
    public async Task<IActionResult> GetMarshalMagicLink(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "marshals/{eventId}/{marshalId}/magic-link")] HttpRequest req,
        string eventId,
        string marshalId)
    {
        try
        {
            // Require authentication
            string? sessionToken = FunctionHelpers.GetSessionToken(req);
            if (string.IsNullOrWhiteSpace(sessionToken))
            {
                return new UnauthorizedObjectResult(new { message = "Authentication required" });
            }

            UserClaims? claims = await _claimsService.GetClaimsAsync(sessionToken, eventId);
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
            string magicLink = $"{frontendUrl}/#/event/{eventId}?code={marshalEntity.MagicCode}";

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

#pragma warning disable MA0051
    [Function("SendMarshalMagicLink")]
    public async Task<IActionResult> SendMarshalMagicLink(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "marshals/{eventId}/{marshalId}/send-magic-link")] HttpRequest req,
        string eventId,
        string marshalId)
    {
        try
        {
            // Require authentication
            string? sessionToken = FunctionHelpers.GetSessionToken(req);
            if (string.IsNullOrWhiteSpace(sessionToken))
            {
                return new UnauthorizedObjectResult(new { message = "Authentication required" });
            }

            UserClaims? claims = await _claimsService.GetClaimsAsync(sessionToken, eventId);
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

            if (string.IsNullOrWhiteSpace(marshalEntity.Email))
            {
                return new BadRequestObjectResult(new { message = "Marshal does not have an email address" });
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

            // Get event name
            EventEntity? eventEntity = await _eventRepository.GetAsync(eventId);
            string eventName = eventEntity?.Name ?? "Event";

            // Get frontend URL - prefer explicit value from request body, fall back to header detection
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            SendMarshalMagicLinkRequest? request = string.IsNullOrWhiteSpace(requestBody)
                ? null
                : JsonSerializer.Deserialize<SendMarshalMagicLinkRequest>(requestBody, FunctionHelpers.JsonOptions);
            string frontendUrl = !string.IsNullOrWhiteSpace(request?.FrontendUrl)
                ? request.FrontendUrl.TrimEnd('/')
                : FunctionHelpers.GetFrontendUrl(req);
            string magicLink = $"{frontendUrl}/#/event/{eventId}?code={marshalEntity.MagicCode}";

            // Send the email
            await _emailService.SendMarshalMagicLinkEmailAsync(
                marshalEntity.Email,
                marshalEntity.Name,
                eventName,
                magicLink
            );

            _logger.LogInformation("Sent magic link email to marshal {MarshalId} at {Email}", marshalId, marshalEntity.Email);

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
            // Require authentication
            string? sessionToken = FunctionHelpers.GetSessionToken(req);
            if (string.IsNullOrWhiteSpace(sessionToken))
            {
                return new UnauthorizedObjectResult(new { message = "Authentication required" });
            }

            UserClaims? claims = await _claimsService.GetClaimsAsync(sessionToken, eventId);
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
}
