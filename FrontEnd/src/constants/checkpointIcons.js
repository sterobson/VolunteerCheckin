/**
 * Checkpoint Icon/Style Constants
 * Defines available checkpoint marker styles and SVG generators
 *
 * Icons are composed of:
 * - Background shape (circle, square, hexagon, pentagon, diamond, star, dot, none)
 * - Icon content (shape-only or content icon)
 * - Customizable colors (backgroundColor, borderColor, iconColor)
 * - Size (33%, 66%, 100%, 150%)
 */

// Available background shapes
export const BACKGROUND_SHAPES = [
  { value: 'circle', label: 'Circle' },
  { value: 'square', label: 'Square' },
  { value: 'hexagon', label: 'Hexagon' },
  { value: 'pentagon', label: 'Pentagon' },
  { value: 'diamond', label: 'Diamond' },
  { value: 'star', label: 'Star' },
  { value: 'dot', label: 'Dot' },
  { value: 'none', label: 'None' },
];

// Available sizes as percentages
export const ICON_SIZES = [
  { value: '33', label: 'Small (33%)' },
  { value: '66', label: 'Medium (66%)' },
  { value: '100', label: 'Normal (100%)' },
  { value: '150', label: 'Large (150%)' },
];

// Icon types available for checkpoint markers
// isShapeOnly: true means the icon IS the background (no separate content)
// category: 'shape' for shapes-only (available at event level), 'content' for content icons
export const CHECKPOINT_ICON_TYPES = [
  // Default - uses status-based colored circles (green/orange/red)
  { value: 'default', label: 'Default', isShapeOnly: false, category: 'default' },
  // Shape-only icons - the shape IS the icon
  { value: 'dot', label: 'Dot', isShapeOnly: true, category: 'shape', defaultColor: '#667eea' },
  { value: 'circle', label: 'Circle', isShapeOnly: true, category: 'shape', defaultColor: '#667eea' },
  { value: 'square', label: 'Square', isShapeOnly: true, category: 'shape', defaultColor: '#667eea' },
  { value: 'triangle', label: 'Triangle', isShapeOnly: true, category: 'shape', defaultColor: '#667eea' },
  { value: 'diamond', label: 'Diamond', isShapeOnly: true, category: 'shape', defaultColor: '#667eea' },
  { value: 'star', label: 'Star', isShapeOnly: true, category: 'shape', defaultColor: '#667eea' },
  { value: 'hexagon', label: 'Hexagon', isShapeOnly: true, category: 'shape', defaultColor: '#667eea' },
  { value: 'pentagon', label: 'Pentagon', isShapeOnly: true, category: 'shape', defaultColor: '#667eea' },
  // Content icons - have a symbol on top of background
  { value: 'car', label: 'Car', isShapeOnly: false, category: 'content', defaultColor: '#EF4444' },
  { value: 'bike', label: 'Bike', isShapeOnly: false, category: 'content', defaultColor: '#EF4444' },
  { value: 'water', label: 'Water', isShapeOnly: false, category: 'content', defaultColor: '#3B82F6' },
  { value: 'food', label: 'Food', isShapeOnly: false, category: 'content', defaultColor: '#F59E0B' },
  { value: 'medical', label: 'Medical', isShapeOnly: false, category: 'content', defaultColor: '#EF4444' },
  { value: 'photo', label: 'Photo', isShapeOnly: false, category: 'content', defaultColor: '#8B5CF6' },
  { value: 'music', label: 'Music', isShapeOnly: false, category: 'content', defaultColor: '#EC4899' },
  { value: 'start', label: 'Start', isShapeOnly: false, category: 'content', defaultColor: '#10B981' },
  { value: 'finish', label: 'Finish', isShapeOnly: false, category: 'content', defaultColor: '#1F2937' },
  { value: 'toilet', label: 'Toilet', isShapeOnly: false, category: 'content', defaultColor: '#6366F1' },
  { value: 'shower', label: 'Shower', isShapeOnly: false, category: 'content', defaultColor: '#06B6D4' },
  { value: 'bus', label: 'Bus', isShapeOnly: false, category: 'content', defaultColor: '#F97316' },
  { value: 'info', label: 'Information', isShapeOnly: false, category: 'content', defaultColor: '#0EA5E9' },
  { value: 'merch', label: 'Merchandise', isShapeOnly: false, category: 'content', defaultColor: '#A855F7' },
  { value: 'cone', label: 'Road closure', isShapeOnly: false, category: 'content', defaultColor: '#FF6B00' },
  { value: 'traffic', label: 'Traffic management', isShapeOnly: false, category: 'content', defaultColor: '#374151' },
  // Directional arrows
  { value: 'arrow-uturn-left', label: 'Left u-turn', isShapeOnly: false, category: 'content', defaultColor: '#3B82F6' },
  { value: 'arrow-left', label: 'Left turn', isShapeOnly: false, category: 'content', defaultColor: '#3B82F6' },
  { value: 'arrow-slight-left', label: 'Slight left', isShapeOnly: false, category: 'content', defaultColor: '#3B82F6' },
  { value: 'arrow-straight', label: 'Straight ahead', isShapeOnly: false, category: 'content', defaultColor: '#3B82F6' },
  { value: 'arrow-slight-right', label: 'Slight right', isShapeOnly: false, category: 'content', defaultColor: '#3B82F6' },
  { value: 'arrow-right', label: 'Right turn', isShapeOnly: false, category: 'content', defaultColor: '#3B82F6' },
  { value: 'arrow-uturn-right', label: 'Right u-turn', isShapeOnly: false, category: 'content', defaultColor: '#3B82F6' },
  { value: 'arrow-fork-left', label: 'Left fork', isShapeOnly: false, category: 'content', defaultColor: '#3B82F6' },
  { value: 'arrow-fork-right', label: 'Right fork', isShapeOnly: false, category: 'content', defaultColor: '#3B82F6' },
  { value: 'arrow-keep-left', label: 'Keep left', isShapeOnly: false, category: 'content', defaultColor: '#3B82F6' },
  { value: 'arrow-keep-right', label: 'Keep right', isShapeOnly: false, category: 'content', defaultColor: '#3B82F6' },
];

