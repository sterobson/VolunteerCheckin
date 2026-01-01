using VolunteerCheckin.Functions.Models;

namespace VolunteerCheckin.Functions.Repositories;

/// <summary>
/// Repository interface for managing notes
/// </summary>
public interface INoteRepository
{
    /// <summary>
    /// Get a note by its ID
    /// </summary>
    Task<NoteEntity?> GetAsync(string eventId, string noteId);

    /// <summary>
    /// Get all notes for an event (excluding deleted)
    /// </summary>
    Task<IEnumerable<NoteEntity>> GetByEventAsync(string eventId);

    /// <summary>
    /// Get all notes for an event, including deleted
    /// </summary>
    Task<IEnumerable<NoteEntity>> GetAllByEventAsync(string eventId);

    /// <summary>
    /// Add a new note
    /// </summary>
    Task AddAsync(NoteEntity note);

    /// <summary>
    /// Update an existing note
    /// </summary>
    Task UpdateAsync(NoteEntity note);

    /// <summary>
    /// Delete a note (soft delete)
    /// </summary>
    Task DeleteAsync(string eventId, string noteId);

    /// <summary>
    /// Permanently delete a note (hard delete)
    /// </summary>
    Task HardDeleteAsync(string eventId, string noteId);
}
