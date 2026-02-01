using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Azure.Data.Tables;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using VolunteerCheckin.Functions.Services;

namespace VolunteerCheckin.Functions.Tests;

/// <summary>
/// Tests for TableStorageService - Azure Table Storage client management.
/// Note: These tests verify the service structure and table client accessor methods.
/// Full integration tests with actual Azure Storage should be separate.
/// </summary>
[TestClass]
public class TableStorageServiceTests
{
    // Use Azure Storage Emulator connection string for local testing
    private const string ConnectionString = "DefaultEndpointsProtocol=https;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;EndpointSuffix=core.windows.net";

    #region Constructor Tests

    [TestMethod]
    public void Constructor_WithValidConnectionString_CreatesInstance()
    {
        // Act
        TableStorageService service = new TableStorageService(ConnectionString);

        // Assert
        service.ShouldNotBeNull();
    }

    [TestMethod]
    public void Constructor_WithEmptyConnectionString_ThrowsArgumentException()
    {
        // Act & Assert - Azure SDK throws ArgumentException for empty connection string
        Should.Throw<ArgumentException>(() => new TableStorageService(""));
    }

    #endregion

    #region Table Getter Tests
    // Note: These tests require Azure Storage connection (Azurite or actual storage)
    // They are marked as Integration tests to skip in fast unit test runs

    [TestMethod]
    [Ignore("Requires Azure Storage (Azurite) to run")]
    public void GetEventsTable_ReturnsTableClient()
    {
        // Arrange
        TableStorageService service = new TableStorageService(ConnectionString);

        // Act
        TableClient table = service.GetEventsTable();

        // Assert
        table.ShouldNotBeNull();
        table.Name.ShouldBe("Events");
    }

    [TestMethod]
    [Ignore("Requires Azure Storage (Azurite) to run")]
    public void GetLocationsTable_ReturnsTableClient()
    {
        // Arrange
        TableStorageService service = new TableStorageService(ConnectionString);

        // Act
        TableClient table = service.GetLocationsTable();

        // Assert
        table.ShouldNotBeNull();
        table.Name.ShouldBe("Locations");
    }

    [TestMethod]
    [Ignore("Requires Azure Storage (Azurite) to run")]
    public void GetAssignmentsTable_ReturnsTableClient()
    {
        // Arrange
        TableStorageService service = new TableStorageService(ConnectionString);

        // Act
        TableClient table = service.GetAssignmentsTable();

        // Assert
        table.ShouldNotBeNull();
        table.Name.ShouldBe("Assignments");
    }

    [TestMethod]
    [Ignore("Requires Azure Storage (Azurite) to run")]
    public void GetMarshalsTable_ReturnsTableClient()
    {
        // Arrange
        TableStorageService service = new TableStorageService(ConnectionString);

        // Act
        TableClient table = service.GetMarshalsTable();

        // Assert
        table.ShouldNotBeNull();
        table.Name.ShouldBe("Marshals");
    }

    [TestMethod]
    [Ignore("Requires Azure Storage (Azurite) to run")]
    public void GetAreasTable_ReturnsTableClient()
    {
        // Arrange
        TableStorageService service = new TableStorageService(ConnectionString);

        // Act
        TableClient table = service.GetAreasTable();

        // Assert
        table.ShouldNotBeNull();
        table.Name.ShouldBe("Areas");
    }

    [TestMethod]
    [Ignore("Requires Azure Storage (Azurite) to run")]
    public void GetChecklistItemsTable_ReturnsTableClient()
    {
        // Arrange
        TableStorageService service = new TableStorageService(ConnectionString);

        // Act
        TableClient table = service.GetChecklistItemsTable();

        // Assert
        table.ShouldNotBeNull();
        table.Name.ShouldBe("ChecklistItems");
    }

    [TestMethod]
    [Ignore("Requires Azure Storage (Azurite) to run")]
    public void GetChecklistCompletionsTable_ReturnsTableClient()
    {
        // Arrange
        TableStorageService service = new TableStorageService(ConnectionString);

        // Act
        TableClient table = service.GetChecklistCompletionsTable();

        // Assert
        table.ShouldNotBeNull();
        table.Name.ShouldBe("ChecklistCompletions");
    }

    [TestMethod]
    [Ignore("Requires Azure Storage (Azurite) to run")]
    public void GetPeopleTable_ReturnsTableClient()
    {
        // Arrange
        TableStorageService service = new TableStorageService(ConnectionString);

        // Act
        TableClient table = service.GetPeopleTable();

        // Assert
        table.ShouldNotBeNull();
        table.Name.ShouldBe("People");
    }

