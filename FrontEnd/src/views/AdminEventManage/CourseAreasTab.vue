<template>
  <div class="course-areas-tab">
    <!-- Layers pills -->
    <div class="layers-pills">
      <button
        v-for="layer in sortedLayers"
        :key="layer.id"
        type="button"
        class="layer-pill"
        :style="{ backgroundColor: layer.routeColor || '#3388ff' }"
        @click="$emit('select-layer', layer)"
        :title="`Edit ${layer.name}`"
      >
        <span class="layer-pill-name">{{ layer.name }}</span>
        <svg class="layer-pill-edit-icon" width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round">
          <path d="M17 3a2.85 2.85 0 0 1 4 4L7.5 20.5 2 22l1.5-5.5Z" />
        </svg>
      </button>
      <button
        type="button"
        class="layer-pill layer-pill-add"
        @click="$emit('add-layer')"
        :title="`Add ${termsLower.course} or layer`"
      >
        <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5">
          <line x1="12" y1="5" x2="12" y2="19" />
          <line x1="5" y1="12" x2="19" y2="12" />
        </svg>
        <span>Add {{ termsLower.course }} or layer</span>
      </button>
    </div>

    <!-- Legacy route settings (for backward compatibility while migrating) -->
    <div v-if="route.length > 0 && layers.length === 0" class="route-settings-bar">
      <span class="route-settings-label">Route style:</span>
      <BrandingColorPicker
        :model-value="localRouteColor"
        :default-color="DEFAULT_ROUTE_COLOR"
        :show-contrast-preview="false"
        :show-default="true"
        @update:model-value="localRouteColor = $event"
      />
      <div class="style-selector" ref="styleSelectorRef">
        <button
          type="button"
          class="style-selector-button"
          @click="toggleStyleDropdown"
        >
          <svg class="style-preview" viewBox="0 0 60 10" preserveAspectRatio="none">
            <line
              x1="0" y1="5" x2="60" y2="5"
              :stroke="effectiveLocalColor"
              stroke-width="3"
              :stroke-dasharray="getStrokeDashArray(localRouteStyle || 'line')"
              :stroke-linecap="getStrokeLineCap(localRouteStyle || 'line')"
            />
          </svg>
          <span class="style-selector-arrow">▼</span>
        </button>
        <div v-if="showStyleDropdown" class="style-dropdown" :style="dropdownBackgroundStyle">
          <button
            v-for="style in lineStyles"
            :key="style.value"
            type="button"
            class="style-option"
            :class="{ selected: (localRouteStyle || 'line') === style.value }"
            @click="selectStyle(style.value)"
          >
            <svg class="style-preview-wide" viewBox="0 0 80 10" preserveAspectRatio="none">
              <line
                x1="0" y1="5" x2="80" y2="5"
                :stroke="effectiveLocalColor"
                stroke-width="3"
                :stroke-dasharray="getStrokeDashArray(style.value)"
                :stroke-linecap="getStrokeLineCap(style.value)"
              />
            </svg>
          </button>
        </div>
      </div>
      <div class="style-selector" ref="weightSelectorRef">
        <button
          type="button"
          class="style-selector-button"
          @click="toggleWeightDropdown"
        >
          <svg class="style-preview" viewBox="0 0 60 10" preserveAspectRatio="none">
            <line
              x1="0" y1="5" x2="60" y2="5"
              :stroke="effectiveLocalColor"
              :stroke-width="localRouteWeight || 4"
              :stroke-dasharray="getStrokeDashArray(localRouteStyle || 'line')"
              :stroke-linecap="getStrokeLineCap(localRouteStyle || 'line')"
            />
          </svg>
          <span class="style-selector-arrow">▼</span>
        </button>
        <div v-if="showWeightDropdown" class="style-dropdown" :style="dropdownBackgroundStyle">
          <button
            v-for="weight in lineWeights"
            :key="weight.value"
            type="button"
            class="style-option"
            :class="{ selected: (localRouteWeight || 4) === weight.value }"
            @click="selectWeight(weight.value)"
          >
            <svg class="style-preview-wide" viewBox="0 0 80 10" preserveAspectRatio="none">
              <line
                x1="0" y1="5" x2="80" y2="5"
                :stroke="effectiveLocalColor"
                :stroke-width="weight.value"
                :stroke-dasharray="getStrokeDashArray(localRouteStyle || 'line')"
                :stroke-linecap="getStrokeLineCap(localRouteStyle || 'line')"
              />
            </svg>
          </button>
        </div>
      </div>
      <button
        type="button"
        class="btn-save-style"
        :disabled="!hasUnsavedChanges"
        @click="saveChanges"
      >
        Save
      </button>
    </div>

    <div class="map-section" ref="mapSection">
      <CommonMap
        ref="commonMapRef"
        :locations="checkpoints"
        :route="displayRoute"
        :route-color="displayRouteColor"
        :route-style="displayRouteStyle"
        :route-weight="displayRouteWeight"
        :layers="displayLayers"
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
import { ref, computed, watch, onUnmounted, defineProps, defineEmits } from 'vue';
import CommonMap from '../../components/common/CommonMap.vue';
import BrandingColorPicker from '../../components/BrandingColorPicker.vue';
import { useTerminology } from '../../composables/useTerminology';

