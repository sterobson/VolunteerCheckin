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

public record EventResponse(
    string Id,
    string Name,
    string Description,
    DateTime EventDate,
    string TimeZoneId,
    string AdminEmail,
    List<EmergencyContact> EmergencyContacts,
    bool IsActive,
    DateTime CreatedDate
);

public record CreateLocationRequest(
    string EventId,
    string Name,
    string Description,
    double Latitude,
    double Longitude,
    int RequiredMarshals
);

public record LocationResponse(
    string Id,
    string EventId,
    string Name,
    string Description,
    double Latitude,
    double Longitude,
    int RequiredMarshals,
    int CheckedInCount
);

public record CreateAssignmentRequest(
    string EventId,
    string LocationId,
    string MarshalName
);

public record AssignmentResponse(
    string Id,
    string EventId,
    string LocationId,
    string MarshalName,
    bool IsCheckedIn,
    DateTime? CheckInTime,
    double? CheckInLatitude,
    double? CheckInLongitude,
    string CheckInMethod
);

public record CheckInRequest(
    string AssignmentId,
    double? Latitude,
    double? Longitude,
    bool ManualCheckIn
);

public record RequestMagicLinkRequest(
    string Email
);

public record MagicLinkResponse(
    bool Success,
    string Message
);

public record ValidateTokenRequest(
    string Token
);

public record ValidateTokenResponse(
    bool IsValid,
    string Email
);

public record EventStatusResponse(
    string EventId,
    List<LocationStatusResponse> Locations
);

public record LocationStatusResponse(
    string Id,
    string Name,
    double Latitude,
    double Longitude,
    int RequiredMarshals,
    int CheckedInCount,
    List<AssignmentResponse> Assignments
);
