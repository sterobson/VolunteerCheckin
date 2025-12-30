/**
 * Composable for area management
 * Centralized area-related operations
 */

import { ref, computed } from 'vue';
import { areasApi } from '../services/api';
import { alphanumericCompare } from '../utils/sortUtils';

export function useAreaManagement(eventId) {
  const areas = ref([]);
  const loading = ref(false);
  const error = ref(null);

  /**
   * Fetch all areas for an event
   */
  const fetchAreas = async () => {
    if (!eventId) return;

    loading.value = true;
    error.value = null;
    try {
      const response = await areasApi.getByEvent(eventId);
      areas.value = response.data;
      return response.data;
    } catch (err) {
      error.value = err.message || 'Failed to fetch areas';
      throw err;
    } finally {
      loading.value = false;
    }
  };

  /**
   * Create a new area
   * @param {Object} areaData - Area data
   */
  const createArea = async (areaData) => {
    if (!eventId) throw new Error('Event ID is required');

    loading.value = true;
    error.value = null;
    try {
      const response = await areasApi.create(areaData);
      areas.value.push(response.data);
      return response.data;
    } catch (err) {
      error.value = err.message || 'Failed to create area';
      throw err;
    } finally {
      loading.value = false;
    }
  };

  /**
   * Update an existing area
   * @param {string} areaId - The area ID
   * @param {Object} areaData - Updated area data
   */
  const updateArea = async (areaId, areaData) => {
    loading.value = true;
    error.value = null;
    try {
      const response = await areasApi.update(eventId, areaId, areaData);

      // Update in areas list
      const index = areas.value.findIndex((a) => a.id === areaId);
      if (index !== -1) {
        areas.value[index] = response.data;
      }

      return response.data;
    } catch (err) {
      error.value = err.message || 'Failed to update area';
      throw err;
    } finally {
      loading.value = false;
    }
  };

  /**
   * Delete an area
   * @param {string} areaId - The area ID
   */
  const deleteArea = async (areaId) => {
    loading.value = true;
    error.value = null;
    try {
      await areasApi.delete(eventId, areaId);

      // Remove from areas list
      areas.value = areas.value.filter((a) => a.id !== areaId);
    } catch (err) {
      error.value = err.message || 'Failed to delete area';
      throw err;
    } finally {
      loading.value = false;
    }
  };

  /**
   * Bulk assign checkpoints to an area
   * @param {string} areaId - The area ID
   * @param {Array} checkpointIds - Array of checkpoint IDs
   */
  const bulkAssignCheckpoints = async (areaId, checkpointIds) => {
    if (!eventId) throw new Error('Event ID is required');

    loading.value = true;
    error.value = null;
    try {
      const response = await areasApi.bulkAssign(eventId, areaId, checkpointIds);
      return response.data;
    } catch (err) {
      error.value = err.message || 'Failed to assign checkpoints';
      throw err;
    } finally {
      loading.value = false;
    }
  };

  /**
   * Get checkpoints in an area
   * @param {string} areaId - The area ID
   */
  const getAreaCheckpoints = async (areaId) => {
    loading.value = true;
    error.value = null;
    try {
      const response = await areasApi.getCheckpoints(eventId, areaId);
      return response.data;
    } catch (err) {
      error.value = err.message || 'Failed to fetch area checkpoints';
      throw err;
    } finally {
      loading.value = false;
    }
  };

  /**
   * Get sorted areas (by display order, then name)
   */
  const sortedAreas = computed(() => {
    return [...areas.value].sort((a, b) => {
      // Sort by display order first
      if (a.displayOrder !== b.displayOrder) {
        return a.displayOrder - b.displayOrder;
      }

      // Then by name (alphanumeric)
      return alphanumericCompare(a.name, b.name);
    });
  });

  /**
   * Find an area by ID
   * @param {string} areaId - The area ID
   */
  const findArea = (areaId) => {
    return areas.value.find((a) => a.id === areaId);
  };

  /**
   * Get area by name
   * @param {string} name - Area name
   */
  const findAreaByName = (name) => {
    return areas.value.find(
      (a) => a.name.toLowerCase() === name.toLowerCase()
    );
  };

  return {
    // State
    areas,
    loading,
    error,

    // Computed
    sortedAreas,

    // Methods
    fetchAreas,
    createArea,
    updateArea,
    deleteArea,
    bulkAssignCheckpoints,
    getAreaCheckpoints,
    findArea,
    findAreaByName,
  };
}
