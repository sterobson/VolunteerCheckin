/**
 * Checkpoint Icon/Style Constants
 * Defines available checkpoint marker styles and SVG generators
 */

// Icon types available for checkpoint markers
export const CHECKPOINT_ICON_TYPES = [
  // Default - uses status-based colored circles (green/orange/red)
  { value: 'default', label: 'Default', colorizable: false },
  // Colorizable shapes
  { value: 'circle', label: 'Circle', colorizable: true },
  { value: 'square', label: 'Square', colorizable: true },
  { value: 'triangle', label: 'Triangle', colorizable: true },
  { value: 'diamond', label: 'Diamond', colorizable: true },
  { value: 'star', label: 'Star', colorizable: true },
  { value: 'hexagon', label: 'Hexagon', colorizable: true },
  { value: 'pentagon', label: 'Pentagon', colorizable: true },
  // Fixed icons with preset colors
  { value: 'water', label: 'Water', fixedColor: '#3B82F6' },
  { value: 'food', label: 'Food', fixedColor: '#F59E0B' },
  { value: 'medical', label: 'Medical', fixedColor: '#EF4444' },
  { value: 'photo', label: 'Photo', fixedColor: '#8B5CF6' },
  { value: 'music', label: 'Music', fixedColor: '#EC4899' },
  { value: 'start', label: 'Start', fixedColor: '#10B981' },
  { value: 'finish', label: 'Finish', fixedColor: '#1F2937' },
];

// Helper to get icon type config
export function getIconTypeConfig(type) {
  return CHECKPOINT_ICON_TYPES.find((t) => t.value === type) || CHECKPOINT_ICON_TYPES[0];
}

// Helper to check if a type is colorizable
export function isColorizable(type) {
  const config = getIconTypeConfig(type);
  return config.colorizable === true;
}

// Helper to get color for an icon type (fixed icons return their preset color)
export function getIconColor(type, customColor) {
  const config = getIconTypeConfig(type);
  if (config.fixedColor) {
    return config.fixedColor;
  }
  return customColor || '#667eea';
}

/**
 * SVG generators for colorizable shapes
 * Each takes a color and size parameter
 */
export const shapeSvgGenerators = {
  circle: (color, size = 32) => {
    const r = size / 2 - 2;
    return `<svg width="${size}" height="${size}" viewBox="0 0 ${size} ${size}" xmlns="http://www.w3.org/2000/svg">
      <circle cx="${size / 2}" cy="${size / 2}" r="${r}" fill="${color}" stroke="#fff" stroke-width="2"/>
    </svg>`;
  },

  square: (color, size = 32) => {
    const inset = 4;
    const cornerRadius = 4;
    return `<svg width="${size}" height="${size}" viewBox="0 0 ${size} ${size}" xmlns="http://www.w3.org/2000/svg">
      <rect x="${inset}" y="${inset}" width="${size - inset * 2}" height="${size - inset * 2}" rx="${cornerRadius}" fill="${color}" stroke="#fff" stroke-width="2"/>
    </svg>`;
  },

  triangle: (color, size = 32) => {
    const cx = size / 2;
    const top = 4;
    const bottom = size - 4;
    const left = 4;
    const right = size - 4;
    return `<svg width="${size}" height="${size}" viewBox="0 0 ${size} ${size}" xmlns="http://www.w3.org/2000/svg">
      <polygon points="${cx},${top} ${right},${bottom} ${left},${bottom}" fill="${color}" stroke="#fff" stroke-width="2" stroke-linejoin="round"/>
    </svg>`;
  },

  diamond: (color, size = 32) => {
    const cx = size / 2;
    const cy = size / 2;
    const d = size / 2 - 4;
    return `<svg width="${size}" height="${size}" viewBox="0 0 ${size} ${size}" xmlns="http://www.w3.org/2000/svg">
      <polygon points="${cx},${cy - d} ${cx + d},${cy} ${cx},${cy + d} ${cx - d},${cy}" fill="${color}" stroke="#fff" stroke-width="2" stroke-linejoin="round"/>
    </svg>`;
  },

  star: (color, size = 32) => {
    const cx = size / 2;
    const cy = size / 2;
    const outerR = size / 2 - 3;
    const innerR = outerR * 0.4;
    const points = [];
    for (let i = 0; i < 5; i++) {
      // Outer point
      const outerAngle = (Math.PI / 2) * -1 + (i * 2 * Math.PI) / 5;
      points.push(`${cx + outerR * Math.cos(outerAngle)},${cy + outerR * Math.sin(outerAngle)}`);
      // Inner point
      const innerAngle = outerAngle + Math.PI / 5;
      points.push(`${cx + innerR * Math.cos(innerAngle)},${cy + innerR * Math.sin(innerAngle)}`);
    }
    return `<svg width="${size}" height="${size}" viewBox="0 0 ${size} ${size}" xmlns="http://www.w3.org/2000/svg">
      <polygon points="${points.join(' ')}" fill="${color}" stroke="#fff" stroke-width="2" stroke-linejoin="round"/>
    </svg>`;
  },

  hexagon: (color, size = 32) => {
    const cx = size / 2;
    const cy = size / 2;
    const r = size / 2 - 4;
    const points = [];
    for (let i = 0; i < 6; i++) {
      const angle = (Math.PI / 6) + (i * Math.PI) / 3;
      points.push(`${cx + r * Math.cos(angle)},${cy + r * Math.sin(angle)}`);
    }
    return `<svg width="${size}" height="${size}" viewBox="0 0 ${size} ${size}" xmlns="http://www.w3.org/2000/svg">
      <polygon points="${points.join(' ')}" fill="${color}" stroke="#fff" stroke-width="2" stroke-linejoin="round"/>
    </svg>`;
  },

  pentagon: (color, size = 32) => {
    const cx = size / 2;
    const cy = size / 2;
    const r = size / 2 - 4;
    const points = [];
    for (let i = 0; i < 5; i++) {
      const angle = (Math.PI / 2) * -1 + (i * 2 * Math.PI) / 5;
      points.push(`${cx + r * Math.cos(angle)},${cy + r * Math.sin(angle)}`);
    }
    return `<svg width="${size}" height="${size}" viewBox="0 0 ${size} ${size}" xmlns="http://www.w3.org/2000/svg">
      <polygon points="${points.join(' ')}" fill="${color}" stroke="#fff" stroke-width="2" stroke-linejoin="round"/>
    </svg>`;
  },
};

