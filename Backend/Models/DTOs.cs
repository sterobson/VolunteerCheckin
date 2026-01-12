namespace VolunteerCheckin.Functions.Models;

public record EmergencyContact(
    string Name,
    string Phone,
    string Details
);

public record CreateEventRequest(
    string Name,
    string Description,
    DateTime EventDate,
    string TimeZoneId,
    string AdminEmail,
    List<EmergencyContact> EmergencyContacts,
    // Terminology settings (optional, defaults will be used if not provided)
    string? PeopleTerm = null,
    string? CheckpointTerm = null,
    string? AreaTerm = null,
    string? ChecklistTerm = null,
    string? CourseTerm = null,
    // Default checkpoint style (optional)
    string? DefaultCheckpointStyleType = null,
    string? DefaultCheckpointStyleColor = null,
    string? DefaultCheckpointStyleBackgroundShape = null,
    string? DefaultCheckpointStyleBackgroundColor = null,
    string? DefaultCheckpointStyleBorderColor = null,
    string? DefaultCheckpointStyleIconColor = null,
    string? DefaultCheckpointStyleSize = null,
    string? DefaultCheckpointStyleMapRotation = null,
    // Marshal mode branding (optional)
    string? BrandingHeaderGradientStart = null,
    string? BrandingHeaderGradientEnd = null,
    string? BrandingLogoUrl = null,
    string? BrandingLogoPosition = null,
    string? BrandingAccentColor = null,
    string? BrandingPageGradientStart = null,
    string? BrandingPageGradientEnd = null
);

public record UpdateEventRequest(
    string Name,
    string Description,
    DateTime EventDate,
    string TimeZoneId,
    // Terminology settings (optional, keeps existing values if not provided)
    string? PeopleTerm = null,
    string? CheckpointTerm = null,
    string? AreaTerm = null,
    string? ChecklistTerm = null,
    string? CourseTerm = null,
    // Default checkpoint style (optional)
    string? DefaultCheckpointStyleType = null,
    string? DefaultCheckpointStyleColor = null,
    string? DefaultCheckpointStyleBackgroundShape = null,
    string? DefaultCheckpointStyleBackgroundColor = null,
    string? DefaultCheckpointStyleBorderColor = null,
    string? DefaultCheckpointStyleIconColor = null,
    string? DefaultCheckpointStyleSize = null,
    string? DefaultCheckpointStyleMapRotation = null,
    // Marshal mode branding (optional, keeps existing values if not provided)
    string? BrandingHeaderGradientStart = null,
    string? BrandingHeaderGradientEnd = null,
    string? BrandingLogoUrl = null,
    string? BrandingLogoPosition = null,
    string? BrandingAccentColor = null,
    string? BrandingPageGradientStart = null,
    string? BrandingPageGradientEnd = null
);

public record RoutePoint(
    double Lat,
    double Lng
);

public record EventResponse(
    string Id,
    string Name,
    string Description,
    DateTime EventDate,
    string TimeZoneId,
    string AdminEmail,
    List<EmergencyContact> EmergencyContacts,
    List<RoutePoint> Route,
    bool IsActive,
    DateTime CreatedDate,
    // Terminology settings
    string PeopleTerm,
    string CheckpointTerm,
    string AreaTerm,
    string ChecklistTerm,
    string CourseTerm,
    // Default checkpoint style
    string DefaultCheckpointStyleType,
    string DefaultCheckpointStyleColor,
    string DefaultCheckpointStyleBackgroundShape,
    string DefaultCheckpointStyleBackgroundColor,
    string DefaultCheckpointStyleBorderColor,
    string DefaultCheckpointStyleIconColor,
    string DefaultCheckpointStyleSize,
    string DefaultCheckpointStyleMapRotation,
    // Marshal mode branding
    string BrandingHeaderGradientStart,
    string BrandingHeaderGradientEnd,
    string BrandingLogoUrl,
    string BrandingLogoPosition,
    string BrandingAccentColor,
    string BrandingPageGradientStart,
    string BrandingPageGradientEnd
);

