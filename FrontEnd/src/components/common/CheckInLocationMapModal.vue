<template>
  <BaseModal
    :show="show"
    :title="modalTitle"
    size="medium"
    :z-index="zIndex"
    @close="$emit('close')"
  >
    <div class="map-modal-content">
      <div class="map-container" ref="mapContainer"></div>

      <div class="location-info">
        <div v-if="checkInLocation" class="info-row">
          <span class="info-label">Check-in location:</span>
          <span class="info-value">{{ formatCoords(checkInLocation.lat, checkInLocation.lng) }}</span>
        </div>
        <div v-if="checkpointLocation" class="info-row">
          <span class="info-label">{{ checkpointTerm }} location:</span>
          <span class="info-value">{{ formatCoords(checkpointLocation.lat, checkpointLocation.lng) }}</span>
        </div>
        <div v-if="distance !== null" class="info-row distance-row">
          <span class="info-label">Distance:</span>
          <span class="info-value distance-value" :class="distanceClass">{{ formattedDistance }}</span>
        </div>
      </div>
    </div>
  </BaseModal>
</template>

<script setup>
import { ref, computed, watch, onMounted, onUnmounted, nextTick, defineProps, defineEmits } from 'vue';
import L from 'leaflet';
import 'leaflet/dist/leaflet.css';
import BaseModal from '../BaseModal.vue';
import { useTerminology } from '../../composables/useTerminology';

const { terms } = useTerminology();

const props = defineProps({
  show: {
    type: Boolean,
    default: false,
  },
  marshalName: {
    type: String,
    default: '',
  },
  checkpointName: {
    type: String,
    default: '',
  },
  // Check-in location (where the marshal was when they checked in)
  checkInLocation: {
    type: Object,
    default: null, // { lat, lng }
  },
  // Checkpoint location (the checkpoint's configured location)
  checkpointLocation: {
    type: Object,
    default: null, // { lat, lng }
  },
  // Pre-calculated distance in meters (optional)
  distance: {
    type: Number,
    default: null,
  },
  zIndex: {
    type: Number,
    default: 1100,
  },
});

defineEmits(['close']);

const mapContainer = ref(null);
let map = null;
let checkInMarker = null;
let checkpointMarker = null;

const checkpointTerm = computed(() => terms.value.checkpoint);

const modalTitle = computed(() => {
  if (props.marshalName && props.checkpointName) {
    return `${props.marshalName} - ${props.checkpointName}`;
  }
  if (props.marshalName) {
    return props.marshalName;
  }
  return 'Check-in location';
});

const formattedDistance = computed(() => {
  if (props.distance === null) return '';
  if (props.distance < 1000) {
    return `${Math.round(props.distance)} m`;
  }
  return `${(props.distance / 1000).toFixed(2)} km`;
});

const distanceClass = computed(() => {
  if (props.distance === null) return '';
  if (props.distance <= 50) return 'distance-close';
  if (props.distance <= 100) return 'distance-medium';
  return 'distance-far';
});

const formatCoords = (lat, lng) => {
  if (lat == null || lng == null) return 'Unknown';
  return `${lat.toFixed(6)}, ${lng.toFixed(6)}`;
};

const initMap = () => {
  if (!mapContainer.value || map) return;

  // Determine initial center and zoom
  let center = [51.505, -0.09];
  let zoom = 15;

  if (props.checkInLocation) {
    center = [props.checkInLocation.lat, props.checkInLocation.lng];
  }

  map = L.map(mapContainer.value).setView(center, zoom);

  L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
    attribution: '&copy; OpenStreetMap contributors',
    maxZoom: 19,
  }).addTo(map);

  updateMarkers();
};

