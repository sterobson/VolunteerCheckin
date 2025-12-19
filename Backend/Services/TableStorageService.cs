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
    private const string MagicLinksTable = "MagicLinks";

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
        await _tableServiceClient.CreateTableIfNotExistsAsync(MagicLinksTable);
    }

    public TableClient GetEventsTable() => _tableServiceClient.GetTableClient(EventsTable);
    public TableClient GetLocationsTable() => _tableServiceClient.GetTableClient(LocationsTable);
    public TableClient GetAssignmentsTable() => _tableServiceClient.GetTableClient(AssignmentsTable);
    public TableClient GetAdminUsersTable() => _tableServiceClient.GetTableClient(AdminUsersTable);
    public TableClient GetMagicLinksTable() => _tableServiceClient.GetTableClient(MagicLinksTable);
}