public record CreateLocationRequest(
    string EventId,
    string Name,
    string Description,
    double Latitude,
    double Longitude,
    int RequiredMarshals,
    string? What3Words,
    DateTime? StartTime,
    DateTime? EndTime,
    List<PendingChecklistItem>? PendingNewChecklistItems = null,
    List<PendingNote>? PendingNewNotes = null,
    // Checkpoint style (optional)
    string? StyleType = null,
    string? StyleColor = null,
    string? StyleBackgroundShape = null,
    string? StyleBackgroundColor = null,
    string? StyleBorderColor = null,
    string? StyleIconColor = null,
    string? StyleSize = null,
    string? StyleMapRotation = null,
    // Terminology override (optional, null = don't change, empty = inherit from area -> event)
    string? PeopleTerm = null,
    string? CheckpointTerm = null,
    // Dynamic checkpoint settings (for lead car/sweep vehicle tracking)
    bool IsDynamic = false,
    List<ScopeConfiguration>? LocationUpdateScopeConfigurations = null
);

public record LocationResponse(
    string Id,
    string EventId,
    string Name,
    string Description,
    double Latitude,
    double Longitude,
    int RequiredMarshals,
    int CheckedInCount,
    string What3Words,
    DateTime? StartTime,
    DateTime? EndTime,
    List<string> AreaIds,
    // Checkpoint style
    string StyleType,
    string StyleColor,
    string StyleBackgroundShape,
    string StyleBackgroundColor,
    string StyleBorderColor,
    string StyleIconColor,
    string StyleSize,
    string StyleMapRotation,
    // Resolved style (computed from checkpoint -> area -> event hierarchy)
    string ResolvedStyleType,
    string ResolvedStyleColor,
    string ResolvedStyleBackgroundShape,
    string ResolvedStyleBackgroundColor,
    string ResolvedStyleBorderColor,
    string ResolvedStyleIconColor,
    string ResolvedStyleSize,
    string ResolvedStyleMapRotation,
    // Terminology override (empty = inherit from area -> event)
    string PeopleTerm,
    string CheckpointTerm,
    // Resolved terminology (computed from checkpoint -> area -> event hierarchy)
    string ResolvedPeopleTerm,
    string ResolvedCheckpointTerm,
    // Dynamic checkpoint settings (for lead car/sweep vehicle tracking)
    bool IsDynamic,
    List<ScopeConfiguration> LocationUpdateScopeConfigurations,
    DateTime? LastLocationUpdate,
    string LastUpdatedByPersonId
);

public record AreaContact(
    string MarshalId,
    string MarshalName,
    string Role,
    string? Phone = null,
    string? Email = null
);

public record CreateAreaRequest(
    string EventId,
    string Name,
    string Description,
    string Color,
    List<AreaContact> Contacts,
    List<RoutePoint>? Polygon,
    // Default checkpoint style for checkpoints in this area (optional)
    string? CheckpointStyleType = null,
    string? CheckpointStyleColor = null,
    string? CheckpointStyleBackgroundShape = null,
    string? CheckpointStyleBackgroundColor = null,
    string? CheckpointStyleBorderColor = null,
    string? CheckpointStyleIconColor = null,
    string? CheckpointStyleSize = null,
    string? CheckpointStyleMapRotation = null
);

public record UpdateAreaRequest(
    string Name,
    string Description,
    string Color,
    List<AreaContact>? Contacts,
    List<RoutePoint>? Polygon,
    int DisplayOrder,
    // Default checkpoint style for checkpoints in this area (optional)
    string? CheckpointStyleType = null,
    string? CheckpointStyleColor = null,
    string? CheckpointStyleBackgroundShape = null,
    string? CheckpointStyleBackgroundColor = null,
    string? CheckpointStyleBorderColor = null,
    string? CheckpointStyleIconColor = null,
    string? CheckpointStyleSize = null,
    string? CheckpointStyleMapRotation = null,
    // Terminology overrides (optional, null = don't change, empty = inherit from event)
    string? PeopleTerm = null,
    string? CheckpointTerm = null,
    // Event contacts to unlink from this area (removes area from their scope)
    List<string>? ContactsToRemove = null
);

