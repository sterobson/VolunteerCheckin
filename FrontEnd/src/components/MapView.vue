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

  updateMarkers();
};

const updateMarkers = () => {
  markers.value.forEach((marker) => marker.remove());
  markers.value = [];

  if (!map) return;

  props.locations.forEach((location) => {
    const checkedInCount = location.checkedInCount || 0;
    const requiredMarshals = location.requiredMarshals || 1;
    const isFull = checkedInCount >= requiredMarshals;
    const isMissing = checkedInCount === 0;

    const color = isFull ? 'green' : isMissing ? 'red' : 'orange';

    const icon = L.divIcon({
      className: 'custom-marker',
      html: `
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
      `,
      iconSize: [30, 30],
      iconAnchor: [15, 15],
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

  if (props.locations.length > 0 && map) {
    const bounds = L.latLngBounds(
      props.locations.map((loc) => [loc.latitude, loc.longitude])
    );
    map.fitBounds(bounds, { padding: [50, 50] });
  }
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
