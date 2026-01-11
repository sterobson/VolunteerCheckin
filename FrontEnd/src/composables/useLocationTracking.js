import { ref, onUnmounted } from 'vue';

/**
 * Composable for GPS location tracking
 * Manages watching the user's position and provides location state
 *
 * @returns {Object} Location tracking state and controls
 */
export function useLocationTracking() {
  const userLocation = ref(null);
  const locationLastUpdated = ref(null);
  let locationWatchId = null;

  const startLocationTracking = () => {
    if (!('geolocation' in navigator)) {
      console.warn('Geolocation not supported');
      return;
    }

    console.log('Starting location tracking...');

    locationWatchId = navigator.geolocation.watchPosition(
      (position) => {
        userLocation.value = {
          lat: position.coords.latitude,
          lng: position.coords.longitude,
        };
        locationLastUpdated.value = Date.now();
      },
      (error) => {
        // Provide helpful error messages
        const errorMessages = {
          1: 'Location permission denied. Please allow location access in your browser settings.',
          2: 'Location unavailable. Make sure GPS is enabled on your device.',
          3: 'Location request timed out. Please try again.',
        };
        console.warn('Geolocation error:', errorMessages[error.code] || error.message);
      },
      {
        enableHighAccuracy: true,
        timeout: 15000,
        maximumAge: 10000,
      }
    );
  };

  const stopLocationTracking = () => {
    if (locationWatchId !== null) {
      navigator.geolocation.clearWatch(locationWatchId);
      locationWatchId = null;
    }
  };

  // Cleanup on unmount
  onUnmounted(() => {
    stopLocationTracking();
  });

  return {
    userLocation,
    locationLastUpdated,
    startLocationTracking,
    stopLocationTracking,
  };
}
