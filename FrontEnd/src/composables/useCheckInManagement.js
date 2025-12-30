import { ref } from 'vue';

/**
 * Composable for managing check-in status changes
 * Tracks pending changes before they're saved to the backend
 */
export function useCheckInManagement(emitDirty) {
  // Track local check-in status changes (assignmentId -> newStatus)
  const pendingCheckInChanges = ref(new Map());

  /**
   * Get the effective check-in status (considering pending changes)
   * @param {Object} assignment - The assignment object
   * @returns {boolean} - Whether the assignment should be shown as checked in
   */
  const getEffectiveCheckInStatus = (assignment) => {
    if (pendingCheckInChanges.value.has(assignment.id)) {
      return pendingCheckInChanges.value.get(assignment.id);
    }
    return assignment.isCheckedIn;
  };

  /**
   * Toggle check-in status for an assignment
   * @param {Object} assignment - The assignment object
   */
  const handleToggleCheckIn = (assignment) => {
    const currentStatus = getEffectiveCheckInStatus(assignment);
    const newStatus = !currentStatus;

    // If toggling back to original status, remove from pending changes
    if (newStatus === assignment.isCheckedIn) {
      pendingCheckInChanges.value.delete(assignment.id);
    } else {
      // Otherwise, track the pending change
      pendingCheckInChanges.value.set(assignment.id, newStatus);
    }

    // Mark form as dirty
    if (emitDirty) {
      emitDirty();
    }
  };

  /**
   * Get status icon for an assignment
   * @param {Object} assignment - The assignment object
   * @returns {string} - Icon character
   */
  const getStatusIcon = (assignment) => {
    if (getEffectiveCheckInStatus(assignment)) return '✓';
    return '✗';
  };

  /**
   * Get status color for an assignment
   * @param {Object} assignment - The assignment object
   * @returns {string} - Color hex code
   */
  const getStatusColor = (assignment) => {
    if (getEffectiveCheckInStatus(assignment)) return '#28a745';
    return '#dc3545';
  };

  /**
   * Get status text for an assignment
   * @param {Object} assignment - The assignment object
   * @returns {string} - Status text
   */
  const getStatusText = (assignment) => {
    if (getEffectiveCheckInStatus(assignment)) return 'Checked in';
    return 'Not checked in';
  };

  /**
   * Format time string to readable format
   * @param {string} timeString - ISO time string
   * @returns {string} - Formatted time
   */
  const formatTime = (timeString) => {
    if (!timeString) return '';
    const date = new Date(timeString);
    return date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
  };

  /**
   * Clear all pending changes
   */
  const clearPendingChanges = () => {
    pendingCheckInChanges.value.clear();
  };

  /**
   * Get pending changes as an array for saving
   * @returns {Array} - Array of {assignmentId, shouldBeCheckedIn} objects
   */
  const getPendingChanges = () => {
    return Array.from(pendingCheckInChanges.value.entries()).map(([assignmentId, shouldBeCheckedIn]) => ({
      assignmentId,
      shouldBeCheckedIn,
    }));
  };

  return {
    pendingCheckInChanges,
    getEffectiveCheckInStatus,
    handleToggleCheckIn,
    getStatusIcon,
    getStatusColor,
    getStatusText,
    formatTime,
    clearPendingChanges,
    getPendingChanges,
  };
}