    [TestMethod]
    [Ignore("Requires Azure Storage (Azurite) to run")]
    public void GetEventRolesTable_ReturnsTableClient()
    {
        // Arrange
        TableStorageService service = new TableStorageService(ConnectionString);

        // Act
        TableClient table = service.GetEventRolesTable();

        // Assert
        table.ShouldNotBeNull();
        table.Name.ShouldBe("EventRoles");
    }

    [TestMethod]
    [Ignore("Requires Azure Storage (Azurite) to run")]
    public void GetAuthTokensTable_ReturnsTableClient()
    {
        // Arrange
        TableStorageService service = new TableStorageService(ConnectionString);

        // Act
        TableClient table = service.GetAuthTokensTable();

        // Assert
        table.ShouldNotBeNull();
        table.Name.ShouldBe("AuthTokens");
    }

    [TestMethod]
    [Ignore("Requires Azure Storage (Azurite) to run")]
    public void GetAuthSessionsTable_ReturnsTableClient()
    {
        // Arrange
        TableStorageService service = new TableStorageService(ConnectionString);

        // Act
        TableClient table = service.GetAuthSessionsTable();

        // Assert
        table.ShouldNotBeNull();
        table.Name.ShouldBe("AuthSessions");
    }

    [TestMethod]
    [Ignore("Requires Azure Storage (Azurite) to run")]
    public void GetNotesTable_ReturnsTableClient()
    {
        // Arrange
        TableStorageService service = new TableStorageService(ConnectionString);

        // Act
        TableClient table = service.GetNotesTable();

        // Assert
        table.ShouldNotBeNull();
        table.Name.ShouldBe("Notes");
    }

    [TestMethod]
    [Ignore("Requires Azure Storage (Azurite) to run")]
    public void GetEventContactsTable_ReturnsTableClient()
    {
        // Arrange
        TableStorageService service = new TableStorageService(ConnectionString);

        // Act
        TableClient table = service.GetEventContactsTable();

        // Assert
        table.ShouldNotBeNull();
        table.Name.ShouldBe("EventContacts");
    }

    [TestMethod]
    [Ignore("Requires Azure Storage (Azurite) to run")]
    public void GetPersonEmailIndexTable_ReturnsTableClient()
    {
        // Arrange
        TableStorageService service = new TableStorageService(ConnectionString);

        // Act
        TableClient table = service.GetPersonEmailIndexTable();

        // Assert
        table.ShouldNotBeNull();
        table.Name.ShouldBe("PersonEmailIndex");
    }

    [TestMethod]
    [Ignore("Requires Azure Storage (Azurite) to run")]
    public void GetIncidentsTable_ReturnsTableClient()
    {
        // Arrange
        TableStorageService service = new TableStorageService(ConnectionString);

        // Act
        TableClient table = service.GetIncidentsTable();

        // Assert
        table.ShouldNotBeNull();
        table.Name.ShouldBe("Incidents");
    }

    #endregion

    #region Lazy Initialization Tests

    [TestMethod]
    [Ignore("Requires Azure Storage (Azurite) to run")]
    public void GetTable_CalledMultipleTimes_ReturnsSameTableClient()
    {
        // Arrange
        TableStorageService service = new TableStorageService(ConnectionString);

        // Act
        TableClient table1 = service.GetEventsTable();
        TableClient table2 = service.GetEventsTable();

        // Assert - Should return the same table client (lazy init)
        table1.Name.ShouldBe(table2.Name);
        table1.AccountName.ShouldBe(table2.AccountName);
    }

    [TestMethod]
    [Ignore("Requires Azure Storage (Azurite) to run")]
    public void GetDifferentTables_ReturnsDistinctClients()
    {
        // Arrange
        TableStorageService service = new TableStorageService(ConnectionString);

        // Act
        TableClient eventsTable = service.GetEventsTable();
        TableClient locationsTable = service.GetLocationsTable();
        TableClient marshalsTable = service.GetMarshalsTable();

        // Assert
        eventsTable.Name.ShouldNotBe(locationsTable.Name);
        locationsTable.Name.ShouldNotBe(marshalsTable.Name);
        eventsTable.Name.ShouldNotBe(marshalsTable.Name);
    }

    #endregion

    #region All Tables Test

