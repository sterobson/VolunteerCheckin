<template>
  <div class="tab-content">
    <form @submit.prevent="$emit('save')">
      <div class="form-group">
        <label>What3Words (optional)</label>
        <input
          :value="form.what3Words"
          @input="handleInput('what3Words', $event.target.value)"
          type="text"
          class="form-input"
          placeholder="e.g. filled.count.soap or filled/count/soap"
        />
        <small v-if="form.what3Words && !isValidWhat3Words(form.what3Words)" class="form-error">
          Invalid format. Must be word.word.word or word/word/word (lowercase letters only, 1-20 characters each)
        </small>
      </div>

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
  </div>
</template>

<script setup>
import { ref, watch, defineProps, defineEmits } from 'vue';
import ScopeConfigurationEditor from '../ScopeConfigurationEditor.vue';

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
  isEditing: {
    type: Boolean,
    default: false,
  },
});

const emit = defineEmits(['update:form', 'input', 'save', 'move-location']);

const expandedDynamic = ref(false);

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

const isValidWhat3Words = (value) => {
  if (!value) return true;
  const parts = value.includes('.') ? value.split('.') : value.split('/');
  if (parts.length !== 3) return false;
  const wordRegex = /^[a-z]{1,20}$/;
  return parts.every(part => wordRegex.test(part));
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
</style>
