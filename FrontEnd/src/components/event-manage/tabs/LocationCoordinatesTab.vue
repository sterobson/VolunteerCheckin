<template>
  <div class="tab-content">
    <form @submit.prevent="$emit('save')">
      <!-- Hide coordinates when dynamic location is enabled -->
      <template v-if="!form.isDynamic">
        <div class="form-row">
          <div class="form-group">
            <label>Longitude</label>
            <input
              v-model="longitudeInput"
              @blur="handleCoordinateBlur('longitude')"
              type="text"
              inputmode="decimal"
              class="form-input"
              :disabled="isMoving"
              placeholder="e.g., -0.091234"
            />
          </div>

          <div class="form-group">
            <label>Latitude</label>
            <input
              v-model="latitudeInput"
              @blur="handleCoordinateBlur('latitude')"
              type="text"
              inputmode="decimal"
              class="form-input"
              :disabled="isMoving"
              placeholder="e.g., 51.505123"
            />
          </div>
        </div>

        <div class="form-group">
          <button
            type="button"
            @click="$emit('move-location')"
            class="btn btn-secondary btn-with-icon"
            :class="{ 'btn-primary': isMoving }"
          >
            <svg class="btn-icon" viewBox="0 0 24 24" fill="currentColor">
              <path d="M12 2C8.13 2 5 5.13 5 9c0 5.25 7 13 7 13s7-7.75 7-13c0-3.87-3.13-7-7-7zm0 9.5c-1.38 0-2.5-1.12-2.5-2.5s1.12-2.5 2.5-2.5 2.5 1.12 2.5 2.5-1.12 2.5-2.5 2.5z"/>
            </svg>
            {{ isMoving ? 'Click on map to set new location...' : `Move ${checkpointTermSingular.toLowerCase()}...` }}
          </button>
        </div>
      </template>
    </form>

    <!-- Dynamic checkpoint accordion -->
    <div class="accordion-item">
      <button
        type="button"
        class="accordion-header"
        :class="{ expanded: expandedDynamic }"
        @click="expandedDynamic = !expandedDynamic"
      >
        <div class="accordion-icon">
          <svg width="24" height="24" viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg">
            <path d="M12 2L15 8L22 9L17 14L18 21L12 18L6 21L7 14L2 9L9 8L12 2Z" fill="#F59E0B" opacity="0.3"/>
            <path d="M12 6L13.5 9L17 9.5L14.5 12L15 15.5L12 14L9 15.5L9.5 12L7 9.5L10.5 9L12 6Z" fill="#F59E0B"/>
            <circle cx="12" cy="11" r="2" fill="white"/>
          </svg>
        </div>
        <span class="accordion-title">Dynamic location</span>
        <span class="accordion-preview">{{ form.isDynamic ? 'Enabled' : 'Disabled' }}</span>
        <span class="accordion-arrow">{{ expandedDynamic ? '▲' : '▼' }}</span>
      </button>
      <div v-if="expandedDynamic" class="accordion-content">
        <p class="section-description">
          Enable this for lead cars, sweep vehicles, or other moving {{ checkpointTermSingular.toLowerCase() }}s whose location can be updated by designated {{ peopleTermPlural.toLowerCase() }} during the event.
        </p>
        <label class="dynamic-checkbox">
          <input
            type="checkbox"
            :checked="form.isDynamic"
            @change="handleDynamicToggle($event.target.checked)"
          />
          <span>This {{ checkpointTermSingular.toLowerCase() }} can move</span>
        </label>

        <div v-if="form.isDynamic" class="dynamic-settings">
          <ScopeConfigurationEditor
            :model-value="form.locationUpdateScopeConfigurations"
            :areas="areas"
            :locations="locations"
            :marshals="marshals"
            :is-editing="isEditing"
            :header-text="`Who can update this ${checkpointTermSingular.toLowerCase()}'s location?`"
            :exclude-scopes="['OnePerCheckpoint', 'OneLeadPerArea', 'OnePerArea']"
            intent="location-update"
            :current-checkpoint-id="form.id"
            @update:model-value="handleScopeInput"
            @user-changed="$emit('input')"
          />
        </div>
      </div>
    </div>

    <!-- Layer selection (only shown if layers exist) -->
    <div v-if="layers && layers.length > 0" class="layer-section">
      <h4 class="section-header">Layers</h4>
      <p class="section-description">
        Choose how this {{ checkpointTermSingular.toLowerCase() }} is assigned to layers.
      </p>

      <!-- Warning for auto mode with no nearby routes -->
      <div v-if="showAutoModeWarning" class="auto-mode-warning">
        <svg class="warning-icon" viewBox="0 0 24 24" fill="currentColor">
          <path d="M1 21h22L12 2 1 21zm12-3h-2v-2h2v2zm0-4h-2v-4h2v4z"/>
        </svg>
        <span>No routes pass within {{ autoDetectDistance }}m of this location. This {{ checkpointTermSingular.toLowerCase() }} won't appear on any layer until a route is nearby or you change the assignment mode.</span>
      </div>

      <div class="layer-selector">
        <div
          class="layer-row"
          :class="{ active: !isAllLayers }"
          @click="showLayerModal = true"
        >
          <div class="layer-label">Selected layers</div>
          <div
            class="layer-pill"
            :class="{ 'has-color': !isAllLayers && selectedLayerColors.length > 0 }"
            :style="layerPillStyle"
          >
            {{ layerPillText }}
          </div>
        </div>
      </div>
    </div>

    <!-- Layer selection modal -->
    <LayerSelectorModal
      :show="showLayerModal"
      :layers="layers"
      :layer-assignment-mode="form.layerAssignmentMode || 'auto'"
      :selected-layer-ids="form.layerIds"
      :checkpoint-term="checkpointTermSingular"
      @close="showLayerModal = false"
      @update:layer-assignment="handleLayerAssignmentChange"
    />
  </div>
