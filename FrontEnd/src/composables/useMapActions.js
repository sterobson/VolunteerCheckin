import { ref, computed } from 'vue';
import { useTerminology } from './useTerminology';

// SVG icons for map toolbar actions
const ICON_MY_LOCATION = '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="currentColor" width="18" height="18"><circle cx="12" cy="12" r="4"/><path d="M13 4.069V2h-2v2.069A8.01 8.01 0 0 0 4.069 11H2v2h2.069A8.008 8.008 0 0 0 11 19.931V22h2v-2.069A8.007 8.007 0 0 0 19.931 13H22v-2h-2.069A8.008 8.008 0 0 0 13 4.069zM12 18c-3.309 0-6-2.691-6-6s2.691-6 6-6 6 2.691 6 6-2.691 6-6 6z"/></svg>';
const ICON_CHECKPOINT = '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="currentColor" width="18" height="18"><path d="M12 2C8.13 2 5 5.13 5 9c0 5.25 7 13 7 13s7-7.75 7-13c0-3.87-3.13-7-7-7zm0 9.5c-1.38 0-2.5-1.12-2.5-2.5s1.12-2.5 2.5-2.5 2.5 1.12 2.5 2.5-1.12 2.5-2.5 2.5z"/></svg>';
// Eye icon - show all checkpoints
const ICON_EYE = '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" width="18" height="18"><path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"/><circle cx="12" cy="12" r="3"/></svg>';
// Eye-off icon - hide other checkpoints
const ICON_EYE_OFF = '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" width="18" height="18"><path d="M17.94 17.94A10.07 10.07 0 0 1 12 20c-7 0-11-8-11-8a18.45 18.45 0 0 1 5.06-5.94M9.9 4.24A9.12 9.12 0 0 1 12 4c7 0 11 8 11 8a18.5 18.5 0 0 1-2.16 3.19m-6.72-1.07a3 3 0 1 1-4.24-4.24"/><line x1="1" y1="1" x2="23" y2="23"/></svg>';

/**
 * Composable for map toolbar actions in the marshal view.
 * Handles both course map and checkpoint map actions.
 *
 * @param {Object} options
 * @param {import('vue').Ref} options.userLocation - User's GPS location
 * @param {import('vue').Ref} options.assignmentsWithDetails - Marshal's checkpoint assignments
 * @param {import('vue').Ref} options.courseMapRef - Reference to the course map component
 */
