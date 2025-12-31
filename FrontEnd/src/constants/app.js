/**
 * Application-wide constants
 * Centralized configuration values and magic numbers
 */

// Check-in configuration
export const CHECK_IN_RADIUS_METERS = 25;

// Map configuration
export const MAP_DEFAULT_ZOOM = 13;
export const MAP_MIN_ZOOM = 3;
export const MAP_MAX_ZOOM = 18;
export const MAX_VISIBLE_MARKERS_FOR_DESCRIPTIONS = 10;

// Z-index layers (to prevent overlap issues)
export const Z_INDEX = {
  BASE: 1,
  MODAL_BACKDROP: 1000,
  MODAL: 1001,
  DROPDOWN: 1002,
  TOAST: 1003,
  TOOLTIP: 1004,
};

// Animation durations (milliseconds)
export const ANIMATION_DURATION = {
  FAST: 150,
  NORMAL: 300,
  SLOW: 500,
};

// Debounce/Throttle delays (milliseconds)
export const DEBOUNCE_DELAY = {
  SEARCH: 300,
  RESIZE: 150,
  SCROLL: 100,
};

// Pagination defaults
export const DEFAULT_PAGE_SIZE = 20;
export const PAGE_SIZE_OPTIONS = [10, 20, 50, 100];

// Coordinate precision
export const COORDINATE_DECIMAL_PLACES = 6;

// Distance thresholds (meters)
export const DISTANCE_THRESHOLD = {
  CLOSE: 10,
  NEARBY: 25,
  FAR: 100,
};

// File upload limits
export const MAX_FILE_SIZE_MB = 10;
export const MAX_FILE_SIZE_BYTES = MAX_FILE_SIZE_MB * 1024 * 1024;

// Allowed file types
export const ALLOWED_FILE_TYPES = {
  GPX: ['.gpx'],
  CSV: ['.csv'],
  IMAGES: ['.jpg', '.jpeg', '.png', '.gif'],
};

// API request timeouts (milliseconds)
export const REQUEST_TIMEOUT = {
  SHORT: 5000,
  NORMAL: 10000,
  LONG: 30000,
  UPLOAD: 60000,
};

// Retry configuration
export const MAX_RETRY_ATTEMPTS = 3;
export const RETRY_DELAY_MS = 1000;

// LocalStorage keys
export const STORAGE_KEYS = {
  AUTH_TOKEN: 'authToken',
  USER_PREFERENCES: 'userPreferences',
  LAST_EVENT_ID: 'lastEventId',
};

// Error messages
export const ERROR_MESSAGES = {
  NETWORK_ERROR: 'Network error. Please check your connection and try again.',
  UNAUTHORIZED: 'You are not authorized to perform this action.',
  NOT_FOUND: 'The requested resource was not found.',
  SERVER_ERROR: 'A server error occurred. Please try again later.',
  VALIDATION_ERROR: 'Please check your input and try again.',
  FILE_TOO_LARGE: `File size exceeds ${MAX_FILE_SIZE_MB}MB limit.`,
  INVALID_FILE_TYPE: 'Invalid file type.',
};

// Success messages
export const SUCCESS_MESSAGES = {
  CREATED: 'Successfully created.',
  UPDATED: 'Successfully updated.',
  DELETED: 'Successfully deleted.',
  SAVED: 'Changes saved successfully.',
};

// Date/Time formats
export const DATE_FORMATS = {
  SHORT: 'M/d/yyyy',
  LONG: 'MMMM d, yyyy',
  WITH_TIME: 'M/d/yyyy h:mm a',
  TIME_ONLY: 'h:mm a',
  ISO: 'yyyy-MM-dd',
  ISO_WITH_TIME: "yyyy-MM-dd'T'HH:mm",
};

// Validation limits
export const VALIDATION = {
  EVENT_NAME_MIN_LENGTH: 3,
  EVENT_NAME_MAX_LENGTH: 100,
  DESCRIPTION_MAX_LENGTH: 500,
  LOCATION_NAME_MAX_LENGTH: 100,
  MARSHAL_NAME_MAX_LENGTH: 100,
  PHONE_MIN_DIGITS: 10,
  WHAT3WORDS_MIN_LENGTH: 5,
  WHAT3WORDS_MAX_LENGTH: 64,
};