</template>

<script setup>
import { ref, watch, computed, defineProps, defineEmits } from 'vue';
import ScopeConfigurationEditor from '../ScopeConfigurationEditor.vue';
import LayerSelectorModal from '../modals/LayerSelectorModal.vue';
import { findLayersNearPoint, formatDistance } from '../../../utils/coordinateUtils';

const props = defineProps({
  form: {
    type: Object,
    required: true,
  },
  isMoving: {
    type: Boolean,
    default: false,
  },
  checkpointTermSingular: {
    type: String,
    default: 'Checkpoint',
  },
  peopleTermPlural: {
    type: String,
    default: 'Marshals',
  },
  areas: {
    type: Array,
    default: () => [],
  },
  locations: {
    type: Array,
    default: () => [],
  },
  marshals: {
    type: Array,
    default: () => [],
  },
  layers: {
    type: Array,
    default: () => [],
  },
  isEditing: {
    type: Boolean,
    default: false,
  },
});

const emit = defineEmits(['update:form', 'input', 'save', 'move-location']);

const expandedDynamic = ref(false);
const showLayerModal = ref(false);

// Auto-detection state
const autoDetectDistance = ref(50); // Default 50 meters
const autoDetectOptions = [
  { value: 25, label: '25 m' },
  { value: 50, label: '50 m' },
  { value: 100, label: '100 m' },
  { value: 200, label: '200 m' },
  { value: 500, label: '500 m' },
];

// Check if we have valid coordinates for auto-detection
const hasValidCoordinates = computed(() => {
  const lat = props.form.latitude;
  const lon = props.form.longitude;
  return lat != null && lon != null && !(lat === 0 && lon === 0);
});

// Detect layers near the checkpoint's coordinates
const detectedLayers = computed(() => {
  if (!hasValidCoordinates.value) {
    return [];
  }

  return findLayersNearPoint(props.form.latitude, props.form.longitude, props.layers, autoDetectDistance.value);
});

// Check if current layer selection matches detected layers
const layersMatchDetected = computed(() => {
  if (detectedLayers.value.length === 0) return true;

  const detectedIds = detectedLayers.value.map(d => d.layer.id).sort();
  const currentIds = (props.form.layerIds || []).slice().sort();

  if (detectedIds.length !== currentIds.length) return false;
  return detectedIds.every((id, i) => id === currentIds[i]);
});

// Check if this is a new checkpoint (no ID)
const isNewCheckpoint = computed(() => {
  return !props.form.id;
});

// Check if layers have been manually set (not auto-detected)
const hasManualLayerSelection = ref(false);

// Apply auto-detected layers to the form
const applyAutoDetectedLayers = () => {
  if (detectedLayers.value.length > 0) {
    const layerIds = detectedLayers.value.map(d => d.layer.id);
    emit('update:form', { ...props.form, layerIds });
    emit('input');
  } else {
    // No layers detected - set to all layers (null)
    emit('update:form', { ...props.form, layerIds: null });
    emit('input');
  }
  hasManualLayerSelection.value = false;
};

// Layer selection computed properties
const sortedLayers = computed(() => {
  return [...props.layers].sort((a, b) => (a.displayOrder || 0) - (b.displayOrder || 0));
});

const isAllLayers = computed(() => {
  // mode "all" = all layers
  return layerAssignmentMode.value === 'all';
});

