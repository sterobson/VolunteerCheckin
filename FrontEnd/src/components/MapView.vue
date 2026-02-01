<template>
  <div class="map-container-wrapper">
    <div ref="mapContainer" class="map-container"></div>

    <!-- Recenter button (shows when highlighted location is out of view) -->
    <button
      v-if="showRecenterButton"
      class="recenter-btn"
      @click="recenterOnHighlighted"
      title="Recenter on checkpoint"
    >
      <svg viewBox="0 0 24 24" fill="currentColor" width="18" height="18">
        <path d="M12 8c-2.21 0-4 1.79-4 4s1.79 4 4 4 4-1.79 4-4-1.79-4-4-4zm8.94 3c-.46-4.17-3.77-7.48-7.94-7.94V1h-2v2.06C6.83 3.52 3.52 6.83 3.06 11H1v2h2.06c.46 4.17 3.77 7.48 7.94 7.94V23h2v-2.06c4.17-.46 7.48-3.77 7.94-7.94H23v-2h-2.06zM12 19c-3.87 0-7-3.13-7-7s3.13-7 7-7 7 3.13 7 7-3.13 7-7 7z"/>
      </svg>
      <span>Recenter</span>
    </button>
  </div>
</template>

<script setup>
import { ref, computed, onMounted, onUnmounted, watch, defineProps, defineEmits } from 'vue';
import L from 'leaflet';
import 'leaflet/dist/leaflet.css';
import 'leaflet-draw/dist/leaflet.draw.css';
import 'leaflet-draw';
import { truncateText, calculateVisibleDescriptions } from '../utils/labelOverlapDetection';
import {
  getCheckpointMarkerHtml,
  getStatusBadgeHtml,
  generateCheckpointSvg,
} from '../constants/checkpointIcons';

const props = defineProps({
  locations: {
    type: Array,
    default: () => [],
  },
  route: {
    type: Array,
    default: () => [],
  },
  routeColor: {
    type: String,
    default: '',
  },
  routeStyle: {
    type: String,
    default: '',
  },
  routeWeight: {
    type: Number,
    default: null,
  },
  layers: {
    type: Array,
    default: () => [],
  },
  center: {
    type: Object,
    default: null,
  },
  zoom: {
    type: Number,
    default: 13,
  },
  clickable: {
    type: Boolean,
    default: false,
  },
  areas: {
    type: Array,
    default: () => [],
  },
  selectedAreaId: {
    type: String,
    default: null,
  },
  drawingMode: {
    type: Boolean,
    default: false,
  },
  editingPolygon: {
    type: Array,
    default: null,
  },
  editingRoute: {
    type: Array, // [{ lat, lng }, ...]
    default: null,
  },
  drawingRouteMode: {
    type: Boolean,
    default: false,
  },
  userLocation: {
    type: Object,
    default: null,
  },
  highlightLocationId: {
    type: String,
    default: null,
  },
  highlightLocationIds: {
    type: Array,
    default: () => [],
  },
  allLocationsForBounds: {
    type: Array,
    default: () => [],
  },
  marshalMode: {
    type: Boolean,
    default: false,
  },
  showStaffingOverlay: {
    type: Boolean,
    default: true,
  },
  showStatusOverlay: {
    type: Boolean,
    default: true,
  },
  simplifyNonHighlighted: {
    type: Boolean,
    default: false,
  },
  hideRecenterButton: {
    type: Boolean,
    default: false,
  },
  skipAutoCentering: {
    type: Boolean,
    default: false,
  },
});

const emit = defineEmits(['location-click', 'map-click', 'area-click', 'polygon-complete', 'polygon-drawing', 'polygon-update', 'route-update', 'route-drawing', 'route-complete', 'visibility-change']);

const mapContainer = ref(null);
let map = null;

// Polygon drawing state
const polygonPoints = ref([]);
const polygonHistory = ref([]); // For undo - stores previous states
const polygonFuture = ref([]); // For redo - stores undone states
let polygonPreviewLayer = null;
let polygonMarkersLayer = null;

// Polygon editing state
const editingPoints = ref([]);
let editingPolygonLayer = null;
let editingMarkersLayer = null;
let justEmittedPolygonUpdate = false; // Flag to prevent watcher from rebuilding after our own emit

// Route drawing state
const routePoints = ref([]);
const routeHistory = ref([]); // For undo - stores previous states
const routeFuture = ref([]); // For redo - stores undone states
let routePreviewLayer = null;
let routeMarkersLayer = null;

// Route editing state
const editingRoutePoints = ref([]);
const editingRouteHistory = ref([]); // For undo - stores previous states
const editingRouteFuture = ref([]); // For redo - stores undone states
let editingRouteLayer = null;
let editingRouteMarkersLayer = null;
let justEmittedRouteUpdate = false; // Flag to prevent watcher from rebuilding after our own emit

// Track if user has manually panned the map (to avoid auto-recentering)
const userHasPanned = ref(false);

// Expose methods to get current map state and control polygon drawing
const getMapCenter = () => {
  if (map) {
    const center = map.getCenter();
    return { lat: center.lat, lng: center.lng };
  }
  return null;
};

const getMapZoom = () => {
  return map ? map.getZoom() : null;
};

const undoPolygonPoint = () => {
  if (polygonPoints.value.length > 0) {
    // Save current state to future for redo
    polygonFuture.value.push([...polygonPoints.value]);
    // Restore previous state
    const previousState = polygonHistory.value.pop() || [];
    polygonPoints.value = previousState;
    updatePolygonPreview();
    emit('polygon-drawing', polygonPoints.value);
  }
};

const redoPolygonPoint = () => {
  if (polygonFuture.value.length > 0) {
    // Save current state to history for undo
    polygonHistory.value.push([...polygonPoints.value]);
    // Restore future state
    const futureState = polygonFuture.value.pop();
    polygonPoints.value = futureState;
    updatePolygonPreview();
    emit('polygon-drawing', polygonPoints.value);
  }
};

const canUndo = () => polygonPoints.value.length > 0;
const canRedo = () => polygonFuture.value.length > 0;
const getPolygonPointCount = () => polygonPoints.value.length;

const completePolygon = () => {
  if (polygonPoints.value.length >= 3) {
    emit('polygon-complete', [...polygonPoints.value]);
    clearPolygonDrawing();
  }
};

const clearPolygonDrawing = () => {
  polygonPoints.value = [];
  polygonHistory.value = [];
  polygonFuture.value = [];
  updatePolygonPreview();
};

// Route drawing methods
const undoRoutePoint = () => {
  if (routePoints.value.length > 0) {
    // Save current state to future for redo
    routeFuture.value.push([...routePoints.value]);
    // Restore previous state
    const previousState = routeHistory.value.pop() || [];
    routePoints.value = previousState;
    updateRoutePreview();
    emit('route-drawing', routePoints.value);
  }
};

const redoRoutePoint = () => {
  if (routeFuture.value.length > 0) {
    // Save current state to history for undo
    routeHistory.value.push([...routePoints.value]);
    // Restore future state
    const futureState = routeFuture.value.pop();
    routePoints.value = futureState;
    updateRoutePreview();
    emit('route-drawing', routePoints.value);
  }
};

const canUndoRoute = () => routePoints.value.length > 0;
const canRedoRoute = () => routeFuture.value.length > 0;
const getRoutePointCount = () => routePoints.value.length;

const completeRoute = () => {
  if (routePoints.value.length >= 2) {
    emit('route-complete', [...routePoints.value]);
    clearRouteDrawing();
  }
};

const clearRouteDrawing = () => {
  routePoints.value = [];
  routeHistory.value = [];
  routeFuture.value = [];
  updateRoutePreview();
};

// Route editing undo/redo methods
const saveRouteEditState = () => {
  // Save current state to history for undo
  editingRouteHistory.value.push(editingRoutePoints.value.map(p => ({ lat: p.lat, lng: p.lng })));
  // Clear redo stack when new changes are made
  editingRouteFuture.value = [];
};

const undoRouteEdit = () => {
  if (editingRouteHistory.value.length > 0) {
    // Save current state to future for redo
    editingRouteFuture.value.push(editingRoutePoints.value.map(p => ({ lat: p.lat, lng: p.lng })));
    // Restore previous state
    const previousState = editingRouteHistory.value.pop();
    editingRoutePoints.value = previousState;
    justEmittedRouteUpdate = true;
    emit('route-update', [...editingRoutePoints.value]);
    updateEditingRoute();
  }
};

const redoRouteEdit = () => {
  if (editingRouteFuture.value.length > 0) {
    // Save current state to history for undo
    editingRouteHistory.value.push(editingRoutePoints.value.map(p => ({ lat: p.lat, lng: p.lng })));
    // Restore future state
    const futureState = editingRouteFuture.value.pop();
    editingRoutePoints.value = futureState;
    justEmittedRouteUpdate = true;
    emit('route-update', [...editingRoutePoints.value]);
    updateEditingRoute();
  }
};

const canUndoRouteEdit = () => editingRouteHistory.value.length > 0;
const canRedoRouteEdit = () => editingRouteFuture.value.length > 0;
const getEditingRoutePointCount = () => editingRoutePoints.value.length;

// Function to recenter map on user's GPS location
const recenterOnUserLocation = () => {
  if (!map || !props.userLocation) return;

  map.setView([props.userLocation.lat, props.userLocation.lng], map.getZoom(), {
    animate: true,
    duration: 0.5
  });

  // Keep userHasPanned true to prevent auto-recentering back to center
  userHasPanned.value = true;
};

// Function to recenter map on specific coordinates
const recenterOnLocation = (lat, lng, zoom = null) => {
  if (!map || lat == null || lng == null) return;

  map.setView([lat, lng], zoom ?? map.getZoom(), {
    animate: true,
    duration: 0.5
  });

  // Reset user panned state only if recentering to the original center
  // Otherwise keep it true to prevent auto-recentering
  const isReturningToCenter = props.center &&
    Math.abs(props.center.lat - lat) < 0.0001 &&
    Math.abs(props.center.lng - lng) < 0.0001;
  userHasPanned.value = !isReturningToCenter;
};

// Track if highlighted location is in view
const highlightedLocationInView = ref(true);