/**
 * Fixed icon SVGs with preset colors
 */
export const fixedIconSvgs = {
  water: (size = 32) => {
    const color = '#3B82F6';
    return `<svg width="${size}" height="${size}" viewBox="0 0 32 32" xmlns="http://www.w3.org/2000/svg">
      <circle cx="16" cy="16" r="14" fill="${color}" stroke="#fff" stroke-width="2"/>
      <path d="M16 8 C16 8 10 14 10 18 C10 21.3 12.7 24 16 24 C19.3 24 22 21.3 22 18 C22 14 16 8 16 8Z" fill="#fff"/>
    </svg>`;
  },

  food: (size = 32) => {
    const color = '#F59E0B';
    return `<svg width="${size}" height="${size}" viewBox="0 0 32 32" xmlns="http://www.w3.org/2000/svg">
      <circle cx="16" cy="16" r="14" fill="${color}" stroke="#fff" stroke-width="2"/>
      <path d="M11 9v6c0 1.1.9 2 2 2h1v7h2v-7h1c1.1 0 2-.9 2-2V9h-2v5h-1V9h-2v5h-1V9h-2z" fill="#fff"/>
      <path d="M21 9v14h2V9h-2z" fill="#fff"/>
    </svg>`;
  },

  medical: (size = 32) => {
    const color = '#EF4444';
    return `<svg width="${size}" height="${size}" viewBox="0 0 32 32" xmlns="http://www.w3.org/2000/svg">
      <circle cx="16" cy="16" r="14" fill="${color}" stroke="#fff" stroke-width="2"/>
      <rect x="14" y="8" width="4" height="16" rx="1" fill="#fff"/>
      <rect x="8" y="14" width="16" height="4" rx="1" fill="#fff"/>
    </svg>`;
  },

  photo: (size = 32) => {
    const color = '#8B5CF6';
    return `<svg width="${size}" height="${size}" viewBox="0 0 32 32" xmlns="http://www.w3.org/2000/svg">
      <circle cx="16" cy="16" r="14" fill="${color}" stroke="#fff" stroke-width="2"/>
      <rect x="8" y="10" width="16" height="12" rx="2" fill="#fff"/>
      <circle cx="16" cy="16" r="3.5" fill="${color}"/>
      <circle cx="21" y="13" r="1" fill="${color}"/>
    </svg>`;
  },

  music: (size = 32) => {
    const color = '#EC4899';
    return `<svg width="${size}" height="${size}" viewBox="0 0 32 32" xmlns="http://www.w3.org/2000/svg">
      <circle cx="16" cy="16" r="14" fill="${color}" stroke="#fff" stroke-width="2"/>
      <path d="M12 22c1.66 0 3-1.34 3-3v-9h5v4h-3v5c0 1.66-1.34 3-3 3s-3-1.34-3-3 1.34-3 3-3c.35 0 .69.07 1 .18V10c0-.55.45-1 1-1h5c.55 0 1 .45 1 1v6h-5v5c0 2.76-2.24 5-5 5s-5-2.24-5-5 2.24-5 5-5c.7 0 1.37.15 2 .41" fill="#fff"/>
      <ellipse cx="12" cy="21" rx="2.5" ry="2" fill="#fff"/>
      <rect x="14" y="10" width="2" height="9" fill="#fff"/>
      <rect x="14" y="10" width="6" height="2" fill="#fff"/>
    </svg>`;
  },

  start: (size = 32) => {
    const color = '#10B981';
    return `<svg width="${size}" height="${size}" viewBox="0 0 32 32" xmlns="http://www.w3.org/2000/svg">
      <circle cx="16" cy="16" r="14" fill="${color}" stroke="#fff" stroke-width="2"/>
      <path d="M11 8v16l2-2V10l7 6-7 6v-4l-2 2v4l12-10-12-10v4z" fill="#fff"/>
    </svg>`;
  },

  finish: (size = 32) => {
    const color = '#1F2937';
    return `<svg width="${size}" height="${size}" viewBox="0 0 32 32" xmlns="http://www.w3.org/2000/svg">
      <circle cx="16" cy="16" r="14" fill="${color}" stroke="#fff" stroke-width="2"/>
      <rect x="10" y="8" width="4" height="4" fill="#fff"/>
      <rect x="14" y="12" width="4" height="4" fill="#fff"/>
      <rect x="18" y="8" width="4" height="4" fill="#fff"/>
      <rect x="10" y="16" width="4" height="4" fill="#fff"/>
      <rect x="18" y="16" width="4" height="4" fill="#fff"/>
      <rect x="14" y="20" width="4" height="4" fill="#fff"/>
    </svg>`;
  },
};