// Get layer assignment mode
const layerAssignmentMode = computed(() => {
  return props.form.layerAssignmentMode || 'auto';
});

// Text for the layer pill summary
const layerPillText = computed(() => {
  const mode = layerAssignmentMode.value;

  if (mode === 'auto') {
    // Use real-time detection results, not saved layerIds
    const detected = detectedLayers.value;
    if (detected.length === 0) {
      return 'Auto (none detected)';
    }
    if (detected.length === 1) {
      return `Auto: ${detected[0].layer.name || detected[0].layer.Name || '1 layer'}`;
    }
    return `Auto: ${detected.length} layers`;
  }

  if (mode === 'all') {
    return 'All layers';
  }

  // mode === 'specific'
  const selectedIds = props.form.layerIds || [];
  const count = selectedIds.length;
  if (count === 0) {
    return 'None';
  }
  if (count === 1) {
    const layer = props.layers.find(l => l.id === selectedIds[0]);
    return layer?.name || '1 layer';
  }
  if (count <= 2) {
    return selectedIds
      .map(id => props.layers.find(l => l.id === id)?.name)
      .filter(Boolean)
      .join(', ');
  }
  return `${count} layers`;
});

// Show warning when auto mode finds no nearby routes
const showAutoModeWarning = computed(() => {
  if (layerAssignmentMode.value !== 'auto') return false;
  // Check actual detection, not just saved layerIds
  return detectedLayers.value.length === 0 && hasValidCoordinates.value;
});

// Colors for selected layers (for pill display)
const selectedLayerColors = computed(() => {
  if (isAllLayers.value) {
    return [];
  }

  // For auto mode, use detected layers
  if (layerAssignmentMode.value === 'auto') {
    return detectedLayers.value.map(d => d.layer.routeColor || d.layer.RouteColor || '#3388ff');
  }

  // For specific mode, use saved layerIds
  const selectedIds = props.form.layerIds || [];
  return selectedIds
    .map(id => {
      const layer = props.layers.find(l => l.id === id);
      return layer?.routeColor || '#3388ff';
    });
});

// Style for the layer pill (background color based on selected layers)
const layerPillStyle = computed(() => {
  const colors = selectedLayerColors.value;
  if (colors.length === 0) {
    return {};
  }
  if (colors.length === 1) {
    return { background: colors[0] };
  }
  // Multiple colors - create a gradient
  const gradientStops = colors.map((color, i) => {
    const start = (i / colors.length) * 100;
    const end = ((i + 1) / colors.length) * 100;
    return `${color} ${start}%, ${color} ${end}%`;
  }).join(', ');
  return { background: `linear-gradient(90deg, ${gradientStops})` };
});

const handleLayerSelectionChange = (newLayerIds) => {
  emit('update:form', { ...props.form, layerIds: newLayerIds });
  emit('input');
  hasManualLayerSelection.value = true;
};

const handleLayerAssignmentChange = ({ mode, layerIds }) => {
  emit('update:form', { ...props.form, layerAssignmentMode: mode, layerIds });
  emit('input');
  hasManualLayerSelection.value = mode === 'specific';
};

// Auto-detect layers when coordinates change for new checkpoints
watch(
  () => [props.form.latitude, props.form.longitude],
  ([newLat, newLon], [oldLat, oldLon]) => {
    // Only auto-detect for new checkpoints that haven't had manual selection
    if (!isNewCheckpoint.value || hasManualLayerSelection.value) return;

    // Skip auto-detection if only one layer (default to all layers)
    if (props.layers.length <= 1) return;

    // Skip if coordinates haven't actually changed
    if (newLat === oldLat && newLon === oldLon) return;

    // Skip if no valid coordinates
    if (newLat == null || newLon == null || (newLat === 0 && newLon === 0)) return;

    // Auto-detect and apply layers
    const detected = findLayersNearPoint(newLat, newLon, props.layers, autoDetectDistance.value);
    if (detected.length > 0) {
      const layerIds = detected.map(d => d.layer.id);
      emit('update:form', { ...props.form, layerIds });
    }
  },
  { immediate: false }
);

// Local state for coordinate inputs (allows free typing without reformatting)
const longitudeInput = ref('');
const latitudeInput = ref('');

// Format coordinate to 6 decimal places (~0.1m accuracy)
const formatCoordinate = (value) => {
  if (value === null || value === undefined || value === '' || isNaN(value)) return '';
  return Number(value).toFixed(6);
};

// Initialize local inputs from form values
watch(() => props.form.longitude, (newVal) => {
  longitudeInput.value = formatCoordinate(newVal);
}, { immediate: true });

