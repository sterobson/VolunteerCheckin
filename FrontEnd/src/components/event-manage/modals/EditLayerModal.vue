<template>
  <BaseModal
    :show="show"
    :title="layer && layer.id ? `Edit layer: ${layer.name}` : 'Create new layer'"
    size="medium"
    :confirm-on-close="true"
    :is-dirty="isDirty"
    @close="handleClose"
  >
    <form @submit.prevent="handleSave">
      <!-- Name -->
      <div class="form-group">
        <label>Name *</label>
        <input
          v-model="form.name"
          type="text"
          required
          class="form-input"
          placeholder="e.g., Main route, Stage 2, Alternative path"
          @input="handleInput"
        />
      </div>

      <!-- Route -->
      <div class="form-group">
        <label>{{ terms.course }}</label>
        <div class="route-upload-area">
          <div v-if="hasRoute" class="route-preview">
            <div class="route-info">
              <span class="route-icon">
                <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                  <path d="M3 12h18M3 6h18M3 18h18" />
                </svg>
              </span>
              <span>{{ terms.course }} loaded ({{ form.route?.length || 0 }} points)</span>
            </div>
            <div class="route-actions">
              <button type="button" class="btn btn-secondary btn-small" @click="handleEditRoute">
                Edit
              </button>
              <button type="button" class="btn btn-secondary btn-small" @click="clearRoute">
                Remove
              </button>
            </div>
          </div>
          <div v-else class="upload-prompt">
            <div class="route-buttons">
              <button type="button" class="btn btn-primary" @click="handleEditRoute">
                Draw {{ termsLower.course }} on map
              </button>
              <span class="or-divider">or</span>
              <input
                ref="fileInput"
                type="file"
                accept=".gpx"
                class="file-input"
                @change="handleFileUpload"
              />
              <button type="button" class="btn btn-secondary" @click="$refs.fileInput.click()">
                Upload GPX file
              </button>
            </div>
            <p class="upload-hint">Draw a {{ termsLower.course }} directly on the map, or upload a GPX file</p>
          </div>
        </div>
        <p v-if="uploadError" class="error-message">{{ uploadError }}</p>
      </div>

      <!-- Route style settings -->
      <div v-if="hasRoute" class="form-group">
        <label>{{ terms.course }} style</label>
        <div class="route-style-bar">
          <BrandingColorPicker
            :model-value="form.routeColor"
            :default-color="DEFAULT_ROUTE_COLOR"
            :show-contrast-preview="false"
            :show-default="true"
            :teleport="true"
            @update:model-value="handleColorChange"
          />
          <div class="style-selector" ref="styleSelectorRef">
            <button
              type="button"
              class="style-selector-button"
              ref="styleButtonRef"
              @click="toggleStyleDropdown"
            >
              <svg class="style-preview" viewBox="0 0 60 10" preserveAspectRatio="none">
                <line
                  x1="0" y1="5" x2="60" y2="5"
                  :stroke="effectiveColor"
                  stroke-width="3"
                  :stroke-dasharray="getStrokeDashArray(form.routeStyle || 'line')"
                  :stroke-linecap="getStrokeLineCap(form.routeStyle || 'line')"
                />
              </svg>
              <span class="style-selector-arrow">▼</span>
            </button>
            <Teleport to="body">
              <div
                v-if="showStyleDropdown"
                class="style-dropdown-fixed"
                :style="{ ...dropdownBackgroundStyle, ...styleDropdownPosition }"
              >
                <button
                  v-for="style in lineStyles"
                  :key="style.value"
                  type="button"
                  class="style-option"
                  :class="{ selected: (form.routeStyle || 'line') === style.value }"
                  @click="selectStyle(style.value)"
                >
                  <svg class="style-preview-wide" viewBox="0 0 80 10" preserveAspectRatio="none">
                    <line
                      x1="0" y1="5" x2="80" y2="5"
                      :stroke="effectiveColor"
                      stroke-width="3"
                      :stroke-dasharray="getStrokeDashArray(style.value)"
                      :stroke-linecap="getStrokeLineCap(style.value)"
                    />
                  </svg>
                </button>
              </div>
            </Teleport>
          </div>
          <div class="style-selector" ref="weightSelectorRef">
            <button
              type="button"
              class="style-selector-button"
              ref="weightButtonRef"
              @click="toggleWeightDropdown"
            >
              <svg class="style-preview" viewBox="0 0 60 10" preserveAspectRatio="none">
                <line
                  x1="0" y1="5" x2="60" y2="5"
                  :stroke="effectiveColor"
                  :stroke-width="form.routeWeight || 4"
                  :stroke-dasharray="getStrokeDashArray(form.routeStyle || 'line')"
                  :stroke-linecap="getStrokeLineCap(form.routeStyle || 'line')"
                />
              </svg>
              <span class="style-selector-arrow">▼</span>
            </button>
            <Teleport to="body">
              <div
                v-if="showWeightDropdown"
                class="style-dropdown-fixed"
                :style="{ ...dropdownBackgroundStyle, ...weightDropdownPosition }"
              >
                <button
                  v-for="weight in lineWeights"
                  :key="weight.value"
                  type="button"
                  class="style-option"
                  :class="{ selected: (form.routeWeight || 4) === weight.value }"
                  @click="selectWeight(weight.value)"
                >
                  <svg class="style-preview-wide" viewBox="0 0 80 10" preserveAspectRatio="none">
                    <line
                      x1="0" y1="5" x2="80" y2="5"
                      :stroke="effectiveColor"
                      :stroke-width="weight.value"
                      :stroke-dasharray="getStrokeDashArray(form.routeStyle || 'line')"
                      :stroke-linecap="getStrokeLineCap(form.routeStyle || 'line')"
                    />
                  </svg>
                </button>
              </div>
            </Teleport>
          </div>
        </div>
      </div>
    </form>

    <!-- Custom footer with left and right aligned buttons -->
    <template #footer>
      <div class="custom-footer">
        <div class="footer-left">
          <button
            v-if="layer && layer.id"
            type="button"
            @click="handleDelete"
            class="btn btn-danger"
          >
            Delete
          </button>
        </div>
        <button type="button" @click="handleSave" class="btn btn-success" :disabled="!form.name">
          Save
        </button>
      </div>
    </template>
  </BaseModal>
