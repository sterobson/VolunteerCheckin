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

    public static LocationResponse ToResponse(this LocationEntity entity, string? areaName = null)
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
            entity.EndTime,
            entity.AreaId,
            areaName
        );
    }

    public static AreaResponse ToResponse(this AreaEntity entity, int checkpointCount = 0)
    {
        List<AreaContact> contacts = JsonSerializer.Deserialize<List<AreaContact>>(entity.ContactsJson) ?? [];
        List<RoutePoint> polygon = JsonSerializer.Deserialize<List<RoutePoint>>(entity.PolygonJson) ?? [];

        return new AreaResponse(
            entity.RowKey,
            entity.EventId,
            entity.Name,
            entity.Description,
            contacts,
            polygon,
            entity.IsDefault,
            entity.DisplayOrder,
            checkpointCount,
            entity.CreatedDate
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