// Track if user location is in view
const userLocationInView = ref(true);

// Function to check if coordinates are within current map bounds
const isLocationInView = (lat, lng) => {
  if (!map || lat == null || lng == null) return true;
  const bounds = map.getBounds();
  const locationLatLng = L.latLng(lat, lng);
  return bounds.contains(locationLatLng);
};

// Fit map to the editing route bounds
const fitToEditingRouteBounds = (padding = [50, 50]) => {
  if (!map || editingRoutePoints.value.length < 2) return;
  const bounds = L.latLngBounds(editingRoutePoints.value.map(p => [p.lat, p.lng]));
  map.fitBounds(bounds, { padding });
};

defineExpose({
  getMapCenter,
  getMapZoom,
  undoPolygonPoint,
  redoPolygonPoint,
  canUndo,
  canRedo,
  getPolygonPointCount,
  completePolygon,
  clearPolygonDrawing,
  // Route drawing methods
  undoRoutePoint,
  redoRoutePoint,
  canUndoRoute,
  canRedoRoute,
  getRoutePointCount,
  completeRoute,
  clearRouteDrawing,
  // Route editing methods
  undoRouteEdit,
  redoRouteEdit,
  canUndoRouteEdit,
  canRedoRouteEdit,
  getEditingRoutePointCount,
  fitToEditingRouteBounds,
  recenterOnUserLocation,
  recenterOnLocation,
  isLocationInView,
  userLocationInView,
  highlightedLocationInView,
});

const markers = ref([]);
let routePolyline = null;
let routeOutlinePolyline = null;
const isInitialLoad = ref(true);
const hasCenteredOnCheckpoints = ref(false);
const showDescriptionsForIds = ref(new Set());
const areaLayers = ref([]);
let drawControl = null;
let drawnItems = null;
let userLocationMarker = null;

// Computed: should show recenter button
const showRecenterButton = computed(() => {
  // Hide if explicitly disabled via prop
  if (props.hideRecenterButton) return false;
  // Only show if there's a highlighted location (single, not multiple)
  // and the user has panned away from it
  if (!props.highlightLocationId) return false;
  if (!userHasPanned.value) return false;
  return !highlightedLocationInView.value;
});

// Track visibility of each highlighted location (keyed by location ID)
const highlightedLocationsVisibility = ref({});

// Function to check if highlighted location is in current map bounds
const checkHighlightedInView = () => {
  const newVisibility = {};

  // Check single highlighted location first
  if (props.highlightLocationId) {
    const highlightedLocation = props.locations.find(loc => loc.id === props.highlightLocationId);
    if (highlightedLocation?.latitude && highlightedLocation?.longitude) {
      const inView = isLocationInView(highlightedLocation.latitude, highlightedLocation.longitude);
      highlightedLocationInView.value = inView;
      newVisibility[props.highlightLocationId] = inView;
      highlightedLocationsVisibility.value = newVisibility;
      return;
    }
  }

  // Check all items in highlightLocationIds array
  if (props.highlightLocationIds && props.highlightLocationIds.length > 0) {
    let allInView = true;
    for (const locId of props.highlightLocationIds) {
      const location = props.locations.find(loc => loc.id === locId);
      if (location?.latitude && location?.longitude) {
        const inView = isLocationInView(location.latitude, location.longitude);
        newVisibility[locId] = inView;
        if (!inView) allInView = false;
      } else {
        newVisibility[locId] = true; // No coords, consider it "in view" to avoid showing button
      }
    }
    // highlightedLocationInView is true only when ALL are in view (so button shows when any is out of view)
    highlightedLocationInView.value = allInView;
    highlightedLocationsVisibility.value = newVisibility;
    return;
  }

  // No highlighted location to track
  if (!map) {
    highlightedLocationInView.value = true;
    highlightedLocationsVisibility.value = {};
    return;
  }
  highlightedLocationInView.value = true;
  highlightedLocationsVisibility.value = {};
};

// Function to check if user location is in current map bounds
const checkUserLocationInView = () => {
  if (!map || !props.userLocation) {
    userLocationInView.value = true;
    return;
  }
  userLocationInView.value = isLocationInView(props.userLocation.lat, props.userLocation.lng);
};

// Function to check all tracked locations
const checkLocationsInView = () => {
  checkHighlightedInView();
  checkUserLocationInView();

  // Emit visibility change event with detailed info for each highlighted location
  emit('visibility-change', {
    userLocationInView: userLocationInView.value,
    highlightedLocationInView: highlightedLocationInView.value,
    highlightedLocationsVisibility: highlightedLocationsVisibility.value,
  });
};

// Function to recenter map on highlighted location
const recenterOnHighlighted = () => {
  if (!map || !props.highlightLocationId) return;

  const highlightedLocation = props.locations.find(loc => loc.id === props.highlightLocationId);
  if (!highlightedLocation || !highlightedLocation.latitude || !highlightedLocation.longitude) return;

  map.setView([highlightedLocation.latitude, highlightedLocation.longitude], map.getZoom(), {
    animate: true,
    duration: 0.5
  });

  // Reset user panned state since we've recentered
  userHasPanned.value = false;
  highlightedLocationInView.value = true;
};

// System default color for checkpoint markers
const SYSTEM_DEFAULT_COLOR = '#667eea';

// Build default marker with system default color and status badge overlay
const buildDefaultMarker = (checkedInCount, requiredMarshals, isHighlighted, marshalMode, skipBadge = false) => {
  const markerSize = isHighlighted ? '36px' : marshalMode ? '20px' : '30px';
  const borderColor = isHighlighted ? '#667eea' : 'white';
  const statusBadge = skipBadge ? '' : getStatusBadgeHtml(checkedInCount, requiredMarshals);

  return `
    <div style="position: relative; width: ${markerSize}; height: ${markerSize};">
      <div style="
        background-color: ${SYSTEM_DEFAULT_COLOR};
        width: ${markerSize};
        height: ${markerSize};
        border-radius: 50%;
        border: 3px solid ${borderColor};
        box-shadow: 0 2px 5px rgba(0,0,0,0.3);
        display: flex;
        align-items: center;
        justify-content: center;
        color: white;
        font-weight: bold;
      "></div>
      ${skipBadge ? '' : `<div style="position: absolute; bottom: -4px; right: -4px;">
        ${statusBadge}
      </div>`}
    </div>
  `;
};

const checkVisibleMarkersAndUpdate = () => {
  if (!map) return;

  const newShowDescriptionsForIds = calculateVisibleDescriptions(map, markers.value);

  // Only update if the set of visible descriptions changed
  const currentIds = Array.from(showDescriptionsForIds.value).sort().join(',');
  const newIds = Array.from(newShowDescriptionsForIds).sort().join(',');

  if (currentIds !== newIds) {
    showDescriptionsForIds.value = newShowDescriptionsForIds;
    updateMarkers();
  }
};

const updateUserLocationMarker = () => {
  if (userLocationMarker) {
    userLocationMarker.remove();
    userLocationMarker = null;
  }

  if (!map || !props.userLocation) return;

  const userIcon = L.divIcon({
    className: 'user-location-marker',
    html: `
      <div style="
        position: relative;
        width: 20px;
        height: 20px;
      ">
        <div style="
          position: absolute;
          top: 50%;
          left: 50%;
          transform: translate(-50%, -50%);
          width: 20px;
          height: 20px;
          background-color: #4285f4;
          border: 3px solid white;
          border-radius: 50%;
          box-shadow: 0 2px 6px rgba(0,0,0,0.3);
        "></div>
        <div style="
          position: absolute;
          top: 50%;
          left: 50%;
          transform: translate(-50%, -50%);
          width: 40px;
          height: 40px;
          background-color: rgba(66, 133, 244, 0.2);
          border-radius: 50%;
          animation: pulse 2s ease-out infinite;
        "></div>
      </div>
    `,
    iconSize: [40, 40],
    iconAnchor: [20, 20],
  });

  userLocationMarker = L.marker([props.userLocation.lat, props.userLocation.lng], { icon: userIcon })
    .addTo(map)
    .bindPopup('Your location');
};

const initMap = () => {
  if (!mapContainer.value) return;

  // Calculate initial center - use allLocationsForBounds for initial centering if available
  let centerLat = 51.505;
  let centerLng = -0.09;
  let zoomLevel = 13;

  // Try to get a reasonable initial center from available data
  // Use allLocationsForBounds (unfiltered) if available, otherwise fall back to locations
  const locationsForCenter = props.allLocationsForBounds.length > 0
    ? props.allLocationsForBounds
    : props.locations;

  // Helper to check if coordinates are valid (not null, undefined, or 0,0)
  const isValidCoord = (lat, lng) => {
    return lat != null && lng != null && !(lat === 0 && lng === 0);
  };

  const allPoints = [];
  if (locationsForCenter.length > 0) {
    // Filter out locations with invalid coordinates (0,0 or null)
    allPoints.push(...locationsForCenter
      .filter(loc => isValidCoord(loc.latitude, loc.longitude))
      .map(loc => ({ lat: loc.latitude, lng: loc.longitude })));
  }
  if (props.route && props.route.length > 0) {
    allPoints.push(...props.route
      .filter(point => isValidCoord(getPointLat(point), getPointLng(point)))
      .map(point => ({ lat: getPointLat(point), lng: getPointLng(point) })));
  }

  if (props.center) {
    centerLat = props.center.lat;
    centerLng = props.center.lng;
    zoomLevel = props.zoom ?? 13;
  } else if (allPoints.length > 0) {
    // Calculate center from all available points
    centerLat = allPoints.reduce((sum, p) => sum + p.lat, 0) / allPoints.length;
    centerLng = allPoints.reduce((sum, p) => sum + p.lng, 0) / allPoints.length;
    zoomLevel = 10;
  }

  map = L.map(mapContainer.value, {
    // Disable marker zoom animation to prevent markers from desyncing
    // from the map on mobile devices during zoom/pan gestures
    markerZoomAnimation: false,
    // Disable fade animation to reduce compositing layer issues on mobile
    fadeAnimation: false,
  }).setView(
    [centerLat, centerLng],
    zoomLevel
  );

  L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
    attribution: '© OpenStreetMap contributors',
    maxZoom: 19,
  }).addTo(map);

  if (props.clickable) {
    map.on('click', (e) => {
      // Don't emit map-click during drawing mode
      if (!props.drawingMode) {
        emit('map-click', { lat: e.latlng.lat, lng: e.latlng.lng });
      }
    });
  }

  // Track zoom animation state to prevent marker updates during animation
  map.on('zoomstart', () => {
    isZoomAnimating = true;
  });

  // Listen for zoom and move events to update label visibility
  // Also process any pending marker updates that were deferred during zoom
  map.on('zoomend moveend', () => {
    isZoomAnimating = false;
    if (pendingMarkerUpdate) {
      updateMarkers();
    } else {
      checkVisibleMarkersAndUpdate();
    }
    // Check if tracked locations are still in view
    checkLocationsInView();
  });

  // Track when user manually pans the map to avoid auto-recentering
  map.on('dragstart', () => {
    userHasPanned.value = true;
  });

  updateMarkers();
  updateRoute();
  updateAreaPolygons();
  updateUserLocationMarker();
  initDrawingMode();
  initEditingMode();
  initRouteDrawingMode();
  initRouteEditingMode();

  // Check visibility after map is fully initialized (use whenReady to ensure tiles are loaded)
  map.whenReady(() => {
    checkLocationsInView();
  });
};

