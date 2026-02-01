using Azure;
using Azure.Data.Tables;
using VolunteerCheckin.Functions.Models;
using VolunteerCheckin.Functions.Services;

namespace VolunteerCheckin.Functions.Repositories;

/// <summary>
/// Azure Table Storage implementation of INoteRepository
/// </summary>
public class TableStorageNoteRepository : INoteRepository
{
    private readonly TableClient _table;

    public TableStorageNoteRepository(TableStorageService tableStorage)
    {
        _table = tableStorage.GetNotesTable();
    }

    public async Task<NoteEntity?> GetAsync(string eventId, string noteId)
    {
        try
        {
            Response<NoteEntity> response = await _table.GetEntityAsync<NoteEntity>(eventId, noteId);
            NoteEntity note = response.Value;

            // Return null for deleted notes
            if (note.IsDeleted)
            {
                return null;
            }

            return note;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task<IEnumerable<NoteEntity>> GetByEventAsync(string eventId)
    {
        List<NoteEntity> notes = [];
        await foreach (NoteEntity note in _table.QueryAsync<NoteEntity>(n => n.PartitionKey == eventId && !n.IsDeleted))
        {
            notes.Add(note);
        }
        return notes
            .OrderByDescending(n => n.IsPinned)
            .ThenBy(n => n.DisplayOrder)
            .ThenByDescending(n => n.CreatedAt);
    }

    public async Task<IEnumerable<NoteEntity>> GetAllByEventAsync(string eventId)
    {
        List<NoteEntity> notes = [];
        await foreach (NoteEntity note in _table.QueryAsync<NoteEntity>(n => n.PartitionKey == eventId))
        {
            notes.Add(note);
        }
        return notes
            .OrderByDescending(n => n.IsPinned)
            .ThenBy(n => n.DisplayOrder)
            .ThenByDescending(n => n.CreatedAt);
    }

    public async Task AddAsync(NoteEntity note)
    {
        await _table.AddEntityAsync(note);
    }

    public async Task UpdateAsync(NoteEntity note)
    {
        await _table.UpdateEntityAsync(note, note.ETag);
    }

    public async Task DeleteAsync(string eventId, string noteId)
    {
        NoteEntity? note = await GetAsync(eventId, noteId);
        if (note != null)
        {
            note.IsDeleted = true;
            await _table.UpdateEntityAsync(note, note.ETag);
        }
    }

    public async Task HardDeleteAsync(string eventId, string noteId)
    {
        await _table.DeleteEntityAsync(eventId, noteId);
    }

    public async Task UpdateDisplayOrdersAsync(string eventId, Dictionary<string, int> noteDisplayOrders)
    {
        foreach (KeyValuePair<string, int> kvp in noteDisplayOrders)
        {
            try
            {
                Response<NoteEntity> response = await _table.GetEntityAsync<NoteEntity>(eventId, kvp.Key);
                NoteEntity note = response.Value;
                note.DisplayOrder = kvp.Value;
                await _table.UpdateEntityAsync(note, note.ETag);
            }
            catch (RequestFailedException)
            {
                // Skip notes that don't exist
            }
        }
    }

    public async Task DeleteAllByEventAsync(string eventId)
    {
        await foreach (NoteEntity note in _table.QueryAsync<NoteEntity>(n => n.PartitionKey == eventId))
        {
            await _table.DeleteEntityAsync(note.PartitionKey, note.RowKey);
        }
    }
}
