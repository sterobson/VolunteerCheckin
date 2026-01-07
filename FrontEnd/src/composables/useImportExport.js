/**
 * Composable for import/export operations
 * Handles CSV imports for locations and marshals, and GPX route uploads
 */

import { ref } from 'vue';
import { eventsApi, locationsApi, marshalsApi } from '../services/api';

export function useImportExport(eventId) {
  // Modal state
  const showUploadRoute = ref(false);
  const showImportLocations = ref(false);
  const showImportMarshals = ref(false);

  // Loading state
  const uploading = ref(false);
  const importing = ref(false);
  const importingMarshals = ref(false);

  // Error state
  const uploadError = ref(null);
  const importError = ref(null);

  // Result state
  const importResult = ref(null);
  const importMarshalsResult = ref(null);

  /**
   * Open upload route modal
   */
  const openUploadRouteModal = () => {
    uploadError.value = null;
    showUploadRoute.value = true;
  };

  /**
   * Close upload route modal
   */
  const closeUploadRouteModal = () => {
    showUploadRoute.value = false;
    uploadError.value = null;
  };

  /**
   * Upload GPX route file
   */
  const uploadRoute = async (file) => {
    if (!file) {
      uploadError.value = 'Please select a GPX file';
      return null;
    }

    uploading.value = true;
    uploadError.value = null;

    try {
      await eventsApi.uploadGpx(eventId.value, file);
      return true;
    } catch (error) {
      console.error('Failed to upload route:', error);
      uploadError.value = error.response?.data?.message || 'Failed to upload route. Please try again.';
      return null;
    } finally {
      uploading.value = false;
    }
  };

  /**
   * Open import locations modal
   */
  const openImportLocationsModal = () => {
    importError.value = null;
    importResult.value = null;
    showImportLocations.value = true;
  };

  /**
   * Close import locations modal
   */
  const closeImportLocationsModal = () => {
    showImportLocations.value = false;
    importError.value = null;
    importResult.value = null;
  };

  /**
   * Import locations from CSV
   */
  const importLocations = async (file, deleteExisting) => {
    if (!file) {
      importError.value = 'Please select a CSV file';
      return null;
    }

    importing.value = true;
    importError.value = null;
    importResult.value = null;

    try {
      const response = await locationsApi.importCsv(eventId.value, file, deleteExisting);
      importResult.value = response.data;
      return response.data;
    } catch (error) {
      console.error('Failed to import locations:', error);
      importError.value = error.response?.data?.message || 'Failed to import locations. Please try again.';
      return null;
    } finally {
      importing.value = false;
    }
  };

  /**
   * Open import marshals modal
   */
  const openImportMarshalsModal = () => {
    importError.value = null;
    importMarshalsResult.value = null;
    showImportMarshals.value = true;
  };

  /**
   * Close import marshals modal
   */
  const closeImportMarshalsModal = () => {
    showImportMarshals.value = false;
    importError.value = null;
    importMarshalsResult.value = null;
  };

  /**
   * Import marshals from CSV
   */
  const importMarshals = async (file) => {
    if (!file) {
      importError.value = 'Please select a CSV file';
      return null;
    }

    importingMarshals.value = true;
    importError.value = null;
    importMarshalsResult.value = null;

    try {
      const response = await marshalsApi.importCsv(eventId.value, file);
      importMarshalsResult.value = response.data;
      return response.data;
    } catch (error) {
      console.error('Failed to import marshals:', error);
      importError.value = error.response?.data?.message || 'Failed to import marshals. Please try again.';
      return null;
    } finally {
      importingMarshals.value = false;
    }
  };

  /**
   * Format import result message for display
   */
  const formatImportResultMessage = (result, entityType = 'location') => {
    const entityPlural = entityType === 'location' ? 'location(s)' : 'marshal(s)';
    const createdField = entityType === 'location' ? 'locationsCreated' : 'marshalsCreated';

    let message = `<p>Created <strong>${result[createdField]}</strong> ${entityPlural} and <strong>${result.assignmentsCreated}</strong> assignment(s)</p>`;

    if (result.errors && result.errors.length > 0) {
      message += '<br><p><strong>Errors:</strong></p><ul style="margin: 0; padding-left: 1.5rem;">';
      result.errors.forEach(err => {
        message += `<li>${err}</li>`;
      });
      message += '</ul>';
    }

    return message;
  };

  return {
    // Modal state
    showUploadRoute,
    showImportLocations,
    showImportMarshals,

    // Loading state
    uploading,
    importing,
    importingMarshals,

    // Error state
    uploadError,
    importError,

    // Result state
    importResult,
    importMarshalsResult,

    // Methods
    openUploadRouteModal,
    closeUploadRouteModal,
    uploadRoute,
    openImportLocationsModal,
    closeImportLocationsModal,
    importLocations,
    openImportMarshalsModal,
    closeImportMarshalsModal,
    importMarshals,
    formatImportResultMessage,
  };
}