</template>

<script setup>
import { ref, computed, watch, onUnmounted } from 'vue';
import BaseModal from '../../BaseModal.vue';
import BrandingColorPicker from '../../BrandingColorPicker.vue';
import { useTerminology } from '../../../composables/useTerminology';

const { terms, termsLower } = useTerminology();

const DEFAULT_ROUTE_COLOR = '#3388ff';

const props = defineProps({
  show: {
    type: Boolean,
    default: false,
  },
  layer: {
    type: Object,
    default: null,
  },
  isDirty: {
    type: Boolean,
    default: false,
  },
});

const emit = defineEmits([
  'close',
  'save',
  'delete',
  'update:isDirty',
  'upload-gpx',
  'edit-route',
]);

const fileInput = ref(null);
const uploadError = ref('');
const styleSelectorRef = ref(null);
const weightSelectorRef = ref(null);
const styleButtonRef = ref(null);
const weightButtonRef = ref(null);
const showStyleDropdown = ref(false);
const showWeightDropdown = ref(false);
const styleDropdownPosition = ref({});
const weightDropdownPosition = ref({});

const form = ref({
  name: '',
  route: null,
  routeColor: DEFAULT_ROUTE_COLOR,
  routeStyle: '',
  routeWeight: null,
});

// Available line styles
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

// Available line weights
const lineWeights = [
  { value: 2, label: 'Thin' },
  { value: 4, label: 'Normal' },
  { value: 6, label: 'Thick' },
  { value: 8, label: 'Extra thick' },
];

// Helper to create empty form
const getEmptyForm = () => ({
  name: '',
  route: null,
  routeColor: DEFAULT_ROUTE_COLOR,
  routeStyle: '',
  routeWeight: null,
});

// Computed
const hasRoute = computed(() => {
  return form.value.route && form.value.route.length > 0;
});

