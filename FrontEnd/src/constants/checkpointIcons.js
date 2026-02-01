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
  { value: 'default', label: 'Default', isShapeOnly: false, category: 'default', searchTerms: 'status' },
  // Shape-only icons - the shape IS the icon
  { value: 'dot', label: 'Dot', isShapeOnly: true, category: 'shape', defaultColor: '#667eea', searchTerms: 'point marker small' },
  { value: 'circle', label: 'Circle', isShapeOnly: true, category: 'shape', defaultColor: '#667eea', searchTerms: 'round shape' },
  { value: 'square', label: 'Square', isShapeOnly: true, category: 'shape', defaultColor: '#667eea', searchTerms: 'box rectangle shape' },
  { value: 'triangle', label: 'Triangle', isShapeOnly: true, category: 'shape', defaultColor: '#667eea', searchTerms: 'shape warning' },
  { value: 'diamond', label: 'Diamond', isShapeOnly: true, category: 'shape', defaultColor: '#667eea', searchTerms: 'shape rhombus' },
  { value: 'star', label: 'Star', isShapeOnly: true, category: 'shape', defaultColor: '#667eea', searchTerms: 'shape favorite special' },
  { value: 'hexagon', label: 'Hexagon', isShapeOnly: true, category: 'shape', defaultColor: '#667eea', searchTerms: 'shape six' },
  { value: 'pentagon', label: 'Pentagon', isShapeOnly: true, category: 'shape', defaultColor: '#667eea', searchTerms: 'shape five' },
  // Content icons - have a symbol on top of background
  { value: 'car', label: 'Car', isShapeOnly: false, category: 'content', defaultColor: '#EF4444', searchTerms: 'vehicle auto automobile transport parking' },
  { value: 'bike', label: 'Bike', isShapeOnly: false, category: 'content', defaultColor: '#EF4444', searchTerms: 'bicycle cycle cycling transport' },
  { value: 'water', label: 'Water', isShapeOnly: false, category: 'content', defaultColor: '#3B82F6', searchTerms: 'drink hydration refreshment drop aid' },
  { value: 'cup', label: 'Cup', isShapeOnly: false, category: 'content', defaultColor: '#3B82F6', searchTerms: 'drink plastic disposable water refreshment aid station' },
  { value: 'bottle', label: 'Bottle', isShapeOnly: false, category: 'content', defaultColor: '#0EA5E9', searchTerms: 'drink water hydration refreshment aid station plastic' },
  { value: 'food', label: 'Food', isShapeOnly: false, category: 'content', defaultColor: '#F59E0B', searchTerms: 'eat meal refreshment aid nutrition snack fork knife' },
  { value: 'fruit', label: 'Fruit', isShapeOnly: false, category: 'content', defaultColor: '#EAB308', searchTerms: 'food eat snack banana apple healthy nutrition aid' },
  { value: 'medical', label: 'Medical', isShapeOnly: false, category: 'content', defaultColor: '#EF4444', searchTerms: 'health first aid emergency hospital cross doctor nurse' },
  { value: 'photo', label: 'Photo', isShapeOnly: false, category: 'content', defaultColor: '#8B5CF6', searchTerms: 'camera picture image photography' },
  { value: 'music', label: 'Music', isShapeOnly: false, category: 'content', defaultColor: '#EC4899', searchTerms: 'entertainment sound audio band dj' },
  { value: 'start', label: 'Start', isShapeOnly: false, category: 'content', defaultColor: '#10B981', searchTerms: 'begin play go race commence' },
  { value: 'finish', label: 'Finish', isShapeOnly: false, category: 'content', defaultColor: '#1F2937', searchTerms: 'end complete flag chequered checkered race' },
  { value: 'toilet', label: 'Toilet', isShapeOnly: false, category: 'content', defaultColor: '#6366F1', searchTerms: 'bathroom restroom wc loo facilities washroom' },
  { value: 'shower', label: 'Shower', isShapeOnly: false, category: 'content', defaultColor: '#06B6D4', searchTerms: 'wash clean bathroom facilities' },
  { value: 'bus', label: 'Bus', isShapeOnly: false, category: 'content', defaultColor: '#F97316', searchTerms: 'transport shuttle coach vehicle' },
  { value: 'info', label: 'Information', isShapeOnly: false, category: 'content', defaultColor: '#0EA5E9', searchTerms: 'help desk enquiry query assistance' },
  { value: 'merch', label: 'Merchandise', isShapeOnly: false, category: 'content', defaultColor: '#A855F7', searchTerms: 'shop store tshirt shirt clothing buy sell retail' },
  { value: 'cone', label: 'Road closure', isShapeOnly: false, category: 'content', defaultColor: '#FF6B00', searchTerms: 'traffic warning barrier closed' },
  { value: 'traffic', label: 'Traffic management', isShapeOnly: false, category: 'content', defaultColor: '#374151', searchTerms: 'lights signal control road' },
  { value: 'track', label: 'Track', isShapeOnly: false, category: 'content', defaultColor: '#78716C', searchTerms: 'railway train rail crossing' },
  { value: 'tunnel', label: 'Tunnel', isShapeOnly: false, category: 'content', defaultColor: '#57534E', searchTerms: 'underpass underground passage' },
  { value: 'bridge', label: 'Bridge', isShapeOnly: false, category: 'content', defaultColor: '#0284C7', searchTerms: 'overpass crossing structure river' },
  { value: 'plane', label: 'Plane', isShapeOnly: false, category: 'content', defaultColor: '#0EA5E9', searchTerms: 'airplane aircraft flight airport travel transport' },
  { value: 'hill', label: 'Hill', isShapeOnly: false, category: 'content', defaultColor: '#65A30D', searchTerms: 'mountain climb elevation incline terrain' },
  { value: 'baggage', label: 'Baggage', isShapeOnly: false, category: 'content', defaultColor: '#A855F7', searchTerms: 'luggage suitcase bag storage travel' },
  { value: 'crown', label: 'Crown', isShapeOnly: false, category: 'content', defaultColor: '#F59E0B', searchTerms: 'king queen royal vip winner prize award' },
  { value: 'crossing', label: 'Crossing point', isShapeOnly: false, category: 'content', defaultColor: '#3B82F6', searchTerms: 'pedestrian zebra crosswalk road' },
  { value: 'trees', label: 'Trees', isShapeOnly: false, category: 'content', defaultColor: '#22C55E', searchTerms: 'forest nature wood park green environment' },
  // Directional arrows
  { value: 'arrow-uturn-left', label: 'Left u-turn', isShapeOnly: false, category: 'content', defaultColor: '#3B82F6', searchTerms: 'direction arrow turn around reverse' },
  { value: 'arrow-left', label: 'Left turn', isShapeOnly: false, category: 'content', defaultColor: '#3B82F6', searchTerms: 'direction arrow' },
  { value: 'arrow-slight-left', label: 'Slight left', isShapeOnly: false, category: 'content', defaultColor: '#3B82F6', searchTerms: 'direction arrow bear veer' },
  { value: 'arrow-straight', label: 'Straight ahead', isShapeOnly: false, category: 'content', defaultColor: '#3B82F6', searchTerms: 'direction arrow forward continue' },
  { value: 'arrow-slight-right', label: 'Slight right', isShapeOnly: false, category: 'content', defaultColor: '#3B82F6', searchTerms: 'direction arrow bear veer' },
  { value: 'arrow-right', label: 'Right turn', isShapeOnly: false, category: 'content', defaultColor: '#3B82F6', searchTerms: 'direction arrow' },
  { value: 'arrow-uturn-right', label: 'Right u-turn', isShapeOnly: false, category: 'content', defaultColor: '#3B82F6', searchTerms: 'direction arrow turn around reverse' },
  { value: 'arrow-fork-left', label: 'Left fork', isShapeOnly: false, category: 'content', defaultColor: '#3B82F6', searchTerms: 'direction arrow split junction' },
  { value: 'arrow-fork-right', label: 'Right fork', isShapeOnly: false, category: 'content', defaultColor: '#3B82F6', searchTerms: 'direction arrow split junction' },
  { value: 'arrow-keep-left', label: 'Keep left', isShapeOnly: false, category: 'content', defaultColor: '#3B82F6', searchTerms: 'direction arrow stay merge' },
  { value: 'arrow-keep-right', label: 'Keep right', isShapeOnly: false, category: 'content', defaultColor: '#3B82F6', searchTerms: 'direction arrow stay merge' },
  // Animal icons
  { value: 'cat', label: 'Cat', isShapeOnly: false, category: 'content', defaultColor: '#F97316', searchTerms: 'animal pet feline kitten kitty meow' },
  { value: 'dog', label: 'Dog', isShapeOnly: false, category: 'content', defaultColor: '#A16207', searchTerms: 'animal pet canine puppy hound woof bark' },
  { value: 'deer', label: 'Deer', isShapeOnly: false, category: 'content', defaultColor: '#84652D', searchTerms: 'animal wildlife stag buck antlers nature forest' },
  { value: 'cow', label: 'Cow', isShapeOnly: false, category: 'content', defaultColor: '#1F2937', searchTerms: 'animal farm cattle livestock moo bovine' },
  { value: 'bear', label: 'Bear', isShapeOnly: false, category: 'content', defaultColor: '#78350F', searchTerms: 'animal wildlife forest nature danger warning' },
  { value: 'frog', label: 'Frog', isShapeOnly: false, category: 'content', defaultColor: '#16A34A', searchTerms: 'animal amphibian toad pond wildlife nature' },
  { value: 'duck', label: 'Duck', isShapeOnly: false, category: 'content', defaultColor: '#EAB308', searchTerms: 'animal bird waterfowl pond lake water quack wildlife' },
  { value: 'fish', label: 'Fish', isShapeOnly: false, category: 'content', defaultColor: '#0EA5E9', searchTerms: 'animal sea ocean water marine aquatic wildlife swimming' },
  // Number icons (1-42)
  ...Array.from({ length: 42 }, (_, i) => ({
    value: `number-${i + 1}`,
    label: `${i + 1}`,
    isShapeOnly: false,
    category: 'number',
    defaultColor: '#667eea',
    searchTerms: 'digit numeral count',
  })),
];

