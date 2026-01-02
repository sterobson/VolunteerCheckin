using Azure.Data.Tables;
using VolunteerCheckin.Functions.Models;
using System.Collections.Concurrent;

namespace VolunteerCheckin.Functions.Services;

public class TableStorageService
{
    private readonly TableServiceClient _tableServiceClient;
    private readonly ConcurrentDictionary<string, bool> _initializedTables = new();

    private const string EventsTable = "Events";
    private const string LocationsTable = "Locations";
    private const string AssignmentsTable = "Assignments";
    private const string AdminUsersTable = "AdminUsers";
    private const string UserEventMappingsTable = "UserEventMappings";
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
    public TableClient GetAdminUsersTable() => GetOrCreateTable(AdminUsersTable);
    public TableClient GetUserEventMappingsTable() => GetOrCreateTable(UserEventMappingsTable);
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
}
