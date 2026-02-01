/**
 * Coordinate and geospatial utilities
 * Functions for working with latitude, longitude, and distances
 */

// Default precision for coordinate rounding (~11cm accuracy)
const COORDINATE_PRECISION = 6;

/**
 * Round a coordinate value to specified decimal places
 * @param {number} value - The coordinate value
 * @param {number} decimals - Number of decimal places (default: 6 for ~11cm precision)
 * @returns {number} Rounded coordinate as a number
 */
export const roundCoordinate = (value, decimals = COORDINATE_PRECISION) => {
  if (value == null || isNaN(value)) return value;
  const factor = Math.pow(10, decimals);
  return Math.round(value * factor) / factor;
};

/**
 * Round latitude and longitude in a coordinate object
 * @param {object} coords - Object with lat/latitude and lng/longitude properties
 * @param {number} decimals - Number of decimal places (default: 6)
 * @returns {object} New object with rounded coordinates
 */
export const roundCoordinateObject = (coords, decimals = COORDINATE_PRECISION) => {
  if (!coords) return coords;
  const result = { ...coords };

  // Handle lat/lng format
  if ('lat' in result) result.lat = roundCoordinate(result.lat, decimals);
  if ('lng' in result) result.lng = roundCoordinate(result.lng, decimals);

  // Handle latitude/longitude format
  if ('latitude' in result) result.latitude = roundCoordinate(result.latitude, decimals);
  if ('longitude' in result) result.longitude = roundCoordinate(result.longitude, decimals);

  // Handle Lat/Lng format (PascalCase)
  if ('Lat' in result) result.Lat = roundCoordinate(result.Lat, decimals);
  if ('Lng' in result) result.Lng = roundCoordinate(result.Lng, decimals);

  return result;
};

/**
 * Round all points in a route array
 * @param {Array<{lat: number, lng: number}>} route - Array of route points
 * @param {number} decimals - Number of decimal places (default: 6)
 * @returns {Array} New array with rounded coordinates
 */
export const roundRoutePoints = (route, decimals = COORDINATE_PRECISION) => {
  if (!route || !Array.isArray(route)) return route;
  return route.map(point => roundCoordinateObject(point, decimals));
};

/**
 * Round all points in a polygon array
 * @param {Array<{lat: number, lng: number}>} polygon - Array of polygon points
 * @param {number} decimals - Number of decimal places (default: 6)
 * @returns {Array} New array with rounded coordinates
 */
export const roundPolygonPoints = (polygon, decimals = COORDINATE_PRECISION) => {
  // Same implementation as route points
  return roundRoutePoints(polygon, decimals);
};

/**
 * Calculate distance between two coordinates using Haversine formula
 * @param {number} lat1 - First latitude
 * @param {number} lon1 - First longitude
 * @param {number} lat2 - Second latitude
 * @param {number} lon2 - Second longitude
 * @returns {number} Distance in meters
 */
export const calculateDistance = (lat1, lon1, lat2, lon2) => {
  const R = 6371000; // Earth's radius in meters
  const φ1 = (lat1 * Math.PI) / 180;
  const φ2 = (lat2 * Math.PI) / 180;
  const Δφ = ((lat2 - lat1) * Math.PI) / 180;
  const Δλ = ((lon2 - lon1) * Math.PI) / 180;

  const a =
    Math.sin(Δφ / 2) * Math.sin(Δφ / 2) +
    Math.cos(φ1) * Math.cos(φ2) * Math.sin(Δλ / 2) * Math.sin(Δλ / 2);
  const c = 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a));

  return R * c;
};

/**
 * Format coordinate value to specified decimal places
 * @param {number|string} value - The coordinate value
 * @param {number} decimals - Number of decimal places (default: 6)
 * @returns {string} Formatted coordinate
 */
export const formatCoordinate = (value, decimals = 6) => {
  if (!value && value !== 0) return '';
  return parseFloat(value).toFixed(decimals);
};

/**
 * Format latitude with N/S designation
 * @param {number} lat - Latitude value
 * @param {number} decimals - Number of decimal places (default: 6)
 * @returns {string} Formatted latitude (e.g., "45.123456°N")
 */
