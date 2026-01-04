<template>
  <div class="course-areas-tab">
    <div class="map-section" ref="mapSection">
      <div class="map-wrapper">
        <MapView
          ref="mapView"
          :locations="visibleCheckpoints"
          :route="visibleRoute"
          :areas="visibleAreas"
          :all-locations-for-bounds="checkpoints"
          :selected-area-id="selectedAreaId"
          :clickable="true"
          :drawing-mode="drawingMode"
          @map-click="$emit('map-click', $event)"
          @location-click="$emit('location-click', $event)"
          @area-click="$emit('area-click', $event)"
          @polygon-complete="$emit('polygon-complete', $event)"
          @polygon-drawing="handlePolygonDrawing"
        />

        <!-- Drawing controls overlay - shown when in drawing mode -->
        <div v-if="drawingMode && polygonPointCount > 0" class="drawing-controls">
          <div class="point-count">{{ polygonPointCount }} point{{ polygonPointCount !== 1 ? 's' : '' }}</div>
          <div class="undo-redo-buttons">
            <button
              @click="handleUndo"
              class="btn-drawing undo-btn"
              :disabled="!canUndoPoints"
              title="Undo last point"
            >
              ↩ Undo
            </button>
            <button
              @click="handleRedo"
              class="btn-drawing redo-btn"
              :disabled="!canRedoPoints"
              title="Redo point"
            >
              Redo ↪
            </button>
          </div>
        </div>
      </div>

      <!-- Visibility Filters -->
      <div class="visibility-filters" :class="{ collapsed: filtersCollapsed }" v-if="!addMenuExpanded">
        <button
          @click="toggleFilters"
          class="filters-toggle"
        >
          <span v-if="filtersCollapsed">Show...</span>
          <span v-else>✕</span>
        </button>

        <div v-if="!filtersCollapsed" class="filters-content">
          <div class="filter-section">
            <h4>Show on map:</h4>
            <label class="filter-checkbox">
              <input type="checkbox" v-model="filters.showRoute" />
              Route
            </label>
            <label class="filter-checkbox">
              <input type="checkbox" v-model="filters.showCheckpoints" />
              Checkpoints
            </label>
            <label class="filter-checkbox">
              <input type="checkbox" v-model="filters.showAreas" />
              Areas
            </label>
          </div>

          <div class="filter-section" v-if="areas.length > 0 && filters.showAreas">
            <h4>Filter areas:</h4>
            <AreasSelection
              :areas="areas"
              :selected-area-ids="filters.selectedAreas"
              @update:selected-area-ids="filters.selectedAreas = $event"
            />
          </div>
        </div>
      </div>

      <!-- Add Button -->
      <div v-if="filtersCollapsed" class="add-menu" :class="{ expanded: addMenuExpanded }">
        <button
          @click="toggleAddMenu"
          class="add-toggle"
          ref="addButtonRef"
        >
          <span v-if="addMenuExpanded">✕</span>
          <span v-else>Add...</span>
        </button>

        <div v-if="addMenuExpanded" class="add-menu-content">
          <button @click="handleAddOption('checkpoint')" class="add-menu-item">
            Checkpoint
          </button>
          <button @click="handleAddOption('many-checkpoints')" class="add-menu-item">
            Many checkpoints
          </button>
          <button @click="handleAddOption('area')" class="add-menu-item">
            Area
          </button>
          <button @click="handleImportCheckpoints" class="add-menu-item">
            Import checkpoints...
          </button>
          <button @click="handleUploadRoute" class="add-menu-item">
            Upload route...
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, watch, defineProps, defineEmits } from 'vue';
import MapView from '../../components/MapView.vue';
import AreasSelection from '../../components/event-manage/AreasSelection.vue';
import { alphanumericCompare } from '../../utils/sortUtils';

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
const mapView = ref(null);
const filtersCollapsed = ref(true);
const addMenuExpanded = ref(false);
const addButtonRef = ref(null);

// Polygon drawing state
const polygonPointCount = ref(0);
const canUndoPoints = ref(false);
const canRedoPoints = ref(false);

const filters = ref({
  showRoute: true,
  showCheckpoints: true,
  showAreas: true,
  selectedAreas: [],
});

const toggleFilters = () => {
  filtersCollapsed.value = !filtersCollapsed.value;
  // Close add menu when opening filters
  if (!filtersCollapsed.value) {
    addMenuExpanded.value = false;
  }
};

