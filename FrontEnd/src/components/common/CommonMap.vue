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
        :layers="filteredLayers"
        :areas="filteredAreas"
        :center="center"
        :zoom="zoom"
        :clickable="clickable || mode === 'select-point'"
        :drawing-mode="mode === 'draw-polygon'"
        :editing-polygon="mode === 'edit-polygon' ? editingPolygon : null"
        :drawing-route-mode="mode === 'draw-route'"
        :editing-route="mode === 'edit-route' ? editingRoute : null"
        :user-location="userLocation"
        :highlight-location-id="highlightLocationId"
        :highlight-location-ids="highlightLocationIds"
        :all-locations-for-bounds="allLocationsForBounds"
        :marshal-mode="marshalMode"
        :simplify-non-highlighted="simplifyNonHighlighted"
        :selected-area-id="selectedAreaId"
        :hide-recenter-button="hideRecenterButton"
        :skip-auto-centering="skipAutoCentering"
        :show-staffing-overlay="internalFilters.showStaffingOverlay !== false"
        :show-status-overlay="internalFilters.showStatusOverlay !== false"
        @location-click="$emit('location-click', $event)"
        @map-click="handleMapClick"
        @area-click="$emit('area-click', $event)"
        @polygon-complete="handlePolygonComplete"
        @polygon-drawing="handlePolygonDrawing"
        @polygon-update="handlePolygonUpdate"
        @route-complete="handleRouteComplete"
        @route-drawing="handleRouteDrawing"
        @route-update="handleRouteUpdate"
        @visibility-change="$emit('visibility-change', $event)"
      />

      <!-- Toolbar -->
      <MapToolbar
        ref="toolbarRef"
        :show-fullscreen="showFullscreen"
        :show-filters="showFilters"
        :filters="internalFilters"
        :areas="areas"
        :layers="layers"
        :actions="toolbarActions"
        :position="toolbarPosition"
        @fullscreen-click="handleFullscreenClick"
        @filter-change="handleFilterChange"
        @action-click="$emit('action-click', $event)"
      />

      <!-- Drawing/editing controls (shown when in draw-polygon, draw-route, or edit-route mode) -->
      <MapDrawingControls
        :show="mode === 'draw-polygon' || mode === 'draw-route' || mode === 'edit-route'"
        :point-count="mode === 'edit-route' ? editingRoutePointCount : (mode === 'draw-route' ? routePointCount : polygonPointCount)"
        :can-undo="mode === 'edit-route' ? canUndoRouteEditPoints : (mode === 'draw-route' ? canUndoRoutePoints : canUndoPoints)"
        :can-redo="mode === 'edit-route' ? canRedoRouteEditPoints : (mode === 'draw-route' ? canRedoRoutePoints : canRedoPoints)"
        :hide-point-count="mode === 'edit-route'"
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
          :route-weight="routeWeight"
          :layers="filteredLayers"
          :areas="filteredAreas"
          :center="center"
          :zoom="zoom"
          :clickable="clickable || mode === 'select-point'"
          :drawing-mode="mode === 'draw-polygon'"
          :editing-polygon="mode === 'edit-polygon' ? editingPolygon : null"
          :drawing-route-mode="mode === 'draw-route'"
          :editing-route="mode === 'edit-route' ? editingRoute : null"
          :user-location="userLocation"
          :highlight-location-id="highlightLocationId"
          :highlight-location-ids="highlightLocationIds"
          :all-locations-for-bounds="allLocationsForBounds"
          :marshal-mode="marshalMode"
          :simplify-non-highlighted="simplifyNonHighlighted"
          :selected-area-id="selectedAreaId"
          :hide-recenter-button="hideRecenterButton"
          :skip-auto-centering="skipAutoCentering"
          :show-staffing-overlay="internalFilters.showStaffingOverlay !== false"
          :show-status-overlay="internalFilters.showStatusOverlay !== false"
          @location-click="$emit('location-click', $event)"
          @map-click="handleMapClick"
          @area-click="$emit('area-click', $event)"
          @polygon-complete="handlePolygonComplete"
          @polygon-drawing="handlePolygonDrawing"
          @polygon-update="handlePolygonUpdate"
          @route-complete="handleRouteComplete"
          @route-drawing="handleRouteDrawing"
          @route-update="handleRouteUpdate"
          @visibility-change="$emit('visibility-change', $event)"
        />

        <!-- Toolbar in fullscreen (no fullscreen button since already fullscreen) -->
        <MapToolbar
          ref="fullscreenToolbarRef"
          :show-fullscreen="false"
          :show-filters="showFilters"
          :filters="internalFilters"
          :areas="areas"
          :layers="layers"
          :actions="toolbarActions"
          :position="toolbarPosition"
          @filter-change="handleFilterChange"
          @action-click="$emit('action-click', $event)"
        />

        <!-- Drawing/editing controls in fullscreen -->
        <MapDrawingControls
          :show="mode === 'draw-polygon' || mode === 'draw-route' || mode === 'edit-route'"
          :point-count="mode === 'edit-route' ? editingRoutePointCount : (mode === 'draw-route' ? routePointCount : polygonPointCount)"
          :can-undo="mode === 'edit-route' ? canUndoRouteEditPoints : (mode === 'draw-route' ? canUndoRoutePoints : canUndoPoints)"
          :can-redo="mode === 'edit-route' ? canRedoRouteEditPoints : (mode === 'draw-route' ? canRedoRoutePoints : canRedoPoints)"
          :hide-point-count="mode === 'edit-route'"
          @undo="handleUndo"
          @redo="handleRedo"
        />
      </div>
    </MapFullscreenModal>
  </div>
