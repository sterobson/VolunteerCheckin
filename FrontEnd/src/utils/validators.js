/**
 * Validation utilities
 * Centralized validation functions to avoid duplication
 */

/**
 * Validate what3words format
 * @param {string} what3Words - The what3words string to validate
 * @returns {boolean} True if valid or empty, false if invalid
 */
export const isValidWhat3Words = (what3Words) => {
  // Empty is valid (optional field)
  if (!what3Words || what3Words.trim() === '') {
    return true;
  }

  // Must be 3 words separated by dots or slashes
  const pattern = /^[a-z]{1,20}[./][a-z]{1,20}[./][a-z]{1,20}$/;
  if (!pattern.test(what3Words)) {
    return false;
  }

  // Must use either dots OR slashes, not both
  const hasDots = what3Words.includes('.');
  const hasSlashes = what3Words.includes('/');

  return hasDots !== hasSlashes; // XOR - one must be true, not both
};

/**
 * Validate email format
 * @param {string} email - The email to validate
 * @returns {boolean} True if valid
 */
export const isValidEmail = (email) => {
  if (!email) return false;
  const pattern = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
  return pattern.test(email);
};

/**
 * Validate phone number (basic)
 * @param {string} phone - The phone number to validate
 * @returns {boolean} True if valid or empty
 */
export const isValidPhone = (phone) => {
  if (!phone || phone.trim() === '') return true;
  // Basic validation - at least 10 digits
  const digitsOnly = phone.replace(/\D/g, '');
  return digitsOnly.length >= 10;
};

/**
 * Validate latitude
 * @param {number|string} lat - The latitude to validate
 * @returns {boolean} True if valid
 */
export const isValidLatitude = (lat) => {
  const latitude = parseFloat(lat);
  return !isNaN(latitude) && latitude >= -90 && latitude <= 90;
};

/**
 * Validate longitude
 * @param {number|string} lon - The longitude to validate
 * @returns {boolean} True if valid
 */
export const isValidLongitude = (lon) => {
  const longitude = parseFloat(lon);
  return !isNaN(longitude) && longitude >= -180 && longitude <= 180;
};

/**
 * Validate coordinate pair
 * @param {number|string} lat - The latitude
 * @param {number|string} lon - The longitude
 * @returns {boolean} True if both are valid
 */
export const isValidCoordinates = (lat, lon) => {
  return isValidLatitude(lat) && isValidLongitude(lon);
};

/**
 * Validate required field
 * @param {any} value - The value to check
 * @returns {boolean} True if not empty
 */
export const isRequired = (value) => {
  if (value === null || value === undefined) return false;
  if (typeof value === 'string') return value.trim() !== '';
  if (typeof value === 'number') return !isNaN(value);
  if (Array.isArray(value)) return value.length > 0;
  return true;
};

/**
 * Validate minimum length
 * @param {string} value - The string to check
 * @param {number} minLength - Minimum length required
 * @returns {boolean} True if meets minimum length
 */
export const hasMinLength = (value, minLength) => {
  if (!value) return false;
  return value.length >= minLength;
};

/**
 * Validate maximum length
 * @param {string} value - The string to check
 * @param {number} maxLength - Maximum length allowed
 * @returns {boolean} True if within maximum length
 */
export const hasMaxLength = (value, maxLength) => {
  if (!value) return true;
  return value.length <= maxLength;
};

/**
 * Validate numeric range
 * @param {number} value - The number to check
 * @param {number} min - Minimum value
 * @param {number} max - Maximum value
 * @returns {boolean} True if within range
 */
export const isInRange = (value, min, max) => {
  const num = parseFloat(value);
  if (isNaN(num)) return false;
  return num >= min && num <= max;
};

/**
 * Validate URL format
 * @param {string} url - The URL to validate
 * @returns {boolean} True if valid URL format
 */
export const isValidUrl = (url) => {
  if (!url) return false;
  try {
    new URL(url);
    return true;
  } catch {
    return false;
  }
};
