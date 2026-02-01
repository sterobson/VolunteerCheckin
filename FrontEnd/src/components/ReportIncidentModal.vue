<template>
  <BaseModal
    :show="show"
    title="Report Incident"
    size="medium"
    :confirm-on-close="true"
    :is-dirty="isDirty"
    @close="handleClose"
  >
    <form @submit.prevent="handleSubmit" class="incident-form">
      <!-- Title (optional) -->
      <div class="form-group">
        <label for="title" class="form-label">Title</label>
        <input
          id="title"
          v-model="form.title"
          type="text"
          class="form-input"
          placeholder="Brief summary (optional)"
        />
      </div>

      <!-- Description -->
      <div class="form-group">
        <label for="description" class="form-label">Description <span class="required">*</span></label>
        <textarea
          id="description"
          v-model="form.description"
          class="form-input form-textarea"
          placeholder="Describe what happened..."
          rows="3"
          required
        ></textarea>
      </div>

      <!-- Severity & Time Row -->
      <div class="form-row">
        <!-- Severity Dropdown -->
        <div class="form-group form-group-half">
          <label for="severity" class="form-label">Severity</label>
          <select
            id="severity"
            v-model="form.severity"
            class="form-input form-select"
          >
            <option
              v-for="option in severityOptions"
              :key="option.value"
              :value="option.value"
            >
              {{ option.label }}
            </option>
          </select>
        </div>

        <!-- Incident Time -->
        <div class="form-group form-group-half">
          <label for="incidentTime" class="form-label">When</label>
          <input
            id="incidentTime"
            v-model="form.incidentTime"
            type="datetime-local"
            class="form-input"
          />
        </div>
      </div>

      <!-- Checkpoint (for leads/admins) -->
      <div v-if="availableCheckpoints.length > 0" class="form-group">
        <label for="checkpoint" class="form-label">{{ termsSentence.checkpoint }}</label>
        <select
          id="checkpoint"
          v-model="selectedCheckpointId"
          class="form-input form-select"
        >
          <option value="">-- None / Unknown --</option>
          <option
            v-for="cp in availableCheckpoints"
            :key="cp.id"
            :value="cp.id"
          >
            {{ cp.name }}{{ cp.description ? ` - ${cp.description}` : '' }}
          </option>
        </select>
      </div>

      <!-- Fixed checkpoint display (for marshals with single assignment) -->
      <div v-else-if="checkpoint" class="form-group">
        <label class="form-label">{{ termsSentence.checkpoint }}</label>
        <div class="checkpoint-toggle">
          <label class="checkbox-label">
            <input type="checkbox" v-model="useFixedCheckpoint" />
            <span class="checkpoint-name">{{ checkpoint.name }}</span>
            <span v-if="checkpoint.description" class="checkpoint-description">
              - {{ checkpoint.description }}
            </span>
          </label>
        </div>
      </div>

      <!-- Location (optional) -->
      <div class="form-group">
        <label class="form-label">Location (optional)</label>
        <div class="location-options">
          <div class="location-buttons">
            <button
              type="button"
              class="location-btn"
              :class="{ active: locationMode === 'gps' }"
              @click="setLocationMode('gps')"
            >
              GPS
            </button>
            <button
              type="button"
              class="location-btn"
              :class="{ active: locationMode === 'map' }"
              @click="setLocationMode('map')"
            >
              Pick on map
            </button>
            <button
              type="button"
              class="location-btn"
              :class="{ active: locationMode === 'none' }"
              @click="setLocationMode('none')"
            >
              None
            </button>
          </div>
          <div v-if="locationMode === 'gps'" class="location-status">
            <div v-if="gettingLocation" class="location-getting">
              Getting location...
            </div>
            <div v-else-if="currentLocation && currentLocation.latitude != null" class="location-display">
              <span>{{ currentLocation.latitude.toFixed(5) }}, {{ currentLocation.longitude.toFixed(5) }}</span>
              <span v-if="locationAccuracy" class="location-accuracy">(±{{ Math.round(locationAccuracy) }}m)</span>
            </div>
            <div v-else-if="locationError" class="location-error">
              {{ locationError }}
            </div>
          </div>
          <div v-if="locationMode === 'map'" class="map-picker-container">
            <div v-if="mapVisible" class="map-picker-hint">Tap to select location</div>
            <CommonMap
              v-if="mapVisible"
              :locations="availableCheckpoints"
              :route="route"
              :route-color="routeColor"
              :route-style="routeStyle"
              :route-weight="routeWeight"
              :layers="layers"
              :center="mapCenter"
              :zoom="14"
              mode="select-point"
              :user-location="initialLocation"
              :highlight-location-id="selectedCheckpointId"
              height="150px"
              @point-selected="handleMapClick"
            />
            <div v-if="currentLocation && currentLocation.latitude != null" class="location-display">
              <span>{{ currentLocation.latitude.toFixed(5) }}, {{ currentLocation.longitude.toFixed(5) }}</span>
              <button v-if="!mapVisible" type="button" class="location-edit-btn" @click="mapVisible = true">Edit</button>
            </div>
          </div>
        </div>
      </div>

      <!-- Error Message -->
      <div v-if="errorMessage" class="error-message">
        {{ errorMessage }}
      </div>
    </form>

    <template #footer>
      <div class="modal-actions">
        <button type="button" class="btn btn-secondary" @click="handleClose">
          Cancel
        </button>
        <button
          type="button"
          class="btn btn-primary disable-on-load"
          @click="handleSubmit"
          :disabled="!isValid || submitting"
        >
          {{ submitting ? 'Reporting...' : 'Report Incident' }}
        </button>
      </div>
    </template>
  </BaseModal>
