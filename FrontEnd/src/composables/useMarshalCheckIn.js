import { ref } from 'vue';
import { checkInApi, queueOfflineAction, getOfflineMode } from '../services/api';
import { calculateDistance } from '../utils/coordinateUtils';
import { useTerminology } from './useTerminology';

/**
 * Composable for managing marshal check-in functionality
 */
export function useMarshalCheckIn({
  eventId,
  assignments,
  allLocations,
  areaLeadRef,
  areaLeadCheckpoints,
  areaLeadMarshalDataVersion,
  updatePendingCount,
  updateCachedField,
}) {
  const { termsLower } = useTerminology();

  // State
  const checkingIn = ref(null);
  const checkingInAssignment = ref(null);
  const checkInError = ref(null);

  // Check-in reminder modal state
  const showCheckInReminderModal = ref(false);
  const checkInReminderCheckpoint = ref(null);
  const dismissedCheckInReminders = ref(new Set());

  // Distance warning state (shown after check-in if too far from checkpoint)
  const showDistanceWarning = ref(false);
  const distanceWarningMessage = ref('');

  // Helper to check if a check-in is stale (more than 24 hours old)
  const isCheckInStale = (checkInTime) => {
    if (!checkInTime) return true;
    const checkInDate = new Date(checkInTime);
    const now = new Date();
    const hoursDiff = (now - checkInDate) / (1000 * 60 * 60);
    return hoursDiff > 24;
  };

  // Format check-in time for display
  const formatCheckInTime = (checkInTime) => {
    if (!checkInTime) return '';
    const date = new Date(checkInTime);
    return date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
  };

  // Format check-in method for display
  const formatCheckInMethod = (method) => {
    if (!method) return '';
    switch (method) {
      case 'GPS': return 'GPS';
      case 'Manual': return 'Manual';
      case 'AreaLead': return 'By area lead';
      default: return method;
    }
  };

  // Dismiss check-in reminder for a checkpoint
  const dismissCheckInReminder = (assignmentId) => {
    dismissedCheckInReminders.value.add(assignmentId);
    showCheckInReminderModal.value = false;
    checkInReminderCheckpoint.value = null;
  };

  // Dismiss distance warning
  const dismissDistanceWarning = () => {
    showDistanceWarning.value = false;
    distanceWarningMessage.value = '';
  };

  // Format distance: < 1000m = round to nearest 10m, >= 1000m = round to nearest 0.1km
  const formatDistanceForWarning = (meters) => {
    if (meters < 1000) {
      return `${Math.round(meters / 10) * 10} m`;
    }
    return `${(Math.round(meters / 100) / 10).toFixed(1)} km`;
  };

  // Check distance from checkpoint and show warning if too far
  const checkDistanceWarning = (gpsLat, gpsLon, locationId) => {
    // Only check if we have GPS coordinates
    if (gpsLat == null || gpsLon == null) return;

    // Find the checkpoint location
    const location = allLocations.value?.find(l => l.id === locationId);
    if (!location) return;

    // Check if checkpoint has coordinates
    const checkpointLat = location.latitude;
    const checkpointLon = location.longitude;
    if (checkpointLat == null || checkpointLon == null) return;

    // Calculate distance
    const distance = calculateDistance(gpsLat, gpsLon, checkpointLat, checkpointLon);

    // Show warning if more than 50m away
    if (distance > 50) {
      const formattedDistance = formatDistanceForWarning(distance);
      distanceWarningMessage.value = `Thanks for checking in. Just so you know, it's possible you are more than ${formattedDistance} from your ${termsLower.value.checkpoint} area.`;
      showDistanceWarning.value = true;
    }
  };

  /**
   * Unified check-in/check-out toggle handler
   * @param {Object} assign - The assignment object
   * @param {boolean} tryGps - Whether to attempt GPS (true for self, false for checking in others)
   * @param {string} locationId - Optional location ID for updating location assignments
   */
  const handleCheckInToggle = async (assign, tryGps = true, locationId = null) => {
    if (!assign?.id) {
      checkInError.value = 'Cannot check in: Assignment ID is missing';
      return;
    }

    checkingIn.value = assign.id;
    checkingInAssignment.value = assign.id;
    checkInError.value = null;

    const evtId = eventId.value;

    if (!evtId) {
      checkInError.value = 'Cannot check in: Event ID is missing';
      checkingIn.value = null;
      checkingInAssignment.value = null;
      return;
    }
    // Use effectiveIsCheckedIn if available, otherwise compute it
    // This ensures stale check-ins (>24h old) are treated as not checked in
    const isCurrentlyCheckedIn = assign.effectiveIsCheckedIn !== undefined
      ? assign.effectiveIsCheckedIn
      : (assign.isCheckedIn && !isCheckInStale(assign.checkInTime));

    // If the check-in is stale (isCheckedIn true but effectiveIsCheckedIn false),
    // we need to check out first, then check in to refresh the timestamp
    const isStaleCheckIn = assign.isCheckedIn && !isCurrentlyCheckedIn;
    const action = isCurrentlyCheckedIn ? 'check-out' : (isStaleCheckIn ? 'refresh' : 'check-in');
    const newIsCheckedIn = !isCurrentlyCheckedIn;

    // Try to get GPS coordinates if requested (for self check-in, not check-out)
    let latitude = null;
    let longitude = null;
    let method = 'Manual';

    if (tryGps && !isCurrentlyCheckedIn && 'geolocation' in navigator) {
      try {
        const position = await new Promise((resolve, reject) => {
          navigator.geolocation.getCurrentPosition(resolve, reject, {
            enableHighAccuracy: true,
            timeout: 5000, // Shorter timeout - fall back to manual quickly
          });
        });
        latitude = position.coords.latitude;
        longitude = position.coords.longitude;
        method = 'GPS';
      } catch (gpsError) {
        // GPS failed, fall back to manual silently
        console.log('GPS unavailable, using manual check-in:', gpsError.message);
      }
    }

    const gpsData = { latitude, longitude, action };

    try {
      if (getOfflineMode()) {
        // Queue for offline sync
        await queueOfflineAction('checkin_toggle', {
          eventId: evtId,
          assignmentId: assign.id,
          gpsData,
        });

        // Optimistic update for own assignments
        const index = assignments.value.findIndex(a => a.id === assign.id);
        if (index !== -1) {
          assignments.value[index] = {
            ...assignments.value[index],
            isCheckedIn: newIsCheckedIn,
            checkInTime: newIsCheckedIn ? new Date().toISOString() : null,
            checkInMethod: newIsCheckedIn ? `${method} (pending)` : null,
            checkInLatitude: latitude,
            checkInLongitude: longitude,
          };
        }

        // Also update in location assignments if checking in another marshal
        if (locationId) {
          const location = allLocations.value.find(l => l.id === locationId);
          if (location?.assignments) {
            const locAssign = location.assignments.find(a => a.id === assign.id || a.marshalId === assign.marshalId);
            if (locAssign) {
              locAssign.isCheckedIn = newIsCheckedIn;
              locAssign.checkInTime = newIsCheckedIn ? new Date().toISOString() : null;
            }
          }
        }

        if (updatePendingCount) await updatePendingCount();
        if (updateCachedField) await updateCachedField(evtId, 'locations', allLocations.value);
      } else {
        const response = await checkInApi.toggleCheckIn(evtId, assign.id, gpsData);

        // Update the assignment in the list
        const index = assignments.value.findIndex(a => a.id === assign.id);
        if (index !== -1) {
          assignments.value[index] = response.data;
        }

        // Also update in location assignments if checking in another marshal
        if (locationId) {
          const location = allLocations.value.find(l => l.id === locationId);
          if (location?.assignments) {
            const locAssign = location.assignments.find(a => a.id === assign.id || a.marshalId === assign.marshalId);
            if (locAssign) {
              Object.assign(locAssign, response.data);
            }
          }
        }
      }

      // Refresh area lead dashboard if applicable
      if (locationId && areaLeadRef?.value?.loadDashboard) {
        await areaLeadRef.value.loadDashboard();
      }
      if (areaLeadMarshalDataVersion) {
        areaLeadMarshalDataVersion.value++;
      }

      // Check distance warning if this was a check-in (not check-out) with GPS
      if (newIsCheckedIn && latitude != null && longitude != null) {
        // Get locationId from assignment if not passed as parameter
        const checkpointLocationId = locationId || assign.locationId;
        if (checkpointLocationId) {
          checkDistanceWarning(latitude, longitude, checkpointLocationId);
        }
      }
    } catch (error) {
      // Check if it's a network error - queue for offline
      if (getOfflineMode() || !error.response) {
        await queueOfflineAction('checkin_toggle', {
          eventId: evtId,
          assignmentId: assign.id,
          gpsData,
        });

        // Optimistic update
        const index = assignments.value.findIndex(a => a.id === assign.id);
        if (index !== -1) {
          assignments.value[index] = {
            ...assignments.value[index],
            isCheckedIn: newIsCheckedIn,
            checkInTime: newIsCheckedIn ? new Date().toISOString() : null,
            checkInMethod: newIsCheckedIn ? 'Manual (pending)' : null,
          };
        }
        if (updatePendingCount) await updatePendingCount();
      } else if (error.response?.data?.message) {
        checkInError.value = error.response.data.message;
      } else if (error.message) {
        checkInError.value = error.message;
      } else {
        checkInError.value = `Failed to ${action}. Please try again.`;
      }
    } finally {
      checkingIn.value = null;
      checkingInAssignment.value = null;
    }
  };

  // Handle check-in for area lead marshals section
  const handleAreaLeadMarshalCheckIn = async (marshal) => {
    // Find a checkpoint that has this marshal to get the assignment ID
    const checkpoints = areaLeadRef?.value?.checkpoints || areaLeadCheckpoints?.value || [];
    const checkpoint = checkpoints.find(c =>
      c.marshals?.some(m => m.marshalId === marshal.marshalId)
    );
    if (!checkpoint) {
      console.error('Could not find checkpoint for marshal:', marshal);
      return;
    }

    // Find the full marshal object from the checkpoint
    const checkpointMarshal = checkpoint.marshals.find(m => m.marshalId === marshal.marshalId);
    if (!checkpointMarshal) return;

    // Build an assignment-like object for handleCheckInToggle
    const assignmentId = checkpointMarshal.assignmentId || checkpointMarshal.id;
    if (!assignmentId) {
      console.error('No assignment ID found for marshal:', checkpointMarshal);
      return;
    }

    const assignmentLike = {
      id: assignmentId,
      marshalId: marshal.marshalId,
      isCheckedIn: marshal.isCheckedIn,
      effectiveIsCheckedIn: marshal.isCheckedIn,
    };

    // Use unified toggle (no GPS for checking in others)
    await handleCheckInToggle(assignmentLike, false, checkpoint.locationId);
  };

  return {
    // State
    checkingIn,
    checkingInAssignment,
    checkInError,
    showCheckInReminderModal,
    checkInReminderCheckpoint,
    dismissedCheckInReminders,
    showDistanceWarning,
    distanceWarningMessage,

    // Functions
    isCheckInStale,
    formatCheckInTime,
    formatCheckInMethod,
    handleCheckInToggle,
    handleAreaLeadMarshalCheckIn,
    dismissCheckInReminder,
    dismissDistanceWarning,
  };
}