/**
 * Status badge SVGs for overlay on checkpoint markers
 */
export const statusBadgeSvgs = {
  // Green checkmark - fully checked in
  full: `<svg width="14" height="14" viewBox="0 0 14 14" xmlns="http://www.w3.org/2000/svg">
    <circle cx="7" cy="7" r="6" fill="#10B981" stroke="#fff" stroke-width="1.5"/>
    <path d="M4 7l2 2 4-4" stroke="#fff" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" fill="none"/>
  </svg>`,

  // Yellow exclamation - partial check-in
  partial: `<svg width="14" height="14" viewBox="0 0 14 14" xmlns="http://www.w3.org/2000/svg">
    <circle cx="7" cy="7" r="6" fill="#F59E0B" stroke="#fff" stroke-width="1.5"/>
    <rect x="6" y="3.5" width="2" height="4.5" rx="1" fill="#fff"/>
    <circle cx="7" cy="10" r="1" fill="#fff"/>
  </svg>`,

  // Red X - no check-ins
  missing: `<svg width="14" height="14" viewBox="0 0 14 14" xmlns="http://www.w3.org/2000/svg">
    <circle cx="7" cy="7" r="6" fill="#EF4444" stroke="#fff" stroke-width="1.5"/>
    <path d="M4.5 4.5l5 5M9.5 4.5l-5 5" stroke="#fff" stroke-width="2" stroke-linecap="round"/>
  </svg>`,
};

/**
 * Get the status type based on check-in count and required
 * @param {number} checkedIn - Number of checked-in marshals
 * @param {number} required - Number of required marshals
 * @returns {'full'|'partial'|'missing'} Status type
 */
export function getCheckpointStatus(checkedIn, required) {
  if (checkedIn === 0) return 'missing';
  if (checkedIn >= required) return 'full';
  return 'partial';
}

/**
 * Get status badge HTML for a checkpoint
 * @param {number} checkedIn - Number of checked-in marshals
 * @param {number} required - Number of required marshals
 * @returns {string} SVG HTML string
 */
export function getStatusBadgeHtml(checkedIn, required) {
  const status = getCheckpointStatus(checkedIn, required);
  return statusBadgeSvgs[status];
}

/**
 * Get the main checkpoint marker SVG
 * @param {string} type - Icon type (default, circle, water, etc.)
 * @param {string} color - Custom color (for colorizable shapes)
 * @param {number} size - Icon size in pixels
 * @returns {string} SVG HTML string
 */
export function getCheckpointIconSvg(type, color, size = 32) {
  // Default type uses status-based rendering (handled elsewhere)
  if (type === 'default') {
    return null;
  }

  // Fixed icons
  if (fixedIconSvgs[type]) {
    return fixedIconSvgs[type](size);
  }

  // Colorizable shapes
  if (shapeSvgGenerators[type]) {
    const effectiveColor = color || '#667eea';
    return shapeSvgGenerators[type](effectiveColor, size);
  }

  // Fallback to default
  return null;
}