</template>

<script setup>
import { ref, computed, watch, defineProps, defineEmits } from 'vue';
import BaseModal from './BaseModal.vue';
import CommonMap from './common/CommonMap.vue';
import { useTerminology } from '../composables/useTerminology';

const { termsSentence } = useTerminology();

const props = defineProps({
  show: {
    type: Boolean,
    required: true,
  },
  eventId: {
    type: String,
    required: true,
  },
  checkpoint: {
    type: Object,
    default: null,
  },
  initialLocation: {
    type: Object,
    default: null,
  },
  // For leads/admins - list of checkpoints they can choose from
  checkpoints: {
    type: Array,
    default: () => [],
  },
  // Event route/course for the map
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
});

const emit = defineEmits(['close', 'submit']);

const form = ref({
  title: '',
  description: '',
  severity: 'medium',
  incidentTime: '',
});

const selectedCheckpointId = ref('');
const useFixedCheckpoint = ref(true); // For fixed checkpoint display - whether to include it
const locationMode = ref('none'); // 'gps', 'map', 'none'
const mapVisible = ref(false);
const currentLocation = ref(null);
const locationAccuracy = ref(null);
const locationError = ref(null);
const gettingLocation = ref(false);
const submitting = ref(false);
const errorMessage = ref(null);

const severityOptions = [
  { value: 'low', label: 'Low' },
  { value: 'medium', label: 'Medium' },
  { value: 'high', label: 'High' },
  { value: 'critical', label: 'Critical' },
];

// Get current datetime in local timezone formatted for datetime-local input
const getLocalDateTimeString = () => {
  const now = new Date();
  const year = now.getFullYear();
  const month = String(now.getMonth() + 1).padStart(2, '0');
  const day = String(now.getDate()).padStart(2, '0');
  const hours = String(now.getHours()).padStart(2, '0');
  const minutes = String(now.getMinutes()).padStart(2, '0');
  return `${year}-${month}-${day}T${hours}:${minutes}`;
};