const toggleAddMenu = () => {
  addMenuExpanded.value = !addMenuExpanded.value;
  // Close filters when opening add menu
  if (addMenuExpanded.value) {
    filtersCollapsed.value = true;
  }
};

const handleAddOption = (option) => {
  addMenuExpanded.value = false;

  if (option === 'checkpoint') {
    emit('add-checkpoint-from-map');
    // Scroll map into view
    scrollMapIntoView();
  } else if (option === 'many-checkpoints') {
    emit('add-many-checkpoints-from-map');
    scrollMapIntoView();
  } else if (option === 'area') {
    emit('add-area-from-map');
    scrollMapIntoView();
  }
};

const handleImportCheckpoints = () => {
  addMenuExpanded.value = false;
  emit('import-checkpoints');
};

const handleUploadRoute = () => {
  addMenuExpanded.value = false;
  emit('upload-route');
};

// Handle polygon drawing updates from MapView
const handlePolygonDrawing = (points) => {
  polygonPointCount.value = points?.length || 0;
  updateUndoRedoState();
};

// Update undo/redo button states
const updateUndoRedoState = () => {
  if (mapView.value) {
    canUndoPoints.value = mapView.value.canUndo();
    canRedoPoints.value = mapView.value.canRedo();
  }
};

// Undo last polygon point
const handleUndo = () => {
  if (mapView.value) {
    mapView.value.undoPolygonPoint();
    updateUndoRedoState();
  }
};

// Redo polygon point
const handleRedo = () => {
  if (mapView.value) {
    mapView.value.redoPolygonPoint();
    updateUndoRedoState();
  }
};

// Expose method to scroll map into view
const scrollMapIntoView = () => {
  if (mapSection.value) {
    mapSection.value.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }
};

// Expose methods to get current map state
const getMapCenter = () => {
  return mapView.value?.getMapCenter() || null;
};

const getMapZoom = () => {
  return mapView.value?.getMapZoom() || null;
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
    const currentSelectedIds = new Set(filters.value.selectedAreas);
    newAreas.forEach(area => {
      if (!currentSelectedIds.has(area.id)) {
        filters.value.selectedAreas.push(area.id);
      }
    });

    // Remove any areas that no longer exist
    filters.value.selectedAreas = filters.value.selectedAreas.filter(id =>
      newAreas.some(area => area.id === id)
    );
  }
}, { immediate: true });

// Reset polygon state when drawing mode changes
watch(() => props.drawingMode, (newDrawingMode) => {
  if (!newDrawingMode) {
    polygonPointCount.value = 0;
    canUndoPoints.value = false;
    canRedoPoints.value = false;
  }
});

const sortedAreas = computed(() => {
  return [...props.areas].sort((a, b) => {
    if (a.displayOrder !== b.displayOrder) {
      return a.displayOrder - b.displayOrder;
    }
    return alphanumericCompare(a.name, b.name);
  });
});

const visibleRoute = computed(() => {
  return filters.value.showRoute ? props.route : [];
});

const visibleCheckpoints = computed(() => {
  if (!filters.value.showCheckpoints) {
    return [];
  }

  // Filter checkpoints based on selected areas
  return props.checkpoints.filter(checkpoint => {
    // Handle both areaIds and AreaIds (case variations)
    const checkpointAreas = checkpoint.areaIds || checkpoint.AreaIds;

    // If checkpoint has areaIds array, check if any of them are selected
    if (checkpointAreas && Array.isArray(checkpointAreas)) {
      return checkpointAreas.some(areaId => filters.value.selectedAreas.includes(areaId));
    }
    // Fallback for backward compatibility (single areaId)
    if (checkpoint.areaId) {
      return filters.value.selectedAreas.includes(checkpoint.areaId);
    }
    // If no area assignment, don't show
    return false;
  });
});

const visibleAreas = computed(() => {
  if (!filters.value.showAreas) {
    return [];
  }
  return props.areas.filter(a => filters.value.selectedAreas.includes(a.id));
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
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.1);
}

.map-wrapper {
  width: 100%;
  height: 100%;
  border-radius: 8px;
  overflow: hidden;
  position: relative;
  z-index: 1;
}

/* Visibility Filters */
.visibility-filters {
  position: absolute;
  top: 1rem;
  right: 1rem;
  background: white;
  border-radius: 8px;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.2);
  z-index: 500;
  transition: all 0.3s ease;
}