watch(() => props.form.latitude, (newVal) => {
  latitudeInput.value = formatCoordinate(newVal);
}, { immediate: true });

// Handle coordinate blur - parse and update form
const handleCoordinateBlur = (field) => {
  const inputRef = field === 'longitude' ? longitudeInput : latitudeInput;
  const value = parseFloat(inputRef.value);

  if (!isNaN(value)) {
    emit('update:form', { ...props.form, [field]: value });
    emit('input');
    // Format the display value
    inputRef.value = formatCoordinate(value);
  } else if (inputRef.value.trim() === '') {
    emit('update:form', { ...props.form, [field]: 0 });
    emit('input');
    inputRef.value = formatCoordinate(0);
  }
};

const handleInput = (field, value) => {
  emit('update:form', { ...props.form, [field]: value });
  emit('input');
};

const handleCheckboxInput = (field, value) => {
  emit('update:form', { ...props.form, [field]: value });
  emit('input');
};

// Handle dynamic toggle - set coordinates to 0 when enabling and set default scope
const handleDynamicToggle = (isDynamic) => {
  if (isDynamic) {
    // Default scope: Everyone at THIS checkpoint
    // For new checkpoints (no ID), use THIS_CHECKPOINT placeholder - backend will resolve to actual ID on save
    // For existing checkpoints, use the actual checkpoint ID so it shows correctly in the UI
    const checkpointIdForScope = props.form.id || 'THIS_CHECKPOINT';
    const defaultScopeConfig = [{
      scope: 'EveryoneAtCheckpoints',
      itemType: 'Checkpoint',
      ids: [checkpointIdForScope],
    }];

    // Always reset to default scope when turning ON isDynamic
    // This ensures turning off then back on resets to the default
    // Users can then customize the scope if needed
    emit('update:form', {
      ...props.form,
      isDynamic: true,
      latitude: 0,
      longitude: 0,
      locationUpdateScopeConfigurations: defaultScopeConfig,
    });
  } else {
    emit('update:form', { ...props.form, isDynamic: false });
  }
  emit('input');
};

const handleScopeInput = (value) => {
  emit('update:form', { ...props.form, locationUpdateScopeConfigurations: value });
  emit('input');
};


</script>

<style scoped>
.tab-content {
  padding-top: 0.5rem;
}

.form-group {
  margin-bottom: 1.5rem;
}

.form-group label {
  display: block;
  margin-bottom: 0.5rem;
  font-weight: 500;
  color: var(--text-primary);
}

.form-input {
  width: 100%;
  padding: 0.5rem;
  border: 1px solid var(--border-color);
  border-radius: 4px;
  font-size: 0.9rem;
  box-sizing: border-box;
  background: var(--input-bg);
  color: var(--text-primary);
}

.form-error {
  display: block;
  color: var(--danger);
  font-size: 0.85rem;
  margin-top: 0.25rem;
}

.form-row {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 1rem;
}

.btn {
  padding: 0.5rem 1rem;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  font-size: 0.9rem;
  transition: background-color 0.2s;
}

.btn-primary {
  background: var(--accent-primary);
  color: var(--btn-primary-text);
}

.btn-primary:hover {
  background: var(--accent-primary-hover);
}

.btn-secondary {
  background: var(--btn-secondary-bg);
  color: var(--btn-secondary-text);
}

.btn-secondary:hover {
  background: var(--btn-secondary-hover);
}

.btn-with-icon {
  display: inline-flex;
  align-items: center;
  gap: 0.5rem;
}

.btn-icon {
  width: 1rem;
  height: 1rem;
  flex-shrink: 0;
}

/* Accordion styles */
.accordion-item {
  border: 1px solid var(--border-color);
  border-radius: 8px;
  overflow: hidden;
  margin-top: 1.5rem;
}

.accordion-header {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  width: 100%;
  padding: 0.875rem 1rem;
  background: var(--bg-tertiary);
  border: none;
  cursor: pointer;
  text-align: left;
  transition: background-color 0.2s;
}

.accordion-header:hover {
  background: var(--bg-secondary);
}

.accordion-header.expanded {
  background: var(--bg-secondary);
  border-bottom: 1px solid var(--border-color);
}

.accordion-icon {
  width: 24px;
  height: 24px;
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
}

.accordion-title {
  font-weight: 500;
  color: var(--text-primary);
  font-size: 0.95rem;
}

.accordion-preview {
  flex: 1;
  text-align: right;
  color: var(--text-secondary);
  font-size: 0.85rem;
  font-weight: normal;
  margin-right: 0.5rem;
}