// Convert local datetime string to UTC ISO string
const localToUtc = (localDateTimeString) => {
  if (!localDateTimeString) return null;
  const localDate = new Date(localDateTimeString);
  return localDate.toISOString();
};

const availableCheckpoints = computed(() => {
  return props.checkpoints || [];
});

// Map center for location picker - priority: picked location > selected checkpoint > user GPS > default checkpoint > first checkpoint > route center
const mapCenter = computed(() => {
  // 1. If we have a picked/stored location, center on that
  if (currentLocation.value?.latitude != null) {
    return { lat: currentLocation.value.latitude, lng: currentLocation.value.longitude };
  }
  // 2. If a checkpoint is selected, center on that
  if (selectedCheckpointId.value) {
    const selectedCp = availableCheckpoints.value.find(cp => cp.id === selectedCheckpointId.value);
    if (selectedCp?.latitude != null) {
      return { lat: selectedCp.latitude, lng: selectedCp.longitude };
    }
  }
  // 3. User's GPS location if available
  if (props.initialLocation?.latitude != null) {
    return { lat: props.initialLocation.latitude, lng: props.initialLocation.longitude };
  }
  // 4. Default checkpoint (checked-in checkpoint)
  if (props.checkpoint?.latitude != null) {
    return { lat: props.checkpoint.latitude, lng: props.checkpoint.longitude };
  }
  // 5. First available checkpoint
  if (availableCheckpoints.value.length > 0) {
    const first = availableCheckpoints.value[0];
    if (first.latitude != null) {
      return { lat: first.latitude, lng: first.longitude };
    }
  }
  // 6. Center of route if available
  if (props.route?.length > 0) {
    const first = props.route[0];
    if (first?.lat != null) {
      return { lat: first.lat, lng: first.lng };
    }
  }
  // Default fallback
  return { lat: -33.9, lng: 151.2 };
});

const isDirty = computed(() => {
  return form.value.description.trim() !== '';
});

const isValid = computed(() => {
  return form.value.description.trim() !== '';
});

// Reset form when modal opens
watch(() => props.show, (isShowing) => {
  if (isShowing) {
    form.value = {
      title: '',
      description: '',
      severity: 'medium',
      incidentTime: getLocalDateTimeString(),
    };
    selectedCheckpointId.value = '';
    useFixedCheckpoint.value = true;
    submitting.value = false;
    errorMessage.value = null;
    locationError.value = null;
    currentLocation.value = null;
    locationAccuracy.value = null;

    // Pre-fill location if available
    if (props.initialLocation && props.initialLocation.latitude != null) {
      currentLocation.value = {
        latitude: props.initialLocation.latitude,
        longitude: props.initialLocation.longitude,
      };
      locationAccuracy.value = props.initialLocation.accuracy;
      locationMode.value = 'gps';
      mapVisible.value = false;
    } else {
      locationMode.value = 'none';
      mapVisible.value = false;
    }

    // Pre-select checkpoint if only one available
    if (props.checkpoint) {
      selectedCheckpointId.value = props.checkpoint.locationId || props.checkpoint.id || '';
    }
  }
});

const setLocationMode = (mode) => {
  locationError.value = null;

  if (mode === 'map') {
    if (locationMode.value === 'map') {
      // Toggle map visibility if already in map mode
      mapVisible.value = !mapVisible.value;
    } else {
      // Switch to map mode and show map
      locationMode.value = 'map';
      mapVisible.value = true;
      locationAccuracy.value = null;
    }
  } else if (mode === 'gps') {
    locationMode.value = 'gps';
    mapVisible.value = false;
    getGpsLocation();
  } else {
    locationMode.value = 'none';
    mapVisible.value = false;
    currentLocation.value = null;
    locationAccuracy.value = null;
  }
};

const handleMapClick = (event) => {
  currentLocation.value = {
    latitude: event.lat,
    longitude: event.lng,
  };
  locationAccuracy.value = null;
  // Hide the map after picking a location
  mapVisible.value = false;
};

