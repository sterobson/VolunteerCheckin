<template>
  <CommonMap
    v-if="show"
    ref="mapRef"
    :mode="hasExistingRoute ? 'edit-route' : 'draw-route'"
    :editing-route="editingRoute"
    :route="displayRoute"
    :route-color="routeColor"
    :route-weight="routeWeight"
    :locations="locations"
    :highlight-location-ids="highlightLocationIds"
    :simplify-non-highlighted="true"
    :skip-auto-centering="skipAutoCentering"
    :center="mapCenter"
    :zoom="mapZoom"
    :areas="areas"
    :layers="layers"
    height="100%"
    :show-fullscreen="false"
    fullscreen-title="Edit route"
    :fullscreen-description="fullscreenDescription"
    @fullscreen-done="handleDone"
    @fullscreen-cancel="handleCancel"
    @route-complete="handleRouteComplete"
    @route-update="handleRouteUpdate"
    @route-drawing="handleRouteDrawing"
  />
</template>

<script setup>
import { ref, computed, watch, nextTick } from 'vue';
import CommonMap from '../../common/CommonMap.vue';

const props = defineProps({
  show: {
    type: Boolean,
    default: false,
  },
  route: {
    type: Array,
    default: () => [],
  },
  routeColor: {
    type: String,
    default: '#3388ff',
  },
  routeWeight: {
    type: Number,
    default: 4,
  },
  locations: {
    type: Array,
    default: () => [],
  },
  areas: {
    type: Array,
    default: () => [],
  },
  layers: {
    type: Array,
    default: () => [],
  },
  highlightLocationIds: {
    type: Array,
    default: () => [],
  },
  focusOnRoute: {
    type: Boolean,
    default: false,
  },
  center: {
    type: Object,
    default: null,
  },
  zoom: {
    type: Number,
    default: null,
  },
});

const emit = defineEmits(['save', 'cancel']);

const mapRef = ref(null);
const editingRoute = ref([]);
const drawnRoute = ref([]);
const drawingRoutePoints = ref([]); // Track points during draw mode for distance calc

// Calculate distance between two points using Haversine formula
const haversineDistance = (lat1, lng1, lat2, lng2) => {
  const R = 6371; // Earth's radius in kilometers
  const dLat = (lat2 - lat1) * Math.PI / 180;
  const dLng = (lng2 - lng1) * Math.PI / 180;
  const a =
    Math.sin(dLat / 2) * Math.sin(dLat / 2) +
    Math.cos(lat1 * Math.PI / 180) * Math.cos(lat2 * Math.PI / 180) *
    Math.sin(dLng / 2) * Math.sin(dLng / 2);
  const c = 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a));
  return R * c;
};

// Calculate total route distance
const calculateRouteDistance = (points) => {
  if (!points || points.length < 2) return 0;
  let total = 0;
  for (let i = 0; i < points.length - 1; i++) {
    total += haversineDistance(
      points[i].lat, points[i].lng,
      points[i + 1].lat, points[i + 1].lng
    );
  }
  return total;
};

// Get the current route points based on mode
const currentRoutePoints = computed(() => {
  if (hasExistingRoute.value) {
    return editingRoute.value;
  }
  return drawingRoutePoints.value;
});

// Route distance in km
const routeDistanceKm = computed(() => {
  return calculateRouteDistance(currentRoutePoints.value);
});

// Format distance for display
const formattedDistance = computed(() => {
  const km = routeDistanceKm.value;
  if (km === 0) return '';
  if (km < 1) {
    return `${Math.round(km * 1000)} m`;
  }
  return `${km.toFixed(2)} km`;
});

// Check if we have an existing route to edit
const hasExistingRoute = computed(() => {
  return props.route && props.route.length > 0;
});

// Skip auto-centering if we have an explicit center or if we're focusing on an existing route
const skipAutoCentering = computed(() => {
  return props.center != null || (props.focusOnRoute && hasExistingRoute.value);
});

// Use provided center, or null to let the map auto-center
const mapCenter = computed(() => props.center);

// Use provided zoom, or default
const mapZoom = computed(() => props.zoom || 13);

// The route to display on the map during editing
const displayRoute = computed(() => {
  // In edit mode, we don't show the route prop since it's in editingRoute
  // In draw mode, we show the drawn route
  if (hasExistingRoute.value) {
    return []; // editingRoute handles the display in edit mode
  }
  return drawnRoute.value;
});

// Description text for the fullscreen header
const fullscreenDescription = computed(() => {
  const distanceText = formattedDistance.value ? ` | Distance: ${formattedDistance.value}` : '';
  if (hasExistingRoute.value) {
    return `Drag points to move. Drag midpoints to insert. Right-click to delete.${distanceText}`;
  }
  return `Click to add points. Drag to move. Right-click to delete.${distanceText}`;
});

// Open fullscreen when shown
watch(() => props.show, async (newVal) => {
  if (newVal) {
    // Initialize editing route from props
    if (props.route && props.route.length > 0) {
      editingRoute.value = props.route.map(p => ({ lat: p.lat, lng: p.lng }));
    } else {
      editingRoute.value = [];
    }
    drawnRoute.value = [];
    drawingRoutePoints.value = [];

    // Wait for CommonMap to mount then open fullscreen
    await nextTick();
    if (mapRef.value) {
      mapRef.value.openFullscreen();

      // If editing an existing route, wait for fullscreen to be ready then zoom to route
      if (props.focusOnRoute && editingRoute.value.length >= 2) {
        // Longer delay to ensure fullscreen map container is properly sized
        // The fullscreen modal needs time to render and size correctly
        setTimeout(() => {
          if (mapRef.value) {
            mapRef.value.fitToEditingRouteBounds([50, 50]);
          }
        }, 350);
      }
    }
  }
}, { immediate: true });

// Handle route drawing updates (from draw mode, while drawing)
const handleRouteDrawing = (points) => {
  drawingRoutePoints.value = points || [];
};

// Handle route completion (from draw mode)
const handleRouteComplete = (points) => {
  drawnRoute.value = points;
  emit('save', points);
};

// Handle route updates (from edit mode)
const handleRouteUpdate = (points) => {
  editingRoute.value = points;
};

// Handle done button click
const handleDone = () => {
  // In edit mode, emit the current editing route
  if (hasExistingRoute.value) {
    emit('save', [...editingRoute.value]);
  }
  // In draw mode, the route-complete event already emitted save
};

// Handle cancel button click
const handleCancel = () => {
  emit('cancel');
};
</script>