public record AreaResponse(
    string Id,
    string EventId,
    string Name,
    string Description,
    string Color,
    List<AreaContact> Contacts,
    List<RoutePoint> Polygon,
    bool IsDefault,
    int DisplayOrder,
    int CheckpointCount,
    DateTime CreatedDate,
    // Default checkpoint style for checkpoints in this area
    string CheckpointStyleType,
    string CheckpointStyleColor,
    string CheckpointStyleBackgroundShape,
    string CheckpointStyleBackgroundColor,
    string CheckpointStyleBorderColor,
    string CheckpointStyleIconColor,
    string CheckpointStyleSize,
    string CheckpointStyleMapRotation,
    // Terminology overrides (empty = inherit from event)
    string PeopleTerm,
    string CheckpointTerm
);

public record BulkAssignCheckpointsRequest(
    List<string> LocationIds,
    string AreaId
);

public record CreateAssignmentRequest(
    string EventId,
    string LocationId,
    string? MarshalId,
    string? MarshalName
);

public record AssignmentResponse(
    string Id,
    string EventId,
    string LocationId,
    string MarshalId,
    string MarshalName,
    bool IsCheckedIn,
    DateTime? CheckInTime,
    double? CheckInLatitude,
    double? CheckInLongitude,
    string CheckInMethod,
    string CheckedInBy
);

public record CheckInRequest(
    string EventId,
    string AssignmentId,
    double? Latitude,
    double? Longitude,
    bool ManualCheckIn
);

public record ToggleCheckInRequest(
    double? Latitude,
    double? Longitude,
    string? Action // "check-in", "check-out", or null for toggle
);

public record InstantLoginRequest(
    string Email
);

public record InstantLoginResponse(
    bool Success,
    string Email,
    string Message
);

public record EventStatusResponse(
    string EventId,
    List<LocationStatusResponse> Locations
);

public record LocationStatusResponse(
    string Id,
    string Name,
    string Description,
    double Latitude,
    double Longitude,
    int RequiredMarshals,
    int CheckedInCount,
    List<AssignmentResponse> Assignments,
    string What3Words,
    DateTime? StartTime,
    DateTime? EndTime,
    List<string> AreaIds,
    // Checkpoint style
    string StyleType,
    string StyleColor,
    string StyleBackgroundShape,
    string StyleBackgroundColor,
    string StyleBorderColor,
    string StyleIconColor,
    string StyleSize,
    string StyleMapRotation,
    // Resolved style (computed from checkpoint -> area -> event hierarchy)
    string ResolvedStyleType,
    string ResolvedStyleColor,
    string ResolvedStyleBackgroundShape,
    string ResolvedStyleBackgroundColor,
    string ResolvedStyleBorderColor,
    string ResolvedStyleIconColor,
    string ResolvedStyleSize,
    string ResolvedStyleMapRotation,
    // Terminology
    string PeopleTerm,
    string CheckpointTerm,
    // Dynamic checkpoint settings
    bool IsDynamic,
    List<ScopeConfiguration> LocationUpdateScopeConfigurations,
    DateTime? LastLocationUpdate
);

public record UserEventMappingResponse(
    string EventId,
    string UserEmail,
    string Role,
    DateTime CreatedDate
);

public record AddEventAdminRequest(
    string UserEmail
);

public record RemoveEventAdminRequest(
    string UserEmail
);

public record ImportLocationsResponse(
    int LocationsCreated,
    int AssignmentsCreated,
    List<string> Errors
);

public record ImportMarshalsResponse(
    int MarshalsCreated,
    int AssignmentsCreated,
    List<string> Errors
);

public record CreateMarshalRequest(
    string EventId,
    string Name,
    string Email,
    string PhoneNumber,
    string Notes,
    List<PendingChecklistItem>? PendingNewChecklistItems = null,
    List<PendingNote>? PendingNewNotes = null
);

/// <summary>
/// A pending checklist item to create, scoped to the entity being created.
/// </summary>
public record PendingChecklistItem(
    string Text
);

/// <summary>
/// A pending note to create, scoped to the entity being created.
/// </summary>
public record PendingNote(
    string Title,
    string Content
);

public record UpdateMarshalRequest(
    string Name,
    string Email,
    string PhoneNumber,
    string Notes
);

public record MarshalResponse(
    string Id,
    string EventId,
    string Name,
    string Email,
    string PhoneNumber,
    string Notes,
    List<string> AssignedLocationIds,
    bool IsCheckedIn,
    DateTime CreatedDate,
    DateTime? LastAccessedDate
);