.visibility-filters.collapsed {
  padding: 0;
}

.filters-toggle {
  padding: 0.5rem 1rem;
  background: #007bff;
  color: white;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  font-size: 0.9rem;
  font-weight: 500;
  min-width: 80px;
  height: auto;
  transition: all 0.2s;
  text-align: center;
  display: flex;
  align-items: center;
  justify-content: center;
  position: relative;
  box-shadow: 0 2px 4px rgba(0, 0, 0, 0.2);
  white-space: nowrap;
}

.filters-toggle:hover {
  background: #0056b3;
}

.filters-content {
  padding: 1rem;
  max-width: 250px;
  position: relative;
  z-index: 501;
}

.filter-section {
  margin-bottom: 1rem;
}

.filter-section:last-child {
  margin-bottom: 0;
}

.filter-section h4 {
  margin: 0 0 0.5rem 0;
  font-size: 0.85rem;
  font-weight: 600;
  color: #333;
  text-transform: uppercase;
  letter-spacing: 0.5px;
}

.filter-checkbox {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  margin-bottom: 0.5rem;
  font-size: 0.9rem;
  cursor: pointer;
  color: #333;
}

.filter-checkbox input[type="checkbox"] {
  cursor: pointer;
  width: 1rem;
  height: 1rem;
}

/* Add Menu */
.add-menu {
  position: absolute;
  top: 5rem;
  right: 1rem;
  background: white;
  border-radius: 8px;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.2);
  z-index: 500;
  transition: all 0.3s ease;
}

.add-menu.expanded {
  padding: 0;
}

.add-toggle {
  padding: 0.5rem 1rem;
  background: #007bff;
  color: white;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  font-size: 0.9rem;
  font-weight: 500;
  min-width: 80px;
  height: auto;
  transition: all 0.2s;
  text-align: center;
  display: flex;
  align-items: center;
  justify-content: center;
  position: relative;
  box-shadow: 0 2px 4px rgba(0, 0, 0, 0.2);
  white-space: nowrap;
}

.add-toggle:hover {
  background: #0056b3;
}

.add-menu-content {
  padding: 0.5rem 0;
  min-width: 180px;
  position: relative;
  z-index: 501;
}

.add-menu-item {
  display: block;
  width: 100%;
  padding: 0.75rem 1rem;
  background: white;
  border: none;
  text-align: left;
  cursor: pointer;
  font-size: 0.9rem;
  color: #333;
  transition: background-color 0.2s;
}

.add-menu-item:hover {
  background: #f5f5f5;
}

.add-menu-item:first-child {
  border-radius: 0 0 0 0;
}

.add-menu-item:last-child {
  border-radius: 0 0 8px 8px;
}

@media (max-width: 768px) {
  .map-section {
    height: 350px;
  }

  .visibility-filters {
    top: 1rem;
    right: 1rem;
    max-height: calc(100vh - 10rem);
  }

  .filters-content {
    max-height: calc(100vh - 14rem);
    overflow-y: auto;
  }

  .add-menu {
    top: 5rem;
    right: 1rem;
  }

  .add-menu-content {
    max-height: calc(100vh - 14rem);
    overflow-y: auto;
  }
}

/* Drawing controls overlay */
.drawing-controls {
  position: absolute;
  bottom: 2rem;
  left: 50%;
  transform: translateX(-50%);
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 0.5rem;
  z-index: 1000;
}

.point-count {
  background: rgba(0, 0, 0, 0.7);
  color: white;
  padding: 0.5rem 1rem;
  border-radius: 20px;
  font-size: 0.9rem;
  font-weight: 500;
}

.undo-redo-buttons {
  display: flex;
  gap: 0.5rem;
}

.btn-drawing {
  padding: 0.75rem 1.25rem;
  border: none;
  border-radius: 8px;
  font-size: 1rem;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.2s ease;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.2);
  min-width: 90px;
  background: white;
  color: #333;
}

.btn-drawing:hover:not(:disabled) {
  background: #f0f0f0;
}

.btn-drawing:disabled {
  opacity: 0.4;
  cursor: not-allowed;
}

@media (max-width: 768px) {
  .drawing-controls {
    bottom: 1rem;
  }

  .btn-drawing {
    padding: 0.6rem 1rem;
    font-size: 0.9rem;
    min-width: 80px;
  }
}
</style>