// Helper functions to get lat/lng from route points, handling both camelCase and PascalCase
const getPointLat = (point) => point.lat ?? point.Lat;
const getPointLng = (point) => point.lng ?? point.Lng;

// Helper function to get dash array and line cap for a route style
const getRouteStyleSettings = (style) => {
  let dashArray = null;
  let lineCap = 'butt';

  switch (style) {
    case 'dash':
      dashArray = '10, 10';
      break;
    case 'dash-long':
      dashArray = '20, 12';
      break;
    case 'dash-short':
      dashArray = '6, 6';
      break;
    case 'dash-dense':
      dashArray = '6, 3';
      break;
    case 'dot':
      dashArray = '2, 8';
      lineCap = 'round';
      break;
    case 'dot-sparse':
      dashArray = '2, 14';
      lineCap = 'round';
      break;
    case 'dot-dense':
      dashArray = '2, 2';
      lineCap = 'round';
      break;
    case 'dash-dot':
      dashArray = '16, 8, 4, 8';
      break;
    case 'dash-dot-dot':
      dashArray = '16, 6, 4, 6, 4, 6';
      break;
    case 'long-short':
      dashArray = '16, 4, 6, 4';
      break;
    case 'double-dash':
      dashArray = '8, 3, 8, 8';
      break;
    // 'line' and default: no dash array (solid line)
  }

  return { dashArray, lineCap };
};

// Track layer polylines (outlines first, then main lines)
let layerOutlinePolylines = [];
let layerPolylines = [];

// Darken a hex color by a percentage (0-1)
const darkenColor = (hex, amount = 0.3) => {
  // Remove # if present
  hex = hex.replace('#', '');
  // Parse RGB
  let r = parseInt(hex.substring(0, 2), 16);
  let g = parseInt(hex.substring(2, 4), 16);
  let b = parseInt(hex.substring(4, 6), 16);
  // Darken
  r = Math.max(0, Math.floor(r * (1 - amount)));
  g = Math.max(0, Math.floor(g * (1 - amount)));
  b = Math.max(0, Math.floor(b * (1 - amount)));
  // Return hex
  return `#${r.toString(16).padStart(2, '0')}${g.toString(16).padStart(2, '0')}${b.toString(16).padStart(2, '0')}`;
};

const updateRoute = () => {
  // Remove existing main route polylines
  if (routeOutlinePolyline) {
    routeOutlinePolyline.remove();
    routeOutlinePolyline = null;
  }
  if (routePolyline) {
    routePolyline.remove();
    routePolyline = null;
  }

  // Remove existing layer polylines (outlines and main)
  layerOutlinePolylines.forEach(polyline => polyline.remove());
  layerOutlinePolylines = [];
  layerPolylines.forEach(polyline => polyline.remove());
  layerPolylines = [];

  if (!map) return;

  // Render main route (legacy support)
  if (props.route.length > 0) {
    const routeCoordinates = props.route.map((point) => [getPointLat(point), getPointLng(point)]);
    const color = props.routeColor || '#3388ff';
    const weight = props.routeWeight || 4;
    const { dashArray, lineCap } = getRouteStyleSettings(props.routeStyle);

    // Draw outline first (darker, thicker, butt lineCap for clean edges)
    const outlineColor = darkenColor(color, 0.4);
    routeOutlinePolyline = L.polyline(routeCoordinates, {
      color: outlineColor,
      weight: weight + 2,
      opacity: 0.7,
      dashArray,
      lineCap: 'butt',
    }).addTo(map);

    // Draw main route on top
    routePolyline = L.polyline(routeCoordinates, {
      color,
      weight,
      opacity: 0.7,
      dashArray,
      lineCap,
    }).addTo(map);
  }

  // Render layer routes
  if (props.layers && props.layers.length > 0) {
    props.layers.forEach(layer => {
      if (!layer.route || layer.route.length === 0) return;

      const routeCoordinates = layer.route.map((point) => [getPointLat(point), getPointLng(point)]);
      const color = layer.routeColor || '#3388ff';
      const weight = layer.routeWeight || 4;
      const { dashArray, lineCap } = getRouteStyleSettings(layer.routeStyle);

      // Draw outline first (darker, thicker, butt lineCap for clean edges)
      const outlineColor = darkenColor(color, 0.4);
      const outlinePolyline = L.polyline(routeCoordinates, {
        color: outlineColor,
        weight: weight + 2,
        opacity: 0.7,
        dashArray,
        lineCap: 'butt',
      }).addTo(map);
      layerOutlinePolylines.push(outlinePolyline);

      // Draw main route on top
      const polyline = L.polyline(routeCoordinates, {
        color,
        weight,
        opacity: 0.7,
        dashArray,
        lineCap,
      }).addTo(map);
      layerPolylines.push(polyline);
    });
  }
};

const updateAreaPolygons = () => {
  // Remove existing area layers
  areaLayers.value.forEach((layer) => layer.remove());
  areaLayers.value = [];

  if (!map || props.areas.length === 0) return;

  props.areas.forEach((area) => {
    // Parse polygon if it's a string
    const polygon = typeof area.polygon === 'string'
      ? JSON.parse(area.polygon)
      : area.polygon;

    if (!polygon || polygon.length === 0) return;

    const coords = polygon.map((p) => [p.lat, p.lng]);
    const isSelected = area.id === props.selectedAreaId;
    const areaColor = area.color || '#667eea';

    const polygonLayer = L.polygon(coords, {
      color: areaColor,
      fillColor: areaColor,
      fillOpacity: isSelected ? 0.4 : 0.2,
      weight: isSelected ? 3 : 2,
      opacity: 0.8,
    }).addTo(map);

    // Set z-index to be above route but below markers
    polygonLayer.setStyle({ pane: 'overlayPane' });

    // Only allow area clicks when not in drawing mode
    if (!props.drawingMode) {
      polygonLayer.on('click', () => emit('area-click', area));
      polygonLayer.on('mouseover', () => {
        polygonLayer.setStyle({ fillOpacity: 0.4 });
      });
      polygonLayer.on('mouseout', () => {
        polygonLayer.setStyle({ fillOpacity: isSelected ? 0.4 : 0.2 });
      });
    }

    areaLayers.value.push(polygonLayer);
  });
};

// Update the polygon preview on the map
const updatePolygonPreview = () => {
  if (!map) return;

  // Remove existing preview layers
  if (polygonPreviewLayer) {
    polygonPreviewLayer.remove();
    polygonPreviewLayer = null;
  }
  if (polygonMarkersLayer) {
    polygonMarkersLayer.remove();
    polygonMarkersLayer = null;
  }

  if (polygonPoints.value.length === 0) return;

  // Create a layer group for point markers
  polygonMarkersLayer = L.layerGroup().addTo(map);

  // Add markers for each point
  polygonPoints.value.forEach((point, index) => {
    const isFirst = index === 0;
    const isLast = index === polygonPoints.value.length - 1;

    const markerIcon = L.divIcon({
      className: 'polygon-point-marker',
      html: `<div style="
        width: ${isFirst ? '16px' : '12px'};
        height: ${isFirst ? '16px' : '12px'};
        background-color: ${isFirst ? '#667eea' : isLast ? '#ff9800' : 'white'};
        border: 3px solid #667eea;
        border-radius: 50%;
        box-shadow: 0 2px 4px rgba(0,0,0,0.3);
      "></div>`,
      iconSize: [isFirst ? 16 : 12, isFirst ? 16 : 12],
      iconAnchor: [isFirst ? 8 : 6, isFirst ? 8 : 6],
    });

    L.marker([point.lat, point.lng], { icon: markerIcon, interactive: false })
      .addTo(polygonMarkersLayer);
  });

  // Draw lines between points
  if (polygonPoints.value.length >= 2) {
    const coords = polygonPoints.value.map(p => [p.lat, p.lng]);
    // Close the polygon visually if we have 3+ points
    if (polygonPoints.value.length >= 3) {
      coords.push(coords[0]);
    }
    polygonPreviewLayer = L.polyline(coords, {
      color: '#667eea',
      weight: 3,
      opacity: 0.8,
      dashArray: polygonPoints.value.length < 3 ? '10, 10' : null,
    }).addTo(map);
  }
};

