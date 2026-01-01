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
            entity.CreatedDate,
            entity.PeopleTerm,
            entity.CheckpointTerm,
            entity.AreaTerm,
            entity.ChecklistTerm
        );
    }

    public static LocationResponse ToResponse(this LocationEntity entity)
    {
        List<string> areaIds = JsonSerializer.Deserialize<List<string>>(entity.AreaIdsJson) ?? [];

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
            areaIds
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
            entity.Color,
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
            entity.MarshalId,
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

    public static ChecklistItemResponse ToResponse(this ChecklistItemEntity entity)
    {
        List<ScopeConfiguration> scopeConfigurations =
            JsonSerializer.Deserialize<List<ScopeConfiguration>>(entity.ScopeConfigurationsJson) ?? [];

        return new ChecklistItemResponse(
            entity.ItemId,
            entity.EventId,
            entity.Text,
            scopeConfigurations,
            entity.DisplayOrder,
            entity.IsRequired,
            entity.VisibleFrom,
            entity.VisibleUntil,
            entity.MustCompleteBy,
            entity.CreatedByAdminEmail,
            entity.CreatedDate,
            entity.LastModifiedDate,
            entity.LastModifiedByAdminEmail
        );
    }

    public static ChecklistCompletionResponse ToResponse(this ChecklistCompletionEntity entity)
    {
        return new ChecklistCompletionResponse(
            entity.RowKey,
            entity.EventId,
            entity.ChecklistItemId,
            entity.CompletionContextType,
            entity.CompletionContextId,
            entity.ContextOwnerMarshalId,
            entity.ContextOwnerMarshalName,
            entity.ActorType,
            entity.ActorId,
            entity.ActorName,
            entity.CompletedAt,
            entity.IsDeleted
        );
    }
}
