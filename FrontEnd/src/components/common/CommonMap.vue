<template>
  <div class="common-map" :style="{ height: height }">
    <!-- Main map container -->
    <div class="map-wrapper">
      <MapView
        ref="mapViewRef"
        :locations="filteredLocations"
        :route="filteredRoute"
        :route-color="routeColor"
        :route-style="routeStyle"
        :route-weight="routeWeight"
        :areas="filteredAreas"
        :center="center"
        :zoom="zoom"
        :clickable="clickable || mode === 'select-point'"
        :drawing-mode="mode === 'draw-polygon'"
        :editing-polygon="mode === 'edit-polygon' ? editingPolygon : null"
        :user-location="userLocation"
        :highlight-location-id="highlightLocationId"
        :highlight-location-ids="highlightLocationIds"
        :all-locations-for-bounds="allLocationsForBounds"
        :marshal-mode="marshalMode"
        :simplify-non-highlighted="simplifyNonHighlighted"
        :selected-area-id="selectedAreaId"
        :hide-recenter-button="hideRecenterButton"
        @location-click="$emit('location-click', $event)"
        @map-click="handleMapClick"
        @area-click="$emit('area-click', $event)"
        @polygon-complete="handlePolygonComplete"
        @polygon-drawing="handlePolygonDrawing"
        @polygon-update="handlePolygonUpdate"
        @visibility-change="$emit('visibility-change', $event)"
      />

      <!-- Toolbar -->
      <MapToolbar
        ref="toolbarRef"
        :show-fullscreen="showFullscreen"
        :show-filters="showFilters"
        :filters="internalFilters"
        :areas="areas"
        :actions="toolbarActions"
        :position="toolbarPosition"
        @fullscreen-click="handleFullscreenClick"
        @filter-change="handleFilterChange"
        @action-click="$emit('action-click', $event)"
      />

      <!-- Drawing controls (shown when in draw-polygon mode) -->
      <MapDrawingControls
        :show="mode === 'draw-polygon'"
        :point-count="polygonPointCount"
        :can-undo="canUndoPoints"
        :can-redo="canRedoPoints"
        @undo="handleUndo"
        @redo="handleRedo"
      />
    </div>

    <!-- Fullscreen modal -->
    <MapFullscreenModal
      :show="isFullscreen"
      :mode="mode"
      :title="fullscreenTitle"
      :description="fullscreenDescription"
      :can-complete="canCompleteFullscreenAction"
      :header-style="fullscreenHeaderStyle"
      :header-text-color="fullscreenHeaderTextColor"
      @done="handleFullscreenDone"
      @cancel="handleFullscreenCancel"
      @close="handleFullscreenClose"
    >
      <!-- Pass through slots (only if content is provided) -->
      <template v-if="$slots['fullscreen-actions']" #actions>
        <slot name="fullscreen-actions"></slot>
      </template>
      <template v-if="$slots['fullscreen-banner']" #banner>
        <slot name="fullscreen-banner"></slot>
      </template>
      <template v-if="$slots['fullscreen-footer']" #footer>
        <slot name="fullscreen-footer"></slot>
      </template>

      <div class="fullscreen-map-wrapper">
        <MapView
          ref="fullscreenMapViewRef"
          :locations="filteredLocations"
          :route="filteredRoute"
          :route-color="routeColor"
          :route-style="routeStyle"
          :areas="filteredAreas"
          :center="center"
          :zoom="zoom"
          :clickable="clickable || mode === 'select-point'"
          :drawing-mode="mode === 'draw-polygon'"
          :editing-polygon="mode === 'edit-polygon' ? editingPolygon : null"
          :user-location="userLocation"
          :highlight-location-id="highlightLocationId"
          :highlight-location-ids="highlightLocationIds"
          :all-locations-for-bounds="allLocationsForBounds"
          :marshal-mode="marshalMode"
          :simplify-non-highlighted="simplifyNonHighlighted"
          :selected-area-id="selectedAreaId"
          :hide-recenter-button="hideRecenterButton"
          @location-click="$emit('location-click', $event)"
          @map-click="handleMapClick"
          @area-click="$emit('area-click', $event)"
          @polygon-complete="handlePolygonComplete"
          @polygon-drawing="handlePolygonDrawing"
          @polygon-update="handlePolygonUpdate"
          @visibility-change="$emit('visibility-change', $event)"
        />

        <!-- Toolbar in fullscreen (no fullscreen button since already fullscreen) -->
        <MapToolbar
          ref="fullscreenToolbarRef"
          :show-fullscreen="false"
          :show-filters="showFilters"
          :filters="internalFilters"
          :areas="areas"
          :actions="toolbarActions"
          :position="toolbarPosition"
          @filter-change="handleFilterChange"
          @action-click="$emit('action-click', $event)"
        />

        <!-- Drawing controls in fullscreen -->
        <MapDrawingControls
          :show="mode === 'draw-polygon'"
          :point-count="polygonPointCount"
          :can-undo="canUndoPoints"
          :can-redo="canRedoPoints"
          @undo="handleUndo"
          @redo="handleRedo"
        />
      </div>
    </MapFullscreenModal>
  </div>
