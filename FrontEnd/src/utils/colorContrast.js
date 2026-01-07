/**
 * Color contrast utilities for auto-calculating text colors based on background luminance.
 */

/**
 * Calculates the relative luminance of a hex color.
 * Uses the formula from WCAG 2.0.
 * @param {string} hexColor - The hex color (e.g., '#667eea' or '667eea')
 * @returns {number} The luminance value between 0 and 1
 */
export function getLuminance(hexColor) {
  // Remove # if present
  const hex = hexColor.replace('#', '');

  // Parse RGB values
  const r = parseInt(hex.slice(0, 2), 16) / 255;
  const g = parseInt(hex.slice(2, 4), 16) / 255;
  const b = parseInt(hex.slice(4, 6), 16) / 255;

  // Apply gamma correction
  const rLinear = r <= 0.03928 ? r / 12.92 : Math.pow((r + 0.055) / 1.055, 2.4);
  const gLinear = g <= 0.03928 ? g / 12.92 : Math.pow((g + 0.055) / 1.055, 2.4);
  const bLinear = b <= 0.03928 ? b / 12.92 : Math.pow((b + 0.055) / 1.055, 2.4);

  // Calculate luminance using standard coefficients
  return 0.2126 * rLinear + 0.7152 * gLinear + 0.0722 * bLinear;
}

/**
 * Determines whether to use light or dark text on a given background color.
 * @param {string} hexColor - The background hex color
 * @returns {string} '#ffffff' for light text or '#333333' for dark text
 */
export function getContrastTextColor(hexColor) {
  if (!hexColor || hexColor === '') {
    return '#ffffff'; // Default to white text
  }

  const luminance = getLuminance(hexColor);

  // Use a threshold of 0.179 (based on WCAG contrast requirements)
  // Colors with luminance > 0.179 are considered "light" and need dark text
  return luminance > 0.179 ? '#333333' : '#ffffff';
}

/**
 * Gets the contrast text color for a gradient.
 * Uses the average luminance of both colors to ensure readability across the gradient.
 * @param {string} startColor - The gradient start color
 * @param {string} endColor - The gradient end color
 * @returns {string} '#ffffff' for light text or '#333333' for dark text
 */
export function getGradientContrastTextColor(startColor, endColor) {
  const start = startColor || DEFAULT_COLORS.headerGradientStart;
  const end = endColor || DEFAULT_COLORS.headerGradientEnd;

  const startLuminance = getLuminance(start);
  const endLuminance = getLuminance(end);

  // Use the average luminance of both colors
  const avgLuminance = (startLuminance + endLuminance) / 2;

  // Use a slightly lower threshold for gradients to prefer white text on colored backgrounds
  return avgLuminance > 0.35 ? '#333333' : '#ffffff';
}

/**
 * Checks if a color is valid hex format.
 * @param {string} color - The color to check
 * @returns {boolean} True if valid hex color
 */
export function isValidHexColor(color) {
  if (!color) return false;
  return /^#?[0-9A-Fa-f]{6}$/.test(color);
}

/**
 * Normalizes a hex color to include the # prefix.
 * @param {string} color - The color to normalize
 * @returns {string} The color with # prefix
 */
export function normalizeHexColor(color) {
  if (!color) return '';
  const hex = color.replace('#', '');
  return `#${hex}`;
}

// Default branding colors
export const DEFAULT_COLORS = {
  headerGradientStart: '#667eea',
  headerGradientEnd: '#764ba2',
  accentColor: '#667eea',
  pageGradientStart: '#667eea',
  pageGradientEnd: '#764ba2',
  emergencyButtonColor: '#ff4444',
};
