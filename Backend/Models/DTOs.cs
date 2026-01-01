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
    string? ChecklistTerm = null
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
    string? ChecklistTerm = null
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
    string ChecklistTerm
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
    DateTime? EndTime
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
    List<string> AreaIds
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
    List<RoutePoint>? Polygon
);

public record UpdateAreaRequest(
    string Name,
    string Description,
    string Color,
    List<AreaContact> Contacts,
    List<RoutePoint>? Polygon,
    int DisplayOrder
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
    DateTime CreatedDate
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
    string CheckInMethod
);

public record CheckInRequest(
    string EventId,
    string AssignmentId,
    double? Latitude,
    double? Longitude,
    bool ManualCheckIn
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
    List<string> AreaIds
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
    string Notes
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
    DateTime CreatedDate
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
    string? ContextId = null     // Optional override for context
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

public record AddAreaLeadRequest(
    string MarshalId
);

public record AreaLeadResponse(
    string MarshalId,
    string MarshalName,
    string Email
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
    string Email
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