</template>

<script setup>
import { ref, computed, watch, defineProps, defineEmits, defineExpose } from 'vue';
import MapView from '../MapView.vue';
import MapToolbar from './MapToolbar.vue';
import MapDrawingControls from './MapDrawingControls.vue';
import MapFullscreenModal from './MapFullscreenModal.vue';

const props = defineProps({
  // Core map data (passed through to MapView)
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
  areas: {
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
  selectedAreaId: {
    type: String,
    default: null,
  },
  marshalMode: {
    type: Boolean,
    default: false,
  },
  simplifyNonHighlighted: {
    type: Boolean,
    default: false,
  },
  clickable: {
    type: Boolean,
    default: false,
  },

  // CommonMap specific
  mode: {
    type: String,
    default: 'view',
    validator: (v) => ['view', 'select-point', 'draw-polygon', 'edit-polygon'].includes(v),
  },
  editingPolygon: {
    type: Array,
    default: null,
  },
  height: {
    type: String,
    default: '400px',
  },

  // Toolbar configuration
  showFullscreen: {
    type: Boolean,
    default: true,
  },
  showFilters: {
    type: Boolean,
    default: false,
  },
  toolbarActions: {
    type: Array,
    default: () => [],
  },
  toolbarPosition: {
    type: String,
    default: 'top-right',
  },

  // Filter state (used when showFilters is true)
  filters: {
    type: Object,
    default: () => ({
      showRoute: true,
      showAreaOverlays: true,
      showUncheckedIn: true,
      showPartiallyCheckedIn: true,
      showFullyCheckedIn: true,
      showAreas: true,
      selectedAreaIds: [],
    }),
  },

  // Fullscreen configuration
  fullscreenTitle: {
    type: String,
    default: '',
  },
  fullscreenDescription: {
    type: String,
    default: '',
  },
  fullscreenHeaderStyle: {
    type: Object,
    default: () => ({}),
  },
  fullscreenHeaderTextColor: {
    type: String,
    default: '',
  },
  hideRecenterButton: {
    type: Boolean,
    default: false,
  },
});

const emit = defineEmits([
  // Passed through from MapView
  'location-click',
  'map-click',
  'area-click',
  'visibility-change',

  // Mode-specific
  'point-selected',
  'polygon-complete',
  'polygon-drawing',
  'polygon-update',

  // Toolbar
  'fullscreen-change',
  'filter-change',
  'action-click',

  // Fullscreen action events
  'fullscreen-done',
  'fullscreen-cancel',
]);

// Refs
const mapViewRef = ref(null);
const fullscreenMapViewRef = ref(null);
const toolbarRef = ref(null);
const fullscreenToolbarRef = ref(null);

// State
const isFullscreen = ref(false);
const polygonPointCount = ref(0);
const canUndoPoints = ref(false);
const canRedoPoints = ref(false);

// Internal filters (synced with props.filters)
const internalFilters = ref({ ...props.filters });

// Watch for external filter changes
watch(() => props.filters, (newFilters) => {
  internalFilters.value = { ...newFilters };
}, { deep: true });

// Initialize selectedAreaIds with all areas when areas become available
watch(() => props.areas, (newAreas) => {
  if (props.showFilters && newAreas.length > 0 &&
      (!internalFilters.value.selectedAreaIds || internalFilters.value.selectedAreaIds.length === 0)) {
    // Only auto-select all if we haven't explicitly set any selection yet
    internalFilters.value.selectedAreaIds = newAreas.map(a => a.id);
  }
}, { immediate: true });

// Computed filtered data based on filter state
const filteredLocations = computed(() => {
  if (!props.showFilters) return props.locations;

  return props.locations.filter(location => {
    const checkedInCount = location.checkedInCount || 0;
    const requiredMarshals = location.requiredMarshals || 1;

    // Determine check-in status
    const isUnchecked = checkedInCount === 0;
    const isPartial = checkedInCount > 0 && checkedInCount < requiredMarshals;
    const isFull = checkedInCount >= requiredMarshals;

    // Filter by check-in status
    if (isUnchecked && !internalFilters.value.showUncheckedIn) return false;
    if (isPartial && !internalFilters.value.showPartiallyCheckedIn) return false;
    if (isFull && !internalFilters.value.showFullyCheckedIn) return false;

    // Filter by selected areas (only when areas exist to filter)
    const locationAreaIds = location.areaIds || location.AreaIds || [];
    if (props.areas.length > 0 && locationAreaIds.length > 0) {
      // If no areas are selected, hide all locations that belong to areas
      if (!internalFilters.value.selectedAreaIds || internalFilters.value.selectedAreaIds.length === 0) {
        return false;
      }
      // If areas are selected, only show locations in at least one of those areas
      const hasMatchingArea = locationAreaIds.some(areaId =>
        internalFilters.value.selectedAreaIds.includes(areaId)
      );
      if (!hasMatchingArea) {
        return false;
      }
    }

    return true;
  });
});

const filteredRoute = computed(() => {
  if (!props.showFilters) return props.route;
  return internalFilters.value.showRoute ? props.route : [];
});

const filteredAreas = computed(() => {
  if (!props.showFilters) return props.areas;

  // If area overlays are hidden, return empty array
  if (internalFilters.value.showAreaOverlays === false) {
    return [];
  }

  // Filter areas by selected IDs - if none selected, show none
  if (internalFilters.value.selectedAreaIds && internalFilters.value.selectedAreaIds.length > 0) {
    return props.areas.filter(area => internalFilters.value.selectedAreaIds.includes(area.id));
  }

  // If no areas selected and we have areas to filter, show none
  if (props.areas.length > 0) {
    return [];
  }

  return props.areas;
});

// Computed: can complete fullscreen action
const canCompleteFullscreenAction = computed(() => {
  if (props.mode === 'select-point') {
    // For point selection, we typically allow completion after a point is selected
    // The parent should track this
    return true;
  }
  if (props.mode === 'draw-polygon') {
    return polygonPointCount.value >= 3;
  }
  return true;
});

// Get the active map ref (fullscreen or regular)
const getActiveMapRef = () => {
  return isFullscreen.value ? fullscreenMapViewRef.value : mapViewRef.value;
};

// Handle fullscreen toggle
const handleFullscreenClick = () => {
  isFullscreen.value = true;
  emit('fullscreen-change', true);
};

const handleFullscreenClose = () => {
  isFullscreen.value = false;
  emit('fullscreen-change', false);
};

const handleFullscreenCancel = () => {
  // Clear drawing if in draw mode
  if (props.mode === 'draw-polygon') {
    const mapRef = getActiveMapRef();
    if (mapRef) {
      mapRef.clearPolygonDrawing();
    }
    polygonPointCount.value = 0;
    canUndoPoints.value = false;
    canRedoPoints.value = false;
  }
  isFullscreen.value = false;
  emit('fullscreen-cancel');
  emit('fullscreen-change', false);
};

const handleFullscreenDone = () => {
  if (props.mode === 'draw-polygon' && polygonPointCount.value >= 3) {
    const mapRef = getActiveMapRef();
    if (mapRef) {
      mapRef.completePolygon();
    }
  }
  isFullscreen.value = false;
  emit('fullscreen-done');
  emit('fullscreen-change', false);
};

// Handle filter changes
const handleFilterChange = (newFilters) => {
  internalFilters.value = newFilters;
  emit('filter-change', newFilters);
};

// Handle map click
const handleMapClick = (coords) => {
  if (props.mode === 'select-point') {
    emit('point-selected', coords);
  }
  emit('map-click', coords);
};

// Handle polygon drawing
const handlePolygonDrawing = (points) => {
  polygonPointCount.value = points?.length || 0;
  updateUndoRedoState();
  emit('polygon-drawing', points);
};

const handlePolygonComplete = (points) => {
  emit('polygon-complete', points);
  // Reset drawing state
  polygonPointCount.value = 0;
  canUndoPoints.value = false;
  canRedoPoints.value = false;
};

// Handle polygon editing (vertex drag)
const handlePolygonUpdate = (points) => {
  emit('polygon-update', points);
};

// Update undo/redo button states
const updateUndoRedoState = () => {
  const mapRef = getActiveMapRef();
  if (mapRef) {
    canUndoPoints.value = mapRef.canUndo ? mapRef.canUndo() : false;
    canRedoPoints.value = mapRef.canRedo ? mapRef.canRedo() : false;
  }
};

// Handle undo/redo
const handleUndo = () => {
  const mapRef = getActiveMapRef();
  if (mapRef && mapRef.undoPolygonPoint) {
    mapRef.undoPolygonPoint();
    updateUndoRedoState();
  }
};

const handleRedo = () => {
  const mapRef = getActiveMapRef();
  if (mapRef && mapRef.redoPolygonPoint) {
    mapRef.redoPolygonPoint();
    updateUndoRedoState();
  }
};

// Close dropdowns when clicking on map
const closeDropdowns = () => {
  if (toolbarRef.value) {
    toolbarRef.value.closeDropdowns();
  }
  if (fullscreenToolbarRef.value) {
    fullscreenToolbarRef.value.closeDropdowns();
  }
};

// Expose methods to parent
defineExpose({
  // Map methods
  getMapCenter: () => getActiveMapRef()?.getMapCenter?.(),
  getMapZoom: () => getActiveMapRef()?.getMapZoom?.(),

  // Polygon methods
  undoPolygonPoint: handleUndo,
  redoPolygonPoint: handleRedo,
  canUndo: () => canUndoPoints.value,
  canRedo: () => canRedoPoints.value,
  getPolygonPointCount: () => polygonPointCount.value,
  completePolygon: () => getActiveMapRef()?.completePolygon?.(),
  clearPolygonDrawing: () => {
    getActiveMapRef()?.clearPolygonDrawing?.();
    polygonPointCount.value = 0;
    canUndoPoints.value = false;
    canRedoPoints.value = false;
  },

  // Recentering methods
  recenterOnUserLocation: () => getActiveMapRef()?.recenterOnUserLocation?.(),
  recenterOnLocation: (lat, lng, zoom) => getActiveMapRef()?.recenterOnLocation?.(lat, lng, zoom),

  // Visibility tracking
  isLocationInView: (lat, lng) => getActiveMapRef()?.isLocationInView?.(lat, lng) ?? true,
  getUserLocationInView: () => getActiveMapRef()?.userLocationInView?.value ?? true,
  getHighlightedLocationInView: () => getActiveMapRef()?.highlightedLocationInView?.value ?? true,

  // Fullscreen control
  openFullscreen: () => {
    isFullscreen.value = true;
    emit('fullscreen-change', true);
  },
  closeFullscreen: handleFullscreenClose,

  // UI control
  closeDropdowns,
});
</script>

<style scoped>
.common-map {
  position: relative;
  width: 100%;
  min-height: 200px;
}

.map-wrapper {
  position: relative;
  width: 100%;
  height: 100%;
}

.fullscreen-map-wrapper {
  position: relative;
  width: 100%;
  height: 100%;
}

/* Ensure toolbar is visible above map in fullscreen */
.fullscreen-map-wrapper :deep(.map-toolbar) {
  z-index: 1001;
}
</style>
