<template>
  <div class="course-areas-tab">
    <div class="map-section" ref="mapSection">
      <CommonMap
        ref="commonMapRef"
        :locations="checkpoints"
        :route="route"
        :areas="areas"
        :all-locations-for-bounds="checkpoints"
        :selected-area-id="selectedAreaId"
        :clickable="true"
        :mode="drawingMode ? 'draw-polygon' : 'view'"
        height="100%"
        show-filters
        :filters="filters"
        :toolbar-actions="toolbarActions"
        :fullscreen-title="drawingMode ? `Draw ${termsLower.area} boundary` : terms.course"
        :fullscreen-description="drawingMode ? 'Click on the map to draw the boundary' : ''"
        @map-click="$emit('map-click', $event)"
        @location-click="$emit('location-click', $event)"
        @area-click="$emit('area-click', $event)"
        @polygon-complete="$emit('polygon-complete', $event)"
        @polygon-drawing="handlePolygonDrawing"
        @filter-change="handleFilterChange"
        @action-click="handleActionClick"
      />
    </div>
  </div>
</template>

<script setup>
import { ref, computed, watch, defineProps, defineEmits } from 'vue';
import CommonMap from '../../components/common/CommonMap.vue';
import { useTerminology } from '../../composables/useTerminology';

const { terms, termsLower } = useTerminology();

const props = defineProps({
  checkpoints: {
    type: Array,
    default: () => [],
  },
  areas: {
    type: Array,
    default: () => [],
  },
  route: {
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

const emit = defineEmits([
  'map-click',
  'location-click',
  'area-click',
  'import-checkpoints',
  'upload-route',
  'select-location',
  'add-area',
  'select-area',
  'polygon-complete',
  'cancel-drawing',
  'add-checkpoint-from-map',
  'add-many-checkpoints-from-map',
  'add-area-from-map',
]);

const mapSection = ref(null);
const commonMapRef = ref(null);

// Polygon drawing state
const polygonPointCount = ref(0);

// Filters state - CommonMap handles the actual filtering
const filters = ref({
  showRoute: true,
  showUncheckedIn: true,
  showPartiallyCheckedIn: true,
  showFullyCheckedIn: true,
  showAreas: true,
  selectedAreaIds: [],
});

// Toolbar actions for the Add menu
const toolbarActions = computed(() => [
  {
    id: 'add',
    icon: 'plus',
    label: 'Add',
    items: [
      { id: 'checkpoint', label: terms.value.checkpoint },
      { id: 'many-checkpoints', label: `Many ${termsLower.value.checkpoints}` },
      { id: 'area', label: terms.value.area },
      { id: 'import', label: `Import ${termsLower.value.checkpoints}...` },
      { id: 'upload', label: `Upload ${termsLower.value.course}...` },
    ],
  },
]);

// Handle action clicks from toolbar
const handleActionClick = ({ actionId, itemId }) => {
  if (actionId === 'add') {
    if (itemId === 'checkpoint') {
      emit('add-checkpoint-from-map');
      scrollMapIntoView();
    } else if (itemId === 'many-checkpoints') {
      emit('add-many-checkpoints-from-map');
      scrollMapIntoView();
    } else if (itemId === 'area') {
      emit('add-area-from-map');
      scrollMapIntoView();
    } else if (itemId === 'import') {
      emit('import-checkpoints');
    } else if (itemId === 'upload') {
      emit('upload-route');
    }
  }
};

// Handle filter changes from CommonMap
const handleFilterChange = (newFilters) => {
  filters.value = newFilters;
};

// Handle polygon drawing updates from CommonMap
const handlePolygonDrawing = (points) => {
  polygonPointCount.value = points?.length || 0;
};

// Expose method to scroll map into view
const scrollMapIntoView = () => {
  if (mapSection.value) {
    mapSection.value.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }
};

// Expose methods to get current map state
const getMapCenter = () => {
  return commonMapRef.value?.getMapCenter() || null;
};

const getMapZoom = () => {
  return commonMapRef.value?.getMapZoom() || null;
};

defineExpose({
  scrollMapIntoView,
  getMapCenter,
  getMapZoom,
});

// Initialize selected areas with all area IDs when areas change
watch(() => props.areas, (newAreas) => {
  if (newAreas.length > 0) {
    // Add any new areas to the selected list (default to all checked)
    const currentSelectedIds = new Set(filters.value.selectedAreaIds);
    newAreas.forEach(area => {
      if (!currentSelectedIds.has(area.id)) {
        filters.value.selectedAreaIds.push(area.id);
      }
    });

    // Remove any areas that no longer exist
    filters.value.selectedAreaIds = filters.value.selectedAreaIds.filter(id =>
      newAreas.some(area => area.id === id)
    );
  }
}, { immediate: true });

// Reset polygon state when drawing mode changes
watch(() => props.drawingMode, (newDrawingMode) => {
  if (!newDrawingMode) {
    polygonPointCount.value = 0;
  }
});
</script>

<style scoped>
.course-areas-tab {
  width: 100%;
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.map-section {
  position: relative;
  height: 500px;
  border-radius: 8px;
  overflow: visible;
  box-shadow: var(--shadow-sm);
}

@media (max-width: 768px) {
  .map-section {
    height: 350px;
  }
}
</style>
