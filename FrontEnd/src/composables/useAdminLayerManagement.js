/**
 * Composable for admin layer management
 * Handles modal state, form data, and layer operations in admin context
 */

import { ref } from 'vue';
import { layersApi } from '../services/api';
import { roundRoutePoints } from '../utils/coordinateUtils';

export function useAdminLayerManagement(eventId) {
  // Data
  const layers = ref([]);
  const loading = ref(false);
  const error = ref(null);

  // Modal state
  const showEditLayer = ref(false);

  // Selected layer
  const selectedLayer = ref(null);

  /**
   * Load layers for event
   */
  const loadLayers = async () => {
    loading.value = true;
    error.value = null;
    try {
      const response = await layersApi.getByEvent(eventId.value);
      layers.value = response.data;
      return response.data;
    } catch (err) {
      console.error('Failed to load layers:', err);
      error.value = err.response?.data?.message || 'Failed to load layers';
      throw err;
    } finally {
      loading.value = false;
    }
  };

  /**
   * Open modal to add new layer
   */
  const handleAddLayer = () => {
    selectedLayer.value = null;
    showEditLayer.value = true;
  };

  /**
   * Select a layer for editing
   */
  const handleSelectLayer = (layer) => {
    selectedLayer.value = layer;
    showEditLayer.value = true;
  };

  /**
   * Close edit layer modal
   */
  const closeEditLayerModal = () => {
    showEditLayer.value = false;
    selectedLayer.value = null;
  };

  /**
   * Save layer (create or update)
   */
  const saveLayer = async (formData) => {
    // Round route coordinates to 6 decimal places before saving
    const dataToSave = { ...formData };
    if (dataToSave.route) {
      dataToSave.route = roundRoutePoints(dataToSave.route);
    }

    if (selectedLayer.value && selectedLayer.value.id) {
      // Update existing layer
      await layersApi.update(eventId.value, selectedLayer.value.id, dataToSave);
    } else {
      // Create new layer
      await layersApi.create(eventId.value, dataToSave);
    }
  };

  /**
   * Delete a layer
   */
  const deleteLayer = async (layerId) => {
    await layersApi.delete(eventId.value, layerId);
  };

  /**
   * Reorder layers
   */
  const reorderLayers = async (orderedLayers) => {
    const items = orderedLayers.map((layer, index) => ({
      id: layer.id,
      displayOrder: index,
    }));
    const response = await layersApi.reorder(eventId.value, items);
    layers.value = response.data;
    return response.data;
  };

  /**
   * Upload GPX to a layer
   */
  const uploadGpxToLayer = async (layerId, file) => {
    const response = await layersApi.uploadGpx(eventId.value, layerId, file);
    // Update the layer in the list with the new route data
    const updatedLayer = response.data.layer;
    if (updatedLayer) {
      const index = layers.value.findIndex((l) => l.id === layerId);
      if (index !== -1) {
        layers.value[index] = updatedLayer;
      }
    }
    return response.data;
  };

  return {
    // Data
    layers,
    loading,
    error,

    // Modal state
    showEditLayer,

    // Layer state
    selectedLayer,

    // Methods
    loadLayers,
    handleAddLayer,
    handleSelectLayer,
    closeEditLayerModal,
    saveLayer,
    deleteLayer,
    reorderLayers,
    uploadGpxToLayer,
  };
}
