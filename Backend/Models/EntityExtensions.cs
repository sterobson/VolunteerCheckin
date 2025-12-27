using System.Text.Json;

namespace VolunteerCheckin.Functions.Models;

public static class EntityExtensions
{
    public static EventResponse ToResponse(this EventEntity entity)
    {
        List<EmergencyContact> emergencyContacts = JsonSerializer.Deserialize<List<EmergencyContact>>(entity.EmergencyContactsJson) ?? [];
        List<RoutePoint> route = JsonSerializer.Deserialize<List<RoutePoint>>(entity.GpxRouteJson) ?? [];

        return new EventResponse(
            entity.RowKey,
            entity.Name,
            entity.Description,
            entity.EventDate,
            entity.TimeZoneId,
            entity.AdminEmail,
            emergencyContacts,
            route,
            entity.IsActive,
            entity.CreatedDate
        );
    }

    public static LocationResponse ToResponse(this LocationEntity entity)
    {
        return new LocationResponse(
            entity.RowKey,
            entity.EventId,
            entity.Name,
            entity.Description,
            entity.Latitude,
            entity.Longitude,
            entity.RequiredMarshals,
            entity.CheckedInCount,
            entity.What3Words,
            entity.StartTime,
            entity.EndTime
        );
    }

    public static AssignmentResponse ToResponse(this AssignmentEntity entity)
    {
        return new AssignmentResponse(
            entity.RowKey,
            entity.EventId,
            entity.LocationId,
            entity.MarshalName,
            entity.IsCheckedIn,
            entity.CheckInTime,
            entity.CheckInLatitude,
            entity.CheckInLongitude,
            entity.CheckInMethod
        );
    }

    public static UserEventMappingResponse ToResponse(this UserEventMappingEntity entity)
    {
        return new UserEventMappingResponse(
            entity.EventId,
            entity.UserEmail,
            entity.Role,
            entity.CreatedDate
        );
    }
}
