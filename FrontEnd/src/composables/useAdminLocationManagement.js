/**
 * Composable for admin location/checkpoint management
 * Handles modal state, form data, and location operations in admin context
 */

import { ref, computed } from 'vue';
import { locationsApi, checklistApi, notesApi } from '../services/api';
import { isValidWhat3Words } from '../utils/validators';

export function useAdminLocationManagement(eventId, eventsStore) {
  // Modal state
  const showAddLocation = ref(false);
  const showEditLocation = ref(false);
  const showAssignMarshalModal = ref(false);
  const isMovingLocation = ref(false);

  // Selected location
  const selectedLocation = ref(null);
  const preservedLocationFormData = ref(null);

  // Form data
  const locationForm = ref(createEmptyLocationForm());
  const editLocationForm = ref(createEmptyLocationForm());

  // Pending changes
  const pendingAssignments = ref([]);
  const pendingDeleteAssignments = ref([]);

  /**
   * Create empty location form
   */
  function createEmptyLocationForm() {
    return {
      id: null,
      name: '',
      description: '',
      latitude: 0,
      longitude: 0,
      requiredMarshals: 1,
      what3Words: '',
    };
  }

  /**
   * Reset location form to empty state
   */
  const resetLocationForm = () => {
    locationForm.value = createEmptyLocationForm();
  };

  /**
   * Reset edit form to empty state
   */
  const resetEditForm = () => {
    editLocationForm.value = createEmptyLocationForm();
  };

  /**
   * Open add location modal
   */
  const openAddLocationModal = () => {
    resetLocationForm();
    showAddLocation.value = true;
  };

  /**
   * Close add location modal
   */
  const closeAddLocationModal = () => {
    showAddLocation.value = false;
    resetLocationForm();
  };

  /**
   * Select a location for editing
   */
  const selectLocation = (location) => {
    selectedLocation.value = location;
    editLocationForm.value = {
      id: location.id,
      name: location.name,
      description: location.description || '',
      latitude: location.latitude,
      longitude: location.longitude,
      requiredMarshals: location.requiredMarshals,
      what3Words: location.what3Words || '',
    };
    pendingAssignments.value = [];
    pendingDeleteAssignments.value = [];
    showEditLocation.value = true;
  };

  /**
   * Open edit modal for a new location (no ID)
   */
  const openNewLocationModal = (coords = null) => {
    selectedLocation.value = {
      id: null,
      name: '',
      description: '',
      latitude: coords?.lat || 0,
      longitude: coords?.lng || 0,
      requiredMarshals: 1,
      what3Words: '',
      areaIds: [],
      assignments: [],
    };
    editLocationForm.value = {
      id: null,
      name: '',
      description: '',
      latitude: coords?.lat || 0,
      longitude: coords?.lng || 0,
      requiredMarshals: 1,
      what3Words: '',
    };
    pendingAssignments.value = [];
    pendingDeleteAssignments.value = [];
    showEditLocation.value = true;
  };

  /**
   * Close edit location modal
   */
  const closeEditLocationModal = () => {
    showEditLocation.value = false;
    selectedLocation.value = null;
    isMovingLocation.value = false;
    showAssignMarshalModal.value = false;
    preservedLocationFormData.value = null;
    resetEditForm();
    pendingAssignments.value = [];
    pendingDeleteAssignments.value = [];
  };

  /**
   * Save new location via add modal
   */
  const saveNewLocation = async (formData) => {
    if (!isValidWhat3Words(formData.what3Words)) {
      throw new Error('Invalid What3Words format. Please use word.word.word or word/word/word');
    }

    await eventsStore.createLocation({
      eventId: eventId.value,
      ...formData,
    });
  };

  /**
   * Update or create location via edit modal
   */
  const saveLocation = async (formData) => {
    if (!isValidWhat3Words(formData.what3Words)) {
      throw new Error('Invalid What3Words format');
    }

    const isNewLocation = !selectedLocation.value?.id;

    if (isNewLocation) {
      // Create new location
      const newLocation = await eventsStore.createLocation({
        eventId: eventId.value,
        name: formData.name,
        description: formData.description,
        latitude: formData.latitude,
        longitude: formData.longitude,
        requiredMarshals: formData.requiredMarshals,
        what3Words: formData.what3Words || null,
        startTime: formData.startTime,
        endTime: formData.endTime,
        styleType: formData.styleType,
        styleColor: formData.styleColor,
        styleBackgroundShape: formData.styleBackgroundShape,
        styleBackgroundColor: formData.styleBackgroundColor,
        styleBorderColor: formData.styleBorderColor,
        styleIconColor: formData.styleIconColor,
        styleSize: formData.styleSize,
        styleMapRotation: formData.styleMapRotation != null ? String(formData.styleMapRotation) : null,
        isDynamic: formData.isDynamic || false,
        locationUpdateScopeConfigurations: formData.locationUpdateScopeConfigurations || [],
        pendingNewChecklistItems: formData.pendingNewChecklistItems || [],
        pendingNewNotes: formData.pendingNewNotes || [],
      });

      // Process pending assignments for new locations
      if (formData.pendingAssignments && formData.pendingAssignments.length > 0) {
        for (const pending of formData.pendingAssignments) {
          await eventsStore.createAssignment({
            eventId: eventId.value,
            locationId: newLocation.id,
            marshalId: pending.marshalId,
          });
        }
      }

      return newLocation;
    } else {
      // Update existing location
      const locationId = selectedLocation.value.id;
      await locationsApi.update(
        eventId.value,
        locationId,
        {
          eventId: eventId.value,
          name: formData.name,
          description: formData.description,
          latitude: formData.latitude,
          longitude: formData.longitude,
          requiredMarshals: formData.requiredMarshals,
          what3Words: formData.what3Words || null,
          startTime: formData.startTime,
          endTime: formData.endTime,
          styleType: formData.styleType,
          styleColor: formData.styleColor,
          styleBackgroundShape: formData.styleBackgroundShape,
          styleBackgroundColor: formData.styleBackgroundColor,
          styleBorderColor: formData.styleBorderColor,
          styleIconColor: formData.styleIconColor,
          styleSize: formData.styleSize,
          styleMapRotation: formData.styleMapRotation != null ? String(formData.styleMapRotation) : null,
          isDynamic: formData.isDynamic || false,
          locationUpdateScopeConfigurations: formData.locationUpdateScopeConfigurations || [],
        }
      );

      // Create pending new checklist items
      if (formData.pendingNewChecklistItems && formData.pendingNewChecklistItems.length > 0) {
        for (const item of formData.pendingNewChecklistItems) {
          if (!item.text?.trim()) continue;
          await checklistApi.create(eventId.value, {
            text: item.text,
            scopeConfigurations: [
              { scope: 'EveryoneAtCheckpoints', itemType: 'Checkpoint', ids: [locationId] }
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
              { scope: 'EveryoneAtCheckpoints', itemType: 'Checkpoint', ids: [locationId] }
            ],
            displayOrder: 0,
            priority: 'Normal',
            isPinned: false,
          });
        }
      }

      // Delete pending assignments
      for (const assignmentId of pendingDeleteAssignments.value) {
        await eventsStore.deleteAssignment(eventId.value, assignmentId);
      }

      // Create pending assignments
      for (const pending of pendingAssignments.value) {
        await eventsStore.createAssignment({
          eventId: eventId.value,
          locationId: selectedLocation.value.id,
          marshalId: pending.marshalId,
        });
      }

      return { id: locationId };
    }
  };

  /**
   * Delete a location
   */
  const deleteLocation = async (locationId) => {
    await locationsApi.delete(eventId.value, locationId);
  };

  /**
   * Add pending assignment to location
   */
  const addPendingAssignment = (marshal) => {
    pendingAssignments.value.push({
      marshalId: marshal.id,
      marshalName: marshal.name,
    });

    // Optimistically update UI
    if (selectedLocation.value) {
      if (!selectedLocation.value.assignments) {
        selectedLocation.value.assignments = [];
      }
      selectedLocation.value.assignments.push({
        id: `temp-${Date.now()}`,
        marshalId: marshal.id,
        marshalName: marshal.name,
        locationId: selectedLocation.value.id,
        isCheckedIn: false,
        isPending: true,
      });
    }
  };

  /**
   * Remove assignment from location
   */
  const removeAssignment = (assignmentId) => {
    if (assignmentId?.toString().startsWith('temp-')) {
      // Remove from pending
      const assignment = selectedLocation.value?.assignments?.find(a => a.id === assignmentId);
      if (assignment) {
        pendingAssignments.value = pendingAssignments.value.filter(
          p => p.marshalId !== assignment.marshalId
        );
      }
    } else {
      // Add to pending deletions
      if (!pendingDeleteAssignments.value.includes(assignmentId)) {
        pendingDeleteAssignments.value.push(assignmentId);
      }
    }

    // Optimistically remove from UI
    if (selectedLocation.value?.assignments) {
      selectedLocation.value.assignments = selectedLocation.value.assignments.filter(
        a => a.id !== assignmentId
      );
    }
  };

  /**
   * Preserve form data before moving location
   */
  const preserveFormData = (formData) => {
    preservedLocationFormData.value = formData;
  };

  /**
   * Restore preserved form data after moving
   */
  const restorePreservedFormData = () => {
    if (preservedLocationFormData.value && selectedLocation.value) {
      Object.assign(selectedLocation.value, preservedLocationFormData.value);
    }
    preservedLocationFormData.value = null;
  };

  /**
   * Update location coordinates (after move)
   */
  const updateLocationCoords = (coords) => {
    if (selectedLocation.value) {
      selectedLocation.value = {
        ...selectedLocation.value,
        ...(preservedLocationFormData.value || {}),
        latitude: coords.lat,
        longitude: coords.lng,
      };
    }
    editLocationForm.value.latitude = coords.lat;
    editLocationForm.value.longitude = coords.lng;
    preservedLocationFormData.value = null;
  };

  /**
   * Open assign marshal modal
   */
  const openAssignMarshalModal = () => {
    showAssignMarshalModal.value = true;
  };

  /**
   * Close assign marshal modal
   */
  const closeAssignMarshalModal = () => {
    showAssignMarshalModal.value = false;
  };

  /**
   * Check if there are unsaved changes
   */
  const hasUnsavedChanges = computed(() => {
    return pendingAssignments.value.length > 0 || pendingDeleteAssignments.value.length > 0;
  });

  /**
   * Clear pending changes
   */
  const clearPendingChanges = () => {
    pendingAssignments.value = [];
    pendingDeleteAssignments.value = [];
  };

  return {
    // Modal state
    showAddLocation,
    showEditLocation,
    showAssignMarshalModal,
    isMovingLocation,

    // Location state
    selectedLocation,
    preservedLocationFormData,
    locationForm,
    editLocationForm,

    // Pending changes
    pendingAssignments,
    pendingDeleteAssignments,

    // Computed
    hasUnsavedChanges,

    // Methods
    resetLocationForm,
    resetEditForm,
    openAddLocationModal,
    closeAddLocationModal,
    selectLocation,
    openNewLocationModal,
    closeEditLocationModal,
    saveNewLocation,
    saveLocation,
    deleteLocation,
    addPendingAssignment,
    removeAssignment,
    preserveFormData,
    restorePreservedFormData,
    updateLocationCoords,
    openAssignMarshalModal,
    closeAssignMarshalModal,
    clearPendingChanges,
  };
}
