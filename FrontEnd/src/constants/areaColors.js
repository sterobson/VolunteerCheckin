/**
 * Area color constants
 * Predefined color palette for area visualization
 */

export const AREA_COLORS = [
  { name: 'Charcoal', hex: '#2d3748' },
  { name: 'Dark Gray', hex: '#4a5568' },
  { name: 'Gray', hex: '#718096' },
  { name: 'Red', hex: '#f56565' },
  { name: 'Coral', hex: '#ff6b6b' },
  { name: 'Rose', hex: '#fb7185' },
  { name: 'Pink', hex: '#ed64a6' },
  { name: 'Magenta', hex: '#d946ef' },
  { name: 'Fuchsia', hex: '#e879f9' },
  { name: 'Purple', hex: '#9f7aea' },
  { name: 'Violet', hex: '#a78bfa' },
  { name: 'Lavender', hex: '#b197fc' },
  { name: 'Indigo', hex: '#5a67d8' },
  { name: 'Navy', hex: '#4263eb' },
  { name: 'Blue', hex: '#667eea' },
  { name: 'Sky', hex: '#0ea5e9' },
  { name: 'Cyan', hex: '#0bc5ea' },
  { name: 'Aqua', hex: '#22d3ee' },
  { name: 'Teal', hex: '#38b2ac' },
  { name: 'Emerald', hex: '#10b981' },
  { name: 'Green', hex: '#48bb78' },
  { name: 'Mint', hex: '#51cf66' },
  { name: 'Lime', hex: '#84cc16' },
  { name: 'Olive', hex: '#a3b18a' },
  { name: 'Yellow', hex: '#ecc94b' },
  { name: 'Amber', hex: '#f59e0b' },
  { name: 'Orange', hex: '#ed8936' },
  { name: 'Peach', hex: '#ff922b' },
  { name: 'Light Gray', hex: '#cbd5e0' },
  { name: 'Silver', hex: '#e2e8f0' },
  { name: 'Cream', hex: '#f7fafc' },
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
