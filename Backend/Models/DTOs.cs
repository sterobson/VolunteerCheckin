using System.Text.Json.Serialization;

namespace VolunteerCheckin.Functions.Models;

public record CreateEventRequest(
    string Name,
    string Description,
    DateTime EventDate,
    string TimeZoneId,
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
    string? BrandingPageGradientEnd = null,
    // Route display settings (optional)
    string? RouteColor = null,
    string? RouteStyle = null,
    int? RouteWeight = null
);

public record RoutePoint(
    double Lat,
    double Lng
);

public record EventSummaryResponse(
    string Id,
    string Name,
    DateTime EventDate
);

public record EventResponse(
    string Id,
    string Name,
    string Description,
    DateTime EventDate,
    string TimeZoneId,
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
    string BrandingPageGradientEnd,
    // Route display settings
    string RouteColor,
    string RouteStyle,
    int? RouteWeight,
    // Sample event fields
    bool IsSampleEvent,
    DateTime? ExpiresAt
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
    List<ScopeConfiguration>? LocationUpdateScopeConfigurations = null,
    // Layer assignment mode: "auto" | "all" | "specific"
    string? LayerAssignmentMode = null,
    // Layer IDs (only used when mode = "specific")
    List<string>? LayerIds = null
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
    string LastUpdatedByPersonId,
    // Layer assignment mode: "auto" | "all" | "specific"
    string LayerAssignmentMode,
    // Layer IDs (null = all layers for mode "all")
    List<string>? LayerIds = null,
    // True if auto mode found nearby routes or if not in auto mode
    bool HasNearbyRoutes = true
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
    DateTime? LastLocationUpdate,
    // Layer assignment mode: "auto" | "all" | "specific"
    string LayerAssignmentMode,
    // Layer assignment (null = all layers for mode "all")
    List<string>? LayerIds = null
);

public record EventAdminResponse(
    string EventId,
    string UserEmail,
    string Role,
    DateTime GrantedAt
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
    List<string>? Roles = null,
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
    string Notes,
    List<string>? Roles = null
);

public record MarshalResponse(
    string Id,
    string EventId,
    string Name,
    string Email,
    string PhoneNumber,
    string Notes,
    List<string> Roles,
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
    List<string> Roles,
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
    string? FrontendUrl = null,
    bool? UseHashRouting = null,
    string? Email = null,
    bool IncludeDetails = false
);

/// <summary>
/// Note info for email (simplified).
/// </summary>
public record NoteEmailInfo(
    string Title,
    string Content,
    string Priority,
    bool IsPinned
);

/// <summary>
/// Checkpoint assignment details for email.
/// </summary>
public record CheckpointEmailInfo(
    string Name,
    string? Description,
    DateTime? ArrivalTime,
    double? Latitude,
    double? Longitude,
    List<NoteEmailInfo> Notes
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
    bool CreateSeparateItems = false,
    bool LinksToCheckIn = false
);

public record UpdateChecklistItemRequest(
    string Text,
    List<ScopeConfiguration> ScopeConfigurations,
    int DisplayOrder,
    bool IsRequired,
    DateTime? VisibleFrom,
    DateTime? VisibleUntil,
    DateTime? MustCompleteBy,
    bool LinksToCheckIn = false
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
    string LastModifiedByAdminEmail,
    bool LinksToCheckIn
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
    string? ContextOwnerMarshalId,  // Marshal ID of the owner (for personal items)
    bool LinksToCheckIn,  // Whether this task is linked to check-in status
    string? LinkedCheckpointId,  // The specific checkpoint this instance is for (when linked)
    string? LinkedCheckpointName  // Name of the checkpoint for display
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
    bool IsDeleted,
    LinkedCheckInInfo? LinkedCheckIn = null  // Info about the check-in triggered by this completion
);

