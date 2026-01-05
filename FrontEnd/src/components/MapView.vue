<template>
  <div ref="mapContainer" class="map-container"></div>
</template>

<script setup>
import { ref, onMounted, onUnmounted, watch, defineProps, defineEmits } from 'vue';
import L from 'leaflet';
import 'leaflet/dist/leaflet.css';
import 'leaflet-draw/dist/leaflet.draw.css';
import 'leaflet-draw';
import { truncateText, calculateVisibleDescriptions } from '../utils/labelOverlapDetection';
import {
  getCheckpointMarkerHtml,
  getStatusBadgeHtml,
  getCheckpointStatus,
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
  simplifyNonHighlighted: {
    type: Boolean,
    default: false,
  },
});

const emit = defineEmits(['location-click', 'map-click', 'area-click', 'polygon-complete', 'polygon-drawing']);

const mapContainer = ref(null);
let map = null;

// Polygon drawing state
const polygonPoints = ref([]);
const polygonHistory = ref([]); // For undo - stores previous states
const polygonFuture = ref([]); // For redo - stores undone states
let polygonPreviewLayer = null;
let polygonMarkersLayer = null;

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
});

const markers = ref([]);
let routePolyline = null;
const isInitialLoad = ref(true);
const hasCenteredOnCheckpoints = ref(false);
const showDescriptionsForIds = ref(new Set());
const areaLayers = ref([]);
let drawControl = null;
let drawnItems = null;
let userLocationMarker = null;

// System default color for checkpoint markers
const SYSTEM_DEFAULT_COLOR = '#667eea';

// Build default marker with system default color and status badge overlay
const buildDefaultMarker = (checkedInCount, requiredMarshals, isHighlighted, marshalMode) => {
  const markerSize = isHighlighted ? '36px' : marshalMode ? '20px' : '30px';
  const borderColor = isHighlighted ? '#667eea' : 'white';
  const statusBadge = getStatusBadgeHtml(checkedInCount, requiredMarshals);

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
      <div style="position: absolute; bottom: -4px; right: -4px;">
        ${statusBadge}
      </div>
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
      .filter(point => isValidCoord(point.lat, point.lng))
      .map(point => ({ lat: point.lat, lng: point.lng })));
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

  map = L.map(mapContainer.value).setView(
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

  // Listen for zoom and move events to update label visibility
  map.on('zoomend moveend', () => {
    checkVisibleMarkersAndUpdate();
  });

  updateMarkers();
  updateRoute();
  updateAreaPolygons();
  updateUserLocationMarker();
  initDrawingMode();
};

const updateRoute = () => {
  if (routePolyline) {
    routePolyline.remove();
    routePolyline = null;
  }

  if (!map || props.route.length === 0) return;

  const routeCoordinates = props.route.map((point) => [point.lat, point.lng]);

  routePolyline = L.polyline(routeCoordinates, {
    color: 'blue',
    weight: 4,
    opacity: 0.7,
  }).addTo(map);
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

const updateMarkers = () => {
  // Don't update markers during map animations to prevent positioning bugs
  if (map && map._animatingZoom) {
    return;
  }

  markers.value.forEach((marker) => marker.remove());
  markers.value = [];

  if (!map) return;

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
    // Show simplified gray dots for non-highlighted checkpoints when simplifyNonHighlighted is true
    const showSimplified = props.simplifyNonHighlighted && !isHighlighted;

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
    // Custom styled checkpoint
    else if (hasCustomStyle) {
      // Show count unless in marshal mode (where we hide counts)
      const showCount = !props.marshalMode;

      // Use the new composable SVG generator with all resolved style fields
      const baseSize = isHighlighted ? 40 : 32;
      const sizePercent = location.resolvedStyleSize || location.ResolvedStyleSize || '100';
      const actualSize = Math.round(baseSize * (parseInt(sizePercent, 10) / 100));

      const customSvg = generateCheckpointSvg({
        type: location.resolvedStyleType || location.ResolvedStyleType,
        backgroundShape: location.resolvedStyleBackgroundShape || location.ResolvedStyleBackgroundShape || 'circle',
        backgroundColor: location.resolvedStyleBackgroundColor || location.ResolvedStyleBackgroundColor || location.resolvedStyleColor || location.ResolvedStyleColor || '',
        borderColor: location.resolvedStyleBorderColor || location.ResolvedStyleBorderColor || '',
        iconColor: location.resolvedStyleIconColor || location.ResolvedStyleIconColor || '',
        size: sizePercent,
      });

      if (customSvg) {
        // Wrap the SVG with status badge overlay
        const status = getCheckpointStatus(checkedInCount, requiredMarshals);
        const statusBadge = getStatusBadgeHtml(checkedInCount, requiredMarshals);

        markerHtml = `
          <div style="position: relative; display: inline-block;">
            ${customSvg}
            <div style="position: absolute; top: -4px; right: -4px;">
              ${statusBadge}
            </div>
            ${showCount ? `<div style="
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
            ">${checkedInCount}/${requiredMarshals}</div>` : ''}
          </div>
        `;
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
        markerHtml = legacyMarker || buildDefaultMarker(checkedInCount, requiredMarshals, isHighlighted, props.marshalMode);
      }
    }
    // Default status-based colored circle with status badge
    else {
      markerHtml = buildDefaultMarker(checkedInCount, requiredMarshals, isHighlighted, props.marshalMode);
    }

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
            ${markerHtml}
          </div>
          <div style="
            background-color: ${labelBg};
            padding: 2px 6px;
            border-radius: 3px;
            font-size: ${isHighlighted ? '12px' : '11px'};
            font-weight: bold;
            color: ${labelColor};
            white-space: nowrap;
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
  if (map && isInitialLoad.value && !props.center && !hasCenteredOnCheckpoints.value) {
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
        .filter((point) => isValidCoord(point.lat, point.lng))
        .map((point) => [point.lat, point.lng]));
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
    if (map && !hasCenteredOnCheckpoints.value && newLocations.length > 0 && !props.center) {
      const lats = newLocations.map(loc => loc.latitude);
      const lngs = newLocations.map(loc => loc.longitude);
      const centerLat = lats.reduce((a, b) => a + b, 0) / lats.length;
      const centerLng = lngs.reduce((a, b) => a + b, 0) / lngs.length;
      map.setView([centerLat, centerLng], 10);
      hasCenteredOnCheckpoints.value = true;
      isInitialLoad.value = false;
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
    if (map && !hasCenteredOnCheckpoints.value && newRoute && newRoute.length > 0 && !props.center) {
      const lats = newRoute.map(point => point.lat);
      const lngs = newRoute.map(point => point.lng);
      const centerLat = lats.reduce((a, b) => a + b, 0) / lats.length;
      const centerLng = lngs.reduce((a, b) => a + b, 0) / lngs.length;
      map.setView([centerLat, centerLng], 10);
      hasCenteredOnCheckpoints.value = true;
      isInitialLoad.value = false;
    }
  },
  { deep: true }
);

watch(
  () => props.center,
  (newCenter) => {
    if (map) {
      map.setView([newCenter.lat, newCenter.lng], props.zoom);
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
.map-container {
  width: 100%;
  height: 100%;
  min-height: 400px;
  border-radius: 8px;
  overflow: hidden;
}
</style>

<style>
/* Global styles for custom markers - not scoped so Leaflet can use them */
.leaflet-marker-icon.custom-marker {
  /* Ensure the marker doesn't have conflicting positioning */
  background: transparent;
  border: none;
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