// Shapes that should display the count text inside them
const SHAPES_WITH_COUNT = ['circle', 'square', 'hexagon', 'pentagon'];

/**
 * Check if a shape type should display the count text
 * @param {string} type - Icon type
 * @returns {boolean} True if the shape should show count text
 */
export function shouldShowCountInShape(type) {
  return SHAPES_WITH_COUNT.includes(type);
}

/**
 * Generate full checkpoint marker HTML with status badge overlay and count
 * @param {string} type - Icon type
 * @param {string} color - Custom color for colorizable shapes
 * @param {number} checkedIn - Number of checked-in marshals
 * @param {number} required - Number of required marshals
 * @param {number} size - Icon size in pixels
 * @param {boolean} showCount - Whether to show the count (default true, but only for suitable shapes)
 * @returns {string} HTML string with icon, count, and status badge
 */
export function getCheckpointMarkerHtml(type, color, checkedIn, required, size = 32, showCount = true) {
  const iconSvg = getCheckpointIconSvg(type, color, size);
  const badgeSvg = getStatusBadgeHtml(checkedIn, required);

  if (!iconSvg) {
    // Return null for default type - will be handled by existing status-based rendering
    return null;
  }

  // Only show count for shapes that have enough space (circle, square, hexagon, pentagon)
  const canShowCount = showCount && shouldShowCountInShape(type);

  // Determine the effective background color for text contrast
  const effectiveColor = getIconColor(type, color);
  const textColor = getContrastTextColor(effectiveColor);
  // For light backgrounds, add a subtle shadow; for dark, use standard shadow
  const textShadow = isLightColor(effectiveColor)
    ? '0 1px 2px rgba(255,255,255,0.5)'
    : '0 1px 2px rgba(0,0,0,0.5)';

  const fontSize = size >= 36 ? '12px' : '10px';
  const countHtml = canShowCount ? `
    <div style="
      position: absolute;
      top: 50%;
      left: 50%;
      transform: translate(-50%, -50%);
      color: ${textColor};
      font-weight: bold;
      font-size: ${fontSize};
      text-shadow: ${textShadow};
      pointer-events: none;
    ">${checkedIn}/${required}</div>
  ` : '';

  // Position badge in bottom-right corner, count centered
  return `
    <div style="position: relative; width: ${size}px; height: ${size}px;">
      ${iconSvg}
      ${countHtml}
      <div style="position: absolute; bottom: -4px; right: -4px;">
        ${badgeSvg}
      </div>
    </div>
  `;
}

/**
 * Get status-based color for default checkpoint rendering
 * @param {number} checkedIn - Number of checked-in marshals
 * @param {number} required - Number of required marshals
 * @returns {string} Hex color code
 */
export function getStatusColor(checkedIn, required) {
  const status = getCheckpointStatus(checkedIn, required);
  switch (status) {
    case 'full':
      return '#10B981'; // Green
    case 'partial':
      return '#F59E0B'; // Orange/Amber
    case 'missing':
      return '#EF4444'; // Red
    default:
      return '#6B7280'; // Gray
  }
}

/**
 * Calculate relative luminance of a hex color
 * @param {string} hexColor - Hex color code (with or without #)
 * @returns {number} Luminance value between 0 and 1
 */
function getLuminance(hexColor) {
  const hex = hexColor.replace('#', '');
  const r = parseInt(hex.substr(0, 2), 16) / 255;
  const g = parseInt(hex.substr(2, 2), 16) / 255;
  const b = parseInt(hex.substr(4, 2), 16) / 255;

  // Convert to linear values
  const rLin = r <= 0.03928 ? r / 12.92 : Math.pow((r + 0.055) / 1.055, 2.4);
  const gLin = g <= 0.03928 ? g / 12.92 : Math.pow((g + 0.055) / 1.055, 2.4);
  const bLin = b <= 0.03928 ? b / 12.92 : Math.pow((b + 0.055) / 1.055, 2.4);

  // Calculate luminance
  return 0.2126 * rLin + 0.7152 * gLin + 0.0722 * bLin;
}

/**
 * Determine if a color is light (should use dark text) or dark (should use light text)
 * @param {string} hexColor - Hex color code
 * @returns {boolean} True if the color is light
 */
export function isLightColor(hexColor) {
  if (!hexColor || hexColor.length < 6) return false;
  return getLuminance(hexColor) > 0.4;
}

/**
 * Get the best contrasting text color for a background
 * @param {string} bgColor - Background hex color
 * @returns {string} Either white or dark gray for good contrast
 */
export function getContrastTextColor(bgColor) {
  return isLightColor(bgColor) ? '#1F2937' : '#FFFFFF';
}
