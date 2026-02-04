using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Azure.Data.Tables;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VolunteerCheckin.Functions.Models;
using VolunteerCheckin.Functions.Services;

namespace VolunteerCheckin.Functions.Tests;

/// <summary>
/// Assembly-level setup for performance tests.
/// Seeds test data once per test run to Azurite.
/// </summary>
[TestClass]
public static class PerformanceTestSetup
{
    public static TableStorageService TableStorage { get; private set; } = null!;
    public const int RecordCount = 500;
    public const string TestPrefix = "perftest_";

    // Known test identifiers for lookup tests
    public static string KnownPersonEmail { get; private set; } = string.Empty;
    public static string KnownPersonId { get; private set; } = string.Empty;
    public static string KnownTokenHash { get; private set; } = string.Empty;
    public static string KnownSessionTokenHash { get; private set; } = string.Empty;

    [AssemblyInitialize]
    public static async Task Initialize(TestContext context)
    {
        // Skip if not running performance tests
        if (Environment.GetEnvironmentVariable("RUN_PERFORMANCE_TESTS") != "true")
        {
            return;
        }

        string connectionString = Environment.GetEnvironmentVariable("AZURITE_CONNECTION_STRING")
            ?? "UseDevelopmentStorage=true";

        TableStorage = new TableStorageService(connectionString);

        await SeedPeopleAsync();
        await SeedAuthTokensAsync();
        await SeedAuthSessionsAsync();
    }

    [AssemblyCleanup]
    public static async Task Cleanup()
    {
        if (TableStorage == null) return;

        await CleanupTableAsync(TableStorage.GetPeopleTable(), "PERSON");
        await CleanupTableAsync(TableStorage.GetPersonEmailIndexTable(), "EMAIL_INDEX");
        await CleanupTableAsync(TableStorage.GetAuthTokensTable(), "AUTHTOKEN");
        await CleanupTableAsync(TableStorage.GetAuthSessionsTable(), "SESSION");
    }

    private static async Task SeedPeopleAsync()
    {
        TableClient peopleTable = TableStorage.GetPeopleTable();
        TableClient emailIndexTable = TableStorage.GetPersonEmailIndexTable();

        for (int i = 0; i < RecordCount; i++)
        {
            string personId = $"{TestPrefix}person_{i}";
            string email = $"{TestPrefix}user{i}@example.com";

            // Store the last one as the known lookup target
            if (i == RecordCount - 1)
            {
                KnownPersonId = personId;
                KnownPersonEmail = email;
            }

            PersonEntity person = new()
            {
                PartitionKey = "PERSON",
                RowKey = personId,
                PersonId = personId,
                Email = email,
                Name = $"Test User {i}",
                CreatedAt = DateTime.UtcNow
            };

            PersonEmailIndexEntity emailIndex = PersonEmailIndexEntity.Create(email, personId);

            await peopleTable.UpsertEntityAsync(person);
            await emailIndexTable.UpsertEntityAsync(emailIndex);
        }
    }

    private static async Task SeedAuthTokensAsync()
    {
        TableClient table = TableStorage.GetAuthTokensTable();

        for (int i = 0; i < RecordCount; i++)
        {
            string tokenHash = HashString($"{TestPrefix}token_{i}");

            if (i == RecordCount - 1)
            {
                KnownTokenHash = tokenHash;
            }

            AuthTokenEntity token = new()
            {
                PartitionKey = "AUTHTOKEN",
                RowKey = tokenHash,
                TokenId = $"{TestPrefix}tokenid_{i}",
                TokenHash = tokenHash,
                PersonId = $"{TestPrefix}person_{i % 100}",
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(1)
            };

            await table.UpsertEntityAsync(token);
        }
    }

    private static async Task SeedAuthSessionsAsync()
    {
        TableClient table = TableStorage.GetAuthSessionsTable();

        for (int i = 0; i < RecordCount; i++)
        {
            string sessionTokenHash = HashString($"{TestPrefix}session_{i}");

            if (i == RecordCount - 1)
            {
                KnownSessionTokenHash = sessionTokenHash;
            }

            AuthSessionEntity session = new()
            {
                PartitionKey = "SESSION",
                RowKey = sessionTokenHash,
                SessionId = $"{TestPrefix}sessionid_{i}",
                SessionTokenHash = sessionTokenHash,
                PersonId = $"{TestPrefix}person_{i % 100}",
                AuthMethod = "SecureEmailLink",
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(24)
            };

            await table.UpsertEntityAsync(session);
        }
    }

    private static async Task CleanupTableAsync(TableClient table, string partitionKey)
    {
        try
        {
            List<Task> deleteTasks = [];

            await foreach (TableEntity entity in table.QueryAsync<TableEntity>(
                e => e.PartitionKey == partitionKey && e.RowKey.CompareTo(TestPrefix) >= 0))
            {
                if (entity.RowKey.StartsWith(TestPrefix) || entity.RowKey.Contains(TestPrefix))
                {
                    deleteTasks.Add(table.DeleteEntityAsync(entity.PartitionKey, entity.RowKey));
                }
            }

            await Task.WhenAll(deleteTasks);
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    private static string HashString(string input)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        // Use hex encoding instead of Base64 to avoid invalid RowKey characters (/, +, =)
        return Convert.ToHexString(bytes);
    }
}
