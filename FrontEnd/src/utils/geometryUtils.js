/**
 * Check if a point is inside a polygon using ray casting algorithm
 * @param {Object} point - Point with lat and lng properties
 * @param {Array} polygon - Array of points with lat and lng properties
 * @returns {boolean} True if point is inside polygon
 */
export function isPointInPolygon(point, polygon) {
  if (!polygon || polygon.length < 3) {
    return false;
  }

  let inside = false;
  const x = point.lng;
  const y = point.lat;

  for (let i = 0, j = polygon.length - 1; i < polygon.length; j = i++) {
    const xi = polygon[i].lng;
    const yi = polygon[i].lat;
    const xj = polygon[j].lng;
    const yj = polygon[j].lat;

    const intersect = ((yi > y) !== (yj > y)) &&
                     (x < (xj - xi) * (y - yi) / (yj - yi) + xi);

    if (intersect) {
      inside = !inside;
    }
  }

  return inside;
}

/**
 * Get all checkpoints that are inside a polygon
 * @param {Array} checkpoints - Array of checkpoint objects with latitude and longitude
 * @param {Array} polygon - Array of points with lat and lng properties
 * @returns {Array} Array of checkpoint IDs that are inside the polygon
 */
export function getCheckpointsInPolygon(checkpoints, polygon) {
  if (!polygon || polygon.length < 3) {
    return [];
  }

  return checkpoints
    .filter(checkpoint => {
      const point = {
        lat: checkpoint.latitude,
        lng: checkpoint.longitude,
      };
      return isPointInPolygon(point, polygon);
    })
    .map(checkpoint => checkpoint.id);
}
