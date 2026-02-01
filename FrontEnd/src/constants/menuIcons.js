/**
 * Menu Icons - Shared SVG icons for menus and UI elements
 *
 * Each icon is an object with:
 * - viewBox: SVG viewBox attribute
 * - paths: Array of path definitions (d attribute and optional fill/stroke overrides)
 *
 * Usage: Import and use with MenuIcon component or directly in templates
 */

// Checkpoint - map pin marker
export const ICON_CHECKPOINT = {
  viewBox: '0 0 24 24',
  paths: [
    { d: 'M12 2C8.13 2 5 5.13 5 9c0 5.25 7 13 7 13s7-7.75 7-13c0-3.87-3.13-7-7-7zm0 9.5c-1.38 0-2.5-1.12-2.5-2.5s1.12-2.5 2.5-2.5 2.5 1.12 2.5 2.5-1.12 2.5-2.5 2.5z', fill: 'currentColor' },
  ],
};

// Many checkpoints - multiple markers/dots
export const ICON_MANY_CHECKPOINTS = {
  viewBox: '0 0 24 24',
  paths: [
    { d: 'M12 2C8.13 2 5 5.13 5 9c0 4.17 4.42 9.92 6.24 12.11.4.48 1.13.48 1.53 0C14.58 18.92 19 13.17 19 9c0-3.87-3.13-7-7-7zm0 9.5c-1.38 0-2.5-1.12-2.5-2.5s1.12-2.5 2.5-2.5 2.5 1.12 2.5 2.5-1.12 2.5-2.5 2.5z', fill: 'currentColor', opacity: '0.4' },
    { d: 'M17 8c0-2.76-2.24-5-5-5S7 5.24 7 8c0 3.53 3.13 7.36 4.43 8.88a.749.749 0 001.14 0C13.87 15.36 17 11.53 17 8zm-5 2.5c-1.38 0-2.5-1.12-2.5-2.5S10.62 5.5 12 5.5s2.5 1.12 2.5 2.5-1.12 2.5-2.5 2.5z', fill: 'currentColor' },
  ],
};

// Import - upload/import arrow
export const ICON_IMPORT = {
  viewBox: '0 0 24 24',
  paths: [
    { d: 'M19 9h-4V3H9v6H5l7 7 7-7zM5 18v2h14v-2H5z', fill: 'currentColor' },
  ],
};

// Zone/Area - polygon shape
export const ICON_ZONE = {
  viewBox: '0 0 24 24',
  paths: [
    { d: 'M12 2L2 7l10 5 10-5-10-5zM2 17l10 5 10-5M2 12l10 5 10-5', fill: 'none', stroke: 'currentColor', strokeWidth: '2', strokeLinecap: 'round', strokeLinejoin: 'round' },
  ],
};

// Alternative Zone icon - hexagon/polygon area
export const ICON_ZONE_ALT = {
  viewBox: '0 0 24 24',
  paths: [
    { d: 'M12 2l9 5v10l-9 5-9-5V7l9-5z', fill: 'none', stroke: 'currentColor', strokeWidth: '2', strokeLinejoin: 'round' },
  ],
};

// Upload - file upload
export const ICON_UPLOAD = {
  viewBox: '0 0 24 24',
  paths: [
    { d: 'M9 16h6v-6h4l-7-7-7 7h4v6zm-4 2h14v2H5v-2z', fill: 'currentColor' },
  ],
};

// Plus - add new
export const ICON_PLUS = {
  viewBox: '0 0 24 24',
  paths: [
    { d: 'M12 5v14M5 12h14', fill: 'none', stroke: 'currentColor', strokeWidth: '2', strokeLinecap: 'round' },
  ],
};

// Edit - pencil
export const ICON_EDIT = {
  viewBox: '0 0 24 24',
  paths: [
    { d: 'M11 4H4a2 2 0 00-2 2v14a2 2 0 002 2h14a2 2 0 002-2v-7', fill: 'none', stroke: 'currentColor', strokeWidth: '2', strokeLinecap: 'round', strokeLinejoin: 'round' },
    { d: 'M18.5 2.5a2.121 2.121 0 013 3L12 15l-4 1 1-4 9.5-9.5z', fill: 'none', stroke: 'currentColor', strokeWidth: '2', strokeLinecap: 'round', strokeLinejoin: 'round' },
  ],
};

// Route - path/line
export const ICON_ROUTE = {
  viewBox: '0 0 24 24',
  paths: [
    { d: 'M3 17l4-4 4 4 4-4 4 4', fill: 'none', stroke: 'currentColor', strokeWidth: '2', strokeLinecap: 'round', strokeLinejoin: 'round' },
    { d: 'M12 3v4M12 17v4', fill: 'none', stroke: 'currentColor', strokeWidth: '2', strokeLinecap: 'round' },
  ],
};

// Course - winding path with start/end markers (matches Course tab icon)
export const ICON_COURSE = {
  viewBox: '0 0 24 24',
  circles: [
    { cx: '6', cy: '19', r: '3' },
    { cx: '18', cy: '5', r: '3' },
  ],
  paths: [
    { d: 'M9 19h8.5a3.5 3.5 0 0 0 0-7h-11a3.5 3.5 0 0 1 0-7H15', fill: 'none', stroke: 'currentColor', strokeWidth: '2', strokeLinecap: 'round', strokeLinejoin: 'round' },
  ],
};

// All available menu icons for easy lookup
export const MENU_ICONS = {
  checkpoint: ICON_CHECKPOINT,
  'many-checkpoints': ICON_MANY_CHECKPOINTS,
  import: ICON_IMPORT,
  zone: ICON_ZONE,
  'zone-alt': ICON_ZONE_ALT,
  upload: ICON_UPLOAD,
  plus: ICON_PLUS,
  edit: ICON_EDIT,
  route: ICON_ROUTE,
  course: ICON_COURSE,
};

/**
 * Renders a menu icon to an SVG string
 * @param {Object} icon - Icon definition from MENU_ICONS
 * @param {number} size - Icon size in pixels (default 18)
 * @returns {string} SVG markup string
 */
export function renderMenuIcon(icon, size = 18) {
  if (!icon) return '';

  let elements = '';

  // Render circles if present
  if (icon.circles) {
    elements += icon.circles.map(c => {
      const attrs = [`cx="${c.cx}"`, `cy="${c.cy}"`, `r="${c.r}"`];
      attrs.push('fill="none"', 'stroke="currentColor"', 'stroke-width="2"');
      return `<circle ${attrs.join(' ')}/>`;
    }).join('');
  }

  // Render paths
  if (icon.paths) {
    elements += icon.paths.map(p => {
      const attrs = [`d="${p.d}"`];
      if (p.fill) attrs.push(`fill="${p.fill}"`);
      if (p.opacity) attrs.push(`opacity="${p.opacity}"`);
      if (p.stroke) attrs.push(`stroke="${p.stroke}"`);
      if (p.strokeWidth) attrs.push(`stroke-width="${p.strokeWidth}"`);
      if (p.strokeLinecap) attrs.push(`stroke-linecap="${p.strokeLinecap}"`);
      if (p.strokeLinejoin) attrs.push(`stroke-linejoin="${p.strokeLinejoin}"`);
      return `<path ${attrs.join(' ')}/>`;
    }).join('');
  }

  return `<svg xmlns="http://www.w3.org/2000/svg" viewBox="${icon.viewBox}" width="${size}" height="${size}">${elements}</svg>`;
}
