/**
 * Composable for admin note management
 * Handles modal state, form data, and note operations in admin context
 */

import { ref } from 'vue';
import { notesApi } from '../services/api';

export function useAdminNoteManagement(eventId) {
  // Data
  const notes = ref([]);

  // Modal state
  const showEditNote = ref(false);
  const noteInitialTab = ref('details');

  // Selected note
  const selectedNote = ref(null);

  /**
   * Load notes for event
   */
  const loadNotes = async () => {
    try {
      const response = await notesApi.getByEvent(eventId.value);
      notes.value = response.data;
      return response.data;
    } catch (error) {
      console.error('Failed to load notes:', error);
      throw error;
    }
  };

  /**
   * Open modal to add new note
   */
  const handleAddNote = () => {
    selectedNote.value = null;
    noteInitialTab.value = 'details';
    showEditNote.value = true;
  };

  /**
   * Select a note for editing
   */
  const handleSelectNote = (note) => {
    selectedNote.value = note;
    noteInitialTab.value = 'details';
    showEditNote.value = true;
  };

  /**
   * Close edit note modal
   */
  const closeEditNoteModal = () => {
    showEditNote.value = false;
    selectedNote.value = null;
  };

  /**
   * Save note (create or update)
   */
  const saveNote = async (formData) => {
    if (selectedNote.value && selectedNote.value.noteId) {
      // Update existing note
      await notesApi.update(eventId.value, selectedNote.value.noteId, formData);
    } else {
      // Create new note
      await notesApi.create(eventId.value, formData);
    }
  };

  /**
   * Delete a note
   */
  const deleteNote = async (noteId) => {
    await notesApi.delete(eventId.value, noteId);
  };

  return {
    // Data
    notes,

    // Modal state
    showEditNote,
    noteInitialTab,

    // Selected note
    selectedNote,

    // Methods
    loadNotes,
    handleAddNote,
    handleSelectNote,
    closeEditNoteModal,
    saveNote,
    deleteNote,
  };
}