// Get icon types for a specific level (event only gets shapes, area/checkpoint get all)
export function getIconTypesForLevel(level) {
  if (level === 'event') {
    return CHECKPOINT_ICON_TYPES.filter(t => t.category === 'default' || t.category === 'shape');
  }
  return CHECKPOINT_ICON_TYPES;
}

// Helper to get icon type config
export function getIconTypeConfig(type) {
  return CHECKPOINT_ICON_TYPES.find((t) => t.value === type) || CHECKPOINT_ICON_TYPES[0];
}

// Helper to check if a type is a shape-only icon (no separate content)
export function isShapeOnlyIcon(type) {
  const config = getIconTypeConfig(type);
  return config.isShapeOnly === true;
}

// Helper to check if a type is colorizable (all icons are now colorizable)
export function isColorizable(type) {
  return type !== 'default';
}

// Helper to get the default color for an icon type
export function getIconDefaultColor(type) {
  const config = getIconTypeConfig(type);
  return config.defaultColor || '#667eea';
}

// Helper to get effective color for an icon type
export function getIconColor(type, customColor) {
  if (customColor) {
    return customColor;
  }
  return getIconDefaultColor(type);
}

/**
 * SVG generators for colorizable shapes
 * Each takes a color and size parameter
 */
export const shapeSvgGenerators = {
  dot: (color, size = 32) => {
    // Dot is 33% the size of a circle (radius is 1/3)
    const r = (size / 2 - 2) * 0.33;
    return `<svg width="${size}" height="${size}" viewBox="0 0 ${size} ${size}" xmlns="http://www.w3.org/2000/svg">
      <circle cx="${size / 2}" cy="${size / 2}" r="${r}" fill="${color}" stroke="#fff" stroke-width="1.5"/>
    </svg>`;
  },

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

  car: (color, size = 32) => {
    // Car body is the chosen color, tires are always black
    return `<svg width="${size}" height="${size}" viewBox="0 0 32 32" xmlns="http://www.w3.org/2000/svg">
      <circle cx="16" cy="16" r="14" fill="${color}" stroke="#fff" stroke-width="2"/>
      <!-- Car body -->
      <rect x="6" y="13" width="20" height="8" rx="2" fill="#fff"/>
      <!-- Car roof/cabin -->
      <path d="M10 13 L12 9 L20 9 L22 13" fill="#fff" stroke="#fff" stroke-width="1"/>
      <!-- Windows -->
      <path d="M11 12 L12.5 9.5 L15 9.5 L15 12 Z" fill="${color}"/>
      <path d="M17 12 L17 9.5 L19.5 9.5 L21 12 Z" fill="${color}"/>
      <!-- Tires (always black) -->
      <circle cx="10" cy="21" r="2.5" fill="#1F2937" stroke="#fff" stroke-width="0.5"/>
      <circle cx="22" cy="21" r="2.5" fill="#1F2937" stroke="#fff" stroke-width="0.5"/>
      <!-- Wheel centers -->
      <circle cx="10" cy="21" r="1" fill="#9CA3AF"/>
      <circle cx="22" cy="21" r="1" fill="#9CA3AF"/>
    </svg>`;
  },

  bike: (color, size = 32) => {
    // Bike is entirely the chosen color (frame and tires)
    return `<svg width="${size}" height="${size}" viewBox="0 0 32 32" xmlns="http://www.w3.org/2000/svg">
      <circle cx="16" cy="16" r="14" fill="${color}" stroke="#fff" stroke-width="2"/>
      <!-- Tires (same color as bike) -->
      <circle cx="9" cy="20" r="4" fill="none" stroke="#fff" stroke-width="2"/>
      <circle cx="23" cy="20" r="4" fill="none" stroke="#fff" stroke-width="2"/>
      <!-- Wheel centers -->
      <circle cx="9" cy="20" r="1" fill="#fff"/>
      <circle cx="23" cy="20" r="1" fill="#fff"/>
      <!-- Frame -->
      <path d="M9 20 L16 12 L23 20 M16 12 L16 20 L9 20" fill="none" stroke="#fff" stroke-width="2" stroke-linejoin="round"/>
      <!-- Handlebars -->
      <path d="M16 12 L18 10 M16 12 L14 10" fill="none" stroke="#fff" stroke-width="1.5" stroke-linecap="round"/>
      <!-- Seat -->
      <ellipse cx="12" cy="13" rx="2" ry="1" fill="#fff"/>
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

  toilet: (size = 32) => {
    const color = '#6366F1';
    return `<svg width="${size}" height="${size}" viewBox="0 0 32 32" xmlns="http://www.w3.org/2000/svg">
      <circle cx="16" cy="16" r="14" fill="${color}" stroke="#fff" stroke-width="2"/>
      <rect x="11" y="7" width="10" height="3" rx="1" fill="#fff"/>
      <ellipse cx="16" cy="15" rx="6" ry="5" fill="#fff"/>
      <ellipse cx="16" cy="15" rx="4" ry="3" fill="${color}"/>
      <path d="M12 19 Q16 24 20 19" stroke="#fff" stroke-width="2" fill="none" stroke-linecap="round"/>
    </svg>`;
  },

  shower: (size = 32) => {
    const color = '#06B6D4';
    return `<svg width="${size}" height="${size}" viewBox="0 0 32 32" xmlns="http://www.w3.org/2000/svg">
      <circle cx="16" cy="16" r="14" fill="${color}" stroke="#fff" stroke-width="2"/>
      <circle cx="16" cy="10" r="3" fill="#fff"/>
      <path d="M11 14h10l-1 4h-8l-1-4z" fill="#fff"/>
      <line x1="12" y1="19" x2="12" y2="23" stroke="#fff" stroke-width="1.5" stroke-linecap="round"/>
      <line x1="14.5" y1="19" x2="14.5" y2="24" stroke="#fff" stroke-width="1.5" stroke-linecap="round"/>
      <line x1="17.5" y1="19" x2="17.5" y2="24" stroke="#fff" stroke-width="1.5" stroke-linecap="round"/>
      <line x1="20" y1="19" x2="20" y2="23" stroke="#fff" stroke-width="1.5" stroke-linecap="round"/>
    </svg>`;
  },

  bus: (size = 32) => {
    const color = '#F97316';
    return `<svg width="${size}" height="${size}" viewBox="0 0 32 32" xmlns="http://www.w3.org/2000/svg">
      <circle cx="16" cy="16" r="14" fill="${color}" stroke="#fff" stroke-width="2"/>
      <rect x="9" y="9" width="14" height="12" rx="2" fill="#fff"/>
      <rect x="10" y="11" width="5" height="4" rx="0.5" fill="${color}"/>
      <rect x="17" y="11" width="5" height="4" rx="0.5" fill="${color}"/>
      <rect x="9" y="17" width="14" height="2" fill="#fff"/>
      <circle cx="12" cy="21" r="1.5" fill="#fff"/>
      <circle cx="20" cy="21" r="1.5" fill="#fff"/>
    </svg>`;
  },

  info: (size = 32) => {
    const color = '#0EA5E9';
    return `<svg width="${size}" height="${size}" viewBox="0 0 32 32" xmlns="http://www.w3.org/2000/svg">
      <circle cx="16" cy="16" r="14" fill="${color}" stroke="#fff" stroke-width="2"/>
      <circle cx="16" cy="10" r="2" fill="#fff"/>
      <rect x="14" y="14" width="4" height="10" rx="1" fill="#fff"/>
    </svg>`;
  },

  merch: (size = 32) => {
    const color = '#A855F7';
    return `<svg width="${size}" height="${size}" viewBox="0 0 32 32" xmlns="http://www.w3.org/2000/svg">
      <circle cx="16" cy="16" r="14" fill="${color}" stroke="#fff" stroke-width="2"/>
      <path d="M10 10 L13 10 L16 13 L19 10 L22 10 L22 14 L20 14 L20 24 L12 24 L12 14 L10 14 Z" fill="#fff"/>
      <path d="M13 10 L16 7 L19 10" stroke="#fff" stroke-width="1.5" fill="none" stroke-linecap="round" stroke-linejoin="round"/>
    </svg>`;
  },

  cone: (size = 32) => {
    const color = '#FF6B00';
    return `<svg width="${size}" height="${size}" viewBox="0 0 32 32" xmlns="http://www.w3.org/2000/svg">
      <circle cx="16" cy="16" r="14" fill="${color}" stroke="#fff" stroke-width="2"/>
      <path d="M16 6 L21 22 H11 L16 6Z" fill="#fff"/>
      <rect x="11" y="17" width="10" height="2" fill="${color}"/>
      <rect x="12" y="12" width="8" height="2" fill="${color}"/>
      <rect x="9" y="22" width="14" height="3" rx="1" fill="#fff"/>
    </svg>`;
  },

  traffic: (size = 32) => {
    const color = '#374151';
    return `<svg width="${size}" height="${size}" viewBox="0 0 32 32" xmlns="http://www.w3.org/2000/svg">
      <circle cx="16" cy="16" r="14" fill="${color}" stroke="#fff" stroke-width="2"/>
      <rect x="12" y="6" width="8" height="20" rx="2" fill="#fff"/>
      <circle cx="16" cy="10" r="2" fill="#EF4444"/>
      <circle cx="16" cy="16" r="2" fill="#F59E0B"/>
      <circle cx="16" cy="22" r="2" fill="#10B981"/>
    </svg>`;
  },

  // Directional arrows
  'arrow-uturn-left': (size = 32) => {
    const color = '#3B82F6';
    // Arrow goes straight up, then u-turns to the left
    return `<svg width="${size}" height="${size}" viewBox="0 0 32 32" xmlns="http://www.w3.org/2000/svg">
      <circle cx="16" cy="16" r="14" fill="${color}" stroke="#fff" stroke-width="2"/>
      <!-- U-turn path: up on right, curve left, down on left -->
      <path d="M20 24 L20 11 C20 7 12 7 12 11 L12 18" fill="none" stroke="#fff" stroke-width="4" stroke-linecap="round" stroke-linejoin="round"/>
      <!-- Arrow head pointing down on left side -->
      <path d="M7 15 L12 21 L17 15" fill="#fff" stroke="#fff" stroke-width="1" stroke-linejoin="round"/>
    </svg>`;
  },

  'arrow-left': (size = 32) => {
    const color = '#3B82F6';
    return `<svg width="${size}" height="${size}" viewBox="0 0 32 32" xmlns="http://www.w3.org/2000/svg">
      <circle cx="16" cy="16" r="14" fill="${color}" stroke="#fff" stroke-width="2"/>
      <path d="M14 8 L6 16 L14 24 L14 19 L26 19 L26 13 L14 13 L14 8Z" fill="#fff"/>
    </svg>`;
  },

  'arrow-slight-left': (size = 32) => {
    const color = '#3B82F6';
    // Upward arrow rotated 30 degrees to the left
    return `<svg width="${size}" height="${size}" viewBox="0 0 32 32" xmlns="http://www.w3.org/2000/svg">
      <circle cx="16" cy="16" r="14" fill="${color}" stroke="#fff" stroke-width="2"/>
      <g transform="rotate(-30 16 16)">
        <path d="M16 6 L8 14 L13 14 L13 26 L19 26 L19 14 L24 14 L16 6Z" fill="#fff"/>
      </g>
    </svg>`;
  },

  'arrow-straight': (size = 32) => {
    const color = '#3B82F6';
    return `<svg width="${size}" height="${size}" viewBox="0 0 32 32" xmlns="http://www.w3.org/2000/svg">
      <circle cx="16" cy="16" r="14" fill="${color}" stroke="#fff" stroke-width="2"/>
      <path d="M16 6 L8 14 L13 14 L13 26 L19 26 L19 14 L24 14 L16 6Z" fill="#fff"/>
    </svg>`;
  },

  'arrow-slight-right': (size = 32) => {
    const color = '#3B82F6';
    // Upward arrow rotated 30 degrees to the right
    return `<svg width="${size}" height="${size}" viewBox="0 0 32 32" xmlns="http://www.w3.org/2000/svg">
      <circle cx="16" cy="16" r="14" fill="${color}" stroke="#fff" stroke-width="2"/>
      <g transform="rotate(30 16 16)">
        <path d="M16 6 L8 14 L13 14 L13 26 L19 26 L19 14 L24 14 L16 6Z" fill="#fff"/>
      </g>
    </svg>`;
  },

  'arrow-right': (size = 32) => {
    const color = '#3B82F6';
    return `<svg width="${size}" height="${size}" viewBox="0 0 32 32" xmlns="http://www.w3.org/2000/svg">
      <circle cx="16" cy="16" r="14" fill="${color}" stroke="#fff" stroke-width="2"/>
      <path d="M18 8 L26 16 L18 24 L18 19 L6 19 L6 13 L18 13 L18 8Z" fill="#fff"/>
    </svg>`;
  },

  'arrow-uturn-right': (size = 32) => {
    const color = '#3B82F6';
    // Arrow goes straight up, then u-turns to the right
    return `<svg width="${size}" height="${size}" viewBox="0 0 32 32" xmlns="http://www.w3.org/2000/svg">
      <circle cx="16" cy="16" r="14" fill="${color}" stroke="#fff" stroke-width="2"/>
      <!-- U-turn path: up on left, curve right, down on right -->
      <path d="M12 24 L12 11 C12 7 20 7 20 11 L20 18" fill="none" stroke="#fff" stroke-width="4" stroke-linecap="round" stroke-linejoin="round"/>
      <!-- Arrow head pointing down on right side -->
      <path d="M15 15 L20 21 L25 15" fill="#fff" stroke="#fff" stroke-width="1" stroke-linejoin="round"/>
    </svg>`;
  },

  'arrow-fork-left': (size = 32) => {
    const color = '#3B82F6';
    // Fork in the road - left path is correct (arrow), right path is X'd out
    return `<svg width="${size}" height="${size}" viewBox="0 0 32 32" xmlns="http://www.w3.org/2000/svg">
      <circle cx="16" cy="16" r="14" fill="${color}" stroke="#fff" stroke-width="2"/>
      <!-- Main stem coming from bottom -->
      <rect x="14" y="18" width="4" height="8" fill="#fff"/>
      <!-- Left fork (correct way - with arrow) -->
      <path d="M16 18 L10 12" stroke="#fff" stroke-width="4" stroke-linecap="round"/>
      <path d="M6 15 L10 9 L14 13" fill="#fff" stroke="#fff" stroke-width="1" stroke-linejoin="round"/>
      <!-- Right fork (wrong way - with X) -->
      <path d="M16 18 L22 12" stroke="#fff" stroke-width="3" stroke-linecap="round" opacity="0.5"/>
      <path d="M20 8 L24 12 M24 8 L20 12" stroke="#EF4444" stroke-width="2" stroke-linecap="round"/>
    </svg>`;
  },

  'arrow-fork-right': (size = 32) => {
    const color = '#3B82F6';
    // Fork in the road - right path is correct (arrow), left path is X'd out
    return `<svg width="${size}" height="${size}" viewBox="0 0 32 32" xmlns="http://www.w3.org/2000/svg">
      <circle cx="16" cy="16" r="14" fill="${color}" stroke="#fff" stroke-width="2"/>
      <!-- Main stem coming from bottom -->
      <rect x="14" y="18" width="4" height="8" fill="#fff"/>
      <!-- Left fork (wrong way - with X) -->
      <path d="M16 18 L10 12" stroke="#fff" stroke-width="3" stroke-linecap="round" opacity="0.5"/>
      <path d="M8 8 L12 12 M12 8 L8 12" stroke="#EF4444" stroke-width="2" stroke-linecap="round"/>
      <!-- Right fork (correct way - with arrow) -->
      <path d="M16 18 L22 12" stroke="#fff" stroke-width="4" stroke-linecap="round"/>
      <path d="M18 13 L22 9 L26 15" fill="#fff" stroke="#fff" stroke-width="1" stroke-linejoin="round"/>
    </svg>`;
  },

  'arrow-keep-left': (size = 32) => {
    const color = '#3B82F6';
    // Upward arrow rotated 150 degrees to the left (pointing back-left)
    return `<svg width="${size}" height="${size}" viewBox="0 0 32 32" xmlns="http://www.w3.org/2000/svg">
      <circle cx="16" cy="16" r="14" fill="${color}" stroke="#fff" stroke-width="2"/>
      <g transform="rotate(-150 16 16)">
        <path d="M16 6 L8 14 L13 14 L13 26 L19 26 L19 14 L24 14 L16 6Z" fill="#fff"/>
      </g>
    </svg>`;
  },

  'arrow-keep-right': (size = 32) => {
    const color = '#3B82F6';
    // Upward arrow rotated 150 degrees to the right (pointing back-right)
    return `<svg width="${size}" height="${size}" viewBox="0 0 32 32" xmlns="http://www.w3.org/2000/svg">
      <circle cx="16" cy="16" r="14" fill="${color}" stroke="#fff" stroke-width="2"/>
      <g transform="rotate(150 16 16)">
        <path d="M16 6 L8 14 L13 14 L13 26 L19 26 L19 14 L24 14 L16 6Z" fill="#fff"/>
      </g>
    </svg>`;
  },
};

/**
 * Background shape generators for composable icons
 * Each returns SVG content for a background shape with customizable colors
 * All work within a 32x32 viewBox
 */
export const backgroundShapeGenerators = {
  circle: ({ backgroundColor, borderColor }) => {
    const stroke = borderColor === 'none' ? 'none' : (borderColor || '#fff');
    const strokeWidth = borderColor === 'none' ? 0 : 2;
    return `<circle cx="16" cy="16" r="14" fill="${backgroundColor}" stroke="${stroke}" stroke-width="${strokeWidth}"/>`;
  },

  square: ({ backgroundColor, borderColor }) => {
    const stroke = borderColor === 'none' ? 'none' : (borderColor || '#fff');
    const strokeWidth = borderColor === 'none' ? 0 : 2;
    return `<rect x="4" y="4" width="24" height="24" rx="4" fill="${backgroundColor}" stroke="${stroke}" stroke-width="${strokeWidth}"/>`;
  },

  hexagon: ({ backgroundColor, borderColor }) => {
    const stroke = borderColor === 'none' ? 'none' : (borderColor || '#fff');
    const strokeWidth = borderColor === 'none' ? 0 : 2;
    const points = [];
    for (let i = 0; i < 6; i++) {
      const angle = (Math.PI / 6) + (i * Math.PI) / 3;
      points.push(`${16 + 12 * Math.cos(angle)},${16 + 12 * Math.sin(angle)}`);
    }
    return `<polygon points="${points.join(' ')}" fill="${backgroundColor}" stroke="${stroke}" stroke-width="${strokeWidth}" stroke-linejoin="round"/>`;
  },

  pentagon: ({ backgroundColor, borderColor }) => {
    const stroke = borderColor === 'none' ? 'none' : (borderColor || '#fff');
    const strokeWidth = borderColor === 'none' ? 0 : 2;
    const points = [];
    for (let i = 0; i < 5; i++) {
      const angle = (Math.PI / 2) * -1 + (i * 2 * Math.PI) / 5;
      points.push(`${16 + 12 * Math.cos(angle)},${16 + 12 * Math.sin(angle)}`);
    }
    return `<polygon points="${points.join(' ')}" fill="${backgroundColor}" stroke="${stroke}" stroke-width="${strokeWidth}" stroke-linejoin="round"/>`;
  },

  diamond: ({ backgroundColor, borderColor }) => {
    const stroke = borderColor === 'none' ? 'none' : (borderColor || '#fff');
    const strokeWidth = borderColor === 'none' ? 0 : 2;
    return `<polygon points="16,4 28,16 16,28 4,16" fill="${backgroundColor}" stroke="${stroke}" stroke-width="${strokeWidth}" stroke-linejoin="round"/>`;
  },

  star: ({ backgroundColor, borderColor }) => {
    const stroke = borderColor === 'none' ? 'none' : (borderColor || '#fff');
    const strokeWidth = borderColor === 'none' ? 0 : 2;
    const outerR = 13;
    const innerR = outerR * 0.4;
    const points = [];
    for (let i = 0; i < 5; i++) {
      const outerAngle = (Math.PI / 2) * -1 + (i * 2 * Math.PI) / 5;
      points.push(`${16 + outerR * Math.cos(outerAngle)},${16 + outerR * Math.sin(outerAngle)}`);
      const innerAngle = outerAngle + Math.PI / 5;
      points.push(`${16 + innerR * Math.cos(innerAngle)},${16 + innerR * Math.sin(innerAngle)}`);
    }
    return `<polygon points="${points.join(' ')}" fill="${backgroundColor}" stroke="${stroke}" stroke-width="${strokeWidth}" stroke-linejoin="round"/>`;
  },

  dot: ({ backgroundColor, borderColor }) => {
    const stroke = borderColor === 'none' ? 'none' : (borderColor || '#fff');
    const strokeWidth = borderColor === 'none' ? 0 : 1.5;
    // Dot is 33% the size of circle (radius ~4-5)
    return `<circle cx="16" cy="16" r="5" fill="${backgroundColor}" stroke="${stroke}" stroke-width="${strokeWidth}"/>`;
  },

  none: () => '',
};

/**
 * Icon content generators - just the symbol/content without background
 * All work within a 32x32 viewBox, designed to overlay on a background shape
 * @param {string} iconColor - Color for the icon content (default white)
 */
export const iconContentGenerators = {
  water: (iconColor = '#fff') => {
    return `<path d="M16 8 C16 8 10 14 10 18 C10 21.3 12.7 24 16 24 C19.3 24 22 21.3 22 18 C22 14 16 8 16 8Z" fill="${iconColor}"/>`;
  },

  food: (iconColor = '#fff') => {
    return `<path d="M11 9v6c0 1.1.9 2 2 2h1v7h2v-7h1c1.1 0 2-.9 2-2V9h-2v5h-1V9h-2v5h-1V9h-2z" fill="${iconColor}"/>
      <path d="M21 9v14h2V9h-2z" fill="${iconColor}"/>`;
  },

  medical: (iconColor = '#fff') => {
    return `<rect x="14" y="8" width="4" height="16" rx="1" fill="${iconColor}"/>
      <rect x="8" y="14" width="16" height="4" rx="1" fill="${iconColor}"/>`;
  },

  photo: (iconColor = '#fff', bgColor = '#667eea') => {
    return `<rect x="8" y="10" width="16" height="12" rx="2" fill="${iconColor}"/>
      <circle cx="16" cy="16" r="3.5" fill="${bgColor}"/>
      <circle cx="21" cy="13" r="1" fill="${bgColor}"/>`;
  },

  music: (iconColor = '#fff') => {
    return `<ellipse cx="12" cy="21" rx="2.5" ry="2" fill="${iconColor}"/>
      <rect x="14" y="10" width="2" height="9" fill="${iconColor}"/>
      <rect x="14" y="10" width="6" height="2" fill="${iconColor}"/>`;
  },

  start: (iconColor = '#fff') => {
    return `<path d="M11 8v16l2-2V10l7 6-7 6v-4l-2 2v4l12-10-12-10v4z" fill="${iconColor}"/>`;
  },

  finish: (iconColor = '#fff') => {
    return `<rect x="10" y="8" width="4" height="4" fill="${iconColor}"/>
      <rect x="14" y="12" width="4" height="4" fill="${iconColor}"/>
      <rect x="18" y="8" width="4" height="4" fill="${iconColor}"/>
      <rect x="10" y="16" width="4" height="4" fill="${iconColor}"/>
      <rect x="18" y="16" width="4" height="4" fill="${iconColor}"/>
      <rect x="14" y="20" width="4" height="4" fill="${iconColor}"/>`;
  },

  toilet: (iconColor = '#fff', bgColor = '#667eea') => {
    return `<rect x="11" y="7" width="10" height="3" rx="1" fill="${iconColor}"/>
      <ellipse cx="16" cy="15" rx="6" ry="5" fill="${iconColor}"/>
      <ellipse cx="16" cy="15" rx="4" ry="3" fill="${bgColor}"/>
      <path d="M12 19 Q16 24 20 19" stroke="${iconColor}" stroke-width="2" fill="none" stroke-linecap="round"/>`;
  },

  shower: (iconColor = '#fff') => {
    return `<circle cx="16" cy="10" r="3" fill="${iconColor}"/>
      <path d="M11 14h10l-1 4h-8l-1-4z" fill="${iconColor}"/>
      <line x1="12" y1="19" x2="12" y2="23" stroke="${iconColor}" stroke-width="1.5" stroke-linecap="round"/>
      <line x1="14.5" y1="19" x2="14.5" y2="24" stroke="${iconColor}" stroke-width="1.5" stroke-linecap="round"/>
      <line x1="17.5" y1="19" x2="17.5" y2="24" stroke="${iconColor}" stroke-width="1.5" stroke-linecap="round"/>
      <line x1="20" y1="19" x2="20" y2="23" stroke="${iconColor}" stroke-width="1.5" stroke-linecap="round"/>`;
  },

  bus: (iconColor = '#fff', bgColor = '#667eea') => {
    return `<rect x="9" y="9" width="14" height="12" rx="2" fill="${iconColor}"/>
      <rect x="10" y="11" width="5" height="4" rx="0.5" fill="${bgColor}"/>
      <rect x="17" y="11" width="5" height="4" rx="0.5" fill="${bgColor}"/>
      <rect x="9" y="17" width="14" height="2" fill="${iconColor}"/>
      <circle cx="12" cy="21" r="1.5" fill="${iconColor}"/>
      <circle cx="20" cy="21" r="1.5" fill="${iconColor}"/>`;
  },

  info: (iconColor = '#fff') => {
    return `<circle cx="16" cy="10" r="2" fill="${iconColor}"/>
      <rect x="14" y="14" width="4" height="10" rx="1" fill="${iconColor}"/>`;
  },

  merch: (iconColor = '#fff') => {
    return `<path d="M10 10 L13 10 L16 13 L19 10 L22 10 L22 14 L20 14 L20 24 L12 24 L12 14 L10 14 Z" fill="${iconColor}"/>
      <path d="M13 10 L16 7 L19 10" stroke="${iconColor}" stroke-width="1.5" fill="none" stroke-linecap="round" stroke-linejoin="round"/>`;
  },

  cone: (iconColor = '#fff', bgColor = '#667eea') => {
    return `<path d="M16 6 L21 22 H11 L16 6Z" fill="${iconColor}"/>
      <rect x="11" y="17" width="10" height="2" fill="${bgColor}"/>
      <rect x="12" y="12" width="8" height="2" fill="${bgColor}"/>
      <rect x="9" y="22" width="14" height="3" rx="1" fill="${iconColor}"/>`;
  },

  traffic: (iconColor = '#fff') => {
    return `<rect x="12" y="6" width="8" height="20" rx="2" fill="${iconColor}"/>
      <circle cx="16" cy="10" r="2" fill="#EF4444"/>
      <circle cx="16" cy="16" r="2" fill="#F59E0B"/>
      <circle cx="16" cy="22" r="2" fill="#10B981"/>`;
  },

  car: (iconColor = '#fff', bgColor = '#667eea') => {
    return `<rect x="6" y="13" width="20" height="8" rx="2" fill="${iconColor}"/>
      <path d="M10 13 L12 9 L20 9 L22 13" fill="${iconColor}" stroke="${iconColor}" stroke-width="1"/>
      <path d="M11 12 L12.5 9.5 L15 9.5 L15 12 Z" fill="${bgColor}"/>
      <path d="M17 12 L17 9.5 L19.5 9.5 L21 12 Z" fill="${bgColor}"/>
      <circle cx="10" cy="21" r="2.5" fill="#1F2937" stroke="${iconColor}" stroke-width="0.5"/>
      <circle cx="22" cy="21" r="2.5" fill="#1F2937" stroke="${iconColor}" stroke-width="0.5"/>
      <circle cx="10" cy="21" r="1" fill="#9CA3AF"/>
      <circle cx="22" cy="21" r="1" fill="#9CA3AF"/>`;
  },

  bike: (iconColor = '#fff') => {
    return `<circle cx="9" cy="20" r="4" fill="none" stroke="${iconColor}" stroke-width="2"/>
      <circle cx="23" cy="20" r="4" fill="none" stroke="${iconColor}" stroke-width="2"/>
      <circle cx="9" cy="20" r="1" fill="${iconColor}"/>
      <circle cx="23" cy="20" r="1" fill="${iconColor}"/>
      <path d="M9 20 L16 12 L23 20 M16 12 L16 20 L9 20" fill="none" stroke="${iconColor}" stroke-width="2" stroke-linejoin="round"/>
      <path d="M16 12 L18 10 M16 12 L14 10" fill="none" stroke="${iconColor}" stroke-width="1.5" stroke-linecap="round"/>
      <ellipse cx="12" cy="13" rx="2" ry="1" fill="${iconColor}"/>`;
  },

  // Arrow content generators
  'arrow-uturn-left': (iconColor = '#fff') => {
    return `<path d="M20 24 L20 11 C20 7 12 7 12 11 L12 18" fill="none" stroke="${iconColor}" stroke-width="4" stroke-linecap="round" stroke-linejoin="round"/>
      <path d="M7 15 L12 21 L17 15" fill="${iconColor}" stroke="${iconColor}" stroke-width="1" stroke-linejoin="round"/>`;
  },

  'arrow-left': (iconColor = '#fff') => {
    return `<path d="M14 8 L6 16 L14 24 L14 19 L26 19 L26 13 L14 13 L14 8Z" fill="${iconColor}"/>`;
  },

  'arrow-slight-left': (iconColor = '#fff') => {
    return `<g transform="rotate(-30 16 16)">
        <path d="M16 6 L8 14 L13 14 L13 26 L19 26 L19 14 L24 14 L16 6Z" fill="${iconColor}"/>
      </g>`;
  },

  'arrow-straight': (iconColor = '#fff') => {
    return `<path d="M16 6 L8 14 L13 14 L13 26 L19 26 L19 14 L24 14 L16 6Z" fill="${iconColor}"/>`;
  },

  'arrow-slight-right': (iconColor = '#fff') => {
    return `<g transform="rotate(30 16 16)">
        <path d="M16 6 L8 14 L13 14 L13 26 L19 26 L19 14 L24 14 L16 6Z" fill="${iconColor}"/>
      </g>`;
  },

  'arrow-right': (iconColor = '#fff') => {
    return `<path d="M18 8 L26 16 L18 24 L18 19 L6 19 L6 13 L18 13 L18 8Z" fill="${iconColor}"/>`;
  },

  'arrow-uturn-right': (iconColor = '#fff') => {
    return `<path d="M12 24 L12 11 C12 7 20 7 20 11 L20 18" fill="none" stroke="${iconColor}" stroke-width="4" stroke-linecap="round" stroke-linejoin="round"/>
      <path d="M15 15 L20 21 L25 15" fill="${iconColor}" stroke="${iconColor}" stroke-width="1" stroke-linejoin="round"/>`;
  },

  'arrow-fork-left': (iconColor = '#fff') => {
    return `<rect x="14" y="18" width="4" height="8" fill="${iconColor}"/>
      <path d="M16 18 L10 12" stroke="${iconColor}" stroke-width="4" stroke-linecap="round"/>
      <path d="M6 15 L10 9 L14 13" fill="${iconColor}" stroke="${iconColor}" stroke-width="1" stroke-linejoin="round"/>
      <path d="M16 18 L22 12" stroke="${iconColor}" stroke-width="3" stroke-linecap="round" opacity="0.5"/>
      <path d="M20 8 L24 12 M24 8 L20 12" stroke="#EF4444" stroke-width="2" stroke-linecap="round"/>`;
  },

  'arrow-fork-right': (iconColor = '#fff') => {
    return `<rect x="14" y="18" width="4" height="8" fill="${iconColor}"/>
      <path d="M16 18 L10 12" stroke="${iconColor}" stroke-width="3" stroke-linecap="round" opacity="0.5"/>
      <path d="M8 8 L12 12 M12 8 L8 12" stroke="#EF4444" stroke-width="2" stroke-linecap="round"/>
      <path d="M16 18 L22 12" stroke="${iconColor}" stroke-width="4" stroke-linecap="round"/>
      <path d="M18 13 L22 9 L26 15" fill="${iconColor}" stroke="${iconColor}" stroke-width="1" stroke-linejoin="round"/>`;
  },

  'arrow-keep-left': (iconColor = '#fff') => {
    return `<g transform="rotate(-150 16 16)">
        <path d="M16 6 L8 14 L13 14 L13 26 L19 26 L19 14 L24 14 L16 6Z" fill="${iconColor}"/>
      </g>`;
  },

  'arrow-keep-right': (iconColor = '#fff') => {
    return `<g transform="rotate(150 16 16)">
        <path d="M16 6 L8 14 L13 14 L13 26 L19 26 L19 14 L24 14 L16 6Z" fill="${iconColor}"/>
      </g>`;
  },
};

/**
 * Generate a complete checkpoint SVG with full customization
 * @param {Object} options
 * @param {string} options.type - Icon type (circle, water, arrow-left, etc.)
 * @param {string} options.backgroundShape - Background shape (circle, square, etc.) - only used for content icons
 * @param {string} options.backgroundColor - Background color (hex or 'none')
 * @param {string} options.borderColor - Border color (hex or 'none')
 * @param {string} options.iconColor - Icon content color (for content icons)
 * @param {string} options.size - Size percentage ('33', '66', '100', '150')
 * @param {number} options.outputSize - Optional fixed output size in pixels (overrides size percentage)
 * @returns {string} Complete SVG string
 */
export function generateCheckpointSvg({
  type,
  backgroundShape = 'circle',
  backgroundColor,
  borderColor,
  iconColor,
  size = '100',
  outputSize = null,
}) {
  if (type === 'default') {
    return null;
  }

  const config = getIconTypeConfig(type);
  const effectiveBackgroundColor = backgroundColor || config.defaultColor || '#667eea';
  const effectiveBorderColor = borderColor || '#fff';
  const effectiveIconColor = iconColor || '#fff';
  const sizeMultiplier = parseInt(size, 10) / 100;
  const actualSize = outputSize || Math.round(32 * sizeMultiplier);

  let svgContent = '';

  if (config.isShapeOnly) {
    // For shape-only icons (circle, square, etc.), the shape IS the icon
    // The type itself is the shape
    if (backgroundShapeGenerators[type]) {
      svgContent = backgroundShapeGenerators[type]({
        backgroundColor: effectiveBackgroundColor,
        borderColor: effectiveBorderColor,
      });
    } else {
      // Fallback to legacy shape generator
      return shapeSvgGenerators[type]?.(effectiveBackgroundColor, actualSize) || null;
    }
  } else {
    // For content icons, render background shape + icon content
    const bgShape = backgroundShape || 'circle';

    // Render background (if not 'none')
    if (bgShape !== 'none' && backgroundShapeGenerators[bgShape]) {
      svgContent += backgroundShapeGenerators[bgShape]({
        backgroundColor: effectiveBackgroundColor,
        borderColor: effectiveBorderColor,
      });
    }

    // Render icon content
    if (iconContentGenerators[type]) {
      svgContent += iconContentGenerators[type](effectiveIconColor, effectiveBackgroundColor);
    }
  }

  // Wrap in SVG element with proper size
  return `<svg width="${actualSize}" height="${actualSize}" viewBox="0 0 32 32" xmlns="http://www.w3.org/2000/svg">
    ${svgContent}
  </svg>`;
}

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
