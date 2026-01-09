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

        <!-- Map action buttons column -->
        <div class="map-buttons-column">
          <!-- Fullscreen button -->
          <button class="map-btn" @click="$emit('fullscreen')" title="Fullscreen">
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
              <path d="M8 3H5a2 2 0 0 0-2 2v3m18 0V5a2 2 0 0 0-2-2h-3m0 18h3a2 2 0 0 0 2-2v-3M3 16v3a2 2 0 0 0 2 2h3"/>
            </svg>
          </button>

          <!-- Show/Filters button -->
          <div class="map-btn-wrapper" v-if="!addMenuExpanded">
            <button class="map-btn" :class="{ active: !filtersCollapsed }" @click="toggleFilters" title="Show/hide options">
              <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                <path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"/>
                <circle cx="12" cy="12" r="3"/>
              </svg>
            </button>

            <!-- Filters dropdown -->
            <div v-if="!filtersCollapsed" class="dropdown-content filters-dropdown">
              <div class="filter-section">
                <h4>Show on map:</h4>
                <label class="filter-checkbox">
                  <input type="checkbox" v-model="filters.showRoute" />
                  {{ terms.course }}
                </label>
                <label class="filter-checkbox">
                  <input type="checkbox" v-model="filters.showUncheckedIn" />
                  Unchecked-in {{ termsLower.checkpoints }}
                </label>
                <label class="filter-checkbox">
                  <input type="checkbox" v-model="filters.showPartiallyCheckedIn" />
                  Partially checked-in {{ termsLower.checkpoints }}
                </label>
                <label class="filter-checkbox">
                  <input type="checkbox" v-model="filters.showFullyCheckedIn" />
                  Fully checked-in {{ termsLower.checkpoints }}
                </label>
                <label class="filter-checkbox">
                  <input type="checkbox" v-model="filters.showAreas" />
                  {{ terms.areas }}
                </label>
              </div>

              <div class="filter-section" v-if="areas.length > 0 && filters.showAreas">
                <h4>Filter {{ termsLower.areas }}:</h4>
                <AreasSelection
                  :areas="areas"
                  :selected-area-ids="filters.selectedAreas"
                  @update:selected-area-ids="filters.selectedAreas = $event"
                />
              </div>
            </div>
          </div>

          <!-- Add button -->
          <div class="map-btn-wrapper" v-if="filtersCollapsed">
            <button class="map-btn" :class="{ active: addMenuExpanded }" @click="toggleAddMenu" ref="addButtonRef" title="Add">
              <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                <line x1="12" y1="5" x2="12" y2="19"/>
                <line x1="5" y1="12" x2="19" y2="12"/>
              </svg>
            </button>

            <!-- Add dropdown -->
            <div v-if="addMenuExpanded" class="dropdown-content add-dropdown">
              <button @click="handleAddOption('checkpoint')" class="dropdown-item">
                {{ terms.checkpoint }}
              </button>
              <button @click="handleAddOption('many-checkpoints')" class="dropdown-item">
                Many {{ termsLower.checkpoints }}
              </button>
              <button @click="handleAddOption('area')" class="dropdown-item">
                {{ terms.area }}
              </button>
              <button @click="handleImportCheckpoints" class="dropdown-item">
                Import {{ termsLower.checkpoints }}...
              </button>
              <button @click="handleUploadRoute" class="dropdown-item">
                Upload {{ termsLower.course }}...
              </button>
            </div>
          </div>
        </div>

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
    </div>
  </div>
</template>

<script setup>
import { ref, computed, watch, defineProps, defineEmits } from 'vue';
import MapView from '../../components/MapView.vue';
import AreasSelection from '../../components/event-manage/AreasSelection.vue';
import { alphanumericCompare } from '../../utils/sortUtils';
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
  'fullscreen',
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
  showUncheckedIn: true,
  showPartiallyCheckedIn: true,
  showFullyCheckedIn: true,
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

// Helper to get checkpoint check-in status
const getCheckpointStatus = (checkpoint) => {
  const checkedIn = checkpoint.checkedInCount || 0;
  const required = checkpoint.requiredMarshals || 1;
  if (checkedIn === 0) return 'unchecked';
  if (checkedIn >= required) return 'full';
  return 'partial';
};

const visibleCheckpoints = computed(() => {
  // If all checkpoint filters are off, show nothing
  if (!filters.value.showUncheckedIn && !filters.value.showPartiallyCheckedIn && !filters.value.showFullyCheckedIn) {
    return [];
  }

  // Filter checkpoints based on selected areas and check-in status
  return props.checkpoints.filter(checkpoint => {
    // First, filter by check-in status
    const status = getCheckpointStatus(checkpoint);
    if (status === 'unchecked' && !filters.value.showUncheckedIn) return false;
    if (status === 'partial' && !filters.value.showPartiallyCheckedIn) return false;
    if (status === 'full' && !filters.value.showFullyCheckedIn) return false;

    // Then, filter by selected areas
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
  box-shadow: var(--shadow-sm);
}

.map-wrapper {
  width: 100%;
  height: 100%;
  border-radius: 8px;
  overflow: hidden;
  position: relative;
  z-index: 1;
}

/* Map buttons column */
.map-buttons-column {
  position: absolute;
  top: 10px;
  right: 10px;
  z-index: 1000;
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.map-btn-wrapper {
  position: relative;
}

.map-btn {
  background: var(--bg-primary);
  border: 1px solid var(--border-color);
  border-radius: 6px;
  padding: 8px;
  cursor: pointer;
  color: var(--text-primary);
  box-shadow: var(--shadow-md);
  display: flex;
  align-items: center;
  justify-content: center;
  transition: background-color 0.2s, transform 0.1s;
  width: 36px;
  height: 36px;
}

.map-btn:hover {
  background: var(--bg-hover);
  transform: scale(1.05);
}

.map-btn:active {
  transform: scale(0.95);
}

.map-btn.active {
  background: var(--accent-primary);
  color: white;
  border-color: var(--accent-primary);
}

.map-btn svg {
  display: block;
}

/* Dropdown content shared styles */
.dropdown-content {
  position: absolute;
  top: 0;
  right: calc(100% + 8px);
  background: var(--card-bg);
  border-radius: 8px;
  box-shadow: var(--shadow-lg);
  z-index: 1001;
  min-width: 200px;
}

/* Filters dropdown */
.filters-dropdown {
  padding: 1rem;
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
  color: var(--text-dark);
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
  color: var(--text-dark);
}

.filter-checkbox input[type="checkbox"] {
  cursor: pointer;
  width: 1rem;
  height: 1rem;
}

/* Add dropdown */
.add-dropdown {
  padding: 0.5rem 0;
}

.dropdown-item {
  display: block;
  width: 100%;
  padding: 0.75rem 1rem;
  background: var(--card-bg);
  border: none;
  text-align: left;
  cursor: pointer;
  font-size: 0.9rem;
  color: var(--text-dark);
  transition: background-color 0.2s;
}

.dropdown-item:hover {
  background: var(--bg-tertiary);
}

.dropdown-item:first-child {
  border-radius: 8px 8px 0 0;
}

.dropdown-item:last-child {
  border-radius: 0 0 8px 8px;
}

@media (max-width: 768px) {
  .map-section {
    height: 350px;
  }

  .dropdown-content {
    right: auto;
    left: auto;
    top: calc(100% + 8px);
    right: 0;
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
  box-shadow: var(--shadow-sm);
  min-width: 90px;
  background: var(--card-bg);
  color: var(--text-dark);
}

.btn-drawing:hover:not(:disabled) {
  background: var(--bg-hover);
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
