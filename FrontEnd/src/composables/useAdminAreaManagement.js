/**
 * Composable for admin area management
 * Handles modal state, form data, and area operations in admin context
 */

import { ref } from 'vue';
import { areasApi, checklistApi } from '../services/api';
import { roundPolygonPoints } from '../utils/coordinateUtils';

export function useAdminAreaManagement(eventId) {
  // Data
  const areas = ref([]);

  // Modal state
  const showEditArea = ref(false);

  // Selected area
  const selectedArea = ref(null);
  const selectedAreaId = ref(null);

  // Pending state for drawing
  const pendingAreaFormData = ref(null);

  /**
   * Load areas for event
   */
  const loadAreas = async () => {
    try {
      const response = await areasApi.getByEvent(eventId.value);
      areas.value = response.data;
      return response.data;
    } catch (error) {
      console.error('Failed to load areas:', error);
      throw error;
    }
  };

  /**
   * Open modal to add new area
   */
  const handleAddArea = () => {
    selectedArea.value = null;
    showEditArea.value = true;
  };

  /**
   * Select an area for editing
   */
  const handleSelectArea = (area) => {
    selectedArea.value = area;
    selectedAreaId.value = area.id;
    showEditArea.value = true;
  };

  /**
   * Close edit area modal
   */
  const closeEditAreaModal = () => {
    showEditArea.value = false;
    selectedArea.value = null;
    selectedAreaId.value = null;
    pendingAreaFormData.value = null;
  };

  /**
   * Create a new area for drawing (from map)
   */
  const createNewAreaForDrawing = () => {
    selectedArea.value = {
      id: null,
      name: '',
      description: '',
      color: '#667eea',
      polygon: [],
      checkpointIds: [],
      marshalIds: [],
      displayOrder: areas.value.length,
    };
    return selectedArea.value;
  };

  /**
   * Preserve form data before entering draw mode
   */
  const preserveFormData = (formData) => {
    pendingAreaFormData.value = formData;
  };

  /**
   * Update selected area with polygon coordinates
   */
  const updateAreaPolygon = (coordinates) => {
    if (pendingAreaFormData.value) {
      selectedArea.value = {
        ...(selectedArea.value || {}),
        ...pendingAreaFormData.value,
        polygon: coordinates,
      };
    } else if (selectedArea.value) {
      selectedArea.value = {
        ...selectedArea.value,
        polygon: coordinates,
      };
    }
  };

  /**
   * Restore form data after drawing
   */
  const restoreFormData = () => {
    if (pendingAreaFormData.value) {
      selectedArea.value = pendingAreaFormData.value;
      pendingAreaFormData.value = null;
    }
  };

  /**
   * Save area (create or update)
   */
  const saveArea = async (formData) => {
    // Round polygon coordinates to 6 decimal places before saving
    const dataToSave = { ...formData };
    if (dataToSave.polygon) {
      dataToSave.polygon = roundPolygonPoints(dataToSave.polygon);
    }

    if (selectedArea.value && selectedArea.value.id) {
      // Update existing area
      await areasApi.update(eventId.value, selectedArea.value.id, dataToSave);

      // Process checklist changes
      if (formData.checklistChanges && formData.checklistChanges.length > 0) {
        for (const change of formData.checklistChanges) {
          try {
            if (change.complete) {
              await checklistApi.complete(eventId.value, change.itemId, {
                marshalId: change.marshalId,
                contextType: change.contextType,
                contextId: change.contextId,
              });
            } else {
              await checklistApi.uncomplete(eventId.value, change.itemId, {
                marshalId: change.marshalId,
                contextType: change.contextType,
                contextId: change.contextId,
              });
            }
          } catch (error) {
            console.error('Failed to update checklist item:', change, error);
          }
        }
      }
    } else {
      // Create new area
      await areasApi.create({
        eventId: eventId.value,
        ...dataToSave,
      });
    }
  };

  /**
   * Delete an area
   */
  const deleteArea = async (areaId) => {
    await areasApi.delete(eventId.value, areaId);
    // Trigger recalculation to reassign checkpoints
    await areasApi.recalculate(eventId.value);
  };

  /**
   * Clear pending form data
   */
  const clearPendingFormData = () => {
    pendingAreaFormData.value = null;
  };

  return {
    // Data
    areas,

    // Modal state
    showEditArea,

    // Area state
    selectedArea,
    selectedAreaId,
    pendingAreaFormData,

    // Methods
    loadAreas,
    handleAddArea,
    handleSelectArea,
    closeEditAreaModal,
    createNewAreaForDrawing,
    preserveFormData,
    updateAreaPolygon,
    restoreFormData,
    saveArea,
    deleteArea,
    clearPendingFormData,
  };
}
