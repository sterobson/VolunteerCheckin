import { ref } from 'vue';
import { checkInApi, checklistApi, queueOfflineAction, getOfflineMode } from '../services/api';
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
  reloadChecklist,
  checklistItems,
  currentMarshalId,
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

  // Helper to check if a check-in is stale - check-ins no longer expire
  const isCheckInStale = () => {
    return false;
  };

  // Format check-in time for display
  // Shows date alongside time if check-in was more than 24 hours ago
  const formatCheckInTime = (checkInTime) => {
    if (!checkInTime) return '';
    const date = new Date(checkInTime);
    const now = new Date();
    const hoursDiff = (now - date) / (1000 * 60 * 60);
    const timeStr = date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
    if (hoursDiff > 24) {
      const dateStr = date.toLocaleDateString([], { day: 'numeric', month: 'short' });
      return `${dateStr}, ${timeStr}`;
    }
    return timeStr;
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

        // Handle response - may be assignment directly or { assignment, linkedTasksCompleted/linkedTasksUncompleted }
        const assignmentData = response.data?.assignment || response.data;
        const linkedTasksCompleted = response.data?.linkedTasksCompleted || [];
        const linkedTasksUncompleted = response.data?.linkedTasksUncompleted || 0;

        // Update the assignment in the list
        const index = assignments.value.findIndex(a => a.id === assign.id);
        if (index !== -1) {
          assignments.value[index] = assignmentData;
        }

        // Also update in location assignments if checking in another marshal
        if (locationId) {
          const location = allLocations.value.find(l => l.id === locationId);
          if (location?.assignments) {
            const locAssign = location.assignments.find(a => a.id === assign.id || a.marshalId === assign.marshalId);
            if (locAssign) {
              Object.assign(locAssign, assignmentData);
            }
          }
        }

        // Reload checklist if linked tasks were affected
        if (reloadChecklist && (linkedTasksCompleted.length > 0 || linkedTasksUncompleted > 0)) {
          await reloadChecklist(true);
        }
      }

      // Refresh area lead dashboard if applicable
      if (locationId && areaLeadRef?.value?.loadDashboard) {
        await areaLeadRef.value.loadDashboard();
        // Sync areaLeadCheckpoints with the reloaded data
        // This ensures the computed picks up the updated check-in status
        if (areaLeadCheckpoints && areaLeadRef?.value?.checkpoints) {
          areaLeadCheckpoints.value = areaLeadRef.value.checkpoints;
        }
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

    const newIsCheckedIn = !marshal.isCheckedIn;
    const checkpointId = checkpoint.checkpointId || checkpoint.locationId;

    const assignmentLike = {
      id: assignmentId,
      marshalId: marshal.marshalId,
      isCheckedIn: marshal.isCheckedIn,
      effectiveIsCheckedIn: marshal.isCheckedIn,
    };

    // Use unified toggle (no GPS for checking in others)
    // Note: dashboard API uses checkpointId, not locationId
    await handleCheckInToggle(assignmentLike, false, checkpointId);

    // Optimistic update: update the marshal's check-in status in all checkpoint data sources
    // This ensures the UI updates immediately without waiting for a full dashboard reload
    const allCheckpointSources = [
      areaLeadRef?.value?.checkpoints,
      areaLeadCheckpoints?.value
    ].filter(Boolean);

    for (const checkpointList of allCheckpointSources) {
      for (const cp of checkpointList) {
        const m = cp.marshals?.find(m => m.marshalId === marshal.marshalId);
        if (m) {
          m.isCheckedIn = newIsCheckedIn;
          m.checkInTime = newIsCheckedIn ? new Date().toISOString() : null;
          m.checkInMethod = newIsCheckedIn ? 'AreaLead' : null;
        }
      }
    }

    // Force recomputation of allAreaLeadMarshals
    if (areaLeadMarshalDataVersion) {
      areaLeadMarshalDataVersion.value++;
    }

    // Handle linked tasks: complete/uncomplete any tasks linked to check-in for this marshal at this checkpoint
    if (checklistItems?.value && eventId?.value && currentMarshalId?.value) {
      const linkedTasks = checklistItems.value.filter(item =>
        item.linksToCheckIn &&
        item.linkedCheckpointId === checkpointId &&
        (item.contextOwnerMarshalId === marshal.marshalId ||
         (item.completionContextType === 'Checkpoint' && item.completionContextId === checkpointId))
      );

      if (linkedTasks.length > 0) {
        for (const task of linkedTasks) {
          try {
            const actionData = {
              marshalId: task.contextOwnerMarshalId || marshal.marshalId,
              contextType: task.completionContextType,
              contextId: task.completionContextId,
              actorMarshalId: currentMarshalId.value,
            };

            if (newIsCheckedIn && !task.isCompleted) {
              // Checking in - complete the linked task
              await checklistApi.complete(eventId.value, task.itemId, actionData);
            } else if (!newIsCheckedIn && task.isCompleted) {
              // Checking out - uncomplete the linked task
              await checklistApi.uncomplete(eventId.value, task.itemId, actionData);
            }
          } catch (err) {
            console.error('Failed to toggle linked task:', err);
          }
        }

        // Reload checklist to reflect changes
        if (reloadChecklist) {
          await reloadChecklist(true);
        }
      }
    }
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
