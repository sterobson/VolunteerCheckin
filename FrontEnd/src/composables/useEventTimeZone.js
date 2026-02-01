/**
 * Composable for formatting dates/times in the event's timezone
 *
 * Use this in marshal mode to ensure all times display in the event's timezone,
 * not the viewer's local timezone.
 */
import { computed } from 'vue';

/**
 * Create timezone-aware date formatting functions
 * @param {Ref<string>|string} timeZoneId - The event's timezone (e.g., 'Europe/London')
 * @returns {Object} Formatting functions that use the event's timezone
 */
export function useEventTimeZone(timeZoneId) {
  // Get the timezone value (handle both refs and plain strings)
  const getTimeZone = () => {
    const tz = typeof timeZoneId === 'object' && timeZoneId.value !== undefined
      ? timeZoneId.value
      : timeZoneId;
    return tz || 'UTC';
  };

  /**
   * Format a date for display (e.g., "Sat, Jan 18, 10:00 AM")
   */
  const formatEventDate = (dateString) => {
    if (!dateString) return '';
    const date = new Date(dateString);
    if (isNaN(date.getTime())) return '';

    return date.toLocaleString('en-US', {
      timeZone: getTimeZone(),
      weekday: 'short',
      month: 'short',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    });
  };

  /**
   * Format a full date/time (e.g., "Saturday, January 18, 2026, 10:00 AM")
   */
  const formatEventDateTime = (dateString) => {
    if (!dateString) return '';
    const date = new Date(dateString);
    if (isNaN(date.getTime())) return '';

    return date.toLocaleString(undefined, {
      timeZone: getTimeZone(),
      weekday: 'long',
      year: 'numeric',
      month: 'long',
      day: 'numeric',
      hour: 'numeric',
      minute: '2-digit',
    });
  };

  /**
   * Format time only (e.g., "10:00 AM")
   */
  const formatTime = (dateString) => {
    if (!dateString) return '';
    const date = new Date(dateString);
    if (isNaN(date.getTime())) return '';

    return date.toLocaleTimeString([], {
      timeZone: getTimeZone(),
      hour: 'numeric',
      minute: '2-digit',
    });
  };

  /**
   * Format a time range (e.g., "10:00 AM - 2:00 PM" or "from 10:00 AM")
   */
  const formatTimeRange = (start, end) => {
    const startFormatted = formatTime(start);
    const endFormatted = formatTime(end);

    if (startFormatted && endFormatted) {
      return `${startFormatted} - ${endFormatted}`;
    }
    if (startFormatted) {
      return `from ${startFormatted}`;
    }
    if (endFormatted) {
      return `until ${endFormatted}`;
    }
    return '';
  };

  /**
   * Format check-in time (e.g., "10:00 AM")
   */
  const formatCheckInTime = (dateString) => {
    return formatTime(dateString);
  };

  /**
   * Smart date/time format - shows time only if within last 24 hours, otherwise full date
   */
  const formatDateTime = (dateString) => {
    if (!dateString) return '';
    const date = new Date(dateString);
    if (isNaN(date.getTime())) return '';

    const now = new Date();
    const diffMs = now - date;
    const diffHours = diffMs / (1000 * 60 * 60);

    if (diffHours < 24 && diffHours >= 0) {
      return date.toLocaleTimeString([], {
        timeZone: getTimeZone(),
        hour: '2-digit',
        minute: '2-digit',
      });
    }

    return date.toLocaleString(undefined, {
      timeZone: getTimeZone(),
    });
  };

  /**
   * Format date only (e.g., "January 18, 2026")
   */
  const formatDateOnly = (dateString) => {
    if (!dateString) return '';
    const date = new Date(dateString);
    if (isNaN(date.getTime())) return '';

    return date.toLocaleDateString(undefined, {
      timeZone: getTimeZone(),
      year: 'numeric',
      month: 'long',
      day: 'numeric',
    });
  };

  /**
   * Format short date (e.g., "Jan 18")
   */
  const formatShortDate = (dateString) => {
    if (!dateString) return '';
    const date = new Date(dateString);
    if (isNaN(date.getTime())) return '';

    return date.toLocaleDateString(undefined, {
      timeZone: getTimeZone(),
      month: 'short',
      day: 'numeric',
    });
  };

  /**
   * Get the timezone display name for showing to users
   */
  const timeZoneDisplay = computed(() => {
    const tz = getTimeZone();
    if (tz === 'UTC') return 'UTC';

    // Extract city name from timezone ID (e.g., "Europe/London" -> "London")
    const parts = tz.split('/');
    return parts[parts.length - 1].replace(/_/g, ' ');
  });

  return {
    formatEventDate,
    formatEventDateTime,
    formatTime,
    formatTimeRange,
    formatCheckInTime,
    formatDateTime,
    formatDateOnly,
    formatShortDate,
    timeZoneDisplay,
  };
}
