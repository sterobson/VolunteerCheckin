/**
 * Composable for admin marshal management
 * Handles modal state, form data, and marshal operations in admin context
 */

import { ref, computed } from 'vue';
import { marshalsApi, checklistApi, notesApi } from '../services/api';

export function useAdminMarshalManagement(eventId, eventsStore) {
  // Data
  const marshals = ref([]);

  // Modal state
  const showEditMarshal = ref(false);
  const showMarshalCreated = ref(false);
  const showAssignmentConflict = ref(false);

  // Selected marshal
  const selectedMarshal = ref(null);
  const newlyCreatedMarshalId = ref(null);
  const newlyCreatedMarshalName = ref('');

  // Form data
  const editMarshalForm = ref(createEmptyMarshalForm());

  // Pending changes
  const pendingMarshalAssignments = ref([]);
  const pendingMarshalDeleteAssignments = ref([]);

  // Conflict handling
  const assignmentConflictData = ref({
    marshalName: '',
    locations: [],
    marshal: null,
  });
  const conflictResolveCallback = ref(null);

  /**
   * Create empty marshal form
   */
  function createEmptyMarshalForm() {
    return {
      id: '',
      name: '',
      email: '',
      phoneNumber: '',
      notes: '',
    };
  }

  /**
   * Load marshals for event
   */
  const loadMarshals = async () => {
    try {
      const response = await marshalsApi.getByEvent(eventId.value);
      marshals.value = response.data;
      return response.data;
    } catch (error) {
      console.error('Failed to load marshals:', error);
      throw error;
    }
  };

  /**
   * Open modal to add new marshal
   */
  const addNewMarshal = () => {
    selectedMarshal.value = null;
    editMarshalForm.value = createEmptyMarshalForm();
    pendingMarshalAssignments.value = [];
    pendingMarshalDeleteAssignments.value = [];
    showEditMarshal.value = true;
  };

  /**
   * Select a marshal for editing
   */
  const selectMarshal = (marshal) => {
    selectedMarshal.value = marshal;
    editMarshalForm.value = {
      id: marshal.id,
      name: marshal.name,
      email: marshal.email || '',
      phoneNumber: marshal.phoneNumber || '',
      notes: marshal.notes || '',
    };
    pendingMarshalAssignments.value = [];
    pendingMarshalDeleteAssignments.value = [];
    showEditMarshal.value = true;
  };

  /**
   * Close edit marshal modal
   */
  const closeEditMarshalModal = () => {
    showEditMarshal.value = false;
    selectedMarshal.value = null;
    editMarshalForm.value = createEmptyMarshalForm();
    pendingMarshalAssignments.value = [];
    pendingMarshalDeleteAssignments.value = [];
  };

  /**
   * Get assignments for currently selected marshal
   */
  const getMarshalAssignments = (locationStatuses) => {
    if (!selectedMarshal.value) return [];

    const assignments = [];
    locationStatuses.value.forEach(location => {
      const marshalAssignments = location.assignments.filter(
        a => a.marshalName === selectedMarshal.value.name
      );
      marshalAssignments.forEach(assignment => {
        assignments.push({
          ...assignment,
          locationName: location.name,
        });
      });
    });

    return assignments;
  };

  /**
   * Save marshal (create or update)
   */
  const saveMarshal = async (formData) => {
    let marshalId = selectedMarshal.value?.id;
    let isNewMarshal = false;

    if (selectedMarshal.value) {
      // Update existing marshal
      await marshalsApi.update(
        eventId.value,
        selectedMarshal.value.id,
        {
          name: formData.name,
          email: formData.email,
          phoneNumber: formData.phoneNumber,
          notes: formData.notes,
        }
      );

      // Create pending new checklist items
      if (formData.pendingNewChecklistItems && formData.pendingNewChecklistItems.length > 0) {
        for (const item of formData.pendingNewChecklistItems) {
          if (!item.text?.trim()) continue;
          await checklistApi.create(eventId.value, {
            text: item.text,
            scopeConfigurations: [
              { scope: 'SpecificPeople', itemType: 'Marshal', ids: [marshalId] }
            ],
            displayOrder: 0,
            isRequired: false,
          });
        }
      }

      // Create pending new notes
      if (formData.pendingNewNotes && formData.pendingNewNotes.length > 0) {
        for (const note of formData.pendingNewNotes) {
          if (!note.title?.trim() && !note.content?.trim()) continue;
          await notesApi.create(eventId.value, {
            title: note.title || 'Untitled',
            content: note.content || '',
            scopeConfigurations: [
              { scope: 'SpecificPeople', itemType: 'Marshal', ids: [marshalId] }
            ],
            displayOrder: 0,
            priority: 'Normal',
            isPinned: false,
          });
        }
      }
    } else {
      // Create new marshal
      const response = await marshalsApi.create({
        eventId: eventId.value,
        name: formData.name,
        email: formData.email,
        phoneNumber: formData.phoneNumber,
        notes: formData.notes,
        pendingNewChecklistItems: formData.pendingNewChecklistItems || [],
        pendingNewNotes: formData.pendingNewNotes || [],
      });
      marshalId = response.data.id;
      isNewMarshal = true;
    }

    // Delete pending assignments
    for (const assignmentId of pendingMarshalDeleteAssignments.value) {
      await eventsStore.deleteAssignment(eventId.value, assignmentId);
    }

    // Create pending assignments
    for (const pending of pendingMarshalAssignments.value) {
      await eventsStore.createAssignment({
        eventId: eventId.value,
        locationId: pending.locationId,
        marshalId: marshalId,
      });
    }

    // Process pending assignments from form (for new marshals)
    if (formData.pendingAssignments && formData.pendingAssignments.length > 0) {
      for (const pending of formData.pendingAssignments) {
        await eventsStore.createAssignment({
          eventId: eventId.value,
          locationId: pending.locationId,
          marshalId: marshalId,
        });
      }
    }

    // Show confirmation modal for newly created marshals
    if (isNewMarshal) {
      newlyCreatedMarshalId.value = marshalId;
      newlyCreatedMarshalName.value = formData.name;
      showMarshalCreated.value = true;
    }

    return { marshalId, isNewMarshal };
  };

  /**
   * Delete a marshal
   */
  const deleteMarshal = async (marshalId) => {
    const idToDelete = marshalId || selectedMarshal.value?.id;
    if (!idToDelete) {
      throw new Error('No marshal ID to delete');
    }
    await marshalsApi.delete(eventId.value, idToDelete);
  };

  /**
   * Add pending assignment to marshal
   */
  const addPendingMarshalAssignment = (locationId) => {
    pendingMarshalAssignments.value.push({ locationId });
  };

  /**
   * Remove assignment from marshal
   */
  const removeMarshalAssignment = (assignmentId) => {
    if (!pendingMarshalDeleteAssignments.value.includes(assignmentId)) {
      pendingMarshalDeleteAssignments.value.push(assignmentId);
    }
  };

  /**
   * Check for assignment conflicts
   */
  const checkAssignmentConflict = async (marshalId, locationStatuses, currentLocationId) => {
    const marshal = marshals.value.find(m => m.id === marshalId);
    if (!marshal) return null;

    const assignedLocations = locationStatuses.value.filter(loc =>
      loc.assignments?.some(a => a.marshalId === marshalId && loc.id !== currentLocationId)
    );

    if (assignedLocations.length > 0) {
      return {
        marshalName: marshal.name,
        locations: assignedLocations.map(loc => loc.name),
        marshal: marshal,
      };
    }
    return null;
  };

  /**
   * Show conflict modal and wait for resolution
   */
  const showConflictAndWait = async (conflictInfo) => {
    assignmentConflictData.value = conflictInfo;

    const choice = await new Promise((resolve) => {
      conflictResolveCallback.value = resolve;
      showAssignmentConflict.value = true;
    });

    return choice;
  };

  /**
   * Handle conflict choice from modal
   */
  const handleConflictChoice = (choice) => {
    if (conflictResolveCallback.value) {
      conflictResolveCallback.value(choice);
      conflictResolveCallback.value = null;
    }
    showAssignmentConflict.value = false;
  };

  /**
   * Create marshal inline (for location assignment)
   */
  const createMarshalInline = async (marshalData) => {
    const response = await marshalsApi.create({
      eventId: eventId.value,
      name: marshalData.name,
      email: marshalData.email,
      phoneNumber: marshalData.phoneNumber,
    });

    // Refresh marshals list
    await loadMarshals();

    return response.data;
  };

  /**
   * Get available locations for marshal assignment
   */
  const getAvailableLocations = (locationStatuses) => {
    if (!selectedMarshal.value) return locationStatuses.value;

    const assignedLocationIds = selectedMarshal.value.assignedLocationIds || [];
    const pendingLocationIds = pendingMarshalAssignments.value.map(p => p.locationId);
    const deleteLocationIds = pendingMarshalDeleteAssignments.value;

    return locationStatuses.value.filter(
      loc => !assignedLocationIds.includes(loc.id) &&
             !pendingLocationIds.includes(loc.id) ||
             deleteLocationIds.includes(loc.id)
    );
  };

  /**
   * Check if there are unsaved changes
   */
  const hasUnsavedChanges = computed(() => {
    return pendingMarshalAssignments.value.length > 0 || pendingMarshalDeleteAssignments.value.length > 0;
  });

  /**
   * Clear pending changes
   */
  const clearPendingChanges = () => {
    pendingMarshalAssignments.value = [];
    pendingMarshalDeleteAssignments.value = [];
  };

  return {
    // Data
    marshals,

    // Modal state
    showEditMarshal,
    showMarshalCreated,
    showAssignmentConflict,

    // Marshal state
    selectedMarshal,
    newlyCreatedMarshalId,
    newlyCreatedMarshalName,
    editMarshalForm,

    // Pending changes
    pendingMarshalAssignments,
    pendingMarshalDeleteAssignments,

    // Conflict handling
    assignmentConflictData,

    // Computed
    hasUnsavedChanges,

    // Methods
    loadMarshals,
    addNewMarshal,
    selectMarshal,
    closeEditMarshalModal,
    getMarshalAssignments,
    saveMarshal,
    deleteMarshal,
    addPendingMarshalAssignment,
    removeMarshalAssignment,
    checkAssignmentConflict,
    showConflictAndWait,
    handleConflictChoice,
    createMarshalInline,
    getAvailableLocations,
    clearPendingChanges,
  };
}
