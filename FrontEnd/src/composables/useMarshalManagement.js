/**
 * Composable for marshal management
 * Centralized marshal-related operations
 */

import { ref, computed } from 'vue';
import api from '@/services/api';

export function useMarshalManagement(eventId) {
  const marshals = ref([]);
  const assignments = ref([]);
  const loading = ref(false);
  const error = ref(null);

  /**
   * Fetch all marshals for an event
   */
  const fetchMarshals = async () => {
    if (!eventId) return;

    loading.value = true;
    error.value = null;
    try {
      const response = await api.getMarshals(eventId);
      marshals.value = response.data;
      return response.data;
    } catch (err) {
      error.value = err.message || 'Failed to fetch marshals';
      throw err;
    } finally {
      loading.value = false;
    }
  };

  /**
   * Fetch all assignments for an event
   */
  const fetchAssignments = async () => {
    if (!eventId) return;

    loading.value = true;
    error.value = null;
    try {
      const response = await api.getAssignments(eventId);
      assignments.value = response.data;
      return response.data;
    } catch (err) {
      error.value = err.message || 'Failed to fetch assignments';
      throw err;
    } finally {
      loading.value = false;
    }
  };

  /**
   * Create a new marshal
   * @param {Object} marshalData - Marshal data
   */
  const createMarshal = async (marshalData) => {
    if (!eventId) throw new Error('Event ID is required');

    loading.value = true;
    error.value = null;
    try {
      const response = await api.createMarshal(eventId, marshalData);
      marshals.value.push(response.data);
      return response.data;
    } catch (err) {
      error.value = err.message || 'Failed to create marshal';
      throw err;
    } finally {
      loading.value = false;
    }
  };

  /**
   * Update an existing marshal
   * @param {number} marshalId - The marshal ID
   * @param {Object} marshalData - Updated marshal data
   */
  const updateMarshal = async (marshalId, marshalData) => {
    loading.value = true;
    error.value = null;
    try {
      const response = await api.updateMarshal(marshalId, marshalData);

      // Update in marshals list
      const index = marshals.value.findIndex((m) => m.id === marshalId);
      if (index !== -1) {
        marshals.value[index] = response.data;
      }

      return response.data;
    } catch (err) {
      error.value = err.message || 'Failed to update marshal';
      throw err;
    } finally {
      loading.value = false;
    }
  };

  /**
   * Delete a marshal
   * @param {number} marshalId - The marshal ID
   */
  const deleteMarshal = async (marshalId) => {
    loading.value = true;
    error.value = null;
    try {
      await api.deleteMarshal(marshalId);

      // Remove from marshals list
      marshals.value = marshals.value.filter((m) => m.id !== marshalId);

      // Remove associated assignments
      assignments.value = assignments.value.filter(
        (a) => a.marshalId !== marshalId
      );
    } catch (err) {
      error.value = err.message || 'Failed to delete marshal';
      throw err;
    } finally {
      loading.value = false;
    }
  };

  /**
   * Assign a marshal to a location
   * @param {number} marshalId - The marshal ID
   * @param {number} locationId - The location ID
   */
  const assignMarshal = async (marshalId, locationId) => {
    loading.value = true;
    error.value = null;
    try {
      const response = await api.assignMarshal(marshalId, locationId);
      assignments.value.push(response.data);
      return response.data;
    } catch (err) {
      error.value = err.message || 'Failed to assign marshal';
      throw err;
    } finally {
      loading.value = false;
    }
  };

  /**
   * Unassign a marshal from a location
   * @param {number} assignmentId - The assignment ID
   */
  const unassignMarshal = async (assignmentId) => {
    loading.value = true;
    error.value = null;
    try {
      await api.deleteAssignment(assignmentId);

      // Remove from assignments list
      assignments.value = assignments.value.filter((a) => a.id !== assignmentId);
    } catch (err) {
      error.value = err.message || 'Failed to unassign marshal';
      throw err;
    } finally {
      loading.value = false;
    }
  };

  /**
   * Import marshals from CSV
   * @param {File} file - CSV file
   */
  const importMarshals = async (file) => {
    if (!eventId) throw new Error('Event ID is required');

    loading.value = true;
    error.value = null;
    try {
      const formData = new FormData();
      formData.append('file', file);
      const response = await api.importMarshals(eventId, formData);

      // Add imported marshals to the list
      if (response.data && Array.isArray(response.data)) {
        marshals.value = [...marshals.value, ...response.data];
      }

      return response.data;
    } catch (err) {
      error.value = err.message || 'Failed to import marshals';
      throw err;
    } finally {
      loading.value = false;
    }
  };

  /**
   * Get assignments for a specific marshal
   * @param {number} marshalId - The marshal ID
   */
  const getMarshalAssignments = (marshalId) => {
    return assignments.value.filter((a) => a.marshalId === marshalId);
  };

  /**
   * Get assignments for a specific location
   * @param {number} locationId - The location ID
   */
  const getLocationAssignments = (locationId) => {
    return assignments.value.filter((a) => a.locationId === locationId);
  };

  /**
   * Find a marshal by ID
   * @param {number} marshalId - The marshal ID
   */
  const findMarshal = (marshalId) => {
    return marshals.value.find((m) => m.id === marshalId);
  };

  /**
   * Get sorted marshals
   */
  const sortedMarshals = computed(() => {
    return [...marshals.value].sort((a, b) => {
      return a.name.localeCompare(b.name);
    });
  });

  return {
    // State
    marshals,
    assignments,
    loading,
    error,

    // Computed
    sortedMarshals,

    // Methods
    fetchMarshals,
    fetchAssignments,
    createMarshal,
    updateMarshal,
    deleteMarshal,
    assignMarshal,
    unassignMarshal,
    importMarshals,
    getMarshalAssignments,
    getLocationAssignments,
    findMarshal,
  };
}
