using System.Text.Json;
using VolunteerCheckin.Functions.Models;
using VolunteerCheckin.Functions.Repositories;

namespace VolunteerCheckin.Functions.Services;

/// <summary>
/// Service for determining who can see and modify contact details.
///
/// Permission levels:
/// - Marshal: Can see own contact details + area leads for their areas + event emergency contacts
/// - Area Lead: Can see contact details for all marshals assigned to checkpoints in their area(s)
/// - Event Admin: Can see all marshal contact details
///
/// Modification permissions:
/// - Marshal: Can modify own name, email, phone (via their own marshal record)
/// - Event Admin: Can modify any marshal's details
/// </summary>
public class ContactPermissionService
{
    private readonly IAreaRepository _areaRepository;
    private readonly ILocationRepository _locationRepository;
    private readonly IAssignmentRepository _assignmentRepository;

    public ContactPermissionService(
        IAreaRepository areaRepository,
        ILocationRepository locationRepository,
        IAssignmentRepository assignmentRepository)
    {
        _areaRepository = areaRepository;
        _locationRepository = locationRepository;
        _assignmentRepository = assignmentRepository;
    }

    /// <summary>
    /// Get the set of marshal IDs whose contact details the user can view.
    /// Returns null if user can see ALL marshals (event admin).
    /// </summary>
    public virtual async Task<ContactPermissions> GetContactPermissionsAsync(UserClaims claims, string eventId)
    {
        // Must be authenticated
        if (claims == null)
        {
            return new ContactPermissions(
                CanViewAll: false,
                ViewableMarshalIds: new HashSet<string>(),
                CanModifyAll: false,
                ModifiableMarshalIds: new HashSet<string>()
            );
        }

        // Event admins can see and modify everyone
        if (claims.IsEventAdmin || claims.IsSystemAdmin)
        {
            return new ContactPermissions(
                CanViewAll: true,
                ViewableMarshalIds: new HashSet<string>(),
                CanModifyAll: true,
                ModifiableMarshalIds: new HashSet<string>()
            );
        }

        HashSet<string> viewableMarshalIds = new();
        HashSet<string> modifiableMarshalIds = new();

        // If user is a marshal, they can see and modify their own details
        if (claims.MarshalId != null)
        {
            viewableMarshalIds.Add(claims.MarshalId);
            modifiableMarshalIds.Add(claims.MarshalId);
        }

        // Get all areas and locations for this event
        IEnumerable<AreaEntity> areas = await _areaRepository.GetByEventAsync(eventId);
        IEnumerable<LocationEntity> locations = await _locationRepository.GetByEventAsync(eventId);
        IEnumerable<AssignmentEntity> allAssignments = await _assignmentRepository.GetByEventAsync(eventId);

        // Build lookup: locationId -> list of areaIds
        Dictionary<string, List<string>> locationToAreas = new();
        foreach (LocationEntity location in locations)
        {
            List<string> areaIds = JsonSerializer.Deserialize<List<string>>(location.AreaIdsJson) ?? new List<string>();
            locationToAreas[location.RowKey] = areaIds;
        }

        // Build lookup: areaId -> list of area lead marshal IDs
        Dictionary<string, List<string>> areaLeadsByArea = new();
        foreach (AreaEntity area in areas)
        {
            List<string> leadIds = JsonSerializer.Deserialize<List<string>>(area.AreaLeadMarshalIdsJson) ?? new List<string>();
            areaLeadsByArea[area.RowKey] = leadIds;
        }

        // If user is a marshal, find which areas they're assigned to and add those area leads
        if (claims.MarshalId != null)
        {
            IEnumerable<AssignmentEntity> userAssignments = allAssignments.Where(a => a.MarshalId == claims.MarshalId);
            HashSet<string> userAreaIds = new();

            foreach (AssignmentEntity assignment in userAssignments)
            {
                if (locationToAreas.TryGetValue(assignment.LocationId, out List<string>? areaIds))
                {
                    foreach (string areaId in areaIds)
                    {
                        userAreaIds.Add(areaId);
                    }
                }
            }

            // Add all area leads for the user's assigned areas
            foreach (string areaId in userAreaIds)
            {
                if (areaLeadsByArea.TryGetValue(areaId, out List<string>? leadIds))
                {
                    foreach (string leadId in leadIds)
                    {
                        viewableMarshalIds.Add(leadId);
                    }
                }
            }
        }

        // Check if user is an area lead or area admin for any area
        List<string> userLeadAreaIds = new();
        foreach (EventRoleInfo role in claims.EventRoles)
        {
            if (role.Role == Constants.RoleEventAreaLead || role.Role == Constants.RoleEventAreaAdmin)
            {
                if (role.AreaIds.Count == 0)
                {
                    // All areas - they can see everyone
                    return new ContactPermissions(
                        CanViewAll: true,
                        ViewableMarshalIds: new HashSet<string>(),
                        CanModifyAll: false,
                        ModifiableMarshalIds: modifiableMarshalIds
                    );
                }
                userLeadAreaIds.AddRange(role.AreaIds);
            }
        }

        // Also check if marshal is designated as area lead via AreaLeadMarshalIdsJson
        if (claims.MarshalId != null)
        {
            foreach (AreaEntity area in areas)
            {
                List<string> leadIds = JsonSerializer.Deserialize<List<string>>(area.AreaLeadMarshalIdsJson) ?? new List<string>();
                if (leadIds.Contains(claims.MarshalId))
                {
                    userLeadAreaIds.Add(area.RowKey);
                }
            }
        }

        // If user is an area lead, add all marshals assigned to checkpoints in their areas
        if (userLeadAreaIds.Count > 0)
        {
            HashSet<string> leadAreaIdSet = new(userLeadAreaIds);

            // Find all locations in the user's lead areas
            HashSet<string> locationsInLeadAreas = new();
            foreach (LocationEntity location in locations)
            {
                List<string> locationAreaIds = JsonSerializer.Deserialize<List<string>>(location.AreaIdsJson) ?? new List<string>();
                if (locationAreaIds.Any(areaId => leadAreaIdSet.Contains(areaId)))
                {
                    locationsInLeadAreas.Add(location.RowKey);
                }
            }

            // Find all marshals assigned to those locations
            foreach (AssignmentEntity assignment in allAssignments)
            {
                if (locationsInLeadAreas.Contains(assignment.LocationId))
                {
                    viewableMarshalIds.Add(assignment.MarshalId);
                }
            }
        }

        return new ContactPermissions(
            CanViewAll: false,
            ViewableMarshalIds: viewableMarshalIds,
            CanModifyAll: false,
            ModifiableMarshalIds: modifiableMarshalIds
        );
    }

    /// <summary>
    /// Check if user can view contact details for a specific marshal.
    /// </summary>
    public virtual bool CanViewContactDetails(ContactPermissions permissions, string marshalId)
    {
        return permissions.CanViewAll || permissions.ViewableMarshalIds.Contains(marshalId);
    }

    /// <summary>
    /// Check if user can modify a specific marshal's details.
    /// </summary>
    public virtual bool CanModifyMarshal(ContactPermissions permissions, string marshalId)
    {
        return permissions.CanModifyAll || permissions.ModifiableMarshalIds.Contains(marshalId);
    }
}

/// <summary>
/// Represents a user's permissions for viewing and modifying contact details.
/// </summary>
public record ContactPermissions(
    bool CanViewAll,
    HashSet<string> ViewableMarshalIds,
    bool CanModifyAll,
    HashSet<string> ModifiableMarshalIds
);
