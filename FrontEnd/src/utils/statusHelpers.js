/**
 * Status helper utilities for check-in assignments
 * Centralized status display logic to avoid duplication
 */

/**
 * Get icon for assignment status
 * @param {Object} assignment - Assignment object with isCheckedIn property
 * @returns {string} Icon character
 */
export const getStatusIcon = (assignment) => {
  if (!assignment) return '○';
  if (assignment.isCheckedIn) return '✓';
  return '○';
};

/**
 * Get color for assignment status
 * @param {Object} assignment - Assignment object with isCheckedIn property
 * @returns {string} CSS color value
 */
export const getStatusColor = (assignment) => {
  if (!assignment) return '#666';
  if (assignment.isCheckedIn) return '#28a745';
  return '#666';
};

/**
 * Get text description for assignment status
 * @param {Object} assignment - Assignment object with isCheckedIn property
 * @returns {string} Status text
 */
export const getStatusText = (assignment) => {
  if (!assignment) return 'Not checked in';
  if (assignment.isCheckedIn) return 'Checked in';
  return 'Not checked in';
};

/**
 * Get CSS class for assignment status
 * @param {Object} assignment - Assignment object with isCheckedIn property
 * @returns {string} CSS class name
 */
export const getStatusClass = (assignment) => {
  if (!assignment) return 'status-not-checked-in';
  if (assignment.isCheckedIn) return 'status-checked-in';
  return 'status-not-checked-in';
};

/**
 * Get detailed status with check-in time
 * @param {Object} assignment - Assignment object with isCheckedIn and checkInTime properties
 * @returns {string} Detailed status text
 */
export const getDetailedStatus = (assignment) => {
  if (!assignment) return 'Not checked in';
  if (assignment.isCheckedIn && assignment.checkInTime) {
    const time = new Date(assignment.checkInTime).toLocaleTimeString('en-US', {
      hour: '2-digit',
      minute: '2-digit',
    });
    return `Checked in at ${time}`;
  }
  if (assignment.isCheckedIn) return 'Checked in';
  return 'Not checked in';
};

/**
 * Check if assignment is overdue (not checked in past expected time)
 * @param {Object} assignment - Assignment object
 * @param {string|Date} expectedTime - Expected check-in time
 * @returns {boolean} True if overdue
 */
export const isOverdue = (assignment, expectedTime) => {
  if (!assignment || !expectedTime) return false;
  if (assignment.isCheckedIn) return false;

  const expected = new Date(expectedTime);
  const now = new Date();

  return now > expected;
};

/**
 * Get status badge variant for UI libraries
 * @param {Object} assignment - Assignment object with isCheckedIn property
 * @returns {string} Badge variant (success, warning, secondary)
 */
export const getStatusBadgeVariant = (assignment) => {
  if (!assignment) return 'secondary';
  if (assignment.isCheckedIn) return 'success';
  return 'secondary';
};
