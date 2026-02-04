using Azure.Data.Tables;
using VolunteerCheckin.Functions.Models;
using System.Collections.Concurrent;

namespace VolunteerCheckin.Functions.Services;

public class TableStorageService
{
    private readonly TableServiceClient _tableServiceClient;
    private readonly ConcurrentDictionary<string, bool> _initializedTables = new(StringComparer.Ordinal);

    private const string EventsTable = "Events";
    private const string LocationsTable = "Locations";
    private const string AssignmentsTable = "Assignments";
    private const string MarshalsTable = "Marshals";
    private const string AreasTable = "Areas";
    private const string ChecklistItemsTable = "ChecklistItems";
    private const string ChecklistCompletionsTable = "ChecklistCompletions";
    private const string PeopleTable = "People";
    private const string EventRolesTable = "EventRoles";
    private const string AuthTokensTable = "AuthTokens";
    private const string AuthSessionsTable = "AuthSessions";
    private const string NotesTable = "Notes";
    private const string EventContactsTable = "EventContacts";
    private const string PersonEmailIndexTable = "PersonEmailIndex";
    private const string IncidentsTable = "Incidents";
    private const string EventRoleDefinitionsTable = "EventRoleDefinitions";
    private const string LayersTable = "Layers";
    private const string SampleEventAdminTable = "SampleEventAdmin";
    private const string EventDeletionsTable = "EventDeletions";
    private const string PaymentsTable = "Payments";
    private const string PendingEventsTable = "PendingEvents";

    public TableStorageService(string connectionString)
    {
        _tableServiceClient = new TableServiceClient(connectionString);
        // Tables are now initialized lazily on first access
    }

    /// <summary>
    /// Gets a table client and ensures the table exists.
    /// Uses lazy initialization to avoid blocking in constructor.
    /// </summary>
    private TableClient GetOrCreateTable(string tableName)
    {
        // Check if already initialized to avoid repeated calls
        if (!_initializedTables.ContainsKey(tableName))
        {
            // CreateTableIfNotExists is idempotent and thread-safe
            _tableServiceClient.CreateTableIfNotExists(tableName);
            _initializedTables.TryAdd(tableName, true);
        }

        return _tableServiceClient.GetTableClient(tableName);
    }

    public TableClient GetEventsTable() => GetOrCreateTable(EventsTable);
    public TableClient GetLocationsTable() => GetOrCreateTable(LocationsTable);
    public TableClient GetAssignmentsTable() => GetOrCreateTable(AssignmentsTable);
    public TableClient GetMarshalsTable() => GetOrCreateTable(MarshalsTable);
    public TableClient GetAreasTable() => GetOrCreateTable(AreasTable);
    public TableClient GetChecklistItemsTable() => GetOrCreateTable(ChecklistItemsTable);
    public TableClient GetChecklistCompletionsTable() => GetOrCreateTable(ChecklistCompletionsTable);
    public TableClient GetPeopleTable() => GetOrCreateTable(PeopleTable);
    public TableClient GetEventRolesTable() => GetOrCreateTable(EventRolesTable);
    public TableClient GetAuthTokensTable() => GetOrCreateTable(AuthTokensTable);
    public TableClient GetAuthSessionsTable() => GetOrCreateTable(AuthSessionsTable);
    public TableClient GetNotesTable() => GetOrCreateTable(NotesTable);
    public TableClient GetEventContactsTable() => GetOrCreateTable(EventContactsTable);
    public TableClient GetPersonEmailIndexTable() => GetOrCreateTable(PersonEmailIndexTable);
    public TableClient GetIncidentsTable() => GetOrCreateTable(IncidentsTable);
    public TableClient GetEventRoleDefinitionsTable() => GetOrCreateTable(EventRoleDefinitionsTable);
    public TableClient GetLayersTable() => GetOrCreateTable(LayersTable);
    public TableClient GetSampleEventAdminTable() => GetOrCreateTable(SampleEventAdminTable);
    public TableClient GetEventDeletionsTable() => GetOrCreateTable(EventDeletionsTable);
    public TableClient GetPaymentsTable() => GetOrCreateTable(PaymentsTable);
    public TableClient GetPendingEventsTable() => GetOrCreateTable(PendingEventsTable);
}