const { terms, termsLower } = useTerminology();

// Default route color (Leaflet's default blue)
const DEFAULT_ROUTE_COLOR = '#3388ff';

const props = defineProps({
  checkpoints: {
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
  'update:routeSettings',
  'add-layer',
  'select-layer',
]);

const mapSection = ref(null);
const commonMapRef = ref(null);
const styleSelectorRef = ref(null);
const weightSelectorRef = ref(null);

// Polygon drawing state
const polygonPointCount = ref(0);

// Dropdown states
const showStyleDropdown = ref(false);
const showWeightDropdown = ref(false);

// Local state for editing (before save)
const localRouteColor = ref(props.routeColor);
const localRouteStyle = ref(props.routeStyle);
const localRouteWeight = ref(props.routeWeight);

// Sync local state when props change (e.g., on initial load)
watch(() => props.routeColor, (newColor) => {
  localRouteColor.value = newColor;
}, { immediate: true });

watch(() => props.routeStyle, (newStyle) => {
  localRouteStyle.value = newStyle;
}, { immediate: true });

watch(() => props.routeWeight, (newWeight) => {
  localRouteWeight.value = newWeight;
}, { immediate: true });

// Check if there are unsaved changes
const hasUnsavedChanges = computed(() => {
  const colorChanged = localRouteColor.value !== props.routeColor;
  const styleChanged = localRouteStyle.value !== props.routeStyle;
  const weightChanged = localRouteWeight.value !== props.routeWeight;
  return colorChanged || styleChanged || weightChanged;
});

// Available line styles with various dash patterns
const lineStyles = [
  { value: 'line', label: 'Solid' },
  { value: 'dash', label: 'Dashed' },
  { value: 'dash-long', label: 'Long dash' },
  { value: 'dash-short', label: 'Short dash' },
  { value: 'dash-dense', label: 'Dense dash' },
  { value: 'dot', label: 'Dotted' },
  { value: 'dot-sparse', label: 'Sparse dots' },
  { value: 'dot-dense', label: 'Dense dots' },
  { value: 'dash-dot', label: 'Dash-dot' },
  { value: 'dash-dot-dot', label: 'Dash-dot-dot' },
  { value: 'long-short', label: 'Long-short' },
  { value: 'double-dash', label: 'Double dash' },
];

// Available line weights/thicknesses
const lineWeights = [
  { value: 2, label: 'Thin' },
  { value: 4, label: 'Normal' },
  { value: 6, label: 'Thick' },
  { value: 8, label: 'Extra thick' },
];

// Computed effective local color (use default if not set)
const effectiveLocalColor = computed(() => localRouteColor.value || DEFAULT_ROUTE_COLOR);

// When layers exist, use layers' routes; otherwise fall back to legacy route
const displayLayers = computed(() => {
  if (props.layers && props.layers.length > 0) {
    return props.layers;
  }
  return [];
});

// Layers sorted by display order for the pills
const sortedLayers = computed(() => {
  return [...props.layers].sort((a, b) => (a.displayOrder || 0) - (b.displayOrder || 0));
});

// Legacy route display (only when no layers exist)
const displayRoute = computed(() => {
  if (props.layers && props.layers.length > 0) {
    // When layers exist, don't show legacy route
    return [];
  }
  return props.route;
});

const displayRouteColor = computed(() => {
  if (props.layers && props.layers.length > 0) {
    return '';
  }
  return localRouteColor.value;
});

const displayRouteStyle = computed(() => {
  if (props.layers && props.layers.length > 0) {
    return '';
  }
  return localRouteStyle.value;
});

const displayRouteWeight = computed(() => {
  if (props.layers && props.layers.length > 0) {
    return null;
  }
  return localRouteWeight.value;
});

// Calculate relative luminance of a color
const getLuminance = (hexColor) => {
  const hex = hexColor.replace('#', '');
  const r = parseInt(hex.substr(0, 2), 16) / 255;
  const g = parseInt(hex.substr(2, 2), 16) / 255;
  const b = parseInt(hex.substr(4, 2), 16) / 255;

  const toLinear = (c) => c <= 0.03928 ? c / 12.92 : Math.pow((c + 0.055) / 1.055, 2.4);
  return 0.2126 * toLinear(r) + 0.7152 * toLinear(g) + 0.0722 * toLinear(b);
};

// Determine if dropdown needs a contrasting background
const dropdownBackgroundStyle = computed(() => {
  const color = effectiveLocalColor.value;
  const luminance = getLuminance(color);

  // If the color is light (high luminance), use a dark background
  // If the color is dark (low luminance), use a light background (default)
  if (luminance > 0.5) {
    return { backgroundColor: '#333', borderColor: '#555' };
  }
  return {};
});

// Get stroke dash array for a given style (for SVG preview)
const getStrokeDashArray = (style) => {
  switch (style) {
    case 'dash': return '8, 6';
    case 'dash-long': return '16, 8';
    case 'dash-short': return '4, 4';
    case 'dash-dense': return '6, 3';
    case 'dot': return '2, 4';
    case 'dot-sparse': return '2, 8';
    case 'dot-dense': return '2, 2';
    case 'dash-dot': return '12, 4, 2, 4';
    case 'dash-dot-dot': return '12, 4, 2, 4, 2, 4';
    case 'long-short': return '16, 4, 6, 4';
    case 'double-dash': return '8, 3, 8, 8';
    default: return 'none';
  }
};

// Get stroke line cap for a given style
const getStrokeLineCap = (style) => {
  if (style === 'dot' || style === 'dot-sparse' || style === 'dot-dense') return 'round';
  return 'butt';
};

// Toggle style dropdown
const toggleStyleDropdown = () => {
  showStyleDropdown.value = !showStyleDropdown.value;
  showWeightDropdown.value = false;
};

// Toggle weight dropdown
const toggleWeightDropdown = () => {
  showWeightDropdown.value = !showWeightDropdown.value;
  showStyleDropdown.value = false;
};

// Select a style and close dropdown
const selectStyle = (style) => {
  localRouteStyle.value = style;
  showStyleDropdown.value = false;
};

// Select a weight and close dropdown
const selectWeight = (weight) => {
  localRouteWeight.value = weight;
  showWeightDropdown.value = false;
};

// Save changes - emit all route settings together to avoid race conditions
const saveChanges = () => {
  emit('update:routeSettings', {
    routeColor: localRouteColor.value,
    routeStyle: localRouteStyle.value,
    routeWeight: localRouteWeight.value,
  });
};

// Close dropdown when clicking outside
const handleClickOutside = (event) => {
  if (styleSelectorRef.value && !styleSelectorRef.value.contains(event.target)) {
    showStyleDropdown.value = false;
  }
  if (weightSelectorRef.value && !weightSelectorRef.value.contains(event.target)) {
    showWeightDropdown.value = false;
  }
};

// Set up click outside listener for dropdowns
watch([showStyleDropdown, showWeightDropdown], ([styleOpen, weightOpen]) => {
  if (styleOpen || weightOpen) {
    document.addEventListener('click', handleClickOutside, true);
  } else {
    document.removeEventListener('click', handleClickOutside, true);
  }
});

// Cleanup on unmount
onUnmounted(() => {
  document.removeEventListener('click', handleClickOutside, true);
});

// Filters state - CommonMap handles the actual filtering
const filters = ref({
  showRoute: true,
  showUncheckedIn: true,
  showPartiallyCheckedIn: true,
  showFullyCheckedIn: true,
  showAreas: true,
  selectedAreaIds: [],
  selectedLayerIds: [],
  showStaffingOverlay: false,
  showStatusOverlay: false,
});

// Toolbar actions for the Add menu
const toolbarActions = computed(() => [
  {
    id: 'add',
    icon: 'plus',
    label: 'Add',
    items: [
      { id: 'checkpoint', label: terms.value.checkpoint, icon: 'checkpoint' },
      { id: 'many-checkpoints', label: `Many ${termsLower.value.checkpoints}`, icon: 'many-checkpoints' },
      { id: 'import', label: `Import ${termsLower.value.checkpoints}`, icon: 'import' },
      { id: 'area', label: terms.value.area, icon: 'zone' },
      { id: 'layer', label: `${terms.value.course} or layer`, icon: 'course' },
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
    } else if (itemId === 'import') {
      emit('import-checkpoints');
    } else if (itemId === 'area') {
      emit('add-area-from-map');
      scrollMapIntoView();
    } else if (itemId === 'layer') {
      emit('add-layer');
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

// Initialize selected layers with all layer IDs when layers change
watch(() => props.layers, (newLayers) => {
  if (newLayers.length > 0) {
    // Add any new layers to the selected list (default to all checked)
    const currentSelectedIds = new Set(filters.value.selectedLayerIds);
    newLayers.forEach(layer => {
      if (!currentSelectedIds.has(layer.id)) {
        filters.value.selectedLayerIds.push(layer.id);
      }
    });

    // Remove any layers that no longer exist
    filters.value.selectedLayerIds = filters.value.selectedLayerIds.filter(id =>
      newLayers.some(layer => layer.id === id)
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

.route-settings-bar {
  display: flex;
  align-items: center;
  gap: 1rem;
  padding: 0.75rem 1rem;
  background: var(--card-bg);
  border-radius: 8px;
  box-shadow: var(--shadow-sm);
  flex-wrap: wrap;
  position: relative;
  z-index: 10;
}

.route-settings-label {
  font-size: 0.9rem;
  font-weight: 500;
  color: var(--text-secondary);
}

/* Custom style selector */
.style-selector {
  position: relative;
}

.style-selector-button {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.5rem 0.75rem;
  border: 1px solid var(--border-medium);
  border-radius: 6px;
  background: var(--card-bg);
  cursor: pointer;
  min-width: 110px;
  transition: border-color 0.2s;
}

.style-selector-button:hover {
  border-color: var(--brand-primary);
}

.style-selector-button:focus {
  outline: none;
  border-color: var(--brand-primary);
  box-shadow: 0 0 0 2px var(--brand-shadow);
}

.style-selector-arrow {
  font-size: 0.65rem;
  color: var(--text-secondary);
  margin-left: auto;
}

.style-preview {
  width: 60px;
  height: 10px;
  flex-shrink: 0;
}

.style-preview-wide {
  width: 80px;
  height: 10px;
  flex-shrink: 0;
}

.style-dropdown {
  position: absolute;
  top: 100%;
  left: 0;
  margin-top: 4px;
  background: var(--card-bg);
  border: 1px solid var(--border-medium);
  border-radius: 6px;
  box-shadow: var(--shadow-lg);
  z-index: 100;
  overflow: hidden;
}

.style-option {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 100%;
  padding: 0.6rem 0.75rem;
  border: none;
  background: transparent;
  cursor: pointer;
  transition: background 0.15s;
}

.style-option:hover {
  background: rgba(128, 128, 128, 0.2);
}

.style-option.selected {
  background: rgba(128, 128, 128, 0.3);
}

.btn-save-style {
  padding: 0.5rem 1rem;
  background: var(--success);
  color: white;
  border: none;
  border-radius: 6px;
  font-size: 0.85rem;
  font-weight: 500;
  cursor: pointer;
  transition: background 0.2s, opacity 0.2s;
}

.btn-save-style:hover:not(:disabled) {
  background: var(--success-hover);
}

.btn-save-style:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.map-section {
  position: relative;
  /* Fill remaining viewport height minus header (~80px), tabs (~60px), padding (~4rem), and layer pills (~50px) */
  height: calc(100vh - 260px);
  min-height: 600px;
  border-radius: 8px;
  overflow: visible;
  box-shadow: var(--shadow-sm);
  z-index: 1;
  isolation: isolate;
}

@media (max-width: 768px) {
  .map-section {
    height: calc(100vh - 300px);
    min-height: 400px;
  }

  .route-settings-bar {
    gap: 0.75rem;
  }
}

/* Layers pills styles */
.layers-pills {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
  padding: 0.5rem 0;
}

.layer-pill {
  display: inline-flex;
  align-items: center;
  gap: 0.375rem;
  padding: 0.375rem 0.75rem;
  border: none;
  border-radius: 9999px;
  font-size: 0.85rem;
  font-weight: 500;
  cursor: pointer;
  transition: opacity 0.2s, transform 0.1s;
  color: #fff;
  text-shadow: 0 1px 2px rgba(0, 0, 0, 0.3);
}

.layer-pill:hover {
  opacity: 0.85;
}

.layer-pill:active {
  transform: scale(0.97);
}

.layer-pill-name {
  max-width: 150px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.layer-pill-edit-icon {
  opacity: 0.7;
  flex-shrink: 0;
}

.layer-pill:hover .layer-pill-edit-icon {
  opacity: 1;
}

.layer-pill-add {
  background: var(--bg-secondary);
  color: var(--text-secondary);
  text-shadow: none;
  border: 1px dashed var(--border-color);
}

.layer-pill-add:hover {
  background: var(--bg-hover);
  color: var(--text-primary);
  opacity: 1;
}

.btn {
  padding: 0.5rem 1rem;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  font-size: 0.9rem;
  transition: background-color 0.2s;
}

.btn-small {
  padding: 0.375rem 0.75rem;
  font-size: 0.85rem;
}

.btn-primary {
  background: var(--btn-primary-bg);
  color: var(--btn-primary-text);
}

.btn-primary:hover {
  background: var(--btn-primary-hover);
}
</style>
