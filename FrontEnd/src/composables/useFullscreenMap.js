/**
 * Composable for fullscreen map functionality
 * Handles checkpoint placement, movement, area drawing, and multiple checkpoint addition
 */

import { ref, computed } from 'vue';

export function useFullscreenMap() {
  // State
  const isFullscreenMapActive = ref(false);
  const fullscreenMode = ref(null); // 'place-checkpoint', 'move-checkpoint', 'draw-area', 'add-multiple'
  const fullscreenContext = ref({});
  const tempLocationCoords = ref(null);
  const currentDrawingPolygon = ref(null);
  const tempCheckpoints = ref([]);
  const savedMapCenter = ref(null);
  const savedMapZoom = ref(null);
  const isDrawingAreaBoundary = ref(false);
  const isAddingMultipleCheckpoints = ref(false);
  const multiCheckpointCounter = ref(1);

  /**
   * Check if fullscreen action can be completed
   */
  const canCompleteFullscreen = computed(() => {
    if (fullscreenMode.value === 'place-checkpoint') {
      return tempLocationCoords.value !== null;
    } else if (fullscreenMode.value === 'move-checkpoint') {
      return tempLocationCoords.value !== null;
    } else if (fullscreenMode.value === 'draw-area') {
      return (currentDrawingPolygon.value && currentDrawingPolygon.value.length >= 3);
    } else if (fullscreenMode.value === 'add-multiple') {
      return true;
    }
    return false;
  });

  /**
   * Save map state before entering fullscreen
   */
  const saveMapState = (mapRef) => {
    if (mapRef) {
      const center = mapRef.getMapCenter?.();
      const zoom = mapRef.getMapZoom?.();
      if (center && zoom !== null) {
        savedMapCenter.value = center;
        savedMapZoom.value = zoom;
      }
    }
  };

  /**
   * Enter place-checkpoint mode
   */
  const enterPlaceCheckpointMode = (context = {}) => {
    tempLocationCoords.value = null;
    isFullscreenMapActive.value = true;
    fullscreenMode.value = 'place-checkpoint';
    fullscreenContext.value = {
      title: context.title || 'Select checkpoint location',
      description: context.description || 'Click on the map to set the location. Click again to reposition.',
    };
  };

  /**
   * Enter move-checkpoint mode
   */
  const enterMoveCheckpointMode = (locationName, checkpointTerm = 'checkpoint') => {
    tempLocationCoords.value = null;
    isFullscreenMapActive.value = true;
    fullscreenMode.value = 'move-checkpoint';
    fullscreenContext.value = {
      title: `Move ${checkpointTerm}: ${locationName}`,
      description: 'Click on the map to set the new location',
    };
  };

  /**
   * Enter draw-area mode
   */
  const enterDrawAreaMode = (context = {}) => {
    isFullscreenMapActive.value = true;
    fullscreenMode.value = 'draw-area';
    isDrawingAreaBoundary.value = true;
    fullscreenContext.value = {
      title: context.title || 'Draw area boundary',
      description: context.description || 'Click to add points. Click Done when finished to complete the area.',
    };
  };

  /**
   * Enter add-multiple mode for adding multiple checkpoints
   */
  const enterAddMultipleMode = (startingNumber = 1) => {
    multiCheckpointCounter.value = startingNumber;
    tempCheckpoints.value = [];
    isFullscreenMapActive.value = true;
    fullscreenMode.value = 'add-multiple';
    isAddingMultipleCheckpoints.value = true;
    fullscreenContext.value = {
      title: 'Add multiple checkpoints',
      description: `Click on map to add checkpoints. Next: Checkpoint ${multiCheckpointCounter.value}`,
    };
  };

  /**
   * Handle map click in fullscreen mode
   */
  const handleFullscreenMapClick = (coords) => {
    if (fullscreenMode.value === 'place-checkpoint') {
      tempLocationCoords.value = coords;
    } else if (fullscreenMode.value === 'move-checkpoint') {
      tempLocationCoords.value = coords;
    } else if (fullscreenMode.value === 'add-multiple') {
      addTempCheckpoint(coords);
    }
  };

  /**
   * Add a temporary checkpoint (for multiple mode)
   */
  const addTempCheckpoint = (coords) => {
    tempCheckpoints.value.push({
      id: `temp-${multiCheckpointCounter.value}`,
      name: `Checkpoint ${multiCheckpointCounter.value}`,
      description: '',
      latitude: coords.lat,
      longitude: coords.lng,
      requiredMarshals: 1,
      what3Words: '',
      assignments: [],
      isTemporary: true,
    });

    multiCheckpointCounter.value++;
    fullscreenContext.value = {
      ...fullscreenContext.value,
      description: `Click on map to add checkpoints. Next: Checkpoint ${multiCheckpointCounter.value}`,
    };
  };

  /**
   * Handle polygon drawing update
   */
  const handlePolygonDrawing = (points) => {
    currentDrawingPolygon.value = points;
  };

  /**
   * Get the completed polygon coordinates
   */
  const getCompletedPolygon = () => {
    if (currentDrawingPolygon.value && currentDrawingPolygon.value.length >= 3) {
      return currentDrawingPolygon.value.map(p => ({
        lat: p.lat,
        lng: p.lng,
      }));
    }
    return null;
  };

  /**
   * Exit fullscreen mode
   */
  const exitFullscreen = () => {
    isFullscreenMapActive.value = false;
    fullscreenMode.value = null;
    fullscreenContext.value = {};
    tempLocationCoords.value = null;
    savedMapCenter.value = null;
    savedMapZoom.value = null;
    currentDrawingPolygon.value = null;
    if (isDrawingAreaBoundary.value) {
      isDrawingAreaBoundary.value = false;
    }
  };

  /**
   * Cancel fullscreen and clear state
   */
  const cancelFullscreen = () => {
    if (fullscreenMode.value === 'add-multiple') {
      tempCheckpoints.value = [];
      isAddingMultipleCheckpoints.value = false;
    } else if (fullscreenMode.value === 'draw-area') {
      isDrawingAreaBoundary.value = false;
      currentDrawingPolygon.value = null;
    }
    exitFullscreen();
  };

  /**
   * Get temp checkpoints for batch saving
   */
  const getTempCheckpoints = () => {
    return [...tempCheckpoints.value];
  };

  /**
   * Clear temp checkpoints after saving
   */
  const clearTempCheckpoints = () => {
    tempCheckpoints.value = [];
    isAddingMultipleCheckpoints.value = false;
  };

  /**
   * Get the placed coordinates for checkpoint placement/movement
   */
  const getPlacedCoords = () => {
    return tempLocationCoords.value;
  };

  /**
   * Create displayCheckpoints computed that includes temp checkpoints
   */
  const createDisplayCheckpoints = (locationStatuses, locationForm, selectedLocation) => {
    return computed(() => {
      const checkpoints = [...locationStatuses.value];

      // Add temporary preview for single checkpoint placement
      if (fullscreenMode.value === 'place-checkpoint' && tempLocationCoords.value) {
        checkpoints.push({
          id: 'temp-single',
          name: locationForm.value?.name || 'New Checkpoint',
          latitude: tempLocationCoords.value.lat,
          longitude: tempLocationCoords.value.lng,
          requiredMarshals: locationForm.value?.requiredMarshals || 1,
          assignments: [],
          isTemporary: true,
        });
      }

      // Add temporary preview for move checkpoint
      if (fullscreenMode.value === 'move-checkpoint' && tempLocationCoords.value && selectedLocation.value) {
        checkpoints.push({
          id: 'temp-move',
          name: selectedLocation.value.name + ' (new position)',
          latitude: tempLocationCoords.value.lat,
          longitude: tempLocationCoords.value.lng,
          requiredMarshals: selectedLocation.value.requiredMarshals,
          assignments: [],
          isTemporary: true,
        });
      }

      // Add temporary checkpoints for multiple placement mode
      if (fullscreenMode.value === 'add-multiple') {
        checkpoints.push(...tempCheckpoints.value);
      }

      return checkpoints;
    });
  };

  /**
   * Create displayAreas computed that includes area being drawn
   */
  const createDisplayAreas = (areas, selectedArea) => {
    return computed(() => {
      const areasList = [...areas.value];

      if (fullscreenMode.value === 'draw-area' && selectedArea.value?.polygon?.length > 0) {
        const existingIndex = areasList.findIndex(a => a.id === selectedArea.value.id);
        if (existingIndex >= 0) {
          areasList[existingIndex] = { ...selectedArea.value };
        } else {
          areasList.push({
            ...selectedArea.value,
            id: selectedArea.value.id || 'temp-area',
          });
        }
      }

      return areasList;
    });
  };

  /**
   * Calculate starting number for multiple checkpoints based on existing checkpoints
   */
  const calculateStartingNumber = (locationStatuses) => {
    const checkpointNumbers = locationStatuses.value
      .map(loc => {
        const match = loc.name.match(/^Checkpoint (\d+)$/);
        return match ? parseInt(match[1]) : 0;
      })
      .filter(n => n > 0);

    return checkpointNumbers.length > 0
      ? Math.max(...checkpointNumbers) + 1
      : 1;
  };

  return {
    // State
    isFullscreenMapActive,
    fullscreenMode,
    fullscreenContext,
    tempLocationCoords,
    currentDrawingPolygon,
    tempCheckpoints,
    savedMapCenter,
    savedMapZoom,
    isDrawingAreaBoundary,
    isAddingMultipleCheckpoints,
    multiCheckpointCounter,

    // Computed
    canCompleteFullscreen,

    // Methods
    saveMapState,
    enterPlaceCheckpointMode,
    enterMoveCheckpointMode,
    enterDrawAreaMode,
    enterAddMultipleMode,
    handleFullscreenMapClick,
    handlePolygonDrawing,
    getCompletedPolygon,
    exitFullscreen,
    cancelFullscreen,
    getTempCheckpoints,
    clearTempCheckpoints,
    getPlacedCoords,
    createDisplayCheckpoints,
    createDisplayAreas,
    calculateStartingNumber,
  };
}