export function useMapActions({ userLocation, assignmentsWithDetails, courseMapRef }) {
  const { termsLower } = useTerminology();

  // Visibility state for course map (includes detailed per-location visibility)
  const courseMapVisibility = ref({
    userLocationInView: false,
    highlightedLocationInView: false,
    highlightedLocationsVisibility: {}, // { locationId: boolean }
  });

  // Toggle state for hiding other checkpoints on course map
  const hideOtherCheckpoints = ref(false);

  // Track last centered checkpoint index for cycling through checkpoints
  const lastCenteredCheckpointIndex = ref(-1);

  // Visibility state for each checkpoint map (keyed by assignment ID)
  const checkpointMapVisibility = ref({});

  // Store refs to checkpoint maps (keyed by assignment ID)
  const checkpointMapRefs = {};

  // All assignments with valid locations (for recentering)
  const assignmentsWithValidLocations = computed(() => {
    return assignmentsWithDetails.value.filter(a =>
      a.location?.latitude && a.location?.longitude &&
      !(a.location.latitude === 0 && a.location.longitude === 0)
    );
  });

  // Course map toolbar actions for recentering (only show when location is off-screen)
  const courseMapActions = computed(() => {
    const actions = [];

    // Toggle visibility of other checkpoints (always shown)
    actions.push({
      id: 'toggle-other-checkpoints',
      label: hideOtherCheckpoints.value
        ? 'Show all ' + termsLower.value.checkpoints
        : 'Show only mine',
      icon: 'custom',
      customIcon: hideOtherCheckpoints.value ? ICON_EYE : ICON_EYE_OFF,
    });

    // Recenter on my location (if GPS is available AND location is off-screen)
    if (userLocation.value && !courseMapVisibility.value.userLocationInView) {
      actions.push({
        id: 'recenter-user',
        label: 'My location',
        icon: 'custom',
        customIcon: ICON_MY_LOCATION,
      });
    }

    // Recenter on my checkpoint (if I have one with a location AND at least one is off-screen)
    if (assignmentsWithValidLocations.value.length > 0 && !courseMapVisibility.value.highlightedLocationInView) {
      actions.push({
        id: 'recenter-checkpoint',
        label: 'My ' + termsLower.value.checkpoint,
        icon: 'custom',
        customIcon: ICON_CHECKPOINT,
      });
    }

    return actions;
  });

  // Handle course map visibility changes (only update if changed to avoid infinite loops)
  const handleCourseMapVisibilityChange = (visibility) => {
    const current = courseMapVisibility.value;
    const visibilityChanged = current.userLocationInView !== visibility.userLocationInView ||
        current.highlightedLocationInView !== visibility.highlightedLocationInView;

    // Also check if detailed visibility changed
    const detailedVisibilityChanged = JSON.stringify(current.highlightedLocationsVisibility) !==
        JSON.stringify(visibility.highlightedLocationsVisibility || {});

    if (visibilityChanged || detailedVisibilityChanged) {
      courseMapVisibility.value = {
        ...visibility,
        highlightedLocationsVisibility: visibility.highlightedLocationsVisibility || {},
      };
    }
  };

  /**
   * Find the next checkpoint to center on based on the rules:
   * 1. If 1 checkpoint: go to it
   * 2. If multiple checkpoints: go to whichever one isn't visible
   * 3. If multiple not visible: go to the first one after the last centered one (cycling)
   * 4. If none are visible: go to the first one
   */
  const findNextCheckpointToCenter = () => {
    const validAssignments = assignmentsWithValidLocations.value;
    if (validAssignments.length === 0) return null;

    // If only 1 checkpoint, always go to it
    if (validAssignments.length === 1) {
      lastCenteredCheckpointIndex.value = 0;
      return validAssignments[0];
    }

    // Get visibility status for each checkpoint
    const visibility = courseMapVisibility.value.highlightedLocationsVisibility || {};

    // Find checkpoints that are not in view
    const notVisibleIndices = [];
    for (let i = 0; i < validAssignments.length; i++) {
      const locationId = validAssignments[i].locationId;
      if (visibility[locationId] === false) {
        notVisibleIndices.push(i);
      }
    }

    // If all are visible, go to the first one
    if (notVisibleIndices.length === 0) {
      lastCenteredCheckpointIndex.value = 0;
      return validAssignments[0];
    }

    // If only one is not visible, go to it
    if (notVisibleIndices.length === 1) {
      lastCenteredCheckpointIndex.value = notVisibleIndices[0];
      return validAssignments[notVisibleIndices[0]];
    }

    // Multiple not visible: cycle through them
    // Find the first not-visible checkpoint after the last centered one
    const lastIndex = lastCenteredCheckpointIndex.value;
    let nextIndex = -1;

    // Look for a not-visible checkpoint after the last centered one
    for (const idx of notVisibleIndices) {
      if (idx > lastIndex) {
        nextIndex = idx;
        break;
      }
    }

    // If none found after last, wrap around to the first not-visible one
    if (nextIndex === -1) {
      nextIndex = notVisibleIndices[0];
    }

    lastCenteredCheckpointIndex.value = nextIndex;
    return validAssignments[nextIndex];
  };

  // Handle course map toolbar actions
  const handleCourseMapAction = ({ actionId }) => {
    if (actionId === 'toggle-other-checkpoints') {
      hideOtherCheckpoints.value = !hideOtherCheckpoints.value;
    } else if (actionId === 'recenter-user' && userLocation.value) {
      courseMapRef.value?.recenterOnUserLocation();
    } else if (actionId === 'recenter-checkpoint') {
      const nextCheckpoint = findNextCheckpointToCenter();
      if (nextCheckpoint?.location) {
        courseMapRef.value?.recenterOnLocation(
          nextCheckpoint.location.latitude,
          nextCheckpoint.location.longitude
        );
      }
    }
  };

  // Get toolbar actions for a checkpoint mini-map
  const getCheckpointMapActions = (assignId) => {
    const actions = [];
    const visibility = checkpointMapVisibility.value[assignId] || { userLocationInView: false, highlightedLocationInView: false };

    // Recenter on my location (if GPS is available AND location is off-screen)
    if (userLocation.value && !visibility.userLocationInView) {
      actions.push({
        id: 'recenter-user',
        label: 'My location',
        icon: 'custom',
        customIcon: ICON_MY_LOCATION,
      });
    }

    // Recenter on this checkpoint (if it's off-screen)
    if (!visibility.highlightedLocationInView) {
      actions.push({
        id: 'recenter-checkpoint',
        label: termsLower.value.checkpoint.charAt(0).toUpperCase() + termsLower.value.checkpoint.slice(1),
        icon: 'custom',
        customIcon: ICON_CHECKPOINT,
      });
    }

    return actions;
  };

  // Handle checkpoint map visibility changes (only update if changed to avoid infinite loops)
  const handleCheckpointMapVisibilityChange = (assignId, visibility) => {
    const current = checkpointMapVisibility.value[assignId];
    if (!current ||
        current.userLocationInView !== visibility.userLocationInView ||
        current.highlightedLocationInView !== visibility.highlightedLocationInView) {
      checkpointMapVisibility.value = {
        ...checkpointMapVisibility.value,
        [assignId]: visibility,
      };
    }
  };

  // Handle checkpoint map toolbar actions
  const handleCheckpointMapAction = (assign, { actionId }) => {
    const mapRef = checkpointMapRefs[assign.id];
    if (actionId === 'recenter-user' && userLocation.value) {
      mapRef?.recenterOnUserLocation();
    } else if (actionId === 'recenter-checkpoint' && assign.location) {
      mapRef?.recenterOnLocation(assign.location.latitude, assign.location.longitude);
    }
  };

  return {
    // Course map
    courseMapVisibility,
    courseMapActions,
    hideOtherCheckpoints,
    handleCourseMapVisibilityChange,
    handleCourseMapAction,

    // Checkpoint maps
    checkpointMapVisibility,
    checkpointMapRefs,
    getCheckpointMapActions,
    handleCheckpointMapVisibilityChange,
    handleCheckpointMapAction,
  };
}
