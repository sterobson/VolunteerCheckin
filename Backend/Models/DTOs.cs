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
    List<EmergencyContact> EmergencyContacts
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
    DateTime CreatedDate
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
    string Role
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
    string MatchedScope  // Which scope configuration matched for this marshal
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