// Update the route preview on the map (for draw-route mode)
// Now supports dragging points to move them and right-click/tap to delete
const updateRoutePreview = () => {
  if (!map) return;

  // Remove existing preview layers
  if (routePreviewLayer) {
    routePreviewLayer.remove();
    routePreviewLayer = null;
  }
  if (routeMarkersLayer) {
    routeMarkersLayer.remove();
    routeMarkersLayer = null;
  }

  if (routePoints.value.length === 0) return;

  // Create a layer group for point markers
  routeMarkersLayer = L.layerGroup().addTo(map);

  // Draw lines between points first (route is always open-ended, never closed)
  if (routePoints.value.length >= 2) {
    const coords = routePoints.value.map(p => [p.lat, p.lng]);
    routePreviewLayer = L.polyline(coords, {
      color: '#3388ff',
      weight: 4,
      opacity: 0.8,
    }).addTo(map);
  }

  // Helper to delete a point
  const deleteRoutePoint = (indexToDelete) => {
    // Save current state for undo
    routeHistory.value.push([...routePoints.value]);
    routeFuture.value = [];
    // Remove the point
    routePoints.value.splice(indexToDelete, 1);
    updateRoutePreview();
    emit('route-drawing', routePoints.value);
  };

  // Add draggable markers for each point
  routePoints.value.forEach((point, index) => {
    const isFirst = index === 0;
    const isLast = index === routePoints.value.length - 1;
    const canDelete = routePoints.value.length > 1; // Can delete if more than 1 point

    const markerIcon = L.divIcon({
      className: 'route-point-marker',
      html: `<div style="
        width: ${isFirst ? '16px' : '12px'};
        height: ${isFirst ? '16px' : '12px'};
        background-color: ${isFirst ? '#3388ff' : isLast ? '#ff9800' : 'white'};
        border: 3px solid #3388ff;
        border-radius: 50%;
        box-shadow: 0 2px 4px rgba(0,0,0,0.3);
        cursor: move;
      "></div>`,
      iconSize: [isFirst ? 16 : 12, isFirst ? 16 : 12],
      iconAnchor: [isFirst ? 8 : 6, isFirst ? 8 : 6],
    });

    const marker = L.marker([point.lat, point.lng], {
      icon: markerIcon,
      draggable: true,
    }).addTo(routeMarkersLayer);

    // Handle drag start - save state for undo
    marker.on('dragstart', () => {
      routeHistory.value.push([...routePoints.value]);
      routeFuture.value = [];
    });

    // Handle drag - update point position and polyline
    marker.on('drag', (e) => {
      const latlng = e.target.getLatLng();
      routePoints.value[index] = { lat: latlng.lat, lng: latlng.lng };
      // Update polyline while dragging
      if (routePreviewLayer && routePoints.value.length >= 2) {
        const newCoords = routePoints.value.map(p => [p.lat, p.lng]);
        routePreviewLayer.setLatLngs(newCoords);
      }
    });

    // Handle drag end - emit update
    marker.on('dragend', () => {
      emit('route-drawing', routePoints.value);
    });

    // Right-click to delete point (desktop)
    marker.on('contextmenu', (e) => {
      L.DomEvent.preventDefault(e);
      if (canDelete) {
        deleteRoutePoint(index);
      }
    });

    // Bind popup for delete option (works on mobile too)
    if (canDelete) {
      const popupContent = document.createElement('div');
      const deleteBtn = document.createElement('button');
      deleteBtn.textContent = 'Delete point';
      deleteBtn.style.cssText = `
        background: #dc3545;
        color: white;
        border: none;
        padding: 0.5rem 1rem;
        border-radius: 4px;
        cursor: pointer;
        font-size: 0.9rem;
      `;

      const handleDelete = (e) => {
        e.preventDefault();
        e.stopPropagation();
        marker.closePopup();
        deleteRoutePoint(index);
      };

      deleteBtn.addEventListener('click', handleDelete);
      deleteBtn.addEventListener('touchend', handleDelete);
      popupContent.appendChild(deleteBtn);

      marker.bindPopup(popupContent, { closeButton: false, className: 'vertex-popup' });
    }
  });
};

// Update the editable polygon on the map with draggable vertices
const updateEditingPolygon = () => {
  if (!map) return;

  // Remove existing editing layers
  if (editingPolygonLayer) {
    editingPolygonLayer.remove();
    editingPolygonLayer = null;
  }
  if (editingMarkersLayer) {
    editingMarkersLayer.remove();
    editingMarkersLayer = null;
  }

  if (editingPoints.value.length === 0) return;

  // Create the polygon shape (use markerPane to be above other polygons)
  const coords = editingPoints.value.map(p => [p.lat, p.lng]);
  editingPolygonLayer = L.polygon(coords, {
    color: '#667eea',
    fillColor: '#667eea',
    fillOpacity: 0.2,
    weight: 3,
    opacity: 0.8,
    pane: 'overlayPane',
  }).addTo(map);

  // Bring editing polygon to front
  editingPolygonLayer.bringToFront();

  // Create a layer group for draggable vertex markers (use popupPane for highest z-index)
  editingMarkersLayer = L.layerGroup({ pane: 'markerPane' }).addTo(map);

  // Helper to delete a vertex
  const deleteVertex = (indexToDelete) => {
    // Minimum 3 points required
    if (editingPoints.value.length <= 3) return;

    editingPoints.value.splice(indexToDelete, 1);
    justEmittedPolygonUpdate = true;
    emit('polygon-update', [...editingPoints.value]);
    updateEditingPolygon(); // Rebuild markers
  };

  // Add draggable markers for each vertex
  editingPoints.value.forEach((point, index) => {
    const canDelete = editingPoints.value.length > 3;

    const markerIcon = L.divIcon({
      className: 'polygon-edit-marker',
      html: `<div class="vertex-marker" style="
        position: relative;
        width: 16px;
        height: 16px;
        background-color: #667eea;
        border: 3px solid white;
        border-radius: 50%;
        box-shadow: 0 2px 6px rgba(0,0,0,0.4);
        cursor: move;
      "></div>`,
      iconSize: [16, 16],
      iconAnchor: [8, 8],
    });

    const marker = L.marker([point.lat, point.lng], {
      icon: markerIcon,
      draggable: true,
    }).addTo(editingMarkersLayer);

    // Handle drag events
    marker.on('drag', (e) => {
      const latlng = e.target.getLatLng();
      editingPoints.value[index] = { lat: latlng.lat, lng: latlng.lng };
      // Update polygon shape while dragging
      const newCoords = editingPoints.value.map(p => [p.lat, p.lng]);
      editingPolygonLayer.setLatLngs(newCoords);
    });

    marker.on('dragend', () => {
      // Emit the updated polygon
      justEmittedPolygonUpdate = true;
      emit('polygon-update', [...editingPoints.value]);
    });

    // Right-click to delete vertex (desktop)
    marker.on('contextmenu', (e) => {
      L.DomEvent.preventDefault(e);
      if (canDelete) {
        deleteVertex(index);
      }
    });

    // Bind popup for delete option (works on mobile too)
    if (canDelete) {
      const popupContent = document.createElement('div');
      const deleteBtn = document.createElement('button');
      deleteBtn.textContent = 'Delete point';
      deleteBtn.style.cssText = `
        background: #dc3545;
        color: white;
        border: none;
        padding: 0.5rem 1rem;
        border-radius: 4px;
        cursor: pointer;
        font-size: 0.9rem;
      `;

      // Use both click and touchend for mobile compatibility
      const handleDelete = (e) => {
        e.preventDefault();
        e.stopPropagation();
        marker.closePopup();
        deleteVertex(index);
      };

      deleteBtn.addEventListener('click', handleDelete);
      deleteBtn.addEventListener('touchend', handleDelete);
      popupContent.appendChild(deleteBtn);

      marker.bindPopup(popupContent, { closeButton: false, className: 'vertex-popup' });
    }
  });
};