/// <summary>
/// Marshal response with permission-based contact visibility.
/// Contact fields (Email, PhoneNumber, Notes) are null if user lacks permission to view them.
/// </summary>
public record MarshalWithPermissionsResponse(
    string Id,
    string EventId,
    string Name,
    string? Email,           // null if user cannot view contact details
    string? PhoneNumber,     // null if user cannot view contact details
    string? Notes,           // null if user cannot view contact details
    List<string> AssignedLocationIds,
    bool IsCheckedIn,
    DateTime CreatedDate,
    DateTime? LastAccessedDate,
    bool CanViewContactDetails,  // true if Email/Phone/Notes are visible
    bool CanModify               // true if user can edit this marshal
);

/// <summary>
/// Response containing marshal magic link information.
/// </summary>
public record MarshalMagicLinkResponse(
    string MagicCode,
    string MagicLink,
    bool HasEmail
);

/// <summary>
/// Request to send a marshal magic link via email.
/// </summary>
public record SendMarshalMagicLinkRequest(
    string? FrontendUrl = null
);

public record BulkUpdateLocationTimesRequest(
    TimeSpan TimeDelta
);

public record PaginatedResponse<T>(
    List<T> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages
);

public record ErrorResponse(
    string Message,
    string? Details = null,
    string? ErrorCode = null
);

// Checklist-related DTOs

public record CreateChecklistItemRequest(
    string Text,
    List<ScopeConfiguration> ScopeConfigurations,
    int DisplayOrder,
    bool IsRequired,
    DateTime? VisibleFrom,
    DateTime? VisibleUntil,
    DateTime? MustCompleteBy,
    bool CreateSeparateItems = false
);

public record UpdateChecklistItemRequest(
    string Text,
    List<ScopeConfiguration> ScopeConfigurations,
    int DisplayOrder,
    bool IsRequired,
    DateTime? VisibleFrom,
    DateTime? VisibleUntil,
    DateTime? MustCompleteBy
);

public record ChecklistItemResponse(
    string ItemId,
    string EventId,
    string Text,
    List<ScopeConfiguration> ScopeConfigurations,
    int DisplayOrder,
    bool IsRequired,
    DateTime? VisibleFrom,
    DateTime? VisibleUntil,
    DateTime? MustCompleteBy,
    string CreatedByAdminEmail,
    DateTime CreatedDate,
    DateTime? LastModifiedDate,
    string LastModifiedByAdminEmail
);

public record ChecklistItemWithStatus(
    string ItemId,
    string EventId,
    string Text,
    List<ScopeConfiguration> ScopeConfigurations,
    int DisplayOrder,
    bool IsRequired,
    DateTime? VisibleFrom,
    DateTime? VisibleUntil,
    DateTime? MustCompleteBy,
    bool IsCompleted,
    bool CanBeCompletedByMe,
    string? CompletedByActorName,
    string? CompletedByActorType,
    string? CompletedByActorId,
    DateTime? CompletedAt,
    string CompletionContextType,
    string CompletionContextId,
    string MatchedScope,  // Which scope configuration matched for this marshal
    string? ContextOwnerName,  // Name of the person this item belongs to (for personal items)
    string? ContextOwnerMarshalId  // Marshal ID of the owner (for personal items)
);

public record CompleteChecklistItemRequest(
    string MarshalId,
    string? ContextType = null,  // Optional override for context
    string? ContextId = null,    // Optional override for context
    string? ActorMarshalId = null // Who is performing the action (e.g., area lead completing on behalf of marshal)
);

public record ChecklistCompletionResponse(
    string CompletionId,
    string EventId,
    string ChecklistItemId,
    string CompletionContextType,
    string CompletionContextId,
    string ContextOwnerMarshalId,
    string ContextOwnerMarshalName,
    string ActorType,
    string ActorId,
    string ActorName,
    DateTime CompletedAt,
    bool IsDeleted
);

// Authentication and Claims DTOs

/// <summary>
/// Information about a person (core identity)
/// </summary>
public record PersonInfo(
    string PersonId,
    string Name,
    string Email,
    string Phone,
    bool IsSystemAdmin
);

