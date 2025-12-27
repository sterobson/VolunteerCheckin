/**
 * Coordinate and geospatial utilities
 * Functions for working with latitude, longitude, and distances
 */

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