// Update the editable route on the map with draggable vertices, midpoints, and extend markers
const updateEditingRoute = () => {
  if (!map) return;

  // Remove existing editing layers
  if (editingRouteLayer) {
    editingRouteLayer.remove();
    editingRouteLayer = null;
  }
  if (editingRouteMarkersLayer) {
    editingRouteMarkersLayer.remove();
    editingRouteMarkersLayer = null;
  }

  if (editingRoutePoints.value.length === 0) return;

  // Get route color from props (use default if not set)
  const routeColor = props.routeColor || '#3388ff';

  // Create the route polyline (open-ended, not closed)
  const coords = editingRoutePoints.value.map(p => [p.lat, p.lng]);
  editingRouteLayer = L.polyline(coords, {
    color: routeColor,
    weight: props.routeWeight || 4,
    opacity: 0.8,
    pane: 'overlayPane',
  }).addTo(map);

  // Bring editing route to front
  editingRouteLayer.bringToFront();

  // Create a layer group for markers
  editingRouteMarkersLayer = L.layerGroup({ pane: 'markerPane' }).addTo(map);

  // Track midpoint markers for updating during drag
  const midpointMarkers = [];
  // Track extend markers for updating during drag
  let extendStartMarker = null;
  let extendEndMarker = null;

  // Helper to delete a vertex
  const deleteVertex = (indexToDelete) => {
    // Minimum 2 points required for a route
    if (editingRoutePoints.value.length <= 2) return;

    saveRouteEditState(); // Save state for undo
    editingRoutePoints.value.splice(indexToDelete, 1);
    justEmittedRouteUpdate = true;
    emit('route-update', [...editingRoutePoints.value]);
    updateEditingRoute(); // Rebuild markers
  };

  // Helper to insert a point at a given index (used by midpoint/extend markers)
  // Note: saveRouteEditState should be called BEFORE this in dragstart
  const insertPoint = (index, point) => {
    editingRoutePoints.value.splice(index, 0, point);
    justEmittedRouteUpdate = true;
    emit('route-update', [...editingRoutePoints.value]);
    updateEditingRoute(); // Rebuild markers
  };

  // Helper to calculate extend marker offset
  const calcExtendOffset = (fromPoint, toPoint, defaultOffsetLat, defaultOffsetLng) => {
    let offsetLat = defaultOffsetLat;
    let offsetLng = defaultOffsetLng;
    if (toPoint) {
      const dx = fromPoint.lng - toPoint.lng;
      const dy = fromPoint.lat - toPoint.lat;
      const dist = Math.sqrt(dx * dx + dy * dy);
      if (dist > 0) {
        const scale = 0.0003 / dist;
        offsetLat = dy * scale;
        offsetLng = dx * scale;
      }
    }
    return { offsetLat, offsetLng };
  };

  // Helper to update all midpoint and extend marker positions
  const updateMidpointPositions = () => {
    // Update midpoint markers
    for (let i = 0; i < midpointMarkers.length; i++) {
      const p1 = editingRoutePoints.value[i];
      const p2 = editingRoutePoints.value[i + 1];
      if (p1 && p2) {
        const midLat = (p1.lat + p2.lat) / 2;
        const midLng = (p1.lng + p2.lng) / 2;
        midpointMarkers[i].setLatLng([midLat, midLng]);
      }
    }

    // Update extend start marker
    if (extendStartMarker && editingRoutePoints.value.length > 0) {
      const firstPoint = editingRoutePoints.value[0];
      const secondPoint = editingRoutePoints.value.length > 1 ? editingRoutePoints.value[1] : null;
      const { offsetLat, offsetLng } = calcExtendOffset(firstPoint, secondPoint, 0.0002, -0.0003);
      extendStartMarker.setLatLng([firstPoint.lat + offsetLat, firstPoint.lng + offsetLng]);
    }

    // Update extend end marker
    if (extendEndMarker && editingRoutePoints.value.length > 0) {
      const lastPoint = editingRoutePoints.value[editingRoutePoints.value.length - 1];
      const secondLastPoint = editingRoutePoints.value.length > 1 ? editingRoutePoints.value[editingRoutePoints.value.length - 2] : null;
      const { offsetLat, offsetLng } = calcExtendOffset(lastPoint, secondLastPoint, 0.0002, 0.0003);
      extendEndMarker.setLatLng([lastPoint.lat + offsetLat, lastPoint.lng + offsetLng]);
    }
  };

  // Add extend marker at start (to prepend points) - draggable
  if (editingRoutePoints.value.length > 0) {
    const firstPoint = editingRoutePoints.value[0];
    const extendStartIcon = L.divIcon({
      className: 'route-extend-marker',
      html: `<div style="
        width: 14px;
        height: 14px;
        background-color: transparent;
        border: 2px dashed ${routeColor};
        border-radius: 50%;
        box-shadow: 0 2px 4px rgba(0,0,0,0.3);
        cursor: grab;
        opacity: 0.7;
      "></div>`,
      iconSize: [14, 14],
      iconAnchor: [7, 7],
    });

    const secondPoint = editingRoutePoints.value.length > 1 ? editingRoutePoints.value[1] : null;
    const { offsetLat, offsetLng } = calcExtendOffset(firstPoint, secondPoint, 0.0002, -0.0003);

    let hasInsertedStartPoint = false;

    extendStartMarker = L.marker(
      [firstPoint.lat + offsetLat, firstPoint.lng + offsetLng],
      { icon: extendStartIcon, draggable: true }
    ).addTo(editingRouteMarkersLayer);

    extendStartMarker.on('dragstart', () => {
      saveRouteEditState(); // Save state for undo before inserting
      const latlng = extendStartMarker.getLatLng();
      // Insert the new point at the start
      editingRoutePoints.value.unshift({ lat: latlng.lat, lng: latlng.lng });
      hasInsertedStartPoint = true;
    });

    extendStartMarker.on('drag', (e) => {
      if (hasInsertedStartPoint) {
        const latlng = e.target.getLatLng();
        editingRoutePoints.value[0] = { lat: latlng.lat, lng: latlng.lng };
        const newCoords = editingRoutePoints.value.map(p => [p.lat, p.lng]);
        editingRouteLayer.setLatLngs(newCoords);
      }
    });

    extendStartMarker.on('dragend', () => {
      if (hasInsertedStartPoint) {
        justEmittedRouteUpdate = true;
        emit('route-update', [...editingRoutePoints.value]);
        updateEditingRoute();
      }
    });

    extendStartMarker.bindTooltip('Drag to add point at start', { direction: 'top', offset: [0, -10] });
  }

  // Add extend marker at end (to append points) - draggable
  if (editingRoutePoints.value.length > 0) {
    const lastPoint = editingRoutePoints.value[editingRoutePoints.value.length - 1];
    const extendEndIcon = L.divIcon({
      className: 'route-extend-marker',
      html: `<div style="
        width: 14px;
        height: 14px;
        background-color: transparent;
        border: 2px dashed ${routeColor};
        border-radius: 50%;
        box-shadow: 0 2px 4px rgba(0,0,0,0.3);
        cursor: grab;
        opacity: 0.7;
      "></div>`,
      iconSize: [14, 14],
      iconAnchor: [7, 7],
    });

    const secondLastPoint = editingRoutePoints.value.length > 1 ? editingRoutePoints.value[editingRoutePoints.value.length - 2] : null;
    const { offsetLat, offsetLng } = calcExtendOffset(lastPoint, secondLastPoint, 0.0002, 0.0003);

    let hasInsertedEndPoint = false;

    extendEndMarker = L.marker(
      [lastPoint.lat + offsetLat, lastPoint.lng + offsetLng],
      { icon: extendEndIcon, draggable: true }
    ).addTo(editingRouteMarkersLayer);

    extendEndMarker.on('dragstart', () => {
      saveRouteEditState(); // Save state for undo before inserting
      const latlng = extendEndMarker.getLatLng();
      // Insert the new point at the end
      editingRoutePoints.value.push({ lat: latlng.lat, lng: latlng.lng });
      hasInsertedEndPoint = true;
    });

    extendEndMarker.on('drag', (e) => {
      if (hasInsertedEndPoint) {
        const latlng = e.target.getLatLng();
        editingRoutePoints.value[editingRoutePoints.value.length - 1] = { lat: latlng.lat, lng: latlng.lng };
        const newCoords = editingRoutePoints.value.map(p => [p.lat, p.lng]);
        editingRouteLayer.setLatLngs(newCoords);
      }
    });

    extendEndMarker.on('dragend', () => {
      if (hasInsertedEndPoint) {
        justEmittedRouteUpdate = true;
        emit('route-update', [...editingRoutePoints.value]);
        updateEditingRoute();
      }
    });

    extendEndMarker.bindTooltip('Drag to add point at end', { direction: 'top', offset: [0, -10] });
  }

  // Add midpoint markers between vertices for insertion (draggable)
  for (let i = 0; i < editingRoutePoints.value.length - 1; i++) {
    const p1 = editingRoutePoints.value[i];
    const p2 = editingRoutePoints.value[i + 1];
    const midLat = (p1.lat + p2.lat) / 2;
    const midLng = (p1.lng + p2.lng) / 2;

    const midpointIcon = L.divIcon({
      className: 'route-midpoint-marker',
      html: `<div style="
        width: 10px;
        height: 10px;
        background-color: ${routeColor};
        border: 2px solid white;
        border-radius: 50%;
        box-shadow: 0 1px 3px rgba(0,0,0,0.3);
        cursor: grab;
        opacity: 0.6;
      "></div>`,
      iconSize: [10, 10],
      iconAnchor: [5, 5],
    });

    const insertIndex = i + 1;
    let hasInsertedPoint = false;
    let insertedIndex = -1;

    const midpointMarker = L.marker([midLat, midLng], {
      icon: midpointIcon,
      draggable: true,
    }).addTo(editingRouteMarkersLayer);

    // On drag start, insert the point into the array
    midpointMarker.on('dragstart', () => {
      saveRouteEditState(); // Save state for undo before inserting
      const latlng = midpointMarker.getLatLng();
      // Insert the new point at the midpoint position
      editingRoutePoints.value.splice(insertIndex, 0, { lat: latlng.lat, lng: latlng.lng });
      hasInsertedPoint = true;
      insertedIndex = insertIndex;
    });

    // During drag, update the inserted point position
    midpointMarker.on('drag', (e) => {
      if (hasInsertedPoint && insertedIndex >= 0) {
        const latlng = e.target.getLatLng();
        editingRoutePoints.value[insertedIndex] = { lat: latlng.lat, lng: latlng.lng };
        // Update the polyline
        const newCoords = editingRoutePoints.value.map(p => [p.lat, p.lng]);
        editingRouteLayer.setLatLngs(newCoords);
      }
    });

    // On drag end, emit update and rebuild to get proper vertex marker
    midpointMarker.on('dragend', () => {
      if (hasInsertedPoint) {
        justEmittedRouteUpdate = true;
        emit('route-update', [...editingRoutePoints.value]);
        // Rebuild to convert midpoint to proper vertex with delete functionality
        updateEditingRoute();
      }
    });

    midpointMarker.bindTooltip('Drag to insert point', { direction: 'top', offset: [0, -8] });
    midpointMarkers.push(midpointMarker);
  }

  // Add draggable markers for each vertex
  editingRoutePoints.value.forEach((point, index) => {
    const canDelete = editingRoutePoints.value.length > 2;

    const markerIcon = L.divIcon({
      className: 'route-edit-marker',
      html: `<div class="vertex-marker" style="
        position: relative;
        width: 16px;
        height: 16px;
        background-color: ${routeColor};
        border: 3px solid white;
        border-radius: 50%;
        box-shadow: 0 2px 6px rgba(0,0,0,0.4);
        cursor: move;
      "></div>`,
      iconSize: [16, 16],
      iconAnchor: [8, 8],
    });

    const marker = L.marker([point.lat, point.lng], {
      icon: markerIcon,
      draggable: true,
    }).addTo(editingRouteMarkersLayer);

    // Handle drag events
    marker.on('dragstart', () => {
      saveRouteEditState(); // Save state for undo before moving
    });

    marker.on('drag', (e) => {
      const latlng = e.target.getLatLng();
      editingRoutePoints.value[index] = { lat: latlng.lat, lng: latlng.lng };
      // Update route shape while dragging
      const newCoords = editingRoutePoints.value.map(p => [p.lat, p.lng]);
      editingRouteLayer.setLatLngs(newCoords);
      // Update midpoint and extend marker positions
      updateMidpointPositions();
    });

    marker.on('dragend', () => {
      // Emit the updated route
      justEmittedRouteUpdate = true;
      emit('route-update', [...editingRoutePoints.value]);
    });

    // Right-click to delete vertex (desktop)
    marker.on('contextmenu', (e) => {
      L.DomEvent.preventDefault(e);
      if (canDelete) {
        deleteVertex(index);
      }
    });

    // Bind popup for delete option (works on mobile too)
    if (canDelete) {
      const popupContent = document.createElement('div');
      const deleteBtn = document.createElement('button');
      deleteBtn.textContent = 'Delete point';
      deleteBtn.style.cssText = `
        background: #dc3545;
        color: white;
        border: none;
        padding: 0.5rem 1rem;
        border-radius: 4px;
        cursor: pointer;
        font-size: 0.9rem;
      `;

      // Use both click and touchend for mobile compatibility
      const handleDelete = (e) => {
        e.preventDefault();
        e.stopPropagation();
        marker.closePopup();
        deleteVertex(index);
      };

      deleteBtn.addEventListener('click', handleDelete);
      deleteBtn.addEventListener('touchend', handleDelete);
      popupContent.appendChild(deleteBtn);

      marker.bindPopup(popupContent, { closeButton: false, className: 'vertex-popup' });
    }
  });
};