const getGpsLocation = () => {
  if (props.initialLocation && props.initialLocation.latitude != null) {
    currentLocation.value = {
      latitude: props.initialLocation.latitude,
      longitude: props.initialLocation.longitude,
    };
    locationAccuracy.value = props.initialLocation.accuracy;
    return;
  }

  if (!navigator.geolocation) {
    locationError.value = 'GPS is not available on this device.';
    return;
  }

  gettingLocation.value = true;
  navigator.geolocation.getCurrentPosition(
    (position) => {
      currentLocation.value = {
        latitude: position.coords.latitude,
        longitude: position.coords.longitude,
      };
      locationAccuracy.value = position.coords.accuracy;
      gettingLocation.value = false;
    },
    (error) => {
      locationError.value = 'Unable to get your location.';
      gettingLocation.value = false;
    },
    { enableHighAccuracy: true, timeout: 10000 }
  );
};

const handleSubmit = async () => {
  if (!isValid.value || submitting.value) return;

  submitting.value = true;
  errorMessage.value = null;

  try {
    const hasLocation = (locationMode.value === 'gps' || locationMode.value === 'map') && currentLocation.value;
    // Determine checkpointId:
    // - If dropdown is shown, use selected value (empty string = null, meaning "None")
    // - If fixed checkbox shown, use checkpoint only if checked
    let checkpointId = null;
    let userExplicitlyChoseNoCheckpoint = false;
    if (availableCheckpoints.value.length > 0) {
      // Dropdown is shown - use exactly what user selected
      if (selectedCheckpointId.value) {
        checkpointId = selectedCheckpointId.value;
      } else {
        // User selected "None" explicitly
        userExplicitlyChoseNoCheckpoint = true;
      }
    } else if (props.checkpoint) {
      // Fixed display with checkbox
      if (useFixedCheckpoint.value) {
        checkpointId = props.checkpoint.locationId || props.checkpoint.id || null;
      } else {
        // User unchecked the checkbox explicitly
        userExplicitlyChoseNoCheckpoint = true;
      }
    }
    const incidentData = {
      title: form.value.title.trim() || null,
      description: form.value.description.trim(),
      severity: form.value.severity,
      incidentTime: localToUtc(form.value.incidentTime),
      latitude: hasLocation ? currentLocation.value.latitude : null,
      longitude: hasLocation ? currentLocation.value.longitude : null,
      checkpointId,
      skipCheckpointAutoAssign: userExplicitlyChoseNoCheckpoint,
    };

    emit('submit', incidentData);
  } catch (error) {
    errorMessage.value = error.message || 'Failed to report incident. Please try again.';
    submitting.value = false;
  }
};

const handleClose = () => {
  emit('close');
};
</script>

<style scoped>
.incident-form {
  display: flex;
  flex-direction: column;
  gap: 0.2rem;
}

.form-row {
  display: flex;
  gap: 0.25rem;
}

.form-group {
  display: flex;
  flex-direction: column;
  gap: 0.1rem;
}

.form-group-half {
  flex: 1;
}

.form-label {
  font-size: 0.75rem;
  font-weight: 500;
  color: var(--text-primary);
}

.required {
  color: var(--danger);
}

.form-input {
  padding: 0.3rem 0.5rem;
  border: 1px solid var(--input-border);
  border-radius: 4px;
  background: var(--input-bg);
  color: var(--text-primary);
  font-size: 0.85rem;
  font-family: inherit;
  transition: border-color 0.2s;
}

.form-input:focus {
  outline: none;
  border-color: var(--accent-primary);
}

.form-select {
  cursor: pointer;
  appearance: none;
  background-image: url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='12' height='12' viewBox='0 0 12 12'%3E%3Cpath fill='%23666' d='M6 8L1 3h10z'/%3E%3C/svg%3E");
  background-repeat: no-repeat;
  background-position: right 0.75rem center;
  padding-right: 2rem;
}

