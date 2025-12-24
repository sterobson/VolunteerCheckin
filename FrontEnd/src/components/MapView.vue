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
const showDescriptions = ref(false);

const truncateText = (text, maxLength) => {
  if (!text) return '';
  if (text.length <= maxLength) return text;
  return text.substring(0, maxLength) + '...';
};

const checkVisibleMarkersAndUpdate = () => {
  if (!map) return;

  const bounds = map.getBounds();
  let visibleCount = 0;

  markers.value.forEach((marker) => {
    const markerLatLng = marker.getLatLng();
    if (bounds.contains(markerLatLng)) {
      visibleCount++;
    }
  });

  const shouldShowDescriptions = visibleCount <= 10;

  // Only update if the state changed
  if (showDescriptions.value !== shouldShowDescriptions) {
    showDescriptions.value = shouldShowDescriptions;
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

    const labelText = showDescriptions.value && location.description
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
