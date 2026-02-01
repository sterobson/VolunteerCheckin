/**
 * Time zone constants
 * Comprehensive list of IANA time zones with friendly labels
 */

/**
 * Get the UTC offset for a timezone at a specific date (in hours)
 */
function getUtcOffset(timeZone, date = new Date()) {
  try {
    const formatter = new Intl.DateTimeFormat('en-US', {
      timeZone,
      timeZoneName: 'shortOffset',
    });
    const parts = formatter.formatToParts(date);
    const offsetPart = parts.find((p) => p.type === 'timeZoneName');
    if (offsetPart) {
      const match = offsetPart.value.match(/GMT([+-]?\d+)?(?::(\d+))?/);
      if (match) {
        const hours = parseInt(match[1] || '0', 10);
        const minutes = parseInt(match[2] || '0', 10);
        return hours + (hours >= 0 ? minutes / 60 : -minutes / 60);
      }
    }
    return 0;
  } catch {
    return 0;
  }
}

/**
 * Format UTC offset as string (e.g., "UTC+10:00" or "UTC-05:00") for a specific date
 */
function formatUtcOffset(timeZone, date = new Date()) {
  try {
    const formatter = new Intl.DateTimeFormat('en-US', {
      timeZone,
      timeZoneName: 'shortOffset',
    });
    const parts = formatter.formatToParts(date);
    const offsetPart = parts.find((p) => p.type === 'timeZoneName');
    if (offsetPart) {
      // Convert "GMT+10" to "UTC+10:00" format
      let offset = offsetPart.value.replace('GMT', 'UTC');
      // Add :00 if not present
      if (offset !== 'UTC' && !offset.includes(':')) {
        offset += ':00';
      }
      // Add +00:00 for plain UTC
      if (offset === 'UTC') {
        offset = 'UTC+00:00';
      }
      return offset;
    }
    return 'UTC+00:00';
  } catch {
    return 'UTC+00:00';
  }
}

/**
 * Generate a friendly label for a timezone at a specific date
 */
function generateLabel(timeZone, date = new Date()) {
  // Special case for UTC
  if (timeZone === 'UTC') {
    return 'UTC (Coordinated Universal Time)';
  }

  const offset = formatUtcOffset(timeZone, date);

  // Convert "America/New_York" to "New York" or "Europe/London" to "London"
  const parts = timeZone.split('/');
  const city = parts[parts.length - 1].replace(/_/g, ' ');
  const region = parts.length > 1 ? parts[0].replace(/_/g, ' ') : '';

  return `(${offset}) ${city}${region ? ` - ${region}` : ''}`;
}

/**
 * Get all supported IANA timezone IDs
 */
function getAllTimeZoneIds() {
  let zones;

  // Try to get all supported timezones from the browser
  if (typeof Intl !== 'undefined' && Intl.supportedValuesOf) {
    try {
      zones = Intl.supportedValuesOf('timeZone');
    } catch {
      zones = null;
    }
  }

  // Fallback to a curated list if browser doesn't support supportedValuesOf
  if (!zones) {
    zones = [
      'UTC',
      'Pacific/Midway',
      'Pacific/Honolulu',
      'America/Anchorage',
      'America/Los_Angeles',
      'America/Denver',
      'America/Phoenix',
      'America/Chicago',
      'America/New_York',
      'America/Toronto',
      'America/Halifax',
      'America/St_Johns',
      'America/Sao_Paulo',
      'Atlantic/South_Georgia',
      'Atlantic/Azores',
      'Europe/London',
      'Europe/Dublin',
      'Europe/Paris',
      'Europe/Berlin',
      'Europe/Amsterdam',
      'Europe/Brussels',
      'Europe/Madrid',
      'Europe/Rome',
      'Europe/Vienna',
      'Europe/Warsaw',
      'Europe/Athens',
      'Europe/Helsinki',
      'Europe/Istanbul',
      'Europe/Moscow',
      'Africa/Cairo',
      'Africa/Johannesburg',
      'Asia/Dubai',
      'Asia/Karachi',
      'Asia/Kolkata',
      'Asia/Dhaka',
      'Asia/Bangkok',
      'Asia/Jakarta',
      'Asia/Singapore',
      'Asia/Hong_Kong',
      'Asia/Shanghai',
      'Asia/Taipei',
      'Asia/Seoul',
      'Asia/Tokyo',
      'Australia/Perth',
      'Australia/Darwin',
      'Australia/Adelaide',
      'Australia/Brisbane',
      'Australia/Sydney',
      'Australia/Melbourne',
      'Australia/Hobart',
      'Pacific/Auckland',
      'Pacific/Fiji',
    ];
  }

  // Add UTC if not present
  if (!zones.includes('UTC')) {
    zones.unshift('UTC');
  }

  return zones;
}

// Cache the timezone IDs (these don't change)
const TIMEZONE_IDS = getAllTimeZoneIds();

/**
 * Get timezone options with labels calculated for a specific date
 * @param {Date|string} date - The date to calculate offsets for (defaults to now)
 * @returns {Array<{value: string, label: string, offset: number}>}
 */
export function getTimeZonesForDate(date = new Date()) {
  // Parse string dates (from datetime-local input)
  const targetDate = typeof date === 'string' && date ? new Date(date) : date;

  // If invalid date, use current date
  const effectiveDate = targetDate instanceof Date && !isNaN(targetDate) ? targetDate : new Date();

  // Create timezone objects with labels and offsets for the target date
  const timeZoneObjects = TIMEZONE_IDS.map((tz) => ({
    value: tz,
    label: generateLabel(tz, effectiveDate),
    offset: getUtcOffset(tz, effectiveDate),
  }));

  // Sort by UTC offset, then by label
  timeZoneObjects.sort((a, b) => {
    if (a.offset !== b.offset) {
      return a.offset - b.offset;
    }
    return a.label.localeCompare(b.label);
  });

  return timeZoneObjects;
}

/**
 * Get the user's current timezone
 */
export function getUserTimeZone() {
  try {
    return Intl.DateTimeFormat().resolvedOptions().timeZone;
  } catch {
    return 'UTC';
  }
}

/**
 * Check if a timezone value is valid
 */
export function isValidTimeZone(timeZone) {
  try {
    Intl.DateTimeFormat(undefined, { timeZone });
    return true;
  } catch {
    return false;
  }
}

// Static list for backwards compatibility (uses current date)
export const TIME_ZONES = getTimeZonesForDate();

// Default to user's timezone, falling back to UTC
export const DEFAULT_TIME_ZONE = getUserTimeZone();