    [TestMethod]
    [Ignore("Requires Azure Storage (Azurite) to run")]
    public void AllTableGetters_ReturnValidTableClients()
    {
        // Arrange
        TableStorageService service = new TableStorageService(ConnectionString);

        // Act & Assert - All table getters should return valid clients
        service.GetEventsTable().ShouldNotBeNull();
        service.GetLocationsTable().ShouldNotBeNull();
        service.GetAssignmentsTable().ShouldNotBeNull();
        service.GetMarshalsTable().ShouldNotBeNull();
        service.GetAreasTable().ShouldNotBeNull();
        service.GetChecklistItemsTable().ShouldNotBeNull();
        service.GetChecklistCompletionsTable().ShouldNotBeNull();
        service.GetPeopleTable().ShouldNotBeNull();
        service.GetEventRolesTable().ShouldNotBeNull();
        service.GetAuthTokensTable().ShouldNotBeNull();
        service.GetAuthSessionsTable().ShouldNotBeNull();
        service.GetNotesTable().ShouldNotBeNull();
        service.GetEventContactsTable().ShouldNotBeNull();
        service.GetPersonEmailIndexTable().ShouldNotBeNull();
        service.GetIncidentsTable().ShouldNotBeNull();
    }

    [TestMethod]
    [Ignore("Requires Azure Storage (Azurite) to run")]
    public void AllTableGetters_ReturnExpectedTableNames()
    {
        // Arrange
        TableStorageService service = new TableStorageService(ConnectionString);

        // Define expected table names
        Dictionary<string, Func<TableClient>> tableGetters = new()
        {
            ["Events"] = service.GetEventsTable,
            ["Locations"] = service.GetLocationsTable,
            ["Assignments"] = service.GetAssignmentsTable,
            ["Marshals"] = service.GetMarshalsTable,
            ["Areas"] = service.GetAreasTable,
            ["ChecklistItems"] = service.GetChecklistItemsTable,
            ["ChecklistCompletions"] = service.GetChecklistCompletionsTable,
            ["People"] = service.GetPeopleTable,
            ["EventRoles"] = service.GetEventRolesTable,
            ["AuthTokens"] = service.GetAuthTokensTable,
            ["AuthSessions"] = service.GetAuthSessionsTable,
            ["Notes"] = service.GetNotesTable,
            ["EventContacts"] = service.GetEventContactsTable,
            ["PersonEmailIndex"] = service.GetPersonEmailIndexTable,
            ["Incidents"] = service.GetIncidentsTable
        };

        // Act & Assert
        foreach (KeyValuePair<string, Func<TableClient>> kvp in tableGetters)
        {
            TableClient table = kvp.Value();
            table.Name.ShouldBe(kvp.Key, $"Table getter for {kvp.Key} returned wrong name");
        }
    }

    #endregion

    #region Thread Safety Tests

    [TestMethod]
    [Ignore("Requires Azure Storage (Azurite) to run")]
    public void GetTable_ConcurrentCalls_DoNotThrow()
    {
        // Arrange
        TableStorageService service = new TableStorageService(ConnectionString);

        // Act - Call multiple getters concurrently
        List<Task<TableClient>> tasks =
        [
            Task.Run(() => service.GetEventsTable()),
            Task.Run(() => service.GetEventsTable()),
            Task.Run(() => service.GetLocationsTable()),
            Task.Run(() => service.GetLocationsTable()),
            Task.Run(() => service.GetMarshalsTable()),
            Task.Run(() => service.GetMarshalsTable())
        ];

        // Assert - Should not throw
        Should.NotThrow(() => Task.WaitAll(tasks.ToArray()));

        foreach (Task<TableClient> task in tasks)
        {
            task.Result.ShouldNotBeNull();
        }
    }

    #endregion

    #region Table Count Test

    [TestMethod]
    [Ignore("Requires Azure Storage (Azurite) to run")]
    public void Service_Has18Tables_AllAccessible()
    {
        // Arrange
        TableStorageService service = new TableStorageService(ConnectionString);

        // Act - Get all tables
        List<TableClient> tables =
        [
            service.GetEventsTable(),
            service.GetLocationsTable(),
            service.GetAssignmentsTable(),
            service.GetMarshalsTable(),
            service.GetAreasTable(),
            service.GetChecklistItemsTable(),
            service.GetChecklistCompletionsTable(),
            service.GetPeopleTable(),
            service.GetEventRolesTable(),
            service.GetAuthTokensTable(),
            service.GetAuthSessionsTable(),
            service.GetNotesTable(),
            service.GetEventContactsTable(),
            service.GetPersonEmailIndexTable(),
            service.GetIncidentsTable(),
            service.GetEventRoleDefinitionsTable(),
            service.GetLayersTable(),
            service.GetSampleEventAdminTable()
        ];

        // Assert
        tables.Count.ShouldBe(18);
        tables.ShouldAllBe(t => t != null);

        // Verify all table names are unique
        HashSet<string> uniqueNames = new HashSet<string>(tables.Select(t => t.Name));
        uniqueNames.Count.ShouldBe(18);
    }

    #endregion
}