const effectiveColor = computed(() => form.value.routeColor || DEFAULT_ROUTE_COLOR);

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
  const color = effectiveColor.value;
  const luminance = getLuminance(color);

  // If the color is light (high luminance), use a dark background
  if (luminance > 0.5) {
    return { backgroundColor: '#333', borderColor: '#555' };
  }
  return {};
});

// Get stroke dash array for a given style
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

// Watch for layer changes
watch(() => props.layer, (newVal) => {
  if (newVal) {
    form.value = {
      name: newVal.name || '',
      route: newVal.route || null,
      routeColor: newVal.routeColor || DEFAULT_ROUTE_COLOR,
      routeStyle: newVal.routeStyle || '',
      routeWeight: newVal.routeWeight || null,
    };
  } else {
    form.value = getEmptyForm();
  }
  uploadError.value = '';
}, { immediate: true });

// Watch for show changes
watch(() => props.show, (newVal) => {
  if (newVal) {
    uploadError.value = '';
    showStyleDropdown.value = false;
    showWeightDropdown.value = false;
    if (!props.layer) {
      form.value = getEmptyForm();
    }
  }
});

// Methods
const handleInput = () => {
  emit('update:isDirty', true);
};

const handleColorChange = (color) => {
  form.value.routeColor = color;
  handleInput();
};

const calculateDropdownPosition = (buttonRef) => {
  if (!buttonRef.value) return {};
  const rect = buttonRef.value.getBoundingClientRect();
  return {
    position: 'fixed',
    top: `${rect.bottom + 4}px`,
    left: `${rect.left}px`,
    zIndex: 10000,
  };
};

const toggleStyleDropdown = () => {
  if (!showStyleDropdown.value) {
    styleDropdownPosition.value = calculateDropdownPosition(styleButtonRef);
  }
  showStyleDropdown.value = !showStyleDropdown.value;
  showWeightDropdown.value = false;
};

const toggleWeightDropdown = () => {
  if (!showWeightDropdown.value) {
    weightDropdownPosition.value = calculateDropdownPosition(weightButtonRef);
  }
  showWeightDropdown.value = !showWeightDropdown.value;
  showStyleDropdown.value = false;
};

const selectStyle = (style) => {
  form.value.routeStyle = style === 'line' ? '' : style;
  showStyleDropdown.value = false;
  handleInput();
};

const selectWeight = (weight) => {
  form.value.routeWeight = weight === 4 ? null : weight;
  showWeightDropdown.value = false;
  handleInput();
};

const handleFileUpload = (event) => {
  const file = event.target.files[0];
  if (!file) return;

  if (!file.name.toLowerCase().endsWith('.gpx')) {
    uploadError.value = 'Please upload a .gpx file';
    return;
  }

  uploadError.value = '';

  // Emit event to parent to handle the upload
  emit('upload-gpx', file);
  handleInput();

  // Reset the file input
  if (fileInput.value) {
    fileInput.value.value = '';
  }
};

const clearRoute = () => {
  form.value.route = null;
  handleInput();
};

const handleEditRoute = () => {
  // Emit event to parent with current route data and layer info
  emit('edit-route', {
    route: form.value.route || [],
    routeColor: form.value.routeColor || DEFAULT_ROUTE_COLOR,
    routeWeight: form.value.routeWeight || 4,
  });
};

const handleSave = () => {
  if (!form.value.name) return;

  const formData = {
    name: form.value.name,
    route: form.value.route,
    routeColor: form.value.routeColor !== DEFAULT_ROUTE_COLOR ? form.value.routeColor : null,
    routeStyle: form.value.routeStyle || null,
    routeWeight: form.value.routeWeight,
  };

  emit('save', formData);
};

const handleDelete = () => {
  emit('delete', props.layer?.id);
};

const handleClose = () => {
  emit('close');
};

