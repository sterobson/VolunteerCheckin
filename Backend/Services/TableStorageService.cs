using Azure.Data.Tables;
using VolunteerCheckin.Functions.Models;

namespace VolunteerCheckin.Functions.Services;

public class TableStorageService
{
    private readonly TableServiceClient _tableServiceClient;
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

    public TableStorageService(string connectionString)
    {
        _tableServiceClient = new TableServiceClient(connectionString);
        InitializeTables().Wait();
    }

    private async Task InitializeTables()
    {
        await _tableServiceClient.CreateTableIfNotExistsAsync(EventsTable);
        await _tableServiceClient.CreateTableIfNotExistsAsync(LocationsTable);
        await _tableServiceClient.CreateTableIfNotExistsAsync(AssignmentsTable);
        await _tableServiceClient.CreateTableIfNotExistsAsync(AdminUsersTable);
        await _tableServiceClient.CreateTableIfNotExistsAsync(UserEventMappingsTable);
        await _tableServiceClient.CreateTableIfNotExistsAsync(MarshalsTable);
        await _tableServiceClient.CreateTableIfNotExistsAsync(AreasTable);
        await _tableServiceClient.CreateTableIfNotExistsAsync(ChecklistItemsTable);
        await _tableServiceClient.CreateTableIfNotExistsAsync(ChecklistCompletionsTable);
        await _tableServiceClient.CreateTableIfNotExistsAsync(PeopleTable);
        await _tableServiceClient.CreateTableIfNotExistsAsync(EventRolesTable);
        await _tableServiceClient.CreateTableIfNotExistsAsync(AuthTokensTable);
        await _tableServiceClient.CreateTableIfNotExistsAsync(AuthSessionsTable);
    }

    public TableClient GetEventsTable() => _tableServiceClient.GetTableClient(EventsTable);
    public TableClient GetLocationsTable() => _tableServiceClient.GetTableClient(LocationsTable);
    public TableClient GetAssignmentsTable() => _tableServiceClient.GetTableClient(AssignmentsTable);
    public TableClient GetAdminUsersTable() => _tableServiceClient.GetTableClient(AdminUsersTable);
    public TableClient GetUserEventMappingsTable() => _tableServiceClient.GetTableClient(UserEventMappingsTable);
    public TableClient GetMarshalsTable() => _tableServiceClient.GetTableClient(MarshalsTable);
    public TableClient GetAreasTable() => _tableServiceClient.GetTableClient(AreasTable);
    public TableClient GetChecklistItemsTable() => _tableServiceClient.GetTableClient(ChecklistItemsTable);
    public TableClient GetChecklistCompletionsTable() => _tableServiceClient.GetTableClient(ChecklistCompletionsTable);
    public TableClient GetPeopleTable() => _tableServiceClient.GetTableClient(PeopleTable);
    public TableClient GetEventRolesTable() => _tableServiceClient.GetTableClient(EventRolesTable);
    public TableClient GetAuthTokensTable() => _tableServiceClient.GetTableClient(AuthTokensTable);
    public TableClient GetAuthSessionsTable() => _tableServiceClient.GetTableClient(AuthSessionsTable);
}