/// <summary>
/// Event-specific role information
/// </summary>
public record EventRoleInfo(
    string Role,              // "EventAdmin", "EventAreaAdmin", "EventAreaLead"
    List<string> AreaIds      // Empty for event-wide roles, specific areas for area-scoped roles
);

/// <summary>
/// Claims for the current user's session.
/// Computed per-request based on PersonId + EventId + AuthMethod.
/// Gates what actions the user can perform.
/// </summary>
public record UserClaims(
    string PersonId,
    string PersonName,
    string PersonEmail,
    bool IsSystemAdmin,
    string? EventId,                  // null for cross-event admin sessions, set for event-specific sessions
    string AuthMethod,                // "MarshalMagicCode" or "SecureEmailLink"
    string? MarshalId,                // If they are a marshal in this event
    List<EventRoleInfo> EventRoles    // Roles in the current event (empty if EventId is null)
)
{
    /// <summary>
    /// Check if user has a specific role in the event
    /// </summary>
    public bool HasRole(string role) => EventRoles.Any(r => r.Role == role);

    /// <summary>
    /// Check if user has EventAdmin role
    /// </summary>
    public bool IsEventAdmin => HasRole(Constants.RoleEventAdmin);

    /// <summary>
    /// Check if user has EventAreaAdmin role for a specific area
    /// </summary>
    public bool IsAreaAdmin(string areaId) =>
        EventRoles.Any(r => r.Role == Constants.RoleEventAreaAdmin &&
                           (r.AreaIds.Count == 0 || r.AreaIds.Contains(areaId)));

    /// <summary>
    /// Check if user has EventAreaLead role for a specific area
    /// </summary>
    public bool IsAreaLead(string areaId) =>
        EventRoles.Any(r => r.Role == Constants.RoleEventAreaLead &&
                           (r.AreaIds.Count == 0 || r.AreaIds.Contains(areaId)));

    /// <summary>
    /// Check if user can perform elevated actions (admin/lead operations)
    /// Only allowed if authenticated via SecureEmailLink
    /// </summary>
    public bool CanUseElevatedPermissions => AuthMethod == Constants.AuthMethodSecureEmailLink;

    /// <summary>
    /// Check if user can act as a marshal (complete tasks, check in, etc.)
    /// </summary>
    public bool CanActAsMarshal => MarshalId != null;
};

/// <summary>
/// Request to send a magic link to an email address
/// </summary>
public record RequestLoginRequest(
    string Email,
    string? FrontendUrl = null
);

/// <summary>
/// Response after requesting a magic link
/// </summary>
public record RequestLoginResponse(
    bool Success,
    string Message
);

/// <summary>
/// Request to verify a magic link token
/// </summary>
public record VerifyTokenRequest(
    string Token
);

/// <summary>
/// Response after verifying a magic link token (returns session token)
/// </summary>
public record VerifyTokenResponse(
    bool Success,
    string? SessionToken,
    PersonInfo? Person,
    string? Message
);

/// <summary>
/// Request to login as a marshal using magic code
/// </summary>
public record MarshalLoginRequest(
    string EventId,
    string MagicCode
);

/// <summary>
/// Response after marshal login (returns session token)
/// </summary>
public record MarshalLoginResponse(
    bool Success,
    string? SessionToken,
    PersonInfo? Person,
    string? MarshalId,
    string? Message
);

/// <summary>
/// Request to update current user's profile
/// </summary>
public record UpdateProfileRequest(
    string Name,
    string Email,
    string? Phone
);

/// <summary>
/// Request for admin to update another person's details
/// </summary>
public record UpdatePersonRequest(
    string Name,
    string Email,
    string? Phone
);

/// <summary>
/// Detailed person response with roles
/// </summary>
public record PersonDetailsResponse(
    string PersonId,
    string Name,
    string Email,
    string Phone,
    bool IsSystemAdmin,
    List<EventRoleInfo> EventRoles,
    DateTime CreatedAt
);

// Note-related DTOs

/// <summary>
/// Request to create a new note
/// </summary>
public record CreateNoteRequest(
    string Title,
    string Content,
    List<ScopeConfiguration> ScopeConfigurations,
    int DisplayOrder = 0,
    string Priority = Constants.NotePriorityNormal,
    string? Category = null,
    bool IsPinned = false
);

