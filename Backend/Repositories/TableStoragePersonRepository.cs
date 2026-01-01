using Azure;
using Azure.Data.Tables;
using VolunteerCheckin.Functions.Models;
using VolunteerCheckin.Functions.Services;

namespace VolunteerCheckin.Functions.Repositories;

public class TableStoragePersonRepository : IPersonRepository
{
    private readonly TableClient _table;
    private readonly TableClient _emailIndexTable;

    public TableStoragePersonRepository(TableStorageService tableStorage)
    {
        _table = tableStorage.GetPeopleTable();
        _emailIndexTable = tableStorage.GetPersonEmailIndexTable();
    }

    public async Task<PersonEntity> AddAsync(PersonEntity person)
    {
        // Add the person
        await _table.AddEntityAsync(person);

        // Add email index for O(1) lookup
        PersonEmailIndexEntity emailIndex = PersonEmailIndexEntity.Create(person.Email, person.PersonId);
        try
        {
            await _emailIndexTable.AddEntityAsync(emailIndex);
        }
        catch (RequestFailedException)
        {
            // Index may already exist (race condition) - update it
            await _emailIndexTable.UpsertEntityAsync(emailIndex);
        }

        return person;
    }

    public async Task<PersonEntity?> GetAsync(string personId)
    {
        try
        {
            Response<PersonEntity> response = await _table.GetEntityAsync<PersonEntity>("PERSON", personId);
            return response.Value;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    /// <summary>
    /// Get person by email using O(1) index lookup.
    /// Falls back to table scan if index is missing (for backward compatibility).
    /// </summary>
    public async Task<PersonEntity?> GetByEmailAsync(string email)
    {
        string normalizedEmail = PersonEmailIndexEntity.NormalizeEmail(email);

        // Try O(1) index lookup first
        try
        {
            Response<PersonEmailIndexEntity> indexResponse = await _emailIndexTable.GetEntityAsync<PersonEmailIndexEntity>(
                "EMAIL_INDEX", normalizedEmail);

            if (indexResponse.Value != null)
            {
                return await GetAsync(indexResponse.Value.PersonId);
            }
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            // Index not found - fall back to table scan for backward compatibility
            // This handles existing data that doesn't have an index entry yet
        }

        // Fallback: scan table (for backward compatibility with existing data)
        await foreach (PersonEntity person in _table.QueryAsync<PersonEntity>(p => p.PartitionKey == "PERSON"))
        {
            if (person.Email.Equals(email, StringComparison.OrdinalIgnoreCase))
            {
                // Create the missing index for future lookups
                try
                {
                    PersonEmailIndexEntity emailIndex = PersonEmailIndexEntity.Create(email, person.PersonId);
                    await _emailIndexTable.UpsertEntityAsync(emailIndex);
                }
                catch
                {
                    // Ignore index creation failures
                }
                return person;
            }
        }
        return null;
    }

    public async Task<IEnumerable<PersonEntity>> GetAllAsync()
    {
        List<PersonEntity> people = [];
        await foreach (PersonEntity person in _table.QueryAsync<PersonEntity>(p => p.PartitionKey == "PERSON"))
        {
            people.Add(person);
        }
        return people;
    }

    public async Task UpdateAsync(PersonEntity person)
    {
        // Get old email to check if it changed
        PersonEntity? existingPerson = await GetAsync(person.PersonId);
        string? oldEmail = existingPerson?.Email;

        // Update the person
        await _table.UpdateEntityAsync(person, person.ETag);

        // Update email index if email changed
        if (oldEmail != null && !oldEmail.Equals(person.Email, StringComparison.OrdinalIgnoreCase))
        {
            // Delete old index
            try
            {
                string oldNormalizedEmail = PersonEmailIndexEntity.NormalizeEmail(oldEmail);
                await _emailIndexTable.DeleteEntityAsync("EMAIL_INDEX", oldNormalizedEmail);
            }
            catch (RequestFailedException)
            {
                // Old index may not exist
            }

            // Add new index
            PersonEmailIndexEntity newIndex = PersonEmailIndexEntity.Create(person.Email, person.PersonId);
            await _emailIndexTable.UpsertEntityAsync(newIndex);
        }
    }

    public async Task DeleteAsync(string personId)
    {
        // Get person to find email for index deletion
        PersonEntity? person = await GetAsync(personId);

        // Delete the person
        await _table.DeleteEntityAsync("PERSON", personId);

        // Delete email index
        if (person != null)
        {
            try
            {
                string normalizedEmail = PersonEmailIndexEntity.NormalizeEmail(person.Email);
                await _emailIndexTable.DeleteEntityAsync("EMAIL_INDEX", normalizedEmail);
            }
            catch (RequestFailedException)
            {
                // Index may not exist
            }
        }
    }
}
