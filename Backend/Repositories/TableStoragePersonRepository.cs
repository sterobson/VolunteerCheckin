using Azure;
using Azure.Data.Tables;
using VolunteerCheckin.Functions.Models;
using VolunteerCheckin.Functions.Services;

namespace VolunteerCheckin.Functions.Repositories;

public class TableStoragePersonRepository : IPersonRepository
{
    private readonly TableClient _table;

    public TableStoragePersonRepository(TableStorageService tableStorage)
    {
        _table = tableStorage.GetPeopleTable();
    }

    public async Task<PersonEntity> AddAsync(PersonEntity person)
    {
        await _table.AddEntityAsync(person);
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

    public async Task<PersonEntity?> GetByEmailAsync(string email)
    {
        await foreach (PersonEntity person in _table.QueryAsync<PersonEntity>(p => p.PartitionKey == "PERSON"))
        {
            if (person.Email.Equals(email, StringComparison.OrdinalIgnoreCase))
            {
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
        await _table.UpdateEntityAsync(person, person.ETag);
    }

    public async Task DeleteAsync(string personId)
    {
        await _table.DeleteEntityAsync("PERSON", personId);
    }
}