/// <summary>
/// Request to update an existing note
/// </summary>
public record UpdateNoteRequest(
    string Title,
    string Content,
    List<ScopeConfiguration> ScopeConfigurations,
    int DisplayOrder,
    string Priority,
    string? Category,
    bool IsPinned
);

/// <summary>
/// Note response for admin views (includes all details)
/// </summary>
public record NoteResponse(
    string NoteId,
    string EventId,
    string Title,
    string Content,
    List<ScopeConfiguration> ScopeConfigurations,
    int DisplayOrder,
    string Priority,
    string? Category,
    bool IsPinned,
    string CreatedByPersonId,
    string CreatedByName,
    DateTime CreatedAt,
    string? UpdatedByPersonId,
    string? UpdatedByName,
    DateTime? UpdatedAt
);

/// <summary>
/// Note response for marshal views (simplified, with relevance info)
/// </summary>
public record NoteForMarshalResponse(
    string NoteId,
    string EventId,
    string Title,
    string Content,
    string Priority,
    string? Category,
    bool IsPinned,
    DateTime CreatedAt,
    string CreatedByName,
    string MatchedScope  // Which scope configuration matched for this marshal
);

// Event Contact DTOs

/// <summary>
/// Request to create a new event contact
/// </summary>
public record CreateEventContactRequest(
    string Role,
    string Name,
    string? Phone = null,
    string? Email = null,
    string? Notes = null,
    string? MarshalId = null,
    List<ScopeConfiguration>? ScopeConfigurations = null,
    int DisplayOrder = 0,
    bool IsPrimary = false
);

/// <summary>
/// Request to update an existing event contact
/// </summary>
public record UpdateEventContactRequest(
    string Role,
    string Name,
    string? Phone = null,
    string? Email = null,
    string? Notes = null,
    string? MarshalId = null,
    List<ScopeConfiguration>? ScopeConfigurations = null,
    int DisplayOrder = 0,
    bool IsPrimary = false
);