// Initialize route editing mode with existing route
const initRouteEditingMode = (fitBounds = true) => {
  if (!map) return;

  // Clean up editing layers when not in editing mode
  if (!props.editingRoute || props.editingRoute.length === 0) {
    if (editingRouteLayer) {
      editingRouteLayer.remove();
      editingRouteLayer = null;
    }
    if (editingRouteMarkersLayer) {
      editingRouteMarkersLayer.remove();
      editingRouteMarkersLayer = null;
    }
    editingRoutePoints.value = [];
    editingRouteHistory.value = [];
    editingRouteFuture.value = [];
    return;
  }

  // Initialize editing points from prop
  editingRoutePoints.value = props.editingRoute.map(p => ({ lat: p.lat, lng: p.lng }));
  // Clear undo/redo history for fresh editing session
  editingRouteHistory.value = [];
  editingRouteFuture.value = [];
  updateEditingRoute();

  // Fit bounds to the editing route
  if (fitBounds && editingRoutePoints.value.length >= 2) {
    const bounds = L.latLngBounds(editingRoutePoints.value.map(p => [p.lat, p.lng]));
    map.fitBounds(bounds, { padding: [50, 50] });
  }
};

// Initialize route drawing mode
const initRouteDrawingMode = () => {
  if (!map) return;

  // Clean up any existing custom route drawing handlers
  if (map._customRouteDrawingHandlers) {
    map.off('click', map._customRouteDrawingHandlers.handleClick);
    map.getContainer().removeEventListener('touchstart', map._customRouteDrawingHandlers.handleTouchStart);
    map.getContainer().removeEventListener('touchend', map._customRouteDrawingHandlers.handleTouchEnd);
    map.getContainer().removeEventListener('touchmove', map._customRouteDrawingHandlers.handleTouchMove);
    delete map._customRouteDrawingHandlers;
  }

  // Clear route preview when exiting drawing mode
  if (!props.drawingRouteMode) {
    clearRouteDrawing();
    return;
  }

  // Custom route drawing mode implementation (similar to polygon drawing)
  let touchStartTime = 0;
  let touchStartPos = null;
  let touchMoved = false;
  let multiTouch = false;
  const TAP_THRESHOLD_MS = 300;
  const MOVE_THRESHOLD_PX = 15;

  const addRoutePoint = (latlng) => {
    // Save current state for undo
    routeHistory.value.push([...routePoints.value]);
    // Clear redo stack when adding new points
    routeFuture.value = [];
    // Add the new point
    routePoints.value.push({ lat: latlng.lat, lng: latlng.lng });
    updateRoutePreview();
    emit('route-drawing', routePoints.value);
  };

  // Handle click events (for desktop)
  const handleClick = (e) => {
    if (e.originalEvent && e.originalEvent.sourceCapabilities?.firesTouchEvents) {
      return;
    }
    addRoutePoint(e.latlng);
  };

  // Handle touch events for mobile
  const handleTouchStart = (e) => {
    if (e.touches.length > 1) {
      multiTouch = true;
      return;
    }
    multiTouch = false;
    touchStartTime = Date.now();
    touchStartPos = { x: e.touches[0].clientX, y: e.touches[0].clientY };
    touchMoved = false;
  };

  const handleTouchMove = (e) => {
    if (!touchStartPos || multiTouch) return;

    const dx = e.touches[0].clientX - touchStartPos.x;
    const dy = e.touches[0].clientY - touchStartPos.y;
    const distance = Math.sqrt(dx * dx + dy * dy);

    if (distance > MOVE_THRESHOLD_PX) {
      touchMoved = true;
    }
  };

  const handleTouchEnd = (e) => {
    if (multiTouch) {
      multiTouch = false;
      touchStartPos = null;
      return;
    }

    const touchDuration = Date.now() - touchStartTime;

    if (touchDuration < TAP_THRESHOLD_MS && !touchMoved && touchStartPos) {
      const touch = e.changedTouches[0];
      const containerPoint = L.point(
        touch.clientX - map.getContainer().getBoundingClientRect().left,
        touch.clientY - map.getContainer().getBoundingClientRect().top
      );
      const latlng = map.containerPointToLatLng(containerPoint);

      e.preventDefault();
      e.stopPropagation();

      addRoutePoint(latlng);
    }

    touchStartPos = null;
    touchMoved = false;
  };

  // Add event listeners
  map.on('click', handleClick);
  map.getContainer().addEventListener('touchstart', handleTouchStart, { passive: true });
  map.getContainer().addEventListener('touchmove', handleTouchMove, { passive: true });
  map.getContainer().addEventListener('touchend', handleTouchEnd, { passive: false });

  // Store handlers for cleanup
  map._customRouteDrawingHandlers = {
    handleClick,
    handleTouchStart,
    handleTouchMove,
    handleTouchEnd,
  };

  // Initialize route preview
  updateRoutePreview();
};

// Initialize editing mode with existing polygon
const initEditingMode = (fitBounds = true) => {
  if (!map) return;

  // Clean up editing layers when not in editing mode
  if (!props.editingPolygon || props.editingPolygon.length === 0) {
    if (editingPolygonLayer) {
      editingPolygonLayer.remove();
      editingPolygonLayer = null;
    }
    if (editingMarkersLayer) {
      editingMarkersLayer.remove();
      editingMarkersLayer = null;
    }
    editingPoints.value = [];
    return;
  }

  // Initialize editing points from prop
  editingPoints.value = props.editingPolygon.map(p => ({ lat: p.lat, lng: p.lng }));
  updateEditingPolygon();

  // Fit bounds to the editing polygon
  if (fitBounds && editingPoints.value.length >= 3) {
    const bounds = L.latLngBounds(editingPoints.value.map(p => [p.lat, p.lng]));
    map.fitBounds(bounds, { padding: [50, 50] });
  }
};

const initDrawingMode = () => {
  if (!map) return;

  // Create feature group for drawn items (legacy, kept for compatibility)
  if (!drawnItems) {
    drawnItems = new L.FeatureGroup();
    map.addLayer(drawnItems);
  }

  // Remove existing draw control
  if (drawControl) {
    map.removeControl(drawControl);
    drawControl = null;
  }

  // Clean up any existing custom drawing handlers
  if (map._customDrawingHandlers) {
    map.off('click', map._customDrawingHandlers.handleClick);
    map.getContainer().removeEventListener('touchstart', map._customDrawingHandlers.handleTouchStart);
    map.getContainer().removeEventListener('touchend', map._customDrawingHandlers.handleTouchEnd);
    map.getContainer().removeEventListener('touchmove', map._customDrawingHandlers.handleTouchMove);
    delete map._customDrawingHandlers;
  }

  // Clear polygon preview when exiting drawing mode
  if (!props.drawingMode) {
    clearPolygonDrawing();
    if (drawnItems) {
      drawnItems.clearLayers();
    }
    return;
  }

  // Custom drawing mode implementation for better mobile support
  // Track touch state for distinguishing taps from gestures
  let touchStartTime = 0;
  let touchStartPos = null;
  let touchMoved = false;
  let multiTouch = false;
  const TAP_THRESHOLD_MS = 300; // Max time for a tap
  const MOVE_THRESHOLD_PX = 15; // Max movement for a tap

  const addPolygonPoint = (latlng) => {
    // Save current state for undo
    polygonHistory.value.push([...polygonPoints.value]);
    // Clear redo stack when adding new points
    polygonFuture.value = [];
    // Add the new point
    polygonPoints.value.push({ lat: latlng.lat, lng: latlng.lng });
    updatePolygonPreview();
    emit('polygon-drawing', polygonPoints.value);
  };

  // Handle click events (for desktop)
  const handleClick = (e) => {
    // Only handle if not from a touch event (touch events handle themselves)
    if (e.originalEvent && e.originalEvent.sourceCapabilities?.firesTouchEvents) {
      return;
    }
    addPolygonPoint(e.latlng);
  };

  // Handle touch events for mobile
  const handleTouchStart = (e) => {
    if (e.touches.length > 1) {
      multiTouch = true;
      return;
    }
    multiTouch = false;
    touchStartTime = Date.now();
    touchStartPos = { x: e.touches[0].clientX, y: e.touches[0].clientY };
    touchMoved = false;
  };

  const handleTouchMove = (e) => {
    if (!touchStartPos || multiTouch) return;

    const dx = e.touches[0].clientX - touchStartPos.x;
    const dy = e.touches[0].clientY - touchStartPos.y;
    const distance = Math.sqrt(dx * dx + dy * dy);

    if (distance > MOVE_THRESHOLD_PX) {
      touchMoved = true;
    }
  };

  const handleTouchEnd = (e) => {
    if (multiTouch) {
      multiTouch = false;
      touchStartPos = null;
      return;
    }

    const touchDuration = Date.now() - touchStartTime;

    // Only register as a tap if:
    // 1. Touch was quick (< TAP_THRESHOLD_MS)
    // 2. Finger didn't move much (< MOVE_THRESHOLD_PX)
    // 3. It was a single touch (not multi-touch)
    if (touchDuration < TAP_THRESHOLD_MS && !touchMoved && touchStartPos) {
      // Get the touch end position
      const touch = e.changedTouches[0];
      const containerPoint = L.point(
        touch.clientX - map.getContainer().getBoundingClientRect().left,
        touch.clientY - map.getContainer().getBoundingClientRect().top
      );
      const latlng = map.containerPointToLatLng(containerPoint);

      // Prevent the map click event
      e.preventDefault();
      e.stopPropagation();

      addPolygonPoint(latlng);
    }

    touchStartPos = null;
    touchMoved = false;
  };

  // Add event listeners
  map.on('click', handleClick);
  map.getContainer().addEventListener('touchstart', handleTouchStart, { passive: true });
  map.getContainer().addEventListener('touchmove', handleTouchMove, { passive: true });
  map.getContainer().addEventListener('touchend', handleTouchEnd, { passive: false });

  // Store handlers for cleanup
  map._customDrawingHandlers = {
    handleClick,
    handleTouchStart,
    handleTouchMove,
    handleTouchEnd,
  };

  // Initialize polygon preview
  updatePolygonPreview();
};