// Get icon types for a specific level (event only gets shapes and numbers, area/checkpoint get all)
export function getIconTypesForLevel(level) {
  if (level === 'event') {
    return CHECKPOINT_ICON_TYPES.filter(t => t.category === 'default' || t.category === 'shape' || t.category === 'number');
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

  cup: (size = 32) => {
    const color = '#3B82F6';
    return `<svg width="${size}" height="${size}" viewBox="0 0 32 32" xmlns="http://www.w3.org/2000/svg">
      <circle cx="16" cy="16" r="14" fill="${color}" stroke="#fff" stroke-width="2"/>
      <!-- Cup body (tapered) -->
      <path d="M10 8 L12 24 L20 24 L22 8 Z" fill="#fff"/>
      <!-- Rim -->
      <ellipse cx="16" cy="8" rx="6" ry="1.5" fill="#fff" stroke="#fff" stroke-width="1"/>
      <!-- Liquid line -->
      <path d="M11 12 L21 12" stroke="${color}" stroke-width="1" opacity="0.3"/>
    </svg>`;
  },

  bottle: (size = 32) => {
    const color = '#0EA5E9';
    return `<svg width="${size}" height="${size}" viewBox="0 0 32 32" xmlns="http://www.w3.org/2000/svg">
      <circle cx="16" cy="16" r="14" fill="${color}" stroke="#fff" stroke-width="2"/>
      <!-- Bottle body -->
      <path d="M11 13 L11 25 Q11 27 13 27 L19 27 Q21 27 21 25 L21 13 Q21 11 19 11 L18 11 L18 8 Q18 7 17 7 L15 7 Q14 7 14 8 L14 11 L13 11 Q11 11 11 13 Z" fill="#fff"/>
      <!-- Cap -->
      <rect x="14" y="5" width="4" height="2.5" rx="1" fill="#fff"/>
      <!-- Water level -->
      <path d="M12 15 L12 25 Q12 26 13 26 L19 26 Q20 26 20 25 L20 15 Z" fill="${color}" opacity="0.25"/>
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

  fruit: (size = 32) => {
    const color = '#EAB308';
    return `<svg width="${size}" height="${size}" viewBox="0 0 32 32" xmlns="http://www.w3.org/2000/svg">
      <circle cx="16" cy="16" r="14" fill="${color}" stroke="#fff" stroke-width="2"/>
      <!-- Apple body -->
      <path d="M16 10 Q10 10 9 16 Q8 22 12 25 Q14 27 16 27 Q18 27 20 25 Q24 22 23 16 Q22 10 16 10 Z" fill="#fff"/>
      <!-- Apple indent -->
      <path d="M14 10 Q16 12 18 10" fill="none" stroke="#fff" stroke-width="1.5"/>
      <!-- Stem -->
      <path d="M16 10 L16 7 Q16 6 17 6" fill="none" stroke="#fff" stroke-width="1.5" stroke-linecap="round"/>
      <!-- Leaf -->
      <path d="M17 7 Q20 6 21 8 Q20 8 17 7" fill="#fff"/>
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
      <!-- Play triangle -->
      <path d="M12 9 L24 16 L12 23 Z" fill="#fff"/>
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

  crown: (size = 32) => {
    const color = '#F59E0B';
    return `<svg width="${size}" height="${size}" viewBox="0 0 32 32" xmlns="http://www.w3.org/2000/svg">
      <circle cx="16" cy="16" r="14" fill="${color}" stroke="#fff" stroke-width="2"/>
      <path d="M8 21 L8 14 L12 17 L16 11 L20 17 L24 14 L24 21 Z" fill="#fff"/>
      <rect x="8" y="21" width="16" height="3" rx="1" fill="#fff"/>
    </svg>`;
  },

  crossing: (size = 32) => {
    const color = '#3B82F6';
    return `<svg width="${size}" height="${size}" viewBox="0 0 32 32" xmlns="http://www.w3.org/2000/svg">
      <circle cx="16" cy="16" r="14" fill="${color}" stroke="#fff" stroke-width="2"/>
      <!-- Crossing stripes -->
      <rect x="6" y="22" width="20" height="2" fill="#fff" opacity="0.5"/>
      <rect x="6" y="25" width="20" height="2" fill="#fff" opacity="0.5"/>
      <!-- Walking pedestrian -->
      <circle cx="16" cy="7" r="2.5" fill="#fff"/>
      <path d="M16 10 L16 16" stroke="#fff" stroke-width="2.5" stroke-linecap="round"/>
      <path d="M13 13 L19 13" stroke="#fff" stroke-width="2" stroke-linecap="round"/>
      <path d="M16 16 L13 22" stroke="#fff" stroke-width="2" stroke-linecap="round"/>
      <path d="M16 16 L19 22" stroke="#fff" stroke-width="2" stroke-linecap="round"/>
    </svg>`;
  },

  trees: (size = 32) => {
    const color = '#22C55E';
    return `<svg width="${size}" height="${size}" viewBox="0 0 32 32" xmlns="http://www.w3.org/2000/svg">
      <circle cx="16" cy="16" r="14" fill="${color}" stroke="#fff" stroke-width="2"/>
      <!-- Left tree -->
      <path d="M10 22 L10 18 L7 18 L11 12 L8 12 L11 7 L14 12 L11 12 L15 18 L12 18 L12 22 Z" fill="#fff"/>
      <!-- Right tree -->
      <path d="M20 22 L20 18 L17 18 L21 12 L18 12 L21 7 L24 12 L21 12 L25 18 L22 18 L22 22 Z" fill="#fff"/>
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
      <path d="M10 12 L6 10 L8 16 Z" fill="#fff"/>
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
      <path d="M22 12 L26 10 L24 16 Z" fill="#fff"/>
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

  // Animal icons
  cat: (size = 32) => {
    const color = '#F97316';
    return `<svg width="${size}" height="${size}" viewBox="0 0 32 32" xmlns="http://www.w3.org/2000/svg">
      <circle cx="16" cy="16" r="14" fill="${color}" stroke="#fff" stroke-width="2"/>
      <!-- Ears (pointed triangles) -->
      <path d="M7 15 L10 6 L14 13 Z" fill="#fff"/>
      <path d="M25 15 L22 6 L18 13 Z" fill="#fff"/>
      <!-- Inner ears -->
      <path d="M9 13 L10.5 8 L12.5 12 Z" fill="#FFB4AB"/>
      <path d="M23 13 L21.5 8 L19.5 12 Z" fill="#FFB4AB"/>
      <!-- Face -->
      <ellipse cx="16" cy="18" rx="8" ry="7" fill="#fff"/>
      <!-- Eyes -->
      <ellipse cx="12" cy="16" rx="1.8" ry="2.5" fill="${color}"/>
      <ellipse cx="20" cy="16" rx="1.8" ry="2.5" fill="${color}"/>
      <!-- Nose -->
      <path d="M16 19.5 L14.5 21.5 L17.5 21.5 Z" fill="#FDA4AF"/>
      <!-- Mouth -->
      <path d="M16 21.5 L16 23 M14 23 Q16 25 18 23" fill="none" stroke="${color}" stroke-width="0.8" stroke-linecap="round"/>
    </svg>`;
  },

  dog: (size = 32) => {
    const color = '#A16207';
    return `<svg width="${size}" height="${size}" viewBox="0 0 32 32" xmlns="http://www.w3.org/2000/svg">
      <circle cx="16" cy="16" r="14" fill="${color}" stroke="#fff" stroke-width="2"/>
      <!-- Floppy ears -->
      <ellipse cx="7" cy="15" rx="4" ry="6" fill="#fff"/>
      <ellipse cx="25" cy="15" rx="4" ry="6" fill="#fff"/>
      <!-- Face -->
      <ellipse cx="16" cy="17" rx="8" ry="7" fill="#fff"/>
      <!-- Eyes -->
      <circle cx="12" cy="15" r="2" fill="${color}"/>
      <circle cx="20" cy="15" r="2" fill="${color}"/>
      <circle cx="12.6" cy="14.4" r="0.8" fill="#fff"/>
      <circle cx="20.6" cy="14.4" r="0.8" fill="#fff"/>
      <!-- Snout -->
      <ellipse cx="16" cy="21" rx="4" ry="3" fill="#E5D3C8"/>
      <!-- Nose -->
      <ellipse cx="16" cy="19" rx="2.5" ry="1.8" fill="#1F2937"/>
      <!-- Tongue -->
      <ellipse cx="16" cy="24" rx="1.5" ry="2" fill="#F87171"/>
    </svg>`;
  },

  deer: (size = 32) => {
    const color = '#84652D';
    return `<svg width="${size}" height="${size}" viewBox="0 0 32 32" xmlns="http://www.w3.org/2000/svg">
      <circle cx="16" cy="16" r="14" fill="${color}" stroke="#fff" stroke-width="2"/>
      <!-- Antlers -->
      <path d="M9 12 L7 6 M7 6 L5 4 M7 6 L9 5" fill="none" stroke="#fff" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>
      <path d="M23 12 L25 6 M25 6 L27 4 M25 6 L23 5" fill="none" stroke="#fff" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>
      <!-- Head -->
      <ellipse cx="16" cy="18" rx="7" ry="8" fill="#fff"/>
      <!-- Ears -->
      <ellipse cx="7" cy="14" rx="2" ry="3.5" fill="#fff" transform="rotate(-30 7 14)"/>
      <ellipse cx="25" cy="14" rx="2" ry="3.5" fill="#fff" transform="rotate(30 25 14)"/>
      <!-- Eyes -->
      <circle cx="12" cy="16" r="1.5" fill="${color}"/>
      <circle cx="20" cy="16" r="1.5" fill="${color}"/>
      <!-- Nose -->
      <ellipse cx="16" cy="22" rx="2.5" ry="2" fill="${color}"/>
    </svg>`;
  },

  cow: (size = 32) => {
    const color = '#1F2937';
    return `<svg width="${size}" height="${size}" viewBox="0 0 32 32" xmlns="http://www.w3.org/2000/svg">
      <circle cx="16" cy="16" r="14" fill="${color}" stroke="#fff" stroke-width="2"/>
      <!-- Horns -->
      <path d="M8 8 Q6 10 7 13" fill="none" stroke="#fff" stroke-width="2" stroke-linecap="round"/>
      <path d="M24 8 Q26 10 25 13" fill="none" stroke="#fff" stroke-width="2" stroke-linecap="round"/>
      <!-- Ears -->
      <ellipse cx="8" cy="14" rx="2" ry="2.5" fill="#fff"/>
      <ellipse cx="24" cy="14" rx="2" ry="2.5" fill="#fff"/>
      <!-- Face -->
      <ellipse cx="16" cy="17" rx="7" ry="6" fill="#fff"/>
      <!-- Spots -->
      <circle cx="12" cy="14" r="2" fill="${color}"/>
      <circle cx="20" cy="15" r="1.5" fill="${color}"/>
      <!-- Eyes -->
      <circle cx="13" cy="16" r="1" fill="${color}"/>
      <circle cx="19" cy="16" r="1" fill="${color}"/>
      <!-- Snout -->
      <ellipse cx="16" cy="20" rx="3.5" ry="2.5" fill="#FCD5CE"/>
      <!-- Nostrils -->
      <circle cx="14.5" cy="20" r="0.8" fill="${color}"/>
      <circle cx="17.5" cy="20" r="0.8" fill="${color}"/>
    </svg>`;
  },

  bear: (size = 32) => {
    const color = '#78350F';
    return `<svg width="${size}" height="${size}" viewBox="0 0 32 32" xmlns="http://www.w3.org/2000/svg">
      <circle cx="16" cy="16" r="14" fill="${color}" stroke="#fff" stroke-width="2"/>
      <!-- Ears -->
      <circle cx="8" cy="10" r="3.5" fill="#fff"/>
      <circle cx="8" cy="10" r="2" fill="${color}"/>
      <circle cx="24" cy="10" r="3.5" fill="#fff"/>
      <circle cx="24" cy="10" r="2" fill="${color}"/>
      <!-- Face -->
      <ellipse cx="16" cy="17" rx="8" ry="7" fill="#fff"/>
      <!-- Eyes -->
      <circle cx="12" cy="15" r="1.5" fill="${color}"/>
      <circle cx="20" cy="15" r="1.5" fill="${color}"/>
      <!-- Snout -->
      <ellipse cx="16" cy="19" rx="3" ry="2.5" fill="#D4A574"/>
      <!-- Nose -->
      <ellipse cx="16" cy="18" rx="1.5" ry="1" fill="${color}"/>
    </svg>`;
  },

  frog: (size = 32) => {
    const color = '#16A34A';
    return `<svg width="${size}" height="${size}" viewBox="0 0 32 32" xmlns="http://www.w3.org/2000/svg">
      <circle cx="16" cy="16" r="14" fill="${color}" stroke="#fff" stroke-width="2"/>
      <!-- Eyes (bulging) -->
      <circle cx="10" cy="10" r="4" fill="#fff"/>
      <circle cx="22" cy="10" r="4" fill="#fff"/>
      <circle cx="10" cy="10" r="2" fill="${color}"/>
      <circle cx="22" cy="10" r="2" fill="${color}"/>
      <!-- Face -->
      <ellipse cx="16" cy="18" rx="8" ry="5" fill="#fff"/>
      <!-- Mouth -->
      <path d="M10 19 Q16 23 22 19" fill="none" stroke="${color}" stroke-width="1.5" stroke-linecap="round"/>
      <!-- Nostrils -->
      <circle cx="14" cy="16" r="0.8" fill="${color}"/>
      <circle cx="18" cy="16" r="0.8" fill="${color}"/>
    </svg>`;
  },

  duck: (size = 32) => {
    const color = '#EAB308';
    return `<svg width="${size}" height="${size}" viewBox="0 0 32 32" xmlns="http://www.w3.org/2000/svg">
      <circle cx="16" cy="16" r="14" fill="${color}" stroke="#fff" stroke-width="2"/>
      <!-- Body -->
      <ellipse cx="14" cy="20" rx="8" ry="5" fill="#fff"/>
      <!-- Head -->
      <circle cx="22" cy="12" r="5" fill="#fff"/>
      <!-- Neck -->
      <path d="M18 17 Q22 16 21 14" fill="#fff" stroke="#fff" stroke-width="3"/>
      <!-- Eye -->
      <circle cx="23" cy="11" r="1.2" fill="${color}"/>
      <!-- Beak -->
      <path d="M26 13 L29 12 L29 15 L26 14 Z" fill="#F97316"/>
    </svg>`;
  },

  fish: (size = 32) => {
    const color = '#0EA5E9';
    return `<svg width="${size}" height="${size}" viewBox="0 0 32 32" xmlns="http://www.w3.org/2000/svg">
      <circle cx="16" cy="16" r="14" fill="${color}" stroke="#fff" stroke-width="2"/>
      <!-- Body -->
      <ellipse cx="15" cy="16" rx="8" ry="5" fill="#fff"/>
      <!-- Tail -->
      <path d="M23 16 L28 12 L28 20 Z" fill="#fff"/>
      <!-- Eye -->
      <circle cx="10" cy="15" r="1.5" fill="${color}"/>
      <!-- Fin -->
      <path d="M15 11 L17 8 L19 11" fill="#fff" stroke="#fff" stroke-width="1"/>
      <!-- Gill -->
      <path d="M12 14 Q13 16 12 18" fill="none" stroke="${color}" stroke-width="1" stroke-linecap="round"/>
    </svg>`;
  },
};

// Generate fixed icon SVGs for number icons (1-42)
for (let i = 1; i <= 42; i++) {
  fixedIconSvgs[`number-${i}`] = (size = 32) => {
    const color = '#667eea';
    const numStr = String(i);
    let fontSize = 18;
    if (numStr.length === 2) fontSize = 15;

    return `<svg width="${size}" height="${size}" viewBox="0 0 32 32" xmlns="http://www.w3.org/2000/svg">
      <circle cx="16" cy="16" r="14" fill="${color}" stroke="#fff" stroke-width="2"/>
      <text x="16" y="16" font-family="Arial, sans-serif" font-size="${fontSize}" font-weight="bold" fill="#fff" text-anchor="middle" dominant-baseline="central">${i}</text>
    </svg>`;
  };
}

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

  cup: (iconColor = '#fff', bgColor = '#667eea') => {
    return `
      <!-- Cup body (tapered) -->
      <path d="M10 8 L12 24 L20 24 L22 8 Z" fill="${iconColor}"/>
      <!-- Rim -->
      <ellipse cx="16" cy="8" rx="6" ry="1.5" fill="${iconColor}"/>
      <!-- Liquid line -->
      <path d="M11 12 L21 12" stroke="${bgColor}" stroke-width="1" opacity="0.3"/>`;
  },

  bottle: (iconColor = '#fff', bgColor = '#667eea') => {
    return `
      <!-- Bottle body -->
      <path d="M11 13 L11 25 Q11 27 13 27 L19 27 Q21 27 21 25 L21 13 Q21 11 19 11 L18 11 L18 8 Q18 7 17 7 L15 7 Q14 7 14 8 L14 11 L13 11 Q11 11 11 13 Z" fill="${iconColor}"/>
      <!-- Cap -->
      <rect x="14" y="5" width="4" height="2.5" rx="1" fill="${iconColor}"/>
      <!-- Water level -->
      <path d="M12 15 L12 25 Q12 26 13 26 L19 26 Q20 26 20 25 L20 15 Z" fill="${bgColor}" opacity="0.25"/>`;
  },

  food: (iconColor = '#fff') => {
    return `<path d="M11 9v6c0 1.1.9 2 2 2h1v7h2v-7h1c1.1 0 2-.9 2-2V9h-2v5h-1V9h-2v5h-1V9h-2z" fill="${iconColor}"/>
      <path d="M21 9v14h2V9h-2z" fill="${iconColor}"/>`;
  },

  fruit: (iconColor = '#fff') => {
    return `
      <!-- Apple body -->
      <path d="M16 10 Q10 10 9 16 Q8 22 12 25 Q14 27 16 27 Q18 27 20 25 Q24 22 23 16 Q22 10 16 10 Z" fill="${iconColor}"/>
      <!-- Apple indent -->
      <path d="M14 10 Q16 12 18 10" fill="none" stroke="${iconColor}" stroke-width="1.5"/>
      <!-- Stem -->
      <path d="M16 10 L16 7 Q16 6 17 6" fill="none" stroke="${iconColor}" stroke-width="1.5" stroke-linecap="round"/>
      <!-- Leaf -->
      <path d="M17 7 Q20 6 21 8 Q20 8 17 7" fill="${iconColor}"/>`;
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
    return `<path d="M12 9 L24 16 L12 23 Z" fill="${iconColor}"/>`;
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
      <!-- Left arrowhead -->
      <path d="M10 12 L6 10 L8 16 Z" fill="${iconColor}"/>
      <!-- Wrong way indicator on right -->
      <path d="M16 18 L22 12" stroke="${iconColor}" stroke-width="3" stroke-linecap="round" opacity="0.5"/>
      <path d="M20 8 L24 12 M24 8 L20 12" stroke="#EF4444" stroke-width="2" stroke-linecap="round"/>`;
  },

  'arrow-fork-right': (iconColor = '#fff') => {
    return `<rect x="14" y="18" width="4" height="8" fill="${iconColor}"/>
      <!-- Wrong way indicator on left -->
      <path d="M16 18 L10 12" stroke="${iconColor}" stroke-width="3" stroke-linecap="round" opacity="0.5"/>
      <path d="M8 8 L12 12 M12 8 L8 12" stroke="#EF4444" stroke-width="2" stroke-linecap="round"/>
      <path d="M16 18 L22 12" stroke="${iconColor}" stroke-width="4" stroke-linecap="round"/>
      <!-- Right arrowhead -->
      <path d="M22 12 L26 10 L24 16 Z" fill="${iconColor}"/>`;
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

  // Track (railway crossing view)
  'track': (iconColor = '#fff') => {
    return `
      <rect x="10" y="7" width="2.5" height="18" fill="${iconColor}"/>
      <rect x="19.5" y="7" width="2.5" height="18" fill="${iconColor}"/>
      <rect x="8" y="9" width="16" height="2.5" rx="0.5" fill="${iconColor}"/>
      <rect x="8" y="14.5" width="16" height="2.5" rx="0.5" fill="${iconColor}"/>
      <rect x="8" y="20" width="16" height="2.5" rx="0.5" fill="${iconColor}"/>`;
  },

  // Tunnel (simple arch entrance)
  'tunnel': (iconColor = '#fff') => {
    return `
      <path d="M7 25 L7 14 C7 8 11 5 16 5 C21 5 25 8 25 14 L25 25" fill="none" stroke="${iconColor}" stroke-width="2.5"/>
      <path d="M11 25 L11 16 C11 12 13 10 16 10 C19 10 21 12 21 16 L21 25" fill="none" stroke="${iconColor}" stroke-width="1.5" opacity="0.6"/>`;
  },

  // Bridge (suspension bridge style)
  'bridge': (iconColor = '#fff') => {
    return `
      <rect x="5" y="18" width="22" height="2.5" rx="0.5" fill="${iconColor}"/>
      <rect x="7" y="10" width="2" height="8" fill="${iconColor}"/>
      <rect x="23" y="10" width="2" height="8" fill="${iconColor}"/>
      <path d="M8 10 Q16 6 24 10" fill="none" stroke="${iconColor}" stroke-width="2" stroke-linecap="round"/>
      <line x1="12" y1="13" x2="12" y2="18" stroke="${iconColor}" stroke-width="1.5"/>
      <line x1="16" y1="11.5" x2="16" y2="18" stroke="${iconColor}" stroke-width="1.5"/>
      <line x1="20" y1="13" x2="20" y2="18" stroke="${iconColor}" stroke-width="1.5"/>`;
  },

  // Plane (airplane - top-down view pointing up)
  'plane': (iconColor = '#fff') => {
    return `
      <g fill="${iconColor}">
        <!-- Fuselage -->
        <path d="M16 4 L18 10 L18 22 L20 26 L16 24 L12 26 L14 22 L14 10 Z"/>
        <!-- Wings -->
        <path d="M14 14 L6 18 L6 20 L14 17 Z"/>
        <path d="M18 14 L26 18 L26 20 L18 17 Z"/>
        <!-- Tail wings -->
        <path d="M14 22 L10 25 L10 26 L14 24 Z"/>
        <path d="M18 22 L22 25 L22 26 L18 24 Z"/>
      </g>`;
  },

  // Hill (mountain peak)
  'hill': (iconColor = '#fff') => {
    return `
      <path d="M6 24 L13 12 L16 16 L20 10 L26 24 Z" fill="${iconColor}"/>
      <path d="M20 10 L22 13 L18 13 Z" fill="${iconColor}" opacity="0.6"/>`;
  },

  // Baggage (suitcase)
  'baggage': (iconColor = '#fff') => {
    return `
      <rect x="8" y="12" width="16" height="12" rx="2" fill="${iconColor}"/>
      <rect x="12" y="8" width="8" height="4" rx="1" fill="none" stroke="${iconColor}" stroke-width="2"/>
      <line x1="12" y1="16" x2="20" y2="16" stroke="currentColor" stroke-width="1.5" opacity="0.3"/>
      <line x1="12" y1="20" x2="20" y2="20" stroke="currentColor" stroke-width="1.5" opacity="0.3"/>`;
  },

  // Crown
  'crown': (iconColor = '#fff') => {
    return `
      <path d="M8 21 L8 14 L12 17 L16 11 L20 17 L24 14 L24 21 Z" fill="${iconColor}"/>
      <rect x="8" y="21" width="16" height="3" rx="1" fill="${iconColor}"/>`;
  },

  // Crossing point (pedestrian walking on crossing stripes)
  'crossing': (iconColor = '#fff') => {
    return `
      <!-- Crossing stripes -->
      <rect x="6" y="22" width="20" height="2" fill="${iconColor}" opacity="0.5"/>
      <rect x="6" y="25" width="20" height="2" fill="${iconColor}" opacity="0.5"/>
      <!-- Walking pedestrian -->
      <circle cx="16" cy="7" r="2.5" fill="${iconColor}"/>
      <path d="M16 10 L16 16" stroke="${iconColor}" stroke-width="2.5" stroke-linecap="round"/>
      <path d="M13 13 L19 13" stroke="${iconColor}" stroke-width="2" stroke-linecap="round"/>
      <path d="M16 16 L13 22" stroke="${iconColor}" stroke-width="2" stroke-linecap="round"/>
      <path d="M16 16 L19 22" stroke="${iconColor}" stroke-width="2" stroke-linecap="round"/>`;
  },

  // Trees
  'trees': (iconColor = '#fff') => {
    return `
      <path d="M10 22 L10 18 L7 18 L11 12 L8 12 L11 7 L14 12 L11 12 L15 18 L12 18 L12 22 Z" fill="${iconColor}"/>
      <path d="M20 22 L20 18 L17 18 L21 12 L18 12 L21 7 L24 12 L21 12 L25 18 L22 18 L22 22 Z" fill="${iconColor}"/>`;
  },

  // Animal icons
  'cat': (iconColor = '#fff', bgColor = '#667eea') => {
    return `
      <!-- Ears (pointed triangles) -->
      <path d="M7 15 L10 6 L14 13 Z" fill="${iconColor}"/>
      <path d="M25 15 L22 6 L18 13 Z" fill="${iconColor}"/>
      <!-- Inner ears -->
      <path d="M9 13 L10.5 8 L12.5 12 Z" fill="#FFB4AB"/>
      <path d="M23 13 L21.5 8 L19.5 12 Z" fill="#FFB4AB"/>
      <!-- Face -->
      <ellipse cx="16" cy="18" rx="8" ry="7" fill="${iconColor}"/>
      <!-- Eyes -->
      <ellipse cx="12" cy="16" rx="1.8" ry="2.5" fill="${bgColor}"/>
      <ellipse cx="20" cy="16" rx="1.8" ry="2.5" fill="${bgColor}"/>
      <!-- Nose -->
      <path d="M16 19.5 L14.5 21.5 L17.5 21.5 Z" fill="#FDA4AF"/>
      <!-- Mouth -->
      <path d="M16 21.5 L16 23 M14 23 Q16 25 18 23" fill="none" stroke="${bgColor}" stroke-width="0.8" stroke-linecap="round"/>`;
  },

  'dog': (iconColor = '#fff', bgColor = '#667eea') => {
    return `
      <!-- Floppy ears -->
      <ellipse cx="7" cy="15" rx="4" ry="6" fill="${iconColor}"/>
      <ellipse cx="25" cy="15" rx="4" ry="6" fill="${iconColor}"/>
      <!-- Face -->
      <ellipse cx="16" cy="17" rx="8" ry="7" fill="${iconColor}"/>
      <!-- Eyes -->
      <circle cx="12" cy="15" r="2" fill="${bgColor}"/>
      <circle cx="20" cy="15" r="2" fill="${bgColor}"/>
      <circle cx="12.6" cy="14.4" r="0.8" fill="${iconColor}"/>
      <circle cx="20.6" cy="14.4" r="0.8" fill="${iconColor}"/>
      <!-- Snout -->
      <ellipse cx="16" cy="21" rx="4" ry="3" fill="#E5D3C8"/>
      <!-- Nose -->
      <ellipse cx="16" cy="19" rx="2.5" ry="1.8" fill="#1F2937"/>
      <!-- Tongue -->
      <ellipse cx="16" cy="24" rx="1.5" ry="2" fill="#F87171"/>`;
  },

  'deer': (iconColor = '#fff', bgColor = '#667eea') => {
    return `
      <!-- Antlers -->
      <path d="M9 12 L7 6 M7 6 L5 4 M7 6 L9 5" fill="none" stroke="${iconColor}" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>
      <path d="M23 12 L25 6 M25 6 L27 4 M25 6 L23 5" fill="none" stroke="${iconColor}" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>
      <!-- Head -->
      <ellipse cx="16" cy="18" rx="7" ry="8" fill="${iconColor}"/>
      <!-- Ears -->
      <ellipse cx="7" cy="14" rx="2" ry="3.5" fill="${iconColor}" transform="rotate(-30 7 14)"/>
      <ellipse cx="25" cy="14" rx="2" ry="3.5" fill="${iconColor}" transform="rotate(30 25 14)"/>
      <!-- Eyes -->
      <circle cx="12" cy="16" r="1.5" fill="${bgColor}"/>
      <circle cx="20" cy="16" r="1.5" fill="${bgColor}"/>
      <!-- Nose -->
      <ellipse cx="16" cy="22" rx="2.5" ry="2" fill="${bgColor}"/>`;
  },

  'cow': (iconColor = '#fff', bgColor = '#667eea') => {
    return `
      <!-- Horns -->
      <path d="M8 8 Q6 10 7 13" fill="none" stroke="${iconColor}" stroke-width="2" stroke-linecap="round"/>
      <path d="M24 8 Q26 10 25 13" fill="none" stroke="${iconColor}" stroke-width="2" stroke-linecap="round"/>
      <!-- Ears -->
      <ellipse cx="8" cy="14" rx="2" ry="2.5" fill="${iconColor}"/>
      <ellipse cx="24" cy="14" rx="2" ry="2.5" fill="${iconColor}"/>
      <!-- Face -->
      <ellipse cx="16" cy="17" rx="7" ry="6" fill="${iconColor}"/>
      <!-- Spots -->
      <circle cx="12" cy="14" r="2" fill="${bgColor}"/>
      <circle cx="20" cy="15" r="1.5" fill="${bgColor}"/>
      <!-- Eyes -->
      <circle cx="13" cy="16" r="1" fill="${bgColor}"/>
      <circle cx="19" cy="16" r="1" fill="${bgColor}"/>
      <!-- Snout -->
      <ellipse cx="16" cy="20" rx="3.5" ry="2.5" fill="#FCD5CE"/>
      <!-- Nostrils -->
      <circle cx="14.5" cy="20" r="0.8" fill="${bgColor}"/>
      <circle cx="17.5" cy="20" r="0.8" fill="${bgColor}"/>`;
  },

  'bear': (iconColor = '#fff', bgColor = '#667eea') => {
    return `
      <!-- Ears -->
      <circle cx="8" cy="10" r="3.5" fill="${iconColor}"/>
      <circle cx="8" cy="10" r="2" fill="${bgColor}"/>
      <circle cx="24" cy="10" r="3.5" fill="${iconColor}"/>
      <circle cx="24" cy="10" r="2" fill="${bgColor}"/>
      <!-- Face -->
      <ellipse cx="16" cy="17" rx="8" ry="7" fill="${iconColor}"/>
      <!-- Eyes -->
      <circle cx="12" cy="15" r="1.5" fill="${bgColor}"/>
      <circle cx="20" cy="15" r="1.5" fill="${bgColor}"/>
      <!-- Snout -->
      <ellipse cx="16" cy="19" rx="3" ry="2.5" fill="#D4A574"/>
      <!-- Nose -->
      <ellipse cx="16" cy="18" rx="1.5" ry="1" fill="${bgColor}"/>`;
  },

  'frog': (iconColor = '#fff', bgColor = '#667eea') => {
    return `
      <!-- Eyes (bulging) -->
      <circle cx="10" cy="10" r="4" fill="${iconColor}"/>
      <circle cx="22" cy="10" r="4" fill="${iconColor}"/>
      <circle cx="10" cy="10" r="2" fill="${bgColor}"/>
      <circle cx="22" cy="10" r="2" fill="${bgColor}"/>
      <!-- Face -->
      <ellipse cx="16" cy="18" rx="8" ry="5" fill="${iconColor}"/>
      <!-- Mouth -->
      <path d="M10 19 Q16 23 22 19" fill="none" stroke="${bgColor}" stroke-width="1.5" stroke-linecap="round"/>
      <!-- Nostrils -->
      <circle cx="14" cy="16" r="0.8" fill="${bgColor}"/>
      <circle cx="18" cy="16" r="0.8" fill="${bgColor}"/>`;
  },

  'duck': (iconColor = '#fff', bgColor = '#667eea') => {
    return `
      <!-- Body -->
      <ellipse cx="14" cy="20" rx="8" ry="5" fill="${iconColor}"/>
      <!-- Head -->
      <circle cx="22" cy="12" r="5" fill="${iconColor}"/>
      <!-- Neck -->
      <path d="M18 17 Q22 16 21 14" fill="${iconColor}" stroke="${iconColor}" stroke-width="3"/>
      <!-- Eye -->
      <circle cx="23" cy="11" r="1.2" fill="${bgColor}"/>
      <!-- Beak -->
      <path d="M26 13 L29 12 L29 15 L26 14 Z" fill="#F97316"/>`;
  },

  'fish': (iconColor = '#fff', bgColor = '#667eea') => {
    return `
      <!-- Body -->
      <ellipse cx="15" cy="16" rx="8" ry="5" fill="${iconColor}"/>
      <!-- Tail -->
      <path d="M23 16 L28 12 L28 20 Z" fill="${iconColor}"/>
      <!-- Eye -->
      <circle cx="10" cy="15" r="1.5" fill="${bgColor}"/>
      <!-- Fin -->
      <path d="M15 11 L17 8 L19 11" fill="${iconColor}" stroke="${iconColor}" stroke-width="1"/>
      <!-- Gill -->
      <path d="M12 14 Q13 16 12 18" fill="none" stroke="${bgColor}" stroke-width="1" stroke-linecap="round"/>`;
  },
};

// Generate number icon content generators (1-42)
for (let i = 1; i <= 42; i++) {
  iconContentGenerators[`number-${i}`] = (iconColor = '#fff') => {
    // Adjust font size based on number of digits
    const numStr = String(i);
    let fontSize = 18;
    if (numStr.length === 2) fontSize = 15;

    return `<text x="16" y="16" font-family="Arial, sans-serif" font-size="${fontSize}" font-weight="bold" fill="${iconColor}" text-anchor="middle" dominant-baseline="central">${i}</text>`;
  };
}

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
  // Handle 'default', empty, or missing type
  // If we have custom colors/shape, render with those; otherwise return null for status-based rendering
  if (!type || type === 'default') {
    const hasCustomProperties = backgroundColor || borderColor || iconColor
      || (backgroundShape && backgroundShape !== 'circle' && backgroundShape !== 'default');
    if (!hasCustomProperties) {
      // Truly default - no custom properties, use status-based rendering
      return null;
    }
    // Has custom properties - render the shape with custom colors
    type = (backgroundShape && backgroundShape !== 'default') ? backgroundShape : 'circle';
  }

  const config = getIconTypeConfig(type);
  const sizeMultiplier = parseInt(size, 10) / 100;
  const actualSize = outputSize || Math.round(32 * sizeMultiplier);

  let svgContent = '';

  if (config.isShapeOnly) {
    // For shape-only icons (circle, square, etc.), the shape IS the icon
    // Use the icon's default color only for shape-only icons
    const effectiveBackgroundColor = backgroundColor || config.defaultColor || '#667eea';
    const effectiveBorderColor = borderColor || '#fff';

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
    // For content icons, background color is INDEPENDENT of the icon
    // Don't use the icon's defaultColor for background - use passed color or generic default
    const effectiveBackgroundColor = backgroundColor || '#667eea';
    const effectiveBorderColor = borderColor || '#fff';
    const effectiveIconColor = iconColor || '#fff';

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