.accordion-arrow {
  color: var(--text-secondary);
  font-size: 0.75rem;
  flex-shrink: 0;
}

.accordion-content {
  padding: 1rem;
  background: var(--card-bg);
}

.section-description {
  color: var(--text-secondary);
  font-size: 0.9rem;
  margin: 0 0 1rem 0;
}

.checkbox-group {
  margin-bottom: 0;
}

.checkbox-label {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  cursor: pointer;
  font-weight: normal;
}

.checkbox-label input[type="checkbox"] {
  width: 1rem;
  height: 1rem;
  cursor: pointer;
}

.dynamic-checkbox {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  cursor: pointer;
  font-weight: normal;
  padding: 0.5rem 0;
}

.dynamic-checkbox input[type="checkbox"] {
  width: 1.125rem;
  height: 1.125rem;
  cursor: pointer;
  flex-shrink: 0;
  margin: 0;
}

.dynamic-checkbox span {
  line-height: 1.4;
}

.dynamic-settings {
  margin-top: 1rem;
  padding-top: 1rem;
  border-top: 1px solid var(--border-color);
}

/* Layer section styles */
.layer-section {
  margin-top: 1.5rem;
}

.section-header {
  margin: 0 0 0.5rem 0;
  color: var(--text-dark);
  font-size: 1rem;
  font-weight: 500;
}

.layer-section .section-description {
  color: var(--text-secondary);
  font-size: 0.9rem;
  margin: 0 0 0.75rem 0;
}

.layer-selector {
  display: flex;
  flex-direction: column;
  border: 1px solid var(--border-medium);
  border-radius: 6px;
  overflow: hidden;
}

.layer-row {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 0.75rem 1rem;
  background: var(--card-bg);
  cursor: pointer;
  transition: background-color 0.15s;
}

.layer-row:hover {
  background: var(--bg-secondary);
}

.layer-row.active {
  background: var(--info-bg);
}

.layer-row.active:hover {
  background: var(--accent-primary-light);
}

.layer-label {
  font-size: 0.9rem;
  color: var(--text-dark);
  font-weight: 500;
}

.layer-pill {
  padding: 0.25rem 0.75rem;
  background: var(--border-light);
  color: var(--text-secondary);
  border-radius: 12px;
  font-size: 0.8rem;
  font-weight: 500;
  white-space: nowrap;
}

.layer-pill.has-color {
  color: white;
  text-shadow: 0 1px 2px rgba(0, 0, 0, 0.3);
}

/* Auto-detect panel styles */
.auto-detect-panel {
  background: var(--bg-tertiary);
  border: 1px solid var(--border-color);
  border-radius: 6px;
  padding: 0.75rem;
  margin-bottom: 0.75rem;
}

.auto-detect-header {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  flex-wrap: wrap;
}

.auto-detect-label {
  font-size: 0.85rem;
  color: var(--text-secondary);
}

.distance-select {
  padding: 0.25rem 0.5rem;
  border: 1px solid var(--border-color);
  border-radius: 4px;
  background: var(--input-bg);
  color: var(--text-primary);
  font-size: 0.85rem;
  cursor: pointer;
}

.detected-layers {
  margin-top: 0.75rem;
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.detected-layer-item {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.375rem 0.5rem;
  background: var(--card-bg);
  border-radius: 4px;
  font-size: 0.85rem;
}

.layer-color-dot {
  width: 12px;
  height: 12px;
  border-radius: 50%;
  flex-shrink: 0;
}

.layer-name {
  flex: 1;
  color: var(--text-primary);
  font-weight: 500;
}

.layer-distance {
  color: var(--text-secondary);
  font-size: 0.8rem;
}

.no-layers-detected {
  margin-top: 0.5rem;
  color: var(--text-secondary);
  font-size: 0.85rem;
  font-style: italic;
}

.apply-btn {
  align-self: flex-start;
  margin-top: 0.25rem;
}

.btn-small {
  padding: 0.375rem 0.75rem;
  font-size: 0.8rem;
}

/* Auto mode warning */
.auto-mode-warning {
  display: flex;
  align-items: flex-start;
  gap: 0.5rem;
  padding: 0.75rem;
  background: var(--warning-bg, #fff3cd);
  border: 1px solid var(--warning-border, #ffc107);
  border-radius: 6px;
  margin-bottom: 0.75rem;
  font-size: 0.85rem;
  color: var(--warning-text, #856404);
}

.warning-icon {
  width: 18px;
  height: 18px;
  flex-shrink: 0;
  margin-top: 0.1rem;
}
</style>