export const formatLatitude = (lat, decimals = 6) => {
  if (!lat && lat !== 0) return '';
  const formatted = Math.abs(lat).toFixed(decimals);
  const direction = lat >= 0 ? 'N' : 'S';
  return `${formatted}°${direction}`;
};

/**
 * Format longitude with E/W designation
 * @param {number} lon - Longitude value
 * @param {number} decimals - Number of decimal places (default: 6)
 * @returns {string} Formatted longitude (e.g., "123.456789°W")
 */
export const formatLongitude = (lon, decimals = 6) => {
  if (!lon && lon !== 0) return '';
  const formatted = Math.abs(lon).toFixed(decimals);
  const direction = lon >= 0 ? 'E' : 'W';
  return `${formatted}°${direction}`;
};

/**
 * Format coordinate pair as string
 * @param {number} lat - Latitude
 * @param {number} lon - Longitude
 * @param {number} decimals - Number of decimal places (default: 6)
 * @returns {string} Formatted coordinate pair (e.g., "45.123456, -123.456789")
 */
export const formatCoordinatePair = (lat, lon, decimals = 6) => {
  if ((!lat && lat !== 0) || (!lon && lon !== 0)) return '';
  return `${formatCoordinate(lat, decimals)}, ${formatCoordinate(lon, decimals)}`;
};

/**
 * Convert meters to human-readable distance
 * @param {number} meters - Distance in meters
 * @returns {string} Formatted distance (e.g., "25 m" or "1.5 km")
 */
export const formatDistance = (meters) => {
  if (!meters && meters !== 0) return '';
  if (meters < 1000) {
    return `${Math.round(meters)} m`;
  }
  return `${(meters / 1000).toFixed(2)} km`;
};

/**
 * Convert meters to miles
 * @param {number} meters - Distance in meters
 * @returns {number} Distance in miles
 */
export const metersToMiles = (meters) => {
  return meters * 0.000621371;
};

/**
 * Convert meters to feet
 * @param {number} meters - Distance in meters
 * @returns {number} Distance in feet
 */
export const metersToFeet = (meters) => {
  return meters * 3.28084;
};

/**
 * Check if coordinate is within bounds of another coordinate
 * @param {number} lat1 - First latitude
 * @param {number} lon1 - First longitude
 * @param {number} lat2 - Second latitude
 * @param {number} lon2 - Second longitude
 * @param {number} radiusMeters - Radius in meters
 * @returns {boolean} True if within radius
 */
export const isWithinRadius = (lat1, lon1, lat2, lon2, radiusMeters) => {
  const distance = calculateDistance(lat1, lon1, lat2, lon2);
  return distance <= radiusMeters;
};

/**
 * Get center point between multiple coordinates
 * @param {Array<{lat: number, lon: number}>} coordinates - Array of coordinate objects
 * @returns {{lat: number, lon: number}} Center coordinate
 */
export const getCenterPoint = (coordinates) => {
  if (!coordinates || coordinates.length === 0) {
    return { lat: 0, lon: 0 };
  }

  let x = 0;
  let y = 0;
  let z = 0;

  coordinates.forEach(({ lat, lon }) => {
    const latitude = (lat * Math.PI) / 180;
    const longitude = (lon * Math.PI) / 180;

    x += Math.cos(latitude) * Math.cos(longitude);
    y += Math.cos(latitude) * Math.sin(longitude);
    z += Math.sin(latitude);
  });

  const total = coordinates.length;

  x = x / total;
  y = y / total;
  z = z / total;

  const centralLongitude = Math.atan2(y, x);
  const centralSquareRoot = Math.sqrt(x * x + y * y);
  const centralLatitude = Math.atan2(z, centralSquareRoot);

  return {
    lat: (centralLatitude * 180) / Math.PI,
    lon: (centralLongitude * 180) / Math.PI,
  };
};

/**
 * Parse coordinate string to object
 * @param {string} coordString - Coordinate string (e.g., "45.123, -123.456")
 * @returns {{lat: number, lon: number}|null} Coordinate object or null if invalid
 */
export const parseCoordinateString = (coordString) => {
  if (!coordString) return null;

  const parts = coordString.split(',').map((s) => s.trim());
  if (parts.length !== 2) return null;

  const lat = parseFloat(parts[0]);
  const lon = parseFloat(parts[1]);

  if (isNaN(lat) || isNaN(lon)) return null;
  if (lat < -90 || lat > 90 || lon < -180 || lon > 180) return null;

  return { lat, lon };
};

