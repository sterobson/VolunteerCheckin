/**
 * Area color constants
 * Predefined color palette for area visualization
 */

export const AREA_COLORS = [
  { name: 'Blue', hex: '#667eea' },
  { name: 'Green', hex: '#48bb78' },
  { name: 'Red', hex: '#f56565' },
  { name: 'Yellow', hex: '#ecc94b' },
  { name: 'Purple', hex: '#9f7aea' },
  { name: 'Orange', hex: '#ed8936' },
  { name: 'Teal', hex: '#38b2ac' },
  { name: 'Pink', hex: '#ed64a6' },
  { name: 'Indigo', hex: '#5a67d8' },
  { name: 'Cyan', hex: '#0bc5ea' },
];

export const DEFAULT_AREA_COLOR = '#667eea';

/**
 * Get the next available color from the palette
 * @param {Array} existingAreas - Array of existing areas with colors
 * @returns {string} Hex color code
 */
export function getNextAvailableColor(existingAreas) {
  const usedColors = existingAreas.map((a) => a.color);
  const availableColor = AREA_COLORS.find((c) => !usedColors.includes(c.hex));
  return availableColor?.hex || DEFAULT_AREA_COLOR;
}