/// <summary>
/// Information about a check-in that was triggered by completing a linked task
/// </summary>
public record LinkedCheckInInfo(
    string AssignmentId,
    string LocationId,
    string LocationName,
    DateTime CheckedInAt
);

/// <summary>
/// Information about a task that was completed by checking in
/// </summary>
public record LinkedTaskCompletedInfo(
    string ItemId,
    string Text
);

// Authentication and Claims DTOs

/// <summary>
/// Information about a person (core identity)
/// </summary>
public record PersonInfo(
    string PersonId,
    string Name,
    string Email,
    string Phone
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
    /// Check if user has EventAdmin role (legacy)
    /// </summary>
    public bool IsEventAdmin => HasRole(Constants.RoleEventAdmin);

    /// <summary>
    /// Check if user is an event owner (includes legacy EventAdmin for backwards compatibility)
    /// </summary>
    public bool IsEventOwner => HasRole(Constants.RoleEventOwner) || HasRole(Constants.RoleEventAdmin);

    /// <summary>
    /// Check if user is an event administrator (not owner)
    /// </summary>
    public bool IsEventAdministrator => HasRole(Constants.RoleEventAdministrator);

    /// <summary>
    /// Check if user is an event contributor
    /// </summary>
    public bool IsEventContributor => HasRole(Constants.RoleEventContributor);

    /// <summary>
    /// Check if user is an event viewer
    /// </summary>
    public bool IsEventViewer => HasRole(Constants.RoleEventViewer);

    /// <summary>
    /// Check if user can manage owners (only owners can)
    /// </summary>
    public bool CanManageOwners => IsEventOwner;

    /// <summary>
    /// Check if user can manage other users (owners and administrators can)
    /// </summary>
    public bool CanManageUsers => IsEventOwner || IsEventAdministrator;

    /// <summary>
    /// Check if user can modify event data (checkpoints, areas, marshals, etc.)
    /// Owners, administrators, and contributors can modify
    /// </summary>
    public bool CanModifyEvent => IsEventOwner || IsEventAdministrator || IsEventContributor;

    /// <summary>
    /// Check if user can delete the event (only owners can)
    /// </summary>
    public bool CanDeleteEvent => IsEventOwner;

    /// <summary>
    /// Check if user has read-only access (viewers only)
    /// </summary>
    public bool IsReadOnly => !CanModifyEvent && (IsEventViewer || HasAnyEventRole);

    /// <summary>
    /// Check if user has any event role
    /// </summary>
    public bool HasAnyEventRole => EventRoles.Count > 0;

    /// <summary>
    /// Get the user's primary role for this event (highest privilege)
    /// </summary>
    public string? PrimaryEventRole
    {
        get
        {
            if (IsEventOwner) return Constants.RoleEventOwner;
            if (IsEventAdministrator) return Constants.RoleEventAdministrator;
            if (IsEventContributor) return Constants.RoleEventContributor;
            if (IsEventViewer) return Constants.RoleEventViewer;
            return null;
        }
    }

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
    /// Allowed if authenticated via SecureEmailLink or SampleCode
    /// </summary>
    public bool CanUseElevatedPermissions =>
        AuthMethod == Constants.AuthMethodSecureEmailLink ||
        AuthMethod == Constants.AuthMethodSampleCode;

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
    string? FrontendUrl = null,
    bool? UseHashRouting = null
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
/// Request to verify a 6-digit login code
/// </summary>
public record VerifyCodeRequest(
    string Email,
    string Code
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
/// Request to login using a sample event admin code
/// </summary>
public record SampleLoginRequest(
    string EventId,
    string AdminCode
);

/// <summary>
/// Response after sample login (returns session token)
/// </summary>
public record SampleLoginResponse(
    bool Success,
    string? SessionToken,
    string? EventId,
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
/// Detailed person response with roles
/// </summary>
public record PersonDetailsResponse(
    string PersonId,
    string Name,
    string Email,
    string Phone,
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
    bool IsPinned = false,
    bool ShowInEmergencyInfo = false
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
    bool IsPinned,
    bool ShowInEmergencyInfo
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
    bool ShowInEmergencyInfo,
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
    bool ShowInEmergencyInfo,
    DateTime CreatedAt,
    string CreatedByName,
    string MatchedScope  // Which scope configuration matched for this marshal
);

// Event Contact DTOs

/// <summary>
/// Request to create a new event contact
/// </summary>
public record CreateEventContactRequest(
    List<string> Roles,
    string Name,
    string? Phone = null,
    string? Email = null,
    string? Notes = null,
    string? MarshalId = null,
    List<ScopeConfiguration>? ScopeConfigurations = null,
    int DisplayOrder = 0,
    bool IsPinned = false,
    bool ShowInEmergencyInfo = false
);

/// <summary>
/// Request to update an existing event contact
/// </summary>
public record UpdateEventContactRequest(
    List<string> Roles,
    string Name,
    string? Phone = null,
    string? Email = null,
    string? Notes = null,
    string? MarshalId = null,
    List<ScopeConfiguration>? ScopeConfigurations = null,
    int DisplayOrder = 0,
    bool IsPinned = false,
    bool ShowInEmergencyInfo = false
);

/// <summary>
/// Event contact response for admin views (includes all details)
/// </summary>
public record EventContactResponse(
    string ContactId,
    string EventId,
    List<string> Roles,
    string Name,
    string? Phone,
    string? Email,
    string? Notes,
    string? MarshalId,
    string? MarshalName,
    List<ScopeConfiguration> ScopeConfigurations,
    int DisplayOrder,
    bool IsPinned,
    bool ShowInEmergencyInfo,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

/// <summary>
/// Event contact response for marshal views (simplified, with visibility info)
/// </summary>
public record EventContactForMarshalResponse(
    string ContactId,
    List<string> Roles,
    string Name,
    string? Phone,
    string? Email,
    string? Notes,
    int DisplayOrder,
    bool IsPinned,
    bool ShowInEmergencyInfo,
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

// Normalized Area Lead Dashboard DTOs

/// <summary>
/// Deduplicated task definition (itemId + text pair).
/// </summary>
public record AreaLeadTaskDefinition(
    [property: JsonPropertyName("r")] int RefIndex,
    [property: JsonPropertyName("t")] string Text
);

/// <summary>
/// Compact task instance using indexes.
/// </summary>
public record CompactAreaLeadTask(
    [property: JsonPropertyName("ti")] int TaskDefIndex,
    [property: JsonPropertyName("si")] int ScopeIndex,
    [property: JsonPropertyName("cti")] int ContextTypeIndex,
    [property: JsonPropertyName("ci")] int ContextRefIndex,
    [property: JsonPropertyName("mi"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? MarshalRefIndex
);

/// <summary>
/// Compact marshal info using indexes.
/// </summary>
public record CompactAreaLeadMarshal(
    [property: JsonPropertyName("r")] int RefIndex,
    [property: JsonPropertyName("ar")] int AssignmentRefIndex,
    [property: JsonPropertyName("n")] string Name,
    [property: JsonPropertyName("e"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Email,
    [property: JsonPropertyName("p"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? PhoneNumber,
    [property: JsonPropertyName("ci"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? IsCheckedIn,
    [property: JsonPropertyName("cit"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DateTime? CheckInTime,
    [property: JsonPropertyName("cmi"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? CheckInMethodIndex,
    [property: JsonPropertyName("la"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DateTime? LastAccessedAt,
    [property: JsonPropertyName("otc")] int OutstandingTaskCount,
    [property: JsonPropertyName("ot"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] List<CompactAreaLeadTask>? OutstandingTasks
);

/// <summary>
/// Compact area info using ref index.
/// </summary>
public record CompactAreaLeadArea(
    [property: JsonPropertyName("r")] int RefIndex,
    [property: JsonPropertyName("n")] string Name,
    [property: JsonPropertyName("c")] string Color
);

/// <summary>
/// Compact checkpoint info using indexes.
/// </summary>
public record CompactAreaLeadCheckpoint(
    [property: JsonPropertyName("r")] int RefIndex,
    [property: JsonPropertyName("n")] string Name,
    [property: JsonPropertyName("de"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Description,
    [property: JsonPropertyName("lat")] double Latitude,
    [property: JsonPropertyName("lng")] double Longitude,
    [property: JsonPropertyName("ani"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? AreaNameIndex,
    [property: JsonPropertyName("ai"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] List<int>? AreaRefIndexes,
    [property: JsonPropertyName("ms"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] List<CompactAreaLeadMarshal>? Marshals,
    [property: JsonPropertyName("otc")] int OutstandingTaskCount,
    [property: JsonPropertyName("ot"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] List<CompactAreaLeadTask>? OutstandingTasks
);

/// <summary>
/// Normalized area lead dashboard response with lookup tables.
/// </summary>
public record NormalizedAreaLeadDashboardResponse(
    [property: JsonPropertyName("_")] Dictionary<string, string> FieldMap,
    [property: JsonPropertyName("_d")] Dictionary<string, bool> Defaults,
    [property: JsonPropertyName("g")] List<string> Refs,
    [property: JsonPropertyName("sc")] List<string> Scopes,
    [property: JsonPropertyName("ct")] List<string> ContextTypes,
    [property: JsonPropertyName("cm")] List<string> CheckInMethods,
    [property: JsonPropertyName("td")] List<AreaLeadTaskDefinition> TaskDefinitions,
    [property: JsonPropertyName("ar")] List<CompactAreaLeadArea> Areas,
    [property: JsonPropertyName("cp")] List<CompactAreaLeadCheckpoint> Checkpoints
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

// Normalized Incidents DTOs

/// <summary>
/// Person reference for normalized incidents (personId + name pair).
/// </summary>
public record IncidentPersonRef(
    [property: JsonPropertyName("r")] int RefIndex,
    [property: JsonPropertyName("n")] string Name,
    [property: JsonPropertyName("m"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? MarshalRefIndex
);

/// <summary>
/// Compact marshal info for incident context.
/// </summary>
public record CompactIncidentMarshal(
    [property: JsonPropertyName("r")] int RefIndex,
    [property: JsonPropertyName("n")] string Name,
    [property: JsonPropertyName("ci"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? WasCheckedIn,
    [property: JsonPropertyName("cit"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DateTime? CheckInTime,
    [property: JsonPropertyName("cmi"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? CheckInMethodIndex
);

/// <summary>
/// Compact checkpoint info for incident context.
/// </summary>
public record CompactIncidentCheckpoint(
    [property: JsonPropertyName("r")] int RefIndex,
    [property: JsonPropertyName("n")] string Name,
    [property: JsonPropertyName("de"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Description,
    [property: JsonPropertyName("lat")] double Latitude,
    [property: JsonPropertyName("lng")] double Longitude,
    [property: JsonPropertyName("ai"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] List<int>? AreaRefIndexes,
    [property: JsonPropertyName("an"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] List<string>? AreaNames
);

/// <summary>
/// Compact context info for incident.
/// </summary>
public record CompactIncidentContext(
    [property: JsonPropertyName("cp"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] CompactIncidentCheckpoint? Checkpoint,
    [property: JsonPropertyName("ms"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] List<CompactIncidentMarshal>? MarshalsPresentAtCheckpoint
);

/// <summary>
/// Compact update/note for incident.
/// </summary>
public record CompactIncidentUpdate(
    [property: JsonPropertyName("r")] int RefIndex,
    [property: JsonPropertyName("ts")] DateTime Timestamp,
    [property: JsonPropertyName("ap")] int AuthorPersonIndex,
    [property: JsonPropertyName("no")] string Note,
    [property: JsonPropertyName("sc"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? StatusChangeIndex
);

/// <summary>
/// Compact incident using indexes.
/// </summary>
public record CompactIncident(
    [property: JsonPropertyName("r")] int RefIndex,
    [property: JsonPropertyName("ti")] string Title,
    [property: JsonPropertyName("de"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Description,
    [property: JsonPropertyName("sv")] int SeverityIndex,
    [property: JsonPropertyName("it")] DateTime IncidentTime,
    [property: JsonPropertyName("ca")] DateTime CreatedAt,
    [property: JsonPropertyName("lat"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? Latitude,
    [property: JsonPropertyName("lng"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? Longitude,
    [property: JsonPropertyName("st")] int StatusIndex,
    [property: JsonPropertyName("rb")] int ReportedByPersonIndex,
    [property: JsonPropertyName("ar"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? AreaRefIndex,
    [property: JsonPropertyName("an"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? AreaName,
    [property: JsonPropertyName("ctx"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] CompactIncidentContext? Context,
    [property: JsonPropertyName("up"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] List<CompactIncidentUpdate>? Updates
);

/// <summary>
/// Normalized incidents list response with lookup tables.
/// </summary>
public record NormalizedIncidentsListResponse(
    [property: JsonPropertyName("_")] Dictionary<string, string> FieldMap,
    [property: JsonPropertyName("_d")] Dictionary<string, bool> Defaults,
    [property: JsonPropertyName("g")] List<string> Refs,
    [property: JsonPropertyName("sv")] List<string> Severities,
    [property: JsonPropertyName("st")] List<string> Statuses,
    [property: JsonPropertyName("cm")] List<string> CheckInMethods,
    [property: JsonPropertyName("p")] List<IncidentPersonRef> Persons,
    [property: JsonPropertyName("i")] List<CompactIncident> Incidents
);

// Reorder DTOs

/// <summary>
/// Single item in a reorder request
/// </summary>
public record ReorderItem(
    string Id,
    int DisplayOrder
);

/// <summary>
/// Request to reorder notes
/// </summary>
public record ReorderNotesRequest(
    List<ReorderItem> Items
);

/// <summary>
/// Request to reorder checklist items
/// </summary>
public record ReorderChecklistItemsRequest(
    List<ReorderItem> Items
);

/// <summary>
/// Request to reorder contacts
/// </summary>
public record ReorderContactsRequest(
    List<ReorderItem> Items
);

// Role Definition DTOs

/// <summary>
/// Request to create a new role definition
/// </summary>
public record CreateRoleDefinitionRequest(
    string Name,
    string Notes = "",
    bool CanManageAreaCheckpoints = false
);

/// <summary>
/// Request to update an existing role definition
/// </summary>
public record UpdateRoleDefinitionRequest(
    string Name,
    string Notes = "",
    bool CanManageAreaCheckpoints = false
);

/// <summary>
/// Role definition response for admin views
/// </summary>
public record RoleDefinitionResponse(
    string RoleId,
    string EventId,
    string Name,
    string Notes,
    bool IsBuiltIn,
    bool CanManageAreaCheckpoints,
    int DisplayOrder,
    int UsageCount,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

/// <summary>
/// Request to reorder role definitions
/// </summary>
public record ReorderRoleDefinitionsRequest(
    List<ReorderItem> Items
);

/// <summary>
/// Unified person entry for role assignment (combines marshals and contacts, deduplicates linked ones)
/// </summary>
public record PersonWithRoleResponse(
    string Id,
    string Name,
    string PersonType,  // "Marshal", "Contact", or "Linked"
    string? MarshalId,
    string? ContactId,
    bool HasRole
);

/// <summary>
/// Request to update role assignments for a role definition
/// </summary>
public record UpdateRoleAssignmentsRequest(
    List<string> MarshalIdsToAdd,
    List<string> MarshalIdsToRemove,
    List<string> ContactIdsToAdd,
    List<string> ContactIdsToRemove
);

/// <summary>
/// Simplified role information for marshals to view their assigned roles.
/// Only includes name and notes (no admin-level details).
/// </summary>
public record MarshalRoleResponse(
    string RoleId,
    string Name,
    string Notes
);

// Normalized Checklist Response DTOs
// Field names are shortened to reduce payload size. See FieldMap (_) for mappings.
// GUIDs are stored in the refs array and referenced by index to eliminate duplication.

/// <summary>
/// Reference to a marshal with ref index and name for lookup tables.
/// </summary>
public record MarshalReference(
    [property: JsonPropertyName("r")] int RefIndex,
    [property: JsonPropertyName("n")] string Name
);

/// <summary>
/// Reference to a completion context with ref index and type index.
/// </summary>
public record ContextReference(
    [property: JsonPropertyName("r")] int RefIndex,
    [property: JsonPropertyName("t")] int TypeIndex
);

/// <summary>
/// Compact scope configuration for normalized API responses.
/// Uses indexes: s=scope index into scopes array, t=itemType, i=ref indexes into refs array
/// </summary>
public record CompactScopeConfiguration(
    [property: JsonPropertyName("s")] int ScopeIndex,
    [property: JsonPropertyName("t")] string? ItemType,
    [property: JsonPropertyName("i")] List<int> RefIndexes
);

/// <summary>
/// Static checklist item definition (shared across all instances).
/// Uses ref indexes for GUIDs. Does not include eventId as it's implicit from the request context.
/// </summary>
public record ChecklistItemDefinition(
    [property: JsonPropertyName("r")] int ItemRefIndex,
    [property: JsonPropertyName("t")] string Text,
    [property: JsonPropertyName("sc")] List<CompactScopeConfiguration> ScopeConfigurations,
    [property: JsonPropertyName("o")] int DisplayOrder,
    [property: JsonPropertyName("r2")] bool? IsRequired,
    [property: JsonPropertyName("vf")] DateTime? VisibleFrom,
    [property: JsonPropertyName("vu")] DateTime? VisibleUntil,
    [property: JsonPropertyName("mb")] DateTime? MustCompleteBy,
    [property: JsonPropertyName("l")] bool? LinksToCheckIn,
    [property: JsonPropertyName("lc")] int? LinkedCheckpointRefIndex,
    [property: JsonPropertyName("ln")] string? LinkedCheckpointName
);

/// <summary>
/// Per-instance checklist data with indexes referencing lookup tables.
/// Null fields are omitted from serialization. Boolean properties use defaults from _d when null.
/// </summary>
public record ChecklistInstance(
    [property: JsonPropertyName("i")] int ItemIndex,
    [property: JsonPropertyName("c")] bool? IsCompleted,
    [property: JsonPropertyName("m")] bool? CanBeCompletedByMe,
    [property: JsonPropertyName("a")] int? ActorIndex,
    [property: JsonPropertyName("at")] int? ActorTypeIndex,
    [property: JsonPropertyName("ca")] DateTime? CompletedAt,
    [property: JsonPropertyName("x")] int ContextIndex,
    [property: JsonPropertyName("s")] int ScopeIndex,
    [property: JsonPropertyName("o")] int? OwnerIndex
);

/// <summary>
/// Normalized checklist response with lookup tables to reduce payload size.
/// All GUIDs are stored in the refs array and referenced by index.
/// Scopes, actor types, context types, marshals, and contexts are stored in arrays.
/// Instances reference these arrays by index instead of repeating strings.
/// The FieldMap (_) provides human-readable mappings for debugging.
/// The Defaults (_d) provides default values for boolean properties - omitted properties use these defaults.
/// </summary>
public record NormalizedChecklistResponse(
    [property: JsonPropertyName("_")] Dictionary<string, string> FieldMap,
    [property: JsonPropertyName("_d")] Dictionary<string, bool> Defaults,
    [property: JsonPropertyName("g")] List<string> Refs,
    [property: JsonPropertyName("s")] List<string> Scopes,
    [property: JsonPropertyName("at")] List<string> ActorTypes,
    [property: JsonPropertyName("ct")] List<string> ContextTypes,
    [property: JsonPropertyName("m")] List<MarshalReference> Marshals,
    [property: JsonPropertyName("c")] List<ContextReference> Contexts,
    [property: JsonPropertyName("d")] List<ChecklistItemDefinition> Items,
    [property: JsonPropertyName("n")] List<ChecklistInstance> Instances
);

// ============================================================================
// Normalized Event Status Response DTOs
// Field names are shortened to reduce payload size. See FieldMap (_) for mappings.
// GUIDs are stored in the refs array and referenced by index.
// ============================================================================

/// <summary>
/// Compact assignment for normalized status response.
/// Removes redundant eventId/locationId, uses indexes for marshals and check-in methods.
/// </summary>
public record CompactAssignment(
    [property: JsonPropertyName("r")] int RefIndex,              // Assignment ID
    [property: JsonPropertyName("m")] int MarshalIndex,          // Index into marshals array
    [property: JsonPropertyName("ci")] bool? IsCheckedIn,        // Nullable for defaults
    [property: JsonPropertyName("ct")] DateTime? CheckInTime,
    [property: JsonPropertyName("cla")] double? CheckInLatitude,
    [property: JsonPropertyName("clo")] double? CheckInLongitude,
    [property: JsonPropertyName("cm")] int? CheckInMethodIndex,  // Index into checkInMethods array
    [property: JsonPropertyName("cb")] int? CheckedInByIndex     // Index into marshals array
);

/// <summary>
/// Compact location update scope configuration using indexes.
/// </summary>
public record CompactLocationUpdateScope(
    [property: JsonPropertyName("s")] int ScopeIndex,
    [property: JsonPropertyName("t")] string? ItemType,
    [property: JsonPropertyName("i")] List<int> RefIndexes
);

/// <summary>
/// Compact location for normalized status response.
/// Uses ref indexes for IDs and area IDs, contains compact assignments.
/// </summary>
public record CompactLocation(
    [property: JsonPropertyName("r")] int RefIndex,              // Location ID
    [property: JsonPropertyName("n")] string Name,
    [property: JsonPropertyName("de")] string? Description,
    [property: JsonPropertyName("lat")] double Latitude,
    [property: JsonPropertyName("lng")] double Longitude,
    [property: JsonPropertyName("rm")] int RequiredMarshals,
    [property: JsonPropertyName("cc")] int CheckedInCount,
    [property: JsonPropertyName("a")] List<CompactAssignment> Assignments,
    [property: JsonPropertyName("w")] string? What3Words,
    [property: JsonPropertyName("st")] DateTime? StartTime,
    [property: JsonPropertyName("et")] DateTime? EndTime,
    [property: JsonPropertyName("ai")] List<int> AreaRefIndexes,
    // Raw style properties
    [property: JsonPropertyName("sty")] string? StyleType,
    [property: JsonPropertyName("sc")] string? StyleColor,
    [property: JsonPropertyName("sbs")] string? StyleBackgroundShape,
    [property: JsonPropertyName("sbc")] string? StyleBackgroundColor,
    [property: JsonPropertyName("sboc")] string? StyleBorderColor,
    [property: JsonPropertyName("sic")] string? StyleIconColor,
    [property: JsonPropertyName("ssz")] string? StyleSize,
    [property: JsonPropertyName("smr")] string? StyleMapRotation,
    // Resolved style properties
    [property: JsonPropertyName("rsty")] string? ResolvedStyleType,
    [property: JsonPropertyName("rsc")] string? ResolvedStyleColor,
    [property: JsonPropertyName("rsbs")] string? ResolvedStyleBackgroundShape,
    [property: JsonPropertyName("rsbc")] string? ResolvedStyleBackgroundColor,
    [property: JsonPropertyName("rsboc")] string? ResolvedStyleBorderColor,
    [property: JsonPropertyName("rsic")] string? ResolvedStyleIconColor,
    [property: JsonPropertyName("rssz")] string? ResolvedStyleSize,
    [property: JsonPropertyName("rsmr")] string? ResolvedStyleMapRotation,
    // Terminology
    [property: JsonPropertyName("pt")] string? PeopleTerm,
    [property: JsonPropertyName("cpt")] string? CheckpointTerm,
    // Dynamic checkpoint settings
    [property: JsonPropertyName("dy")] bool? IsDynamic,
    [property: JsonPropertyName("lus")] List<CompactLocationUpdateScope>? LocationUpdateScopes,
    [property: JsonPropertyName("llu")] DateTime? LastLocationUpdate,
    // Layer assignment (null = all layers)
    [property: JsonPropertyName("li")] List<int>? LayerRefIndexes = null
);

/// <summary>
/// Normalized event status response with lookup tables to reduce payload size.
/// </summary>
public record NormalizedEventStatusResponse(
    [property: JsonPropertyName("_")] Dictionary<string, string> FieldMap,
    [property: JsonPropertyName("_d")] Dictionary<string, bool> Defaults,
    [property: JsonPropertyName("_ds")] Dictionary<string, string> StringDefaults,
    [property: JsonPropertyName("g")] List<string> Refs,
    [property: JsonPropertyName("m")] List<MarshalReference> Marshals,
    [property: JsonPropertyName("cm")] List<string> CheckInMethods,
    [property: JsonPropertyName("sc")] List<string> Scopes,
    [property: JsonPropertyName("l")] List<CompactLocation> Locations
);

// ============================================================================
// Layer DTOs
// ============================================================================

/// <summary>
/// Request to create a new layer
/// </summary>
public record CreateLayerRequest(
    string EventId,
    string Name,
    List<RoutePoint>? Route = null,
    string? RouteColor = null,
    string? RouteStyle = null,
    int? RouteWeight = null
);

/// <summary>
/// Request to update an existing layer
/// </summary>
public record UpdateLayerRequest(
    string? Name = null,
    int? DisplayOrder = null,
    List<RoutePoint>? Route = null,
    string? RouteColor = null,
    string? RouteStyle = null,
    int? RouteWeight = null
);

/// <summary>
/// Request to reorder layers
/// </summary>
public record ReorderLayersRequest(
    List<ReorderItem> Items
);

/// <summary>
/// Layer response for API
/// </summary>
public record LayerResponse(
    string Id,
    string EventId,
    string Name,
    int DisplayOrder,
    List<RoutePoint> Route,
    string RouteColor,
    string RouteStyle,
    int? RouteWeight,
    int CheckpointCount,
    DateTime CreatedDate
);

// ============================================================================
// Event User Management DTOs
// ============================================================================

/// <summary>
/// Response for an event user (replaces EventAdminResponse)
/// </summary>
public record EventUserResponse(
    string EventId,
    string PersonId,
    string UserEmail,
    string UserName,
    string Role,
    DateTime GrantedAt,
    string? GrantedByName
);

/// <summary>
/// Request to add a new user to an event
/// </summary>
public record AddEventUserRequest(
    string UserEmail,
    string Role,
    string? UserName = null
);

/// <summary>
/// Request to update a user's role in an event
/// </summary>
public record UpdateEventUserRoleRequest(
    string Role
);

/// <summary>
/// Response containing the current user's permissions for an event
/// </summary>
public record EventPermissionsResponse(
    string Role,
    bool CanManageOwners,
    bool CanManageUsers,
    bool CanModifyEvent,
    bool CanDeleteEvent,
    bool IsReadOnly
);
