/**
 * Utility functions for detecting and preventing label overlap on maps
 */

/**
 * Truncate text to a maximum length with ellipsis
 * @param {string} text - The text to truncate
 * @param {number} maxLength - Maximum length before truncation
 * @returns {string} - Truncated text
 */
export const truncateText = (text, maxLength) => {
  if (!text) return '';
  if (text.length <= maxLength) return text;
  return text.substring(0, maxLength) + '...';
};

/**
 * Estimate label width in pixels based on text length
 * @param {string} text - The text to measure
 * @returns {number} - Estimated width in pixels
 */
export const estimateLabelWidth = (text) => {
  // Approximate: 6px per character for 11px font with bold weight
  // Add padding (6px on each side = 12px total)
  return (text.length * 6) + 12;
};

/**
 * Check if two bounding boxes overlap
 * @param {Object} box1 - First box {left, right, top, bottom}
 * @param {Object} box2 - Second box {left, right, top, bottom}
 * @returns {boolean} - Whether the boxes overlap
 */
export const boxesOverlap = (box1, box2) => {
  return !(box1.right < box2.left ||
           box1.left > box2.right ||
           box1.bottom < box2.top ||
           box1.top > box2.bottom);
};

/**
 * Calculate which markers can show descriptions without overlapping
 * @param {Object} map - Leaflet map instance
 * @param {Array} markers - Array of Leaflet marker objects with locationData
 * @returns {Set} - Set of location IDs that can show descriptions
 */
export const calculateVisibleDescriptions = (map, markers) => {
  if (!map || markers.length === 0) return new Set();

  const visibleMarkers = [];
  const bounds = map.getBounds();

  // Get visible markers with their pixel positions
  markers.forEach((marker) => {
    if (bounds.contains(marker.getLatLng())) {
      const pixelPos = map.latLngToContainerPoint(marker.getLatLng());
      const location = marker.locationData;
      if (location) {
        visibleMarkers.push({ marker, pixelPos, location });
      }
    }
  });

  // Calculate bounding boxes for each marker
  const markerBoxes = visibleMarkers.map(({ marker, pixelPos, location }) => {
    const markerRadius = 15; // 30px diameter / 2
    const labelHeight = 20; // Approximate label height
    const baseLabelWidth = estimateLabelWidth(location.name);
    const descLabelWidth = location.description
      ? estimateLabelWidth(`${location.name}: ${truncateText(location.description, 50)}`)
      : baseLabelWidth;

    return {
      marker,
      location,
      // Marker circle box
      markerBox: {
        left: pixelPos.x - markerRadius,
        right: pixelPos.x + markerRadius,
        top: pixelPos.y - markerRadius,
        bottom: pixelPos.y + markerRadius,
      },
      // Base label box (name only)
      baseLabelBox: {
        left: pixelPos.x - baseLabelWidth / 2,
        right: pixelPos.x + baseLabelWidth / 2,
        top: pixelPos.y + markerRadius + 2,
        bottom: pixelPos.y + markerRadius + 2 + labelHeight,
      },
      // Extended label box (with description)
      descLabelBox: location.description ? {
        left: pixelPos.x - descLabelWidth / 2,
        right: pixelPos.x + descLabelWidth / 2,
        top: pixelPos.y + markerRadius + 2,
        bottom: pixelPos.y + markerRadius + 2 + labelHeight,
      } : null,
    };
  });

  // Determine which markers can show descriptions without overlap
  const canShowDescription = new Set();

  markerBoxes.forEach(({ marker, location, markerBox, baseLabelBox, descLabelBox }, i) => {
    if (!location.description) return; // No description to show

    let hasOverlap = false;

    // Check against all other markers
    for (let j = 0; j < markerBoxes.length; j++) {
      if (i === j) continue;

      const other = markerBoxes[j];

      // Check if this marker's description box would overlap with:
      // 1. Other marker's circle
      // 2. Other marker's base label
      if (boxesOverlap(descLabelBox, other.markerBox) ||
          boxesOverlap(descLabelBox, other.baseLabelBox)) {
        hasOverlap = true;
        break;
      }

      // If the other marker will show description, check against that too
      if (other.descLabelBox && canShowDescription.has(other.location.id)) {
        if (boxesOverlap(descLabelBox, other.descLabelBox)) {
          hasOverlap = true;
          break;
        }
      }
    }

    if (!hasOverlap) {
      canShowDescription.add(location.id);
    }
  });

  return canShowDescription;
};
