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
});

const emit = defineEmits(['location-click', 'map-click', 'area-click', 'polygon-complete', 'polygon-drawing']);

const mapContainer = ref(null);
let map = null;

// Expose methods to get current map state
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

defineExpose({
  getMapCenter,
  getMapZoom,
});
const markers = ref([]);
let routePolyline = null;
let isInitialLoad = true;
let hasCenteredOnCheckpoints = false;
const showDescriptionsForIds = ref(new Set());
const areaLayers = ref([]);
let drawControl = null;
let drawnItems = null;

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

const initMap = () => {
  if (!mapContainer.value) return;

  // Calculate center from checkpoints if available and no explicit center provided
  let centerLat = 51.505;
  let centerLng = -0.09;
  let zoomLevel = 13;

  if (!props.center && props.locations.length > 0) {
    // Calculate center from all checkpoints
    const lats = props.locations.map(loc => loc.latitude);
    const lngs = props.locations.map(loc => loc.longitude);
    centerLat = lats.reduce((a, b) => a + b, 0) / lats.length;
    centerLng = lngs.reduce((a, b) => a + b, 0) / lngs.length;
    zoomLevel = 10; // Start a bit zoomed out to see all points
    hasCenteredOnCheckpoints = true;
  } else if (props.center) {
    centerLat = props.center.lat;
    centerLng = props.center.lng;
    zoomLevel = props.zoom ?? 13;
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

const initDrawingMode = () => {
  if (!map) return;

  // Create feature group for drawn items
  if (!drawnItems) {
    drawnItems = new L.FeatureGroup();
    map.addLayer(drawnItems);
  }

  // Remove existing draw control
  if (drawControl) {
    map.removeControl(drawControl);
    drawControl = null;
  }

  if (props.drawingMode) {
    // Enable drawing mode with improved configuration
    drawControl = new L.Control.Draw({
      draw: {
        polygon: {
          allowIntersection: false,
          showArea: false, // Disable area display to avoid leaflet-draw bug
          shapeOptions: {
            color: '#667eea',
            weight: 3,
            opacity: 0.8,
            fillOpacity: 0.3,
          },
          repeatMode: false, // Don't start a new polygon after finishing one
          touchExtend: true, // Better touch support
          metric: true, // Use metric system
          feet: false, // Don't use imperial
          nautic: false, // Don't use nautic
        },
        polyline: false,
        rectangle: false,
        circle: false,
        marker: false,
        circlemarker: false,
      },
      edit: {
        featureGroup: drawnItems,
        remove: false,
      },
    });

    map.addControl(drawControl);

    // Disable map dragging while actively placing a vertex to prevent accidental panning
    let mousedownPos = null;
    let isDragging = false;
    const dragThreshold = 5; // pixels

    const handleMouseDown = (e) => {
      mousedownPos = { x: e.originalEvent.clientX, y: e.originalEvent.clientY };
      isDragging = false;
    };

    const handleMouseMove = (e) => {
      if (mousedownPos) {
        const dx = e.originalEvent.clientX - mousedownPos.x;
        const dy = e.originalEvent.clientY - mousedownPos.y;
        const distance = Math.sqrt(dx * dx + dy * dy);

        if (distance > dragThreshold) {
          isDragging = true;
        }
      }
    };

    const handleMouseUp = () => {
      mousedownPos = null;
    };

    map.on('mousedown', handleMouseDown);
    map.on('mousemove', handleMouseMove);
    map.on('mouseup', handleMouseUp);

    // Start drawing automatically
    const polygonDrawer = new L.Draw.Polygon(map, drawControl.options.draw.polygon);
    polygonDrawer.enable();

    // Store handlers for cleanup
    map._drawingHandlers = { handleMouseDown, handleMouseMove, handleMouseUp };

    // Listen for drawing vertex events to track intermediate polygon state
    const handleDrawVertex = (e) => {
      // Emit current polygon points as user draws
      if (e.layers && e.layers.getLayers().length > 0) {
        const currentLayer = e.layers.getLayers()[0];
        if (currentLayer && currentLayer.getLatLngs) {
          const currentPoints = currentLayer.getLatLngs()[0] || [];
          emit('polygon-drawing', currentPoints);
        }
      }
    };

    map.on(L.Draw.Event.DRAWVERTEX, handleDrawVertex);
    map._drawVertexHandler = handleDrawVertex;

    // Listen for created event
    map.on(L.Draw.Event.CREATED, (event) => {
      const layer = event.layer;
      const coordinates = layer.getLatLngs()[0].map((latlng) => ({
        lat: latlng.lat,
        lng: latlng.lng,
      }));

      emit('polygon-complete', coordinates);

      // Clear the drawn layer
      drawnItems.clearLayers();

      // Clean up handlers
      if (map._drawingHandlers) {
        map.off('mousedown', map._drawingHandlers.handleMouseDown);
        map.off('mousemove', map._drawingHandlers.handleMouseMove);
        map.off('mouseup', map._drawingHandlers.handleMouseUp);
        delete map._drawingHandlers;
      }
      if (map._drawVertexHandler) {
        map.off(L.Draw.Event.DRAWVERTEX, map._drawVertexHandler);
        delete map._drawVertexHandler;
      }
    });
  } else {
    // Disable drawing mode
    if (drawnItems) {
      drawnItems.clearLayers();
    }
    map.off(L.Draw.Event.CREATED);

    // Clean up drawing handlers if they exist
    if (map._drawingHandlers) {
      map.off('mousedown', map._drawingHandlers.handleMouseDown);
      map.off('mousemove', map._drawingHandlers.handleMouseMove);
      map.off('mouseup', map._drawingHandlers.handleMouseUp);
      delete map._drawingHandlers;
    }
    if (map._drawVertexHandler) {
      map.off(L.Draw.Event.DRAWVERTEX, map._drawVertexHandler);
      delete map._drawVertexHandler;
    }
  }
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
    const checkedInCount = location.checkedInCount || 0;
    const requiredMarshals = location.requiredMarshals || 1;
    const isFull = checkedInCount >= requiredMarshals;
    const isMissing = checkedInCount === 0;

    const color = isFull ? 'green' : isMissing ? 'red' : 'orange';

    // Area color indicator
    const areaColor = location.areaColor || '#999';
    const hasArea = location.areaId;

    const shouldShowDesc = showDescriptionsForIds.value.has(location.id);
    const labelText = shouldShowDesc && location.description
      ? `${location.name}: ${truncateText(location.description, 50)}`
      : location.name;

    const icon = L.divIcon({
      className: 'custom-marker',
      html: `
        <div style="display: flex; flex-direction: column; align-items: center;">
          <div style="position: relative; display: flex; align-items: center; gap: 4px;">
            ${hasArea ? `<div style="
              width: 8px;
              height: 8px;
              border-radius: 50%;
              background-color: ${areaColor};
              border: 2px solid white;
              box-shadow: 0 1px 2px rgba(0,0,0,0.3);
            "></div>` : ''}
            <div style="
              background-color: ${color};
              width: 30px;
              height: 30px;
              border-radius: 50%;
              border: 3px solid white;
              box-shadow: 0 2px 5px rgba(0,0,0,0.3);
              display: flex;
              align-items: center;
              justify-content: center;
              color: white;
              font-weight: bold;
              font-size: 12px;
            ">
              ${checkedInCount}/${requiredMarshals}
            </div>
          </div>
          <div style="
            background-color: white;
            padding: 2px 6px;
            border-radius: 3px;
            font-size: 11px;
            font-weight: bold;
            color: #333;
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

    const marker = L.marker([location.latitude, location.longitude], { icon })
      .addTo(map)
      .bindPopup(`
        <b>${location.name}</b><br>
        ${location.description || ''}<br>
        Checked in: ${checkedInCount}/${requiredMarshals}
      `);

    marker.on('click', () => {
      emit('location-click', location);
    });

    // Store location data for overlap detection
    marker.locationData = location;

    markers.value.push(marker);
  });

  // Only fit bounds on initial load if center/zoom are not explicitly provided
  // If center/zoom are provided, respect those instead of auto-fitting
  if (map && isInitialLoad && !props.center && !hasCenteredOnCheckpoints) {
    const allPoints = [];

    // Add location points
    if (props.locations.length > 0) {
      allPoints.push(...props.locations.map((loc) => [loc.latitude, loc.longitude]));
    }

    // Add route points
    if (props.route.length > 0) {
      allPoints.push(...props.route.map((point) => [point.lat, point.lng]));
    }

    if (allPoints.length > 0) {
      const bounds = L.latLngBounds(allPoints);
      map.fitBounds(bounds, { padding: [50, 50] });
      isInitialLoad = false;
      hasCenteredOnCheckpoints = true;
    }
  } else if (props.center) {
    // Center is explicitly provided, so don't auto-fit
    isInitialLoad = false;
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

    // If we haven't centered on checkpoints yet and we now have checkpoints, center on them
    if (map && !hasCenteredOnCheckpoints && newLocations.length > 0 && !props.center) {
      const lats = newLocations.map(loc => loc.latitude);
      const lngs = newLocations.map(loc => loc.longitude);
      const centerLat = lats.reduce((a, b) => a + b, 0) / lats.length;
      const centerLng = lngs.reduce((a, b) => a + b, 0) / lngs.length;
      map.setView([centerLat, centerLng], 10);
      hasCenteredOnCheckpoints = true;
      isInitialLoad = false;
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
  () => props.route,
  () => {
    updateRoute();
    updateMarkers(); // Re-fit bounds to include route
  },
  { deep: true }
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
</style>