const updateMarkers = () => {
  if (!map) return;

  // Remove existing markers
  if (checkInMarker) {
    checkInMarker.remove();
    checkInMarker = null;
  }
  if (checkpointMarker) {
    checkpointMarker.remove();
    checkpointMarker = null;
  }

  const bounds = [];

  // Add check-in location marker (blue pulsing dot - "You are here" style)
  if (props.checkInLocation) {
    const checkInIcon = L.divIcon({
      className: 'check-in-location-marker',
      html: `
        <div style="position: relative; width: 24px; height: 24px;">
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

    checkInMarker = L.marker([props.checkInLocation.lat, props.checkInLocation.lng], { icon: checkInIcon })
      .addTo(map)
      .bindPopup(`<b>${props.marshalName || 'Marshal'}</b><br>Check-in location`);

    bounds.push([props.checkInLocation.lat, props.checkInLocation.lng]);
  }

  // Add checkpoint location marker (standard checkpoint marker)
  if (props.checkpointLocation && props.checkpointLocation.lat && props.checkpointLocation.lng) {
    const checkpointIcon = L.divIcon({
      className: 'checkpoint-location-marker',
      html: `
        <div style="display: flex; flex-direction: column; align-items: center;">
          <div style="
            width: 32px;
            height: 32px;
            background-color: #667eea;
            border: 3px solid white;
            border-radius: 50%;
            box-shadow: 0 2px 5px rgba(0,0,0,0.3);
            display: flex;
            align-items: center;
            justify-content: center;
          ">
            <svg width="16" height="16" viewBox="0 0 24 24" fill="white">
              <path d="M12 2C8.13 2 5 5.13 5 9c0 5.25 7 13 7 13s7-7.75 7-13c0-3.87-3.13-7-7-7zm0 9.5c-1.38 0-2.5-1.12-2.5-2.5s1.12-2.5 2.5-2.5 2.5 1.12 2.5 2.5-1.12 2.5-2.5 2.5z"/>
            </svg>
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
            ${props.checkpointName || terms.value.checkpoint}
          </div>
        </div>
      `,
      iconSize: [30, 50],
      iconAnchor: [15, 25],
    });

    checkpointMarker = L.marker([props.checkpointLocation.lat, props.checkpointLocation.lng], { icon: checkpointIcon })
      .addTo(map)
      .bindPopup(`<b>${props.checkpointName || terms.value.checkpoint}</b><br>${terms.value.checkpoint} location`);

    bounds.push([props.checkpointLocation.lat, props.checkpointLocation.lng]);
  }

  // Fit bounds to show both markers
  if (bounds.length === 2) {
    const latLngBounds = L.latLngBounds(bounds);
    map.fitBounds(latLngBounds, { padding: [50, 50], maxZoom: 17 });
  } else if (bounds.length === 1) {
    map.setView(bounds[0], 16);
  }
};

const destroyMap = () => {
  if (map) {
    map.remove();
    map = null;
    checkInMarker = null;
    checkpointMarker = null;
  }
};

watch(() => props.show, async (newVal) => {
  if (newVal) {
    await nextTick();
    // Small delay to ensure container is sized
    setTimeout(() => {
      initMap();
    }, 100);
  } else {
    destroyMap();
  }
});

watch([() => props.checkInLocation, () => props.checkpointLocation], () => {
  if (map) {
    updateMarkers();
  }
}, { deep: true });

onMounted(() => {
  if (props.show) {
    nextTick(() => {
      setTimeout(() => {
        initMap();
      }, 100);
    });
  }
});

onUnmounted(() => {
  destroyMap();
});
</script>

<style scoped>
.map-modal-content {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.map-container {
  width: 100%;
  height: 300px;
  border-radius: 8px;
  overflow: hidden;
  background: var(--bg-tertiary);
}

.location-info {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  padding: 0.75rem;
  background: var(--bg-secondary);
  border-radius: 8px;
}

.info-row {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 1rem;
}

.info-label {
  color: var(--text-secondary);
  font-size: 0.85rem;
}

.info-value {
  color: var(--text-primary);
  font-size: 0.85rem;
  font-family: monospace;
}

.distance-row {
  padding-top: 0.5rem;
  border-top: 1px solid var(--border-color);
}

.distance-value {
  padding: 0.2rem 0.5rem;
  border-radius: 8px;
  font-weight: 600;
  font-family: inherit;
}

.distance-value.distance-close {
  background: var(--status-success-bg);
  color: var(--accent-success);
}

.distance-value.distance-medium {
  background: var(--status-warning-bg);
  color: var(--warning-text, #92400e);
}

.distance-value.distance-far {
  background: var(--status-danger-bg);
  color: var(--accent-danger);
}
</style>

<style>
/* Global styles for Leaflet markers */
.check-in-location-marker,
.checkpoint-location-marker {
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
</style>