/* Severity dropdown styling */
#severity {
  font-weight: 500;
}

#severity option[value="low"] { color: var(--severity-low-text); }
#severity option[value="medium"] { color: var(--severity-medium-text); }
#severity option[value="high"] { color: var(--status-acknowledged); }
#severity option[value="critical"] { color: var(--danger-text); }

.form-textarea {
  resize: vertical;
  min-height: 50px;
}

.checkpoint-display {
  display: flex;
  align-items: center;
  gap: 0.3rem;
  padding: 0.25rem 0.5rem;
  background: var(--bg-secondary);
  border-radius: 4px;
  font-size: 0.8rem;
}

.checkpoint-toggle {
  padding: 0.25rem 0;
}

.checkbox-label {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  cursor: pointer;
  font-size: 0.85rem;
}

.checkbox-label input[type="checkbox"] {
  width: 1rem;
  height: 1rem;
  cursor: pointer;
}

.checkpoint-name {
  font-weight: 500;
  color: var(--text-primary);
}

.checkpoint-description {
  color: var(--text-secondary);
}

.location-options {
  display: flex;
  flex-direction: column;
  gap: 0.2rem;
}

.location-buttons {
  display: flex;
  gap: 0.25rem;
}

.location-btn {
  padding: 0.25rem 0.5rem;
  border: 1px solid var(--border-color);
  border-radius: 4px;
  background: var(--bg-primary);
  color: var(--text-primary);
  cursor: pointer;
  font-size: 0.75rem;
  transition: all 0.2s;
}

.location-btn:hover {
  background: var(--bg-hover);
}

.location-btn.active {
  background: var(--accent-primary);
  color: white;
  border-color: var(--accent-primary);
}

.location-status {
  font-size: 0.75rem;
}

.location-getting {
  color: var(--text-muted);
  font-style: italic;
}

.location-display {
  display: flex;
  align-items: center;
  gap: 0.3rem;
  color: var(--text-secondary);
  font-size: 0.75rem;
}

.location-edit-btn {
  padding: 0.15rem 0.4rem;
  background: var(--bg-secondary);
  border: 1px solid var(--border-color);
  border-radius: 3px;
  color: var(--text-secondary);
  font-size: 0.7rem;
  cursor: pointer;
  transition: all 0.2s;
}

.location-edit-btn:hover {
  background: var(--bg-hover);
  color: var(--text-primary);
}

.location-accuracy {
  color: var(--text-muted);
}

.location-error {
  color: var(--danger);
}

.map-picker-container {
  display: flex;
  flex-direction: column;
  gap: 0.15rem;
}

.map-picker-hint {
  font-size: 0.7rem;
  color: var(--text-muted);
  font-style: italic;
}

.location-picker-map {
  height: 150px;
  border-radius: 4px;
  overflow: hidden;
  border: 1px solid var(--border-color);
}

.error-message {
  padding: 0.25rem 0.5rem;
  background: var(--danger-bg);
  color: var(--danger-text);
  border-radius: 4px;
  font-size: 0.75rem;
}

.modal-actions {
  display: flex;
  justify-content: space-between;
  gap: 0.75rem;
}

.btn {
  padding: 0.5rem 1.25rem;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  font-size: 0.9rem;
  font-weight: 500;
  transition: background-color 0.2s;
}

.btn-primary {
  background: var(--accent-primary);
  color: white;
}

.btn-primary:hover:not(:disabled) {
  background: var(--accent-primary-hover);
}

.btn-primary:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.btn-secondary {
  background: var(--bg-tertiary);
  color: var(--text-primary);
}

.btn-secondary:hover {
  background: var(--bg-hover);
}

@media (max-width: 480px) {
  .form-row {
    flex-direction: column;
  }

  .location-buttons {
    flex-direction: column;
  }
}
</style>