// Track if marker update was requested during zoom
let pendingMarkerUpdate = false;
// Track if zoom animation is in progress (more reliable than map._animatingZoom)
let isZoomAnimating = false;

const updateMarkers = () => {
  // Don't update markers during map animations to prevent positioning bugs
  if (map && (map._animatingZoom || isZoomAnimating)) {
    pendingMarkerUpdate = true;
    return;
  }

  pendingMarkerUpdate = false;

  // Close any open popups before removing markers
  if (map) {
    map.closePopup();
  }

  // Safely remove markers - clear event listeners first to prevent animation errors
  markers.value.forEach((marker) => {
    if (marker._map) {
      marker.off(); // Remove all event listeners
      marker.remove();
    }
  });
  markers.value = [];

  if (!map) return;

  // Calculate max label width as 75% of map container width
  const mapWidth = mapContainer.value?.clientWidth || 400;
  const maxLabelWidth = Math.floor(mapWidth * 0.75);

  props.locations.forEach((location) => {
    // Skip locations without valid coordinates (no location or 0,0)
    const lat = location.latitude ?? location.Latitude;
    const lng = location.longitude ?? location.Longitude;
    if (lat == null || lng == null || (lat === 0 && lng === 0)) {
      return;
    }

    const checkedInCount = location.checkedInCount || 0;
    const requiredMarshals = location.requiredMarshals || 1;
    const isHighlighted = props.highlightLocationId === location.id ||
                          props.highlightLocationIds.includes(location.id);
    // Check if this is a dynamic checkpoint (should not be simplified)
    const isDynamic = location.isDynamic || location.IsDynamic;
    // Show simplified gray dots for non-highlighted checkpoints when simplifyNonHighlighted is true
    // Dynamic checkpoints are always shown normally (not simplified)
    const showSimplified = props.simplifyNonHighlighted && !isHighlighted && !isDynamic;

    // Check if location has a custom style (any non-default resolved property)
    // This includes custom type, background color, border color, icon color, or non-circle shape
    const resolvedType = location.resolvedStyleType || location.ResolvedStyleType;
    const resolvedBgColor = location.resolvedStyleBackgroundColor || location.ResolvedStyleBackgroundColor
      || location.resolvedStyleColor || location.ResolvedStyleColor;
    const resolvedBorderColor = location.resolvedStyleBorderColor || location.ResolvedStyleBorderColor;
    const resolvedIconColor = location.resolvedStyleIconColor || location.ResolvedStyleIconColor;
    const resolvedShape = location.resolvedStyleBackgroundShape || location.ResolvedStyleBackgroundShape;

    const hasCustomStyle = (resolvedType && resolvedType !== 'default')
      || resolvedBgColor
      || resolvedBorderColor
      || resolvedIconColor
      || (resolvedShape && resolvedShape !== 'circle' && resolvedShape !== 'default');

    // Get resolved map rotation (can be from checkpoint, area, or event)
    // Fall back to direct styleMapRotation if resolved value not available
    const resolvedMapRotation = location.resolvedStyleMapRotation
      ?? location.ResolvedStyleMapRotation
      ?? location.styleMapRotation
      ?? location.StyleMapRotation
      ?? 0;
    const rotationDegrees = parseInt(resolvedMapRotation, 10) || 0;

    // Area color indicator (hide in marshal mode)
    const areaColor = location.areaColor || '#999';
    const hasArea = location.areaId && !props.marshalMode;

    const shouldShowDesc = showDescriptionsForIds.value.has(location.id);
    const labelText = shouldShowDesc && location.description
      ? `${location.name}: ${truncateText(location.description, 50)}`
      : location.name;

    // Highlight ring for assigned location
    const highlightRing = isHighlighted ? `
      <div style="
        position: absolute;
        top: 50%;
        left: 50%;
        transform: translate(-50%, -50%);
        width: 50px;
        height: 50px;
        border: 3px solid #667eea;
        border-radius: 50%;
        animation: highlight-pulse 1.5s ease-out infinite;
      "></div>
    ` : '';

    // Style adjustments for highlighted/simplified markers
    const labelBg = isHighlighted ? '#667eea' : showSimplified ? '#e0e0e0' : 'white';
    const labelColor = isHighlighted ? 'white' : showSimplified ? '#888' : '#333';

    let markerHtml;

    // Simplified gray dot for non-highlighted checkpoints when simplifyNonHighlighted is true
    if (showSimplified) {
      const markerSize = '20px';
      markerHtml = `
        <div style="
          background-color: #999;
          width: ${markerSize};
          height: ${markerSize};
          border-radius: 50%;
          border: 3px solid white;
          box-shadow: 0 2px 5px rgba(0,0,0,0.3);
        "></div>
      `;
    }
    // Show count unless in marshal mode or staffing overlay is disabled
    const showCount = !props.marshalMode && props.showStaffingOverlay;

    // Generate badge HTML separately so it won't be rotated
    const statusBadge = getStatusBadgeHtml(checkedInCount, requiredMarshals);
    const countBadge = showCount ? `<div style="
      position: absolute;
      bottom: -2px;
      right: -6px;
      background-color: white;
      border-radius: 8px;
      padding: 1px 4px;
      font-size: 9px;
      font-weight: bold;
      color: #333;
      box-shadow: 0 1px 2px rgba(0,0,0,0.3);
    ">${checkedInCount}/${requiredMarshals}</div>` : '';

    // Custom styled checkpoint (skip if showing simplified)
    if (!showSimplified && hasCustomStyle) {
      // Use the new composable SVG generator with all resolved style fields
      const baseSize = isHighlighted ? 40 : 32;
      const sizePercent = location.resolvedStyleSize || location.ResolvedStyleSize || '100';

      const customSvg = generateCheckpointSvg({
        type: location.resolvedStyleType || location.ResolvedStyleType,
        backgroundShape: location.resolvedStyleBackgroundShape || location.ResolvedStyleBackgroundShape || 'circle',
        backgroundColor: location.resolvedStyleBackgroundColor || location.ResolvedStyleBackgroundColor || location.resolvedStyleColor || location.ResolvedStyleColor || '',
        borderColor: location.resolvedStyleBorderColor || location.ResolvedStyleBorderColor || '',
        iconColor: location.resolvedStyleIconColor || location.ResolvedStyleIconColor || '',
        size: sizePercent,
      });

      if (customSvg) {
        // Just the SVG - badge will be added separately outside rotation
        markerHtml = customSvg;
      } else {
        // Fallback to legacy marker if new generator fails
        const legacyMarker = getCheckpointMarkerHtml(
          location.resolvedStyleType,
          location.resolvedStyleColor,
          checkedInCount,
          requiredMarshals,
          baseSize,
          showCount
        );
        markerHtml = legacyMarker || buildDefaultMarker(checkedInCount, requiredMarshals, isHighlighted, props.marshalMode, true);
      }
    }
    // Default status-based colored circle with status badge (skip if showing simplified)
    else if (!showSimplified) {
      // Skip badge in the marker itself - we'll add it separately
      markerHtml = buildDefaultMarker(checkedInCount, requiredMarshals, isHighlighted, props.marshalMode, true);
    }

    // Build rotation style if rotation is set
    const rotationStyle = rotationDegrees !== 0 ? `transform: rotate(${rotationDegrees}deg);` : '';

    const icon = L.divIcon({
      className: 'custom-marker',
      html: `
        <div style="display: flex; flex-direction: column; align-items: center; position: relative;">
          ${highlightRing}
          <div style="position: relative; display: flex; align-items: center; gap: 4px;">
            ${hasArea ? `<div style="
              width: 8px;
              height: 8px;
              border-radius: 50%;
              background-color: ${areaColor};
              border: 2px solid white;
              box-shadow: 0 1px 2px rgba(0,0,0,0.3);
            "></div>` : ''}
            <div style="position: relative; display: inline-block;">
              <div style="${rotationStyle}">
                ${markerHtml}
              </div>
              ${(showSimplified || props.marshalMode || !props.showStatusOverlay) ? '' : `<div style="position: absolute; top: -4px; right: -4px;">
                ${statusBadge}
              </div>`}
              ${(showSimplified || !showCount) ? '' : countBadge}
            </div>
          </div>
          <div style="
            background-color: ${labelBg};
            padding: 2px 6px;
            border-radius: 3px;
            font-size: ${isHighlighted ? '12px' : '11px'};
            font-weight: bold;
            color: ${labelColor};
            white-space: nowrap;
            overflow: hidden;
            text-overflow: ellipsis;
            max-width: ${maxLabelWidth}px;
            box-shadow: 0 1px 3px rgba(0,0,0,0.3);
            margin-top: 2px;
          ">
            ${labelText}
          </div>
        </div>
      `,
      iconSize: [30, 50],
      iconAnchor: [15, 25],
    });

    // Popup content - simpler in marshal mode (no counts)
    const popupContent = props.marshalMode
      ? `<b>${location.name}</b>${location.description ? `<br>${location.description}` : ''}`
      : `<b>${location.name}</b><br>${location.description || ''}<br>Checked in: ${checkedInCount}/${requiredMarshals}`;

    const marker = L.marker([location.latitude, location.longitude], { icon })
      .addTo(map)
      .bindPopup(popupContent);

    marker.on('click', () => {
      emit('location-click', location);
    });

    // Store location data for overlap detection
    marker.locationData = location;

    markers.value.push(marker);
  });

  // Only fit bounds on initial load if center/zoom are not explicitly provided
  // If center/zoom are provided, respect those instead of auto-fitting
  // Skip auto-centering if skipAutoCentering prop is true (used in route editor)
  if (map && isInitialLoad.value && !props.center && !hasCenteredOnCheckpoints.value && !props.skipAutoCentering) {
    const allPoints = [];

    // Use allLocationsForBounds (unfiltered) if available for better initial bounds
    const locationsForBounds = props.allLocationsForBounds.length > 0
      ? props.allLocationsForBounds
      : props.locations;

    // Helper to check if coordinates are valid (not null, undefined, or 0,0)
    const isValidCoord = (lat, lng) => lat != null && lng != null && !(lat === 0 && lng === 0);

    // Add location points (filter out invalid coordinates)
    if (locationsForBounds.length > 0) {
      allPoints.push(...locationsForBounds
        .filter((loc) => isValidCoord(loc.latitude, loc.longitude))
        .map((loc) => [loc.latitude, loc.longitude]));
    }

    // Add route points (filter out invalid coordinates)
    if (props.route.length > 0) {
      allPoints.push(...props.route
        .filter((point) => isValidCoord(getPointLat(point), getPointLng(point)))
        .map((point) => [getPointLat(point), getPointLng(point)]));
    }

    if (allPoints.length > 0) {
      const bounds = L.latLngBounds(allPoints);
      map.fitBounds(bounds, { padding: [50, 50] });
      isInitialLoad.value = false;
      hasCenteredOnCheckpoints.value = true;
    }
  } else if (props.center) {
    // Center is explicitly provided, so don't auto-fit
    isInitialLoad.value = false;
  }

  // Check if we should show descriptions after markers are updated
  // Use setTimeout to ensure markers are fully rendered
  setTimeout(() => {
    if (map && !map._animatingZoom) {
      checkVisibleMarkersAndUpdate();
    }
  }, 100);
};

onMounted(() => {
  // Reset centering state on mount
  hasCenteredOnCheckpoints.value = false;
  isInitialLoad.value = true;

  initMap();

  // Watch for container resize to invalidate map size (needed for fullscreen transitions)
  let resizeTimeout;
  let lastWidth = 0;
  let lastHeight = 0;

  const resizeObserver = new ResizeObserver((entries) => {
    if (map && entries.length > 0) {
      const { width, height } = entries[0].contentRect;

      // Only invalidate if size actually changed significantly
      if (Math.abs(width - lastWidth) > 10 || Math.abs(height - lastHeight) > 10) {
        lastWidth = width;
        lastHeight = height;

        // Debounce the invalidateSize call
        clearTimeout(resizeTimeout);
        resizeTimeout = setTimeout(() => {
          map.invalidateSize();
        }, 150);
      }
    }
  });

  if (mapContainer.value) {
    resizeObserver.observe(mapContainer.value);
  }

  onUnmounted(() => {
    clearTimeout(resizeTimeout);
    resizeObserver.disconnect();
  });
});

watch(
  () => props.locations,
  (newLocations) => {
    updateMarkers();

    // If we haven't centered yet and we now have checkpoints, center on them
    // Skip if skipAutoCentering is true (used in route editor)
    if (map && !hasCenteredOnCheckpoints.value && newLocations.length > 0 && !props.center && !props.skipAutoCentering) {
      // Filter out locations with invalid coordinates (0,0 or null)
      const validLocations = newLocations.filter(loc =>
        loc.latitude != null && loc.longitude != null &&
        !(loc.latitude === 0 && loc.longitude === 0)
      );
      if (validLocations.length > 0) {
        const lats = validLocations.map(loc => loc.latitude);
        const lngs = validLocations.map(loc => loc.longitude);
        const centerLat = lats.reduce((a, b) => a + b, 0) / lats.length;
        const centerLng = lngs.reduce((a, b) => a + b, 0) / lngs.length;
        map.setView([centerLat, centerLng], 10);
        hasCenteredOnCheckpoints.value = true;
        isInitialLoad.value = false;
      }
    }

    // Check visibility after markers update (with slight delay to ensure map has rendered)
    if (map) {
      setTimeout(() => checkLocationsInView(), 50);
    }
  },
  { deep: true }
);

// Center on route if it arrives before checkpoints
watch(
  () => props.route,
  (newRoute) => {
    updateRoute();
    updateMarkers();

    // If we haven't centered yet and we now have route data, center on it
    // Skip if skipAutoCentering is true (used in route editor)
    if (map && !hasCenteredOnCheckpoints.value && newRoute && newRoute.length > 0 && !props.center && !props.skipAutoCentering) {
      // Filter out points with invalid coordinates (0,0 or null)
      const validPoints = newRoute.filter(point => {
        const lat = getPointLat(point);
        const lng = getPointLng(point);
        return lat != null && lng != null && !(lat === 0 && lng === 0);
      });
      if (validPoints.length > 0) {
        const lats = validPoints.map(point => getPointLat(point));
        const lngs = validPoints.map(point => getPointLng(point));
        const centerLat = lats.reduce((a, b) => a + b, 0) / lats.length;
        const centerLng = lngs.reduce((a, b) => a + b, 0) / lngs.length;
        map.setView([centerLat, centerLng], 10);
        hasCenteredOnCheckpoints.value = true;
        isInitialLoad.value = false;
      }
    }
  },
  { deep: true }
);

// Update route when color, style or weight changes
watch(
  () => [props.routeColor, props.routeStyle, props.routeWeight],
  () => {
    updateRoute();
  }
);

// Update routes when layers change
watch(
  () => props.layers,
  () => {
    updateRoute();
  },
  { deep: true }
);

watch(
  () => props.center,
  (newCenter, oldCenter) => {
    // Don't recenter if user has manually panned the map
    if (map && !userHasPanned.value) {
      // Only recenter if the center values actually changed (not just object identity)
      const centerChanged = !oldCenter ||
        Math.abs(newCenter.lat - oldCenter.lat) > 0.000001 ||
        Math.abs(newCenter.lng - oldCenter.lng) > 0.000001;
      if (centerChanged) {
        map.setView([newCenter.lat, newCenter.lng], props.zoom);
      }
    }
  }
);

watch(
  () => props.areas,
  () => {
    updateAreaPolygons();
  },
  { deep: true }
);

watch(
  () => props.selectedAreaId,
  () => {
    updateAreaPolygons();
  }
);

watch(
  () => props.drawingMode,
  () => {
    initDrawingMode();
  }
);

watch(
  () => props.editingPolygon,
  () => {
    // Skip rebuilding if we just emitted an update (prevents losing drag state)
    if (justEmittedPolygonUpdate) {
      justEmittedPolygonUpdate = false;
      return;
    }
    initEditingMode();
  },
  { deep: true }
);

watch(
  () => props.drawingRouteMode,
  () => {
    initRouteDrawingMode();
  }
);

watch(
  () => props.editingRoute,
  () => {
    // Skip rebuilding if we just emitted an update (prevents losing drag state)
    if (justEmittedRouteUpdate) {
      justEmittedRouteUpdate = false;
      return;
    }
    initRouteEditingMode();
  },
  { deep: true }
);

watch(
  () => props.userLocation,
  () => {
    updateUserLocationMarker();
  },
  { deep: true }
);

watch(
  () => props.highlightLocationId,
  () => {
    updateMarkers();
  }
);

watch(
  () => props.highlightLocationIds,
  () => {
    updateMarkers();
    // Check visibility when highlighted locations change
    if (map) {
      setTimeout(() => checkLocationsInView(), 50);
    }
  },
  { deep: true }
);

watch(
  () => props.marshalMode,
  () => {
    updateMarkers();
  }
);

watch(
  () => props.simplifyNonHighlighted,
  () => {
    updateMarkers();
  }
);

// Watch for clickable prop changes and update click handler
watch(
  () => props.clickable,
  (newClickable) => {
    if (!map) return;

    // Remove existing click handler
    map.off('click');

    // Add click handler if clickable is true
    if (newClickable) {
      map.on('click', (e) => {
        if (!props.drawingMode) {
          emit('map-click', { lat: e.latlng.lat, lng: e.latlng.lng });
        }
      });
    }
  }
);
</script>

<style scoped>
.map-container-wrapper {
  position: relative;
  width: 100%;
  height: 100%;
}

.map-container {
  width: 100%;
  height: 100%;
  min-height: 200px;
  border-radius: 8px;
  overflow: hidden;
}

.recenter-btn {
  position: absolute;
  bottom: 20px;
  left: 50%;
  transform: translateX(-50%);
  z-index: 1000;
  display: flex;
  align-items: center;
  gap: 0.5rem;
  background: var(--card-bg, white);
  border: 1px solid var(--border-color, #ddd);
  border-radius: 20px;
  padding: 0.5rem 1rem;
  font-size: 0.875rem;
  font-weight: 500;
  color: var(--text-primary, #333);
  cursor: pointer;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.15);
  transition: all 0.2s ease;
}

.recenter-btn:hover {
  background: var(--bg-hover, #f5f5f5);
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.2);
}

.recenter-btn:active {
  transform: translateX(-50%) scale(0.98);
}

.recenter-btn svg {
  flex-shrink: 0;
}
</style>

<style>
/* Global styles for custom markers - not scoped so Leaflet can use them */
.leaflet-marker-icon.custom-marker {
  /* Ensure the marker doesn't have conflicting positioning */
  background: transparent;
  border: none;
  /* Prevent any transitions that could cause markers to lag during pan/zoom */
  transition: none !important;
}

/* Ensure marker pane transforms stay in sync with map on mobile */
.leaflet-marker-pane {
  will-change: transform;
}

.leaflet-marker-icon.user-location-marker {
  background: transparent;
  border: none;
}

@keyframes pulse {
  0% {
    transform: translate(-50%, -50%) scale(1);
    opacity: 1;
  }
  100% {
    transform: translate(-50%, -50%) scale(2);
    opacity: 0;
  }
}

@keyframes highlight-pulse {
  0% {
    transform: translate(-50%, -50%) scale(1);
    opacity: 1;
  }
  100% {
    transform: translate(-50%, -50%) scale(1.5);
    opacity: 0;
  }
}
</style>
