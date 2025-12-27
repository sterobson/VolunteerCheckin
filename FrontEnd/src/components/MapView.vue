<template>
  <div ref="mapContainer" class="map-container"></div>
</template>

<script setup>
import { ref, onMounted, watch, defineProps, defineEmits } from 'vue';
import L from 'leaflet';
import 'leaflet/dist/leaflet.css';

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
    default: () => ({ lat: 51.505, lng: -0.09 }),
  },
  zoom: {
    type: Number,
    default: 13,
  },
  clickable: {
    type: Boolean,
    default: false,
  },
});

const emit = defineEmits(['location-click', 'map-click']);

const mapContainer = ref(null);
let map = null;
const markers = ref([]);
let routePolyline = null;
let isInitialLoad = true;
const showDescriptionsForIds = ref(new Set());

const truncateText = (text, maxLength) => {
  if (!text) return '';
  if (text.length <= maxLength) return text;
  return text.substring(0, maxLength) + '...';
};

const estimateLabelWidth = (text) => {
  // Approximate: 6px per character for 11px font with bold weight
  // Add padding (6px on each side = 12px total)
  return (text.length * 6) + 12;
};

const boxesOverlap = (box1, box2) => {
  return !(box1.right < box2.left ||
           box1.left > box2.right ||
           box1.bottom < box2.top ||
           box1.top > box2.bottom);
};

const calculateVisibleDescriptions = () => {
  if (!map || markers.value.length === 0) return new Set();

  const visibleMarkers = [];
  const bounds = map.getBounds();

  // Get visible markers with their pixel positions
  markers.value.forEach((marker) => {
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

const checkVisibleMarkersAndUpdate = () => {
  if (!map) return;

  const newShowDescriptionsForIds = calculateVisibleDescriptions();

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

  map = L.map(mapContainer.value).setView(
    [props.center.lat, props.center.lng],
    props.zoom
  );

  L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
    attribution: '© OpenStreetMap contributors',
    maxZoom: 19,
  }).addTo(map);

  if (props.clickable) {
    map.on('click', (e) => {
      emit('map-click', { lat: e.latlng.lat, lng: e.latlng.lng });
    });
  }

  // Listen for zoom and move events to update label visibility
  map.on('zoomend moveend', () => {
    checkVisibleMarkersAndUpdate();
  });

  updateMarkers();
  updateRoute();
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

    const shouldShowDesc = showDescriptionsForIds.value.has(location.id);
    const labelText = shouldShowDesc && location.description
      ? `${location.name}: ${truncateText(location.description, 50)}`
      : location.name;

    const icon = L.divIcon({
      className: 'custom-marker',
      html: `
        <div style="display: flex; flex-direction: column; align-items: center;">
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

  // Only fit bounds on initial load, not on subsequent updates
  if (map && isInitialLoad) {
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
    }
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
});

watch(
  () => props.locations,
  () => {
    updateMarkers();
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