/// <summary>
/// Event contact response for admin views (includes all details)
/// </summary>
public record EventContactResponse(
    string ContactId,
    string EventId,
    string Role,
    string Name,
    string Phone,
    string? Email,
    string? Notes,
    string? MarshalId,
    string? MarshalName,
    List<ScopeConfiguration> ScopeConfigurations,
    int DisplayOrder,
    bool IsPrimary,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

/// <summary>
/// Event contact response for marshal views (simplified, with visibility info)
/// </summary>
public record EventContactForMarshalResponse(
    string ContactId,
    string Role,
    string Name,
    string Phone,
    string? Email,
    string? Notes,
    bool IsPrimary,
    string MatchedScope  // Which scope configuration matched for this marshal
);

/// <summary>
/// Response containing available contact roles
/// </summary>
public record ContactRolesResponse(
    List<string> BuiltInRoles,
    List<string> CustomRoles
);

// Area Lead Dashboard DTOs

/// <summary>
/// Response for the area lead dashboard - contains all data in a single call
/// </summary>
public record AreaLeadDashboardResponse(
    List<AreaLeadAreaInfo> Areas,
    List<AreaLeadCheckpointInfo> Checkpoints
);

/// <summary>
/// Area information for area lead dashboard
/// </summary>
public record AreaLeadAreaInfo(
    string AreaId,
    string Name,
    string Color
);

/// <summary>
/// Checkpoint information for area lead dashboard
/// </summary>
public record AreaLeadCheckpointInfo(
    string CheckpointId,
    string Name,
    string Description,
    double Latitude,
    double Longitude,
    string? AreaName,
    List<string> AreaIds,
    List<AreaLeadMarshalInfo> Marshals,
    int OutstandingTaskCount,
    List<AreaLeadTaskInfo> OutstandingTasks
);

/// <summary>
/// Marshal information for area lead dashboard
/// </summary>
public record AreaLeadMarshalInfo(
    string MarshalId,
    string AssignmentId,
    string Name,
    string? Email,
    string? PhoneNumber,
    bool IsCheckedIn,
    DateTime? CheckInTime,
    string? CheckInMethod,
    DateTime? LastAccessedAt,
    int OutstandingTaskCount,
    List<AreaLeadTaskInfo> OutstandingTasks
);

/// <summary>
/// Task information for area lead dashboard
/// </summary>
public record AreaLeadTaskInfo(
    string ItemId,
    string Text,
    string Scope,
    string ContextType,
    string ContextId,
    string? MarshalId
);

// Dynamic Checkpoint Location Update DTOs

/// <summary>
/// Request to update a dynamic checkpoint's location
/// </summary>
public record UpdateCheckpointLocationRequest(
    double Latitude,
    double Longitude,
    string? SourceType = null,  // "gps", "checkpoint", "manual"
    string? SourceCheckpointId = null  // If copying from another checkpoint
);

/// <summary>
/// Response after updating a dynamic checkpoint's location
/// </summary>
public record UpdateCheckpointLocationResponse(
    bool Success,
    string CheckpointId,
    double Latitude,
    double Longitude,
    DateTime LastLocationUpdate,
    string? Message = null
);

/// <summary>
/// Response for getting dynamic checkpoint locations (for polling)
/// </summary>
public record DynamicCheckpointResponse(
    string CheckpointId,
    string Name,
    double Latitude,
    double Longitude,
    DateTime? LastLocationUpdate,
    string? LastUpdatedByPersonId
);

// Incident Reporting DTOs

/// <summary>
/// Request to create a new incident report
/// </summary>
public record CreateIncidentRequest(
    string Description,
    string? Title = null,
    string Severity = "medium",  // "low", "medium", "high", "critical"
    DateTime? IncidentTime = null,  // Defaults to now if not provided
    double? Latitude = null,
    double? Longitude = null,
    string? CheckpointId = null,  // Checkpoint where reporter was assigned
    bool SkipCheckpointAutoAssign = false  // If true, don't auto-assign checkpoint from marshal's assignment
);

/// <summary>
/// Request to update an incident's status
/// </summary>
public record UpdateIncidentStatusRequest(
    string Status,  // "acknowledged", "in_progress", "resolved", "closed"
    string? Note = null  // Optional explanation for the status change
);

/// <summary>
/// Request to add a note to an incident
/// </summary>
public record AddIncidentNoteRequest(
    string Note
);

/// <summary>
/// Information about who reported the incident
/// </summary>
public record IncidentReporterInfo(
    string PersonId,
    string Name,
    string? MarshalId
);

/// <summary>
/// Information about the area associated with an incident
/// </summary>
public record IncidentAreaInfo(
    string AreaId,
    string AreaName
);

/// <summary>
/// Checkpoint snapshot for incident response
/// </summary>
public record IncidentCheckpointInfo(
    string CheckpointId,
    string Name,
    string Description,
    double Latitude,
    double Longitude,
    List<string> AreaIds,
    List<string> AreaNames
);

/// <summary>
/// Marshal snapshot for incident response
/// </summary>
public record IncidentMarshalInfo(
    string MarshalId,
    string Name,
    bool WasCheckedIn,
    DateTime? CheckInTime,
    string? CheckInMethod
);

/// <summary>
/// Context information for incident response
/// </summary>
public record IncidentContextInfo(
    IncidentCheckpointInfo? Checkpoint,
    List<IncidentMarshalInfo> MarshalsPresentAtCheckpoint
);

/// <summary>
/// Update/note added to an incident
/// </summary>
public record IncidentUpdateInfo(
    string UpdateId,
    DateTime Timestamp,
    string AuthorPersonId,
    string AuthorName,
    string Note,
    string? StatusChange
);

/// <summary>
/// Full incident response
/// </summary>
public record IncidentResponse(
    string IncidentId,
    string EventId,
    string Title,
    string Description,
    string Severity,
    DateTime IncidentTime,
    DateTime CreatedAt,
    double? Latitude,
    double? Longitude,
    string Status,
    IncidentReporterInfo ReportedBy,
    IncidentAreaInfo? Area,
    IncidentContextInfo Context,
    List<IncidentUpdateInfo> Updates
);

/// <summary>
/// Response containing list of incidents
/// </summary>
public record IncidentsListResponse(
    List<IncidentResponse> Incidents
);