</template>

<script setup>
import { ref, computed, watch } from 'vue';
// defineProps, defineEmits, defineExpose are compiler macros - no import needed
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
  layers: {
    type: Array,
    default: () => [],
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
    validator: (v) => ['view', 'select-point', 'draw-polygon', 'edit-polygon', 'draw-route', 'edit-route'].includes(v),
  },
  editingPolygon: {
    type: Array,
    default: null,
  },
  editingRoute: {
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
      selectedLayerIds: [],
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
  skipAutoCentering: {
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
  'route-complete',
  'route-drawing',
  'route-update',

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
const routePointCount = ref(0);
const canUndoRoutePoints = ref(false);
const canRedoRoutePoints = ref(false);
// Route editing state
const editingRoutePointCount = ref(0);
const canUndoRouteEditPoints = ref(false);
const canRedoRouteEditPoints = ref(false);

// Internal filters (synced with props.filters)
const internalFilters = ref({ ...props.filters });

// Watch for external filter changes
watch(() => props.filters, (newFilters) => {
  // Merge filters, preserving selectedLayerIds if already populated and new value is empty
  const merged = { ...newFilters };
  if (internalFilters.value.selectedLayerIds?.length > 0 &&
      (!newFilters.selectedLayerIds || newFilters.selectedLayerIds.length === 0)) {
    merged.selectedLayerIds = internalFilters.value.selectedLayerIds;
  }
  internalFilters.value = merged;
}, { deep: true });

// Initialize selectedAreaIds with all areas when areas become available
watch(() => props.areas, (newAreas) => {
  if (props.showFilters && newAreas.length > 0 &&
      (!internalFilters.value.selectedAreaIds || internalFilters.value.selectedAreaIds.length === 0)) {
    // Only auto-select all if we haven't explicitly set any selection yet
    internalFilters.value.selectedAreaIds = newAreas.map(a => a.id);
  }
}, { immediate: true });

// Initialize selectedLayerIds with all layers when layers become available
// Only do this if parent hasn't provided selectedLayerIds in filters prop
watch(() => props.layers, (newLayers) => {
  if (props.showFilters && newLayers.length > 0 &&
      (!internalFilters.value.selectedLayerIds || internalFilters.value.selectedLayerIds.length === 0)) {
    // Only auto-select all if we haven't explicitly set any selection yet
    const newIds = newLayers.map(l => l.id);
    internalFilters.value.selectedLayerIds = newIds;
    // Emit back to parent so state stays in sync
    emit('filter-change', { ...internalFilters.value });
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

    // Filter by selected layers (only when layers exist to filter)
    if (props.layers.length > 0) {
      const selectedLayerIds = internalFilters.value.selectedLayerIds || [];

      // If no layers are selected, hide all checkpoints
      if (selectedLayerIds.length === 0) {
        return false;
      }

      // Get the checkpoint's layer IDs (null means all layers)
      const locationLayerIds = location.layerIds || location.LayerIds;

      // If locationLayerIds is null/undefined, checkpoint belongs to all layers
      // Show it if at least one layer is selected (which we already checked above)
      if (locationLayerIds == null) {
        return true;
      }

      // If locationLayerIds is an array, check if any match selected layers
      if (Array.isArray(locationLayerIds) && locationLayerIds.length > 0) {
        const hasMatchingLayer = locationLayerIds.some(layerId =>
          selectedLayerIds.includes(layerId)
        );
        if (!hasMatchingLayer) {
          return false;
        }
      }
    }

    return true;
  });
});

const filteredRoute = computed(() => {
  if (!props.showFilters) return props.route;

  // If we have layers, the legacy route should not be shown (it's migrated to layers)
  if (props.layers.length > 0) {
    return [];
  }

  // Legacy: no layers, use showRoute toggle
  return internalFilters.value.showRoute ? props.route : [];
});

const filteredLayers = computed(() => {
  if (!props.showFilters) return props.layers;

  // If we have layers, filter by selectedLayerIds
  if (props.layers.length > 0) {
    const selectedIds = internalFilters.value.selectedLayerIds || [];
    return props.layers.filter(layer => selectedIds.includes(layer.id));
  }

  // Legacy: no layers, use showRoute toggle
  return internalFilters.value.showRoute ? props.layers : [];
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
  if (props.mode === 'draw-route') {
    return routePointCount.value >= 2;
  }
  if (props.mode === 'edit-route') {
    return true; // Always can complete when editing
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
  if (props.mode === 'draw-route') {
    const mapRef = getActiveMapRef();
    if (mapRef) {
      mapRef.clearRouteDrawing();
    }
    routePointCount.value = 0;
    canUndoRoutePoints.value = false;
    canRedoRoutePoints.value = false;
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
  if (props.mode === 'draw-route' && routePointCount.value >= 2) {
    const mapRef = getActiveMapRef();
    if (mapRef) {
      mapRef.completeRoute();
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

// Handle route drawing
const handleRouteDrawing = (points) => {
  routePointCount.value = points?.length || 0;
  updateRouteUndoRedoState();
  emit('route-drawing', points);
};

const handleRouteComplete = (points) => {
  emit('route-complete', points);
  // Reset drawing state
  routePointCount.value = 0;
  canUndoRoutePoints.value = false;
  canRedoRoutePoints.value = false;
};

// Handle route editing (vertex drag, insert, delete)
const handleRouteUpdate = (points) => {
  emit('route-update', points);
  // Update undo/redo state after route edit
  updateEditRouteUndoRedoState();
};

// Update undo/redo button states
const updateUndoRedoState = () => {
  const mapRef = getActiveMapRef();
  if (mapRef) {
    canUndoPoints.value = mapRef.canUndo ? mapRef.canUndo() : false;
    canRedoPoints.value = mapRef.canRedo ? mapRef.canRedo() : false;
  }
};

// Update route undo/redo button states (for draw mode)
const updateRouteUndoRedoState = () => {
  const mapRef = getActiveMapRef();
  if (mapRef) {
    canUndoRoutePoints.value = mapRef.canUndoRoute ? mapRef.canUndoRoute() : false;
    canRedoRoutePoints.value = mapRef.canRedoRoute ? mapRef.canRedoRoute() : false;
  }
};

// Update route editing undo/redo button states
const updateEditRouteUndoRedoState = () => {
  const mapRef = getActiveMapRef();
  if (mapRef) {
    canUndoRouteEditPoints.value = mapRef.canUndoRouteEdit ? mapRef.canUndoRouteEdit() : false;
    canRedoRouteEditPoints.value = mapRef.canRedoRouteEdit ? mapRef.canRedoRouteEdit() : false;
    editingRoutePointCount.value = mapRef.getEditingRoutePointCount ? mapRef.getEditingRoutePointCount() : 0;
  }
};

// Handle undo/redo for polygons and routes
const handleUndo = () => {
  const mapRef = getActiveMapRef();
  if (props.mode === 'edit-route') {
    if (mapRef && mapRef.undoRouteEdit) {
      mapRef.undoRouteEdit();
      updateEditRouteUndoRedoState();
    }
  } else if (props.mode === 'draw-route') {
    if (mapRef && mapRef.undoRoutePoint) {
      mapRef.undoRoutePoint();
      updateRouteUndoRedoState();
    }
  } else {
    if (mapRef && mapRef.undoPolygonPoint) {
      mapRef.undoPolygonPoint();
      updateUndoRedoState();
    }
  }
};

const handleRedo = () => {
  const mapRef = getActiveMapRef();
  if (props.mode === 'edit-route') {
    if (mapRef && mapRef.redoRouteEdit) {
      mapRef.redoRouteEdit();
      updateEditRouteUndoRedoState();
    }
  } else if (props.mode === 'draw-route') {
    if (mapRef && mapRef.redoRoutePoint) {
      mapRef.redoRoutePoint();
      updateRouteUndoRedoState();
    }
  } else {
    if (mapRef && mapRef.redoPolygonPoint) {
      mapRef.redoPolygonPoint();
      updateUndoRedoState();
    }
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

  // Route drawing methods
  undoRoutePoint: handleUndo,
  redoRoutePoint: handleRedo,
  canUndoRoute: () => canUndoRoutePoints.value,
  canRedoRoute: () => canRedoRoutePoints.value,
  getRoutePointCount: () => routePointCount.value,
  completeRoute: () => getActiveMapRef()?.completeRoute?.(),
  clearRouteDrawing: () => {
    getActiveMapRef()?.clearRouteDrawing?.();
    routePointCount.value = 0;
    canUndoRoutePoints.value = false;
    canRedoRoutePoints.value = false;
  },
  // Route editing methods
  undoRouteEdit: () => {
    handleUndo();
  },
  redoRouteEdit: () => {
    handleRedo();
  },
  canUndoRouteEdit: () => canUndoRouteEditPoints.value,
  canRedoRouteEdit: () => canRedoRouteEditPoints.value,
  fitToEditingRouteBounds: (padding) => getActiveMapRef()?.fitToEditingRouteBounds?.(padding),

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
