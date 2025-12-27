/**
 * Composable for location/checkpoint management
 * Centralized location-related operations
 */

import { ref, computed } from 'vue';
import api from '@/services/api';

export function useLocationManagement(eventId) {
  const locations = ref([]);
  const loading = ref(false);
  const error = ref(null);

  /**
   * Fetch all locations for an event
   */
  const fetchLocations = async () => {
    if (!eventId) return;

    loading.value = true;
    error.value = null;
    try {
      const response = await api.getLocations(eventId);
      locations.value = response.data;
      return response.data;
    } catch (err) {
      error.value = err.message || 'Failed to fetch locations';
      throw err;
    } finally {
      loading.value = false;
    }
  };

  /**
   * Create a new location
   * @param {Object} locationData - Location data
   */
  const createLocation = async (locationData) => {
    if (!eventId) throw new Error('Event ID is required');

    loading.value = true;
    error.value = null;
    try {
      const response = await api.createLocation(eventId, locationData);
      locations.value.push(response.data);
      return response.data;
    } catch (err) {
      error.value = err.message || 'Failed to create location';
      throw err;
    } finally {
      loading.value = false;
    }
  };

  /**
   * Update an existing location
   * @param {number} locationId - The location ID
   * @param {Object} locationData - Updated location data
   */
  const updateLocation = async (locationId, locationData) => {
    loading.value = true;
    error.value = null;
    try {
      const response = await api.updateLocation(locationId, locationData);

      // Update in locations list
      const index = locations.value.findIndex((l) => l.id === locationId);
      if (index !== -1) {
        locations.value[index] = response.data;
      }

      return response.data;
    } catch (err) {
      error.value = err.message || 'Failed to update location';
      throw err;
    } finally {
      loading.value = false;
    }
  };

  /**
   * Delete a location
   * @param {number} locationId - The location ID
   */
  const deleteLocation = async (locationId) => {
    loading.value = true;
    error.value = null;
    try {
      await api.deleteLocation(locationId);

      // Remove from locations list
      locations.value = locations.value.filter((l) => l.id !== locationId);
    } catch (err) {
      error.value = err.message || 'Failed to delete location';
      throw err;
    } finally {
      loading.value = false;
    }
  };

  /**
   * Import locations from CSV
   * @param {File} file - CSV file
   */
  const importLocations = async (file) => {
    if (!eventId) throw new Error('Event ID is required');

    loading.value = true;
    error.value = null;
    try {
      const formData = new FormData();
      formData.append('file', file);
      const response = await api.importLocations(eventId, formData);

      // Add imported locations to the list
      if (response.data && Array.isArray(response.data)) {
        locations.value = [...locations.value, ...response.data];
      }

      return response.data;
    } catch (err) {
      error.value = err.message || 'Failed to import locations';
      throw err;
    } finally {
      loading.value = false;
    }
  };

  /**
   * Get sorted locations
   */
  const sortedLocations = computed(() => {
    return [...locations.value].sort((a, b) => {
      // Try numeric comparison first
      const aNum = parseFloat(a.name);
      const bNum = parseFloat(b.name);
      const aIsNum = !isNaN(aNum) && String(aNum) === a.name.trim();
      const bIsNum = !isNaN(bNum) && String(bNum) === b.name.trim();

      if (aIsNum && bIsNum) {
        return aNum - bNum;
      }

      // Fall back to alphabetic comparison
      return a.name.localeCompare(b.name, undefined, {
        numeric: true,
        sensitivity: 'base',
      });
    });
  });

  /**
   * Find a location by ID
   * @param {number} locationId - The location ID
   */
  const findLocation = (locationId) => {
    return locations.value.find((l) => l.id === locationId);
  };

  /**
   * Get location by name
   * @param {string} name - Location name
   */
  const findLocationByName = (name) => {
    return locations.value.find(
      (l) => l.name.toLowerCase() === name.toLowerCase()
    );
  };

  return {
    // State
    locations,
    loading,
    error,

    // Computed
    sortedLocations,

    // Methods
    fetchLocations,
    createLocation,
    updateLocation,
    deleteLocation,
    importLocations,
    findLocation,
    findLocationByName,
  };
}