/**
 * Calculate the minimum distance from a point to a line segment
 * @param {number} pointLat - Point latitude
 * @param {number} pointLon - Point longitude
 * @param {number} segStartLat - Segment start latitude
 * @param {number} segStartLon - Segment start longitude
 * @param {number} segEndLat - Segment end latitude
 * @param {number} segEndLon - Segment end longitude
 * @returns {number} Distance in meters to the closest point on the segment
 */
export const distanceToLineSegment = (pointLat, pointLon, segStartLat, segStartLon, segEndLat, segEndLon) => {
  // Vector from segment start to point
  const dx = pointLon - segStartLon;
  const dy = pointLat - segStartLat;

  // Vector from segment start to segment end
  const sx = segEndLon - segStartLon;
  const sy = segEndLat - segStartLat;

  // Segment length squared
  const segLengthSq = sx * sx + sy * sy;

  if (segLengthSq === 0) {
    // Segment is a point, return distance to that point
    return calculateDistance(pointLat, pointLon, segStartLat, segStartLon);
  }

  // Project point onto segment line, clamped to [0, 1]
  const t = Math.max(0, Math.min(1, (dx * sx + dy * sy) / segLengthSq));

  // Closest point on segment
  const closestLon = segStartLon + t * sx;
  const closestLat = segStartLat + t * sy;

  return calculateDistance(pointLat, pointLon, closestLat, closestLon);
};

/**
 * Get latitude from a route point, handling both camelCase and PascalCase
 * @param {object} point - Route point object
 * @returns {number|undefined} Latitude value
 */
const getPointLat = (point) => point.lat ?? point.Lat;

/**
 * Get longitude from a route point, handling both camelCase and PascalCase
 * @param {object} point - Route point object
 * @returns {number|undefined} Longitude value
 */
const getPointLng = (point) => point.lng ?? point.Lng;

/**
 * Calculate the minimum distance from a point to a route (polyline)
 * @param {number} pointLat - Point latitude
 * @param {number} pointLon - Point longitude
 * @param {Array<{lat: number, lng: number}>} route - Array of route points
 * @returns {number} Minimum distance in meters to the route
 */
export const distanceToRoute = (pointLat, pointLon, route) => {
  if (!route || route.length === 0) return Infinity;

  if (route.length === 1) {
    const p = route[0];
    return calculateDistance(pointLat, pointLon, getPointLat(p), getPointLng(p));
  }

  let minDistance = Infinity;

  for (let i = 0; i < route.length - 1; i++) {
    const segStart = route[i];
    const segEnd = route[i + 1];
    const dist = distanceToLineSegment(
      pointLat, pointLon,
      getPointLat(segStart), getPointLng(segStart),
      getPointLat(segEnd), getPointLng(segEnd)
    );
    if (dist < minDistance) {
      minDistance = dist;
    }
  }

  return minDistance;
};

/**
 * Get route from a layer, handling both camelCase and PascalCase
 * @param {object} layer - Layer object
 * @returns {Array|undefined} Route array
 */
const getLayerRoute = (layer) => layer.route ?? layer.Route;

/**
 * Find layers whose routes pass within a given distance of a point
 * @param {number} pointLat - Point latitude
 * @param {number} pointLon - Point longitude
 * @param {Array<{id: string, name: string, route: Array}>} layers - Array of layer objects with routes
 * @param {number} maxDistanceMeters - Maximum distance in meters (default: 50)
 * @returns {Array<{layer: object, distance: number}>} Array of matching layers with their distances, sorted by distance
 */
export const findLayersNearPoint = (pointLat, pointLon, layers, maxDistanceMeters = 50) => {
  if (!layers || layers.length === 0) return [];
  if (pointLat == null || pointLon == null) return [];

  const matches = [];

  for (const layer of layers) {
    const route = getLayerRoute(layer);
    if (!route || route.length === 0) continue;

    const distance = distanceToRoute(pointLat, pointLon, route);
    if (distance <= maxDistanceMeters) {
      matches.push({ layer, distance });
    }
  }

  // Sort by distance (closest first)
  matches.sort((a, b) => a.distance - b.distance);

  return matches;
};
