using Azure;
using Azure.Data.Tables;
using VolunteerCheckin.Functions.Models;
using VolunteerCheckin.Functions.Services;

namespace VolunteerCheckin.Functions.Repositories;

/// <summary>
/// Azure Table Storage implementation of IEventContactRepository
/// </summary>
public class TableStorageEventContactRepository : IEventContactRepository
{
    private readonly TableClient _table;

    public TableStorageEventContactRepository(TableStorageService tableStorage)
    {
        _table = tableStorage.GetEventContactsTable();
    }

    public async Task<EventContactEntity?> GetAsync(string eventId, string contactId)
    {
        try
        {
            Response<EventContactEntity> response = await _table.GetEntityAsync<EventContactEntity>(eventId, contactId);
            EventContactEntity contact = response.Value;

            // Return null for deleted contacts
            if (contact.IsDeleted)
            {
                return null;
            }

            return contact;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task<IEnumerable<EventContactEntity>> GetByEventAsync(string eventId)
    {
        List<EventContactEntity> contacts = [];
        await foreach (EventContactEntity contact in _table.QueryAsync<EventContactEntity>(c => c.PartitionKey == eventId && !c.IsDeleted))
        {
            contacts.Add(contact);
        }
        return contacts
            .OrderByDescending(c => c.IsPrimary)
            .ThenBy(c => c.DisplayOrder)
            .ThenBy(c => c.Role, StringComparer.Ordinal)
            .ThenBy(c => c.Name, StringComparer.Ordinal);
    }

    public async Task<IEnumerable<EventContactEntity>> GetByRoleAsync(string eventId, string role)
    {
        List<EventContactEntity> contacts = [];
        await foreach (EventContactEntity contact in _table.QueryAsync<EventContactEntity>(c => c.PartitionKey == eventId && c.Role == role && !c.IsDeleted))
        {
            contacts.Add(contact);
        }
        return contacts
            .OrderByDescending(c => c.IsPrimary)
            .ThenBy(c => c.DisplayOrder)
            .ThenBy(c => c.Name, StringComparer.Ordinal);
    }

    public async Task<IEnumerable<EventContactEntity>> GetByMarshalAsync(string eventId, string marshalId)
    {
        List<EventContactEntity> contacts = [];
        await foreach (EventContactEntity contact in _table.QueryAsync<EventContactEntity>(c => c.PartitionKey == eventId && c.MarshalId == marshalId && !c.IsDeleted))
        {
            contacts.Add(contact);
        }
        return contacts
            .OrderBy(c => c.Role, StringComparer.Ordinal)
            .ThenBy(c => c.DisplayOrder);
    }

    public async Task AddAsync(EventContactEntity contact)
    {
        await _table.AddEntityAsync(contact);
    }

    public async Task UpdateAsync(EventContactEntity contact)
    {
        await _table.UpdateEntityAsync(contact, contact.ETag, TableUpdateMode.Replace);
    }

    public async Task DeleteAsync(string eventId, string contactId)
    {
        EventContactEntity? contact = await GetAsync(eventId, contactId);
        if (contact != null)
        {
            contact.IsDeleted = true;
            await _table.UpdateEntityAsync(contact, contact.ETag);
        }
    }

    public async Task HardDeleteAsync(string eventId, string contactId)
    {
        await _table.DeleteEntityAsync(eventId, contactId);
    }

    public async Task UpdateDisplayOrdersAsync(string eventId, Dictionary<string, int> contactDisplayOrders)
    {
        foreach (KeyValuePair<string, int> kvp in contactDisplayOrders)
        {
            try
            {
                Response<EventContactEntity> response = await _table.GetEntityAsync<EventContactEntity>(eventId, kvp.Key);
                EventContactEntity contact = response.Value;
                contact.DisplayOrder = kvp.Value;
                await _table.UpdateEntityAsync(contact, contact.ETag, TableUpdateMode.Replace);
            }
            catch (RequestFailedException)
            {
                // Skip contacts that don't exist
            }
        }
    }
}