// Close dropdowns when clicking outside
const handleClickOutside = (event) => {
  // Check if click is inside the style dropdown or its button
  const isInsideStyleDropdown = event.target.closest('.style-dropdown-fixed') ||
    (styleSelectorRef.value && styleSelectorRef.value.contains(event.target));
  if (!isInsideStyleDropdown) {
    showStyleDropdown.value = false;
  }

  // Check if click is inside the weight dropdown or its button
  const isInsideWeightDropdown = event.target.closest('.style-dropdown-fixed') ||
    (weightSelectorRef.value && weightSelectorRef.value.contains(event.target));
  if (!isInsideWeightDropdown) {
    showWeightDropdown.value = false;
  }
};

// Set up click outside listener
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

// Expose method to update route after GPX upload
defineExpose({
  setRoute: (route) => {
    form.value.route = route;
    handleInput();
  },
  setUploadError: (error) => {
    uploadError.value = error;
  },
});
</script>

<style scoped>
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
  border: 1px solid var(--input-border);
  border-radius: 4px;
  font-size: 0.9rem;
  box-sizing: border-box;
  background: var(--input-bg);
  color: var(--text-primary);
}

.route-upload-area {
  border: 1px dashed var(--border-color);
  border-radius: 8px;
  padding: 1rem;
  background: var(--bg-tertiary);
}

.route-preview {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.route-actions {
  display: flex;
  gap: 0.5rem;
}

.route-buttons {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  flex-wrap: wrap;
  justify-content: center;
}

.or-divider {
  color: var(--text-secondary);
  font-size: 0.85rem;
}

.route-info {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  color: var(--text-primary);
}

.route-icon {
  color: var(--success);
}

.upload-prompt {
  text-align: center;
}

.file-input {
  display: none;
}

.upload-hint {
  margin: 0.5rem 0 0;
  font-size: 0.85rem;
  color: var(--text-secondary);
}

.error-message {
  margin: 0.5rem 0 0;
  font-size: 0.85rem;
  color: var(--danger);
}

/* Route style bar - matches CourseAreasTab */
.route-style-bar {
  display: flex;
  align-items: center;
  gap: 1rem;
  padding: 0.75rem 1rem;
  background: var(--bg-tertiary);
  border-radius: 8px;
  flex-wrap: wrap;
}

/* Style selector dropdowns */
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

/* Fixed dropdown - teleported to body to escape modal overflow */
/* Note: Using :global() because dropdowns are teleported outside component scope */
:global(.style-dropdown-fixed) {
  background: var(--card-bg);
  border: 1px solid var(--border-medium);
  border-radius: 6px;
  box-shadow: var(--shadow-lg);
  overflow: hidden;
}

:global(.style-dropdown-fixed .style-option) {
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

:global(.style-dropdown-fixed .style-option:hover) {
  background: rgba(128, 128, 128, 0.2);
}

:global(.style-dropdown-fixed .style-option.selected) {
  background: rgba(128, 128, 128, 0.3);
}

:global(.style-dropdown-fixed .style-preview-wide) {
  width: 80px;
  height: 10px;
  flex-shrink: 0;
}

.custom-footer {
  display: flex;
  justify-content: space-between;
  align-items: center;
  width: 100%;
}

.footer-left {
  display: flex;
  gap: 0.5rem;
}

.btn {
  padding: 0.5rem 1rem;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  font-size: 0.9rem;
  transition: background-color 0.2s;
}

.btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.btn-small {
  padding: 0.375rem 0.75rem;
  font-size: 0.85rem;
}

.btn-primary {
  background: var(--btn-primary-bg);
  color: var(--btn-primary-text);
}

.btn-primary:hover:not(:disabled) {
  background: var(--btn-primary-hover);
}

.btn-secondary {
  background: var(--btn-secondary-bg);
  color: var(--btn-secondary-text);
}

.btn-secondary:hover:not(:disabled) {
  background: var(--btn-secondary-hover);
}

.btn-success {
  background: var(--success);
  color: white;
}

.btn-success:hover:not(:disabled) {
  background: var(--success-hover);
}

.btn-danger {
  background: var(--danger);
  color: var(--btn-primary-text);
}

.btn-danger:hover:not(:disabled) {
  background: var(--danger-hover);
}
</style>
