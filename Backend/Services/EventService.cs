using VolunteerCheckin.Functions.Models;
using VolunteerCheckin.Functions.Repositories;

namespace VolunteerCheckin.Functions.Services;

/// <summary>
/// Service for event-level operations that span multiple repositories.
/// </summary>
public class EventService : IEventService
{
    private readonly IEventRepository _eventRepository;
    private readonly IAreaRepository _areaRepository;
    private readonly ILocationRepository _locationRepository;
    private readonly IMarshalRepository _marshalRepository;
    private readonly IAssignmentRepository _assignmentRepository;
    private readonly IChecklistItemRepository _checklistItemRepository;
    private readonly IChecklistCompletionRepository _checklistCompletionRepository;
    private readonly IEventContactRepository _eventContactRepository;
    private readonly ILayerRepository _layerRepository;
    private readonly INoteRepository _noteRepository;
    private readonly IIncidentRepository _incidentRepository;
    private readonly IEventRoleRepository _eventRoleRepository;
    private readonly IEventRoleDefinitionRepository _eventRoleDefinitionRepository;
    private readonly IPersonRepository _personRepository;
    private readonly TableStorageService _tableStorageService;

    public EventService(
        IEventRepository eventRepository,
        IAreaRepository areaRepository,
        ILocationRepository locationRepository,
        IMarshalRepository marshalRepository,
        IAssignmentRepository assignmentRepository,
        IChecklistItemRepository checklistItemRepository,
        IChecklistCompletionRepository checklistCompletionRepository,
        IEventContactRepository eventContactRepository,
        ILayerRepository layerRepository,
        INoteRepository noteRepository,
        IIncidentRepository incidentRepository,
        IEventRoleRepository eventRoleRepository,
        IEventRoleDefinitionRepository eventRoleDefinitionRepository,
        IPersonRepository personRepository,
        TableStorageService tableStorageService)
    {
        _eventRepository = eventRepository;
        _areaRepository = areaRepository;
        _locationRepository = locationRepository;
        _marshalRepository = marshalRepository;
        _assignmentRepository = assignmentRepository;
        _checklistItemRepository = checklistItemRepository;
        _checklistCompletionRepository = checklistCompletionRepository;
        _eventContactRepository = eventContactRepository;
        _layerRepository = layerRepository;
        _noteRepository = noteRepository;
        _incidentRepository = incidentRepository;
        _eventRoleRepository = eventRoleRepository;
        _eventRoleDefinitionRepository = eventRoleDefinitionRepository;
        _personRepository = personRepository;
        _tableStorageService = tableStorageService;
    }

    /// <summary>
    /// Delete an event and all related data.
    /// </summary>
    public async Task DeleteEventWithAllDataAsync(string eventId)
    {
        // Delete in order to avoid orphans (child entities first)
        // Note: Check-ins are stored on AssignmentEntity, so they're deleted with assignments

        // 1. Assignments (includes check-in data)
        await _assignmentRepository.DeleteAllByEventAsync(eventId);

        // 2. Checklist completions
        await _checklistCompletionRepository.DeleteAllByEventAsync(eventId);

        // 3. Checklist items
        await _checklistItemRepository.DeleteAllByEventAsync(eventId);

        // 4. Incidents
        await _incidentRepository.DeleteAllByEventAsync(eventId);

        // 5. Notes
        await _noteRepository.DeleteAllByEventAsync(eventId);

        // 6. Contacts
        await _eventContactRepository.DeleteAllByEventAsync(eventId);

        // 7. Locations
        await _locationRepository.DeleteAllByEventAsync(eventId);

        // 8. Marshals
        await _marshalRepository.DeleteAllByEventAsync(eventId);

        // 9. Areas
        await _areaRepository.DeleteAllByEventAsync(eventId);

        // 10. Layers
        await _layerRepository.DeleteAllByEventAsync(eventId);

        // 11. Event roles - collect PersonIds first, then delete roles, then clean up orphaned people
        IEnumerable<EventRoleEntity> eventRoles = await _eventRoleRepository.GetByEventAsync(eventId);
        HashSet<string> personIdsInEvent = new(eventRoles.Select(r => r.PersonId).Where(id => !string.IsNullOrEmpty(id)));
        await _eventRoleRepository.DeleteAllByEventAsync(eventId);

        // 12. Event role definitions (custom role types for this event)
        await _eventRoleDefinitionRepository.DeleteAllByEventAsync(eventId);

        // 13. Clean up orphaned people (those with no roles in any other event)
        await DeleteOrphanedPeopleAsync(personIdsInEvent, eventId);

        // 14. Sample event admin entry (if this was a sample event)
        await DeleteSampleEventAdminByEventIdAsync(eventId);

        // 15. Finally, the event itself (may already be deleted if DeleteEventRecordImmediateAsync was called)
        try
        {
            await _eventRepository.DeleteAsync(eventId);
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            // Event already deleted - this is expected if DeleteEventRecordImmediateAsync was called first
        }
    }

    /// <summary>
    /// Immediately delete the Event and SampleEventAdmin records.
    /// Used when a deletion is requested to prevent further access.
    /// The remaining data will be cleaned up by a background job.
    /// </summary>
    public async Task DeleteEventRecordImmediateAsync(string eventId)
    {
        // Delete the sample event admin entry first (if this was a sample event)
        await DeleteSampleEventAdminByEventIdAsync(eventId);

        // Delete the event record
        await _eventRepository.DeleteAsync(eventId);
    }

    /// <summary>
    /// Delete people who no longer have roles in any event.
    /// Skips system admins as they should persist regardless of event roles.
    /// </summary>
    private async Task DeleteOrphanedPeopleAsync(HashSet<string> personIds, string deletedEventId)
    {
        foreach (string personId in personIds)
        {
            try
            {
                // Check if person has roles in any other event
                bool hasOtherRoles = await _eventRoleRepository.HasRolesInOtherEventsAsync(personId, deletedEventId);
                if (hasOtherRoles)
                {
                    continue; // Person has roles elsewhere, keep them
                }

                // Get the person to check if they're a system admin
                PersonEntity? person = await _personRepository.GetAsync(personId);
                if (person == null)
                {
                    continue; // Person doesn't exist
                }

                if (person.IsSystemAdmin)
                {
                    continue; // Never delete system admins
                }

                // Person has no roles in any event and is not a system admin - delete them
                await _personRepository.DeleteAsync(personId);
            }
            catch (Azure.RequestFailedException)
            {
                // Ignore errors for individual person cleanup
            }
        }
    }

    /// <summary>
    /// Delete the SampleEventAdmin entry for a given event ID (if it exists).
    /// </summary>
    private async Task DeleteSampleEventAdminByEventIdAsync(string eventId)
    {
        Azure.Data.Tables.TableClient table = _tableStorageService.GetSampleEventAdminTable();

        // Find and delete any entry with matching EventId
        await foreach (SampleEventAdminEntity entity in table.QueryAsync<SampleEventAdminEntity>(
            e => e.PartitionKey == "sample"))
        {
            if (entity.EventId == eventId)
            {
                await table.DeleteEntityAsync(entity.PartitionKey, entity.RowKey);
                return;
            }
        }
    }
}
