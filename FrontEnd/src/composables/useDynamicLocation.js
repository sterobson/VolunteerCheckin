import { ref } from 'vue';
import { locationsApi } from '../services/api';

/**
 * Composable for managing dynamic checkpoint location functionality
 */
export function useDynamicLocation({
  eventId,
  allLocations,
  userLocation,
  locationLastUpdated,
}) {
  // State
  const showLocationUpdateModal = ref(false);
  const updatingLocationFor = ref(null);
  const locationUpdateError = ref(null);
  const locationUpdateSuccess = ref(null);
  const updatingLocation = ref(false);
  const autoUpdateEnabled = ref(false);
  const selectingLocationOnMap = ref(false);

  let autoUpdateInterval = null;
  let dynamicCheckpointPollInterval = null;
  let allCheckpointsPollInterval = null;

  // Check if a location is a dynamic checkpoint
  const isDynamicCheckpoint = (location) => {
    return location?.isDynamic === true || location?.IsDynamic === true;
  };

  // Update local checkpoint position in allLocations
  const updateLocalCheckpointPosition = (locationId, lat, lng, lastUpdate) => {
    const location = allLocations.value.find(l => l.id === locationId);
    if (location) {
      location.latitude = lat;
      location.longitude = lng;
      location.lastLocationUpdate = lastUpdate;
    }
  };

  // Open location update modal
  const openLocationUpdateModal = (assign) => {
    updatingLocationFor.value = assign;
    locationUpdateError.value = null;
    locationUpdateSuccess.value = null;
    showLocationUpdateModal.value = true;
  };

  // Close location update modal
  const closeLocationUpdateModal = () => {
    showLocationUpdateModal.value = false;
    updatingLocationFor.value = null;
    locationUpdateError.value = null;
    locationUpdateSuccess.value = null;
    selectingLocationOnMap.value = false;
  };

  // Start map location selection
  const startMapLocationSelect = () => {
    showLocationUpdateModal.value = false;
    selectingLocationOnMap.value = true;
  };

  // Cancel map location selection
  const cancelMapLocationSelect = () => {
    selectingLocationOnMap.value = false;
    showLocationUpdateModal.value = true;
  };

  // Unified function to update dynamic checkpoint location
  const updateDynamicCheckpointLocation = async (locationId, lat, lng, sourceType, sourceCheckpointId = null) => {
    updatingLocation.value = true;

    try {
      const evtId = eventId.value;
      const payload = {
        latitude: lat,
        longitude: lng,
        sourceType,
      };
      if (sourceCheckpointId) {
        payload.sourceCheckpointId = sourceCheckpointId;
      }

      const response = await locationsApi.updatePosition(evtId, locationId, payload);

      if (response.data.success) {
        if (autoUpdateEnabled.value) {
          stopAutoUpdate();
        }
        updateLocalCheckpointPosition(locationId, response.data.latitude, response.data.longitude, response.data.lastLocationUpdate);
      }
    } catch (error) {
      console.error('Failed to update location:', error);
      if (error.response?.status === 403) {
        alert('You do not have permission to update this location.');
      } else {
        alert('Failed to update location. Please try again.');
      }
    } finally {
      updatingLocation.value = false;
    }
  };

  // Update location using GPS
  const updateLocationWithGps = async () => {
    if (!updatingLocationFor.value) return;

    updatingLocation.value = true;
    locationUpdateError.value = null;

    try {
      let latitude, longitude;

      // Use cached location if available and recent
      if (userLocation.value && locationLastUpdated?.value) {
        const ageMs = Date.now() - locationLastUpdated.value;
        if (ageMs < 30000) {
          latitude = userLocation.value.lat;
          longitude = userLocation.value.lng;
        }
      }

      // Fall back to getCurrentPosition
      if (!latitude || !longitude) {
        if (!('geolocation' in navigator)) {
          throw new Error('Geolocation is not supported by your browser');
        }

        const position = await new Promise((resolve, reject) => {
          navigator.geolocation.getCurrentPosition(resolve, reject, {
            enableHighAccuracy: true,
            timeout: 15000,
            maximumAge: 30000,
          });
        });

        latitude = position.coords.latitude;
        longitude = position.coords.longitude;
      }

      const evtId = eventId.value;
      const locationId = updatingLocationFor.value.locationId;

      const response = await locationsApi.updatePosition(evtId, locationId, {
        latitude,
        longitude,
        sourceType: 'gps',
      });

      if (response.data.success) {
        locationUpdateSuccess.value = 'Location updated successfully!';
        updateLocalCheckpointPosition(locationId, response.data.latitude, response.data.longitude, response.data.lastLocationUpdate);
        setTimeout(() => closeLocationUpdateModal(), 1500);
      }
    } catch (error) {
      console.error('Failed to update location with GPS:', error);
      if (error.response?.status === 403) {
        locationUpdateError.value = 'You do not have permission to update this location.';
      } else if (error.code === 1) {
        locationUpdateError.value = 'Location access denied. Please enable location permissions.';
      } else if (error.code === 2) {
        locationUpdateError.value = 'Unable to determine your location. Please try again.';
      } else if (error.code === 3 || error.message?.includes('Timeout')) {
        locationUpdateError.value = 'GPS timed out. Please ensure location services are enabled and try again.';
      } else {
        locationUpdateError.value = 'Failed to update location. Please try again.';
      }
    } finally {
      updatingLocation.value = false;
    }
  };

  // Update location from another checkpoint
  const updateLocationFromCheckpoint = async (sourceCheckpointId) => {
    if (!updatingLocationFor.value) return;

    const sourceLocation = allLocations.value.find(l => l.id === sourceCheckpointId);
    if (!sourceLocation) {
      locationUpdateError.value = 'Source checkpoint not found.';
      return;
    }

    updatingLocation.value = true;
    locationUpdateError.value = null;

    try {
      const evtId = eventId.value;
      const locationId = updatingLocationFor.value.locationId;

      const response = await locationsApi.updatePosition(evtId, locationId, {
        latitude: sourceLocation.latitude,
        longitude: sourceLocation.longitude,
        sourceType: 'checkpoint',
        sourceCheckpointId: sourceCheckpointId,
      });

      if (response.data.success) {
        if (autoUpdateEnabled.value) {
          stopAutoUpdate();
        }
        locationUpdateSuccess.value = `Location copied from ${sourceLocation.name}!`;
        updateLocalCheckpointPosition(locationId, response.data.latitude, response.data.longitude, response.data.lastLocationUpdate);
        setTimeout(() => closeLocationUpdateModal(), 1500);
      }
    } catch (error) {
      console.error('Failed to copy location from checkpoint:', error);
      if (error.response?.status === 403) {
        locationUpdateError.value = 'You do not have permission to update this location.';
      } else {
        locationUpdateError.value = 'Failed to update location. Please try again.';
      }
    } finally {
      updatingLocation.value = false;
    }
  };

  // Update location manually (from map click)
  const updateLocationManually = async (lat, lng) => {
    if (!updatingLocationFor.value) return;

    updatingLocation.value = true;
    locationUpdateError.value = null;

    try {
      const evtId = eventId.value;
      const locationId = updatingLocationFor.value.locationId;

      const response = await locationsApi.updatePosition(evtId, locationId, {
        latitude: lat,
        longitude: lng,
        sourceType: 'manual',
      });

      if (response.data.success) {
        locationUpdateSuccess.value = 'Location updated successfully!';
        updateLocalCheckpointPosition(locationId, response.data.latitude, response.data.longitude, response.data.lastLocationUpdate);
        setTimeout(() => closeLocationUpdateModal(), 1500);
      }
    } catch (error) {
      console.error('Failed to update location manually:', error);
      if (error.response?.status === 403) {
        locationUpdateError.value = 'You do not have permission to update this location.';
      } else {
        locationUpdateError.value = 'Failed to update location. Please try again.';
      }
    } finally {
      updatingLocation.value = false;
    }
  };

  // Auto-update functions
  const performAutoUpdate = async (assign) => {
    if (!assign || !('geolocation' in navigator)) return;

    try {
      const position = await new Promise((resolve, reject) => {
        navigator.geolocation.getCurrentPosition(resolve, reject, {
          enableHighAccuracy: true,
          timeout: 15000,
        });
      });

      const evtId = eventId.value;
      const locationId = assign.locationId;

      await locationsApi.updatePosition(evtId, locationId, {
        latitude: position.coords.latitude,
        longitude: position.coords.longitude,
        sourceType: 'gps',
      }, { skipLoadingOverlay: true });

      updateLocalCheckpointPosition(locationId, position.coords.latitude, position.coords.longitude, new Date().toISOString());
    } catch (error) {
      console.warn('Auto-update location failed:', error);
    }
  };

  const startAutoUpdate = (assign) => {
    autoUpdateEnabled.value = true;
    performAutoUpdate(assign);
    autoUpdateInterval = setInterval(() => {
      performAutoUpdate(assign);
    }, 60000);
  };

  const stopAutoUpdate = () => {
    autoUpdateEnabled.value = false;
    if (autoUpdateInterval) {
      clearInterval(autoUpdateInterval);
      autoUpdateInterval = null;
    }
  };

  const toggleAutoUpdate = (assign) => {
    if (autoUpdateEnabled.value) {
      stopAutoUpdate();
    } else {
      startAutoUpdate(assign);
    }
  };

  // Dynamic checkpoint polling
  const startDynamicCheckpointPolling = () => {
    if (dynamicCheckpointPollInterval) return;

    const hasDynamicCheckpoints = allLocations.value.some(l => l.isDynamic || l.IsDynamic);
    if (!hasDynamicCheckpoints) return;

    dynamicCheckpointPollInterval = setInterval(async () => {
      try {
        const evtId = eventId.value;
        const response = await locationsApi.getDynamicCheckpoints(evtId, { skipLoadingOverlay: true });
        if (response.data && Array.isArray(response.data)) {
          for (const dynamicCp of response.data) {
            const location = allLocations.value.find(l => l.id === dynamicCp.checkpointId);
            if (location) {
              location.latitude = dynamicCp.latitude;
              location.longitude = dynamicCp.longitude;
              location.lastLocationUpdate = dynamicCp.lastLocationUpdate;
            }
          }
        }
      } catch (error) {
        console.warn('Failed to poll dynamic checkpoints:', error);
      }
    }, 60000);
  };

  const stopDynamicCheckpointPolling = () => {
    if (dynamicCheckpointPollInterval) {
      clearInterval(dynamicCheckpointPollInterval);
      dynamicCheckpointPollInterval = null;
    }
  };

  // Poll all checkpoint locations (used when a map is visible)
  // This uses a single API call to fetch all locations efficiently
  const pollAllCheckpointLocations = async () => {
    try {
      const evtId = eventId.value;
      const response = await locationsApi.getByEvent(evtId, { skipLoadingOverlay: true });
      if (response.data && Array.isArray(response.data)) {
        // Update only coordinate data to avoid disrupting UI state
        for (const fetchedLoc of response.data) {
          const existingLoc = allLocations.value.find(l => l.id === fetchedLoc.id);
          if (existingLoc) {
            // Silently update position data
            existingLoc.latitude = fetchedLoc.latitude;
            existingLoc.longitude = fetchedLoc.longitude;
            if (fetchedLoc.lastLocationUpdate) {
              existingLoc.lastLocationUpdate = fetchedLoc.lastLocationUpdate;
            }
          }
        }
      }
    } catch (error) {
      console.warn('Failed to poll all checkpoint locations:', error);
    }
  };

  const startAllCheckpointsPolling = () => {
    if (allCheckpointsPollInterval) return;

    // Poll immediately, then every 60 seconds
    pollAllCheckpointLocations();
    allCheckpointsPollInterval = setInterval(pollAllCheckpointLocations, 60000);
  };

  const stopAllCheckpointsPolling = () => {
    if (allCheckpointsPollInterval) {
      clearInterval(allCheckpointsPollInterval);
      allCheckpointsPollInterval = null;
    }
  };

  // Handle map click for location selection
  const handleMapClickForLocation = (coords) => {
    if (selectingLocationOnMap.value && updatingLocationFor.value) {
      updateDynamicCheckpointLocation(
        updatingLocationFor.value.locationId,
        coords.lat,
        coords.lng,
        'manual'
      );
      selectingLocationOnMap.value = false;
    }
  };

  return {
    // State
    showLocationUpdateModal,
    updatingLocationFor,
    locationUpdateError,
    locationUpdateSuccess,
    updatingLocation,
    autoUpdateEnabled,
    selectingLocationOnMap,

    // Functions
    isDynamicCheckpoint,
    openLocationUpdateModal,
    closeLocationUpdateModal,
    startMapLocationSelect,
    cancelMapLocationSelect,
    updateDynamicCheckpointLocation,
    updateLocationWithGps,
    updateLocationFromCheckpoint,
    updateLocationManually,
    toggleAutoUpdate,
    startAutoUpdate,
    stopAutoUpdate,
    startDynamicCheckpointPolling,
    stopDynamicCheckpointPolling,
    startAllCheckpointsPolling,
    stopAllCheckpointsPolling,
    handleMapClickForLocation,
  };
}
