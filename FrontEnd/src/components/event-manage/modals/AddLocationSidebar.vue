<template>
  <BaseModal
    :show="show"
    title="Add location"
    variant="sidebar"
    :confirm-on-close="true"
    :is-dirty="isDirty"
    @close="handleClose"
  >
    <!-- Instruction -->
    <p class="instruction">
      <strong>👆 Click on the map</strong> to set the location, or enter coordinates manually
    </p>

    <div v-if="form.latitude !== 0 && form.longitude !== 0" class="location-set-notice">
      ✓ Location set on map
    </div>

    <!-- Form -->
    <form @submit.prevent="handleSubmit">
      <div class="form-group">
        <label>Name</label>
        <input
          v-model="form.name"
          type="text"
          required
          class="form-input"
          @input="handleInput"
        />
      </div>

      <div class="form-group">
        <label>Description</label>
        <input
          v-model="form.description"
          type="text"
          class="form-input"
          @input="handleInput"
        />
      </div>

      <div class="form-group">
        <label>What3Words (optional)</label>
        <input
          v-model="form.what3Words"
          type="text"
          class="form-input"
          placeholder="e.g. filled.count.soap or filled/count/soap"
          @input="handleInput"
        />
        <small v-if="form.what3Words && !isValidWhat3Words(form.what3Words)" class="form-error">
          Invalid format. Must be word.word.word or word/word/word (lowercase letters only, 1-20 characters each)
        </small>
      </div>

      <div class="form-group checkbox-group">
        <label class="checkbox-label">
          <input
            v-model="form.useCustomTimes"
            type="checkbox"
            @change="handleCustomTimesToggle"
          />
          <span>Use custom date/time for this checkpoint</span>
        </label>
        <small class="form-help">
          By default, marshals are expected during the event date/time. Enable this to set a specific time range.
        </small>
      </div>

      <div v-if="form.useCustomTimes" class="custom-times-section">
        <div class="form-group">
          <label>Start date & time (optional)</label>
          <input
            v-model="form.startTime"
            type="datetime-local"
            class="form-input"
            @input="handleInput"
          />
          <small class="form-help">
            When marshal should arrive at this checkpoint
          </small>
        </div>

        <div class="form-group">
          <label>End date & time (optional)</label>
          <input
            v-model="form.endTime"
            type="datetime-local"
            class="form-input"
            @input="handleInput"
          />
          <small class="form-help">
            When marshal can leave this checkpoint
          </small>
        </div>
      </div>

      <div class="form-row">
        <div class="form-group">
          <label>Latitude</label>
          <input
            v-model.number="form.latitude"
            type="number"
            step="any"
            required
            class="form-input"
            @input="handleInput"
          />
        </div>

        <div class="form-group">
          <label>Longitude</label>
          <input
            v-model.number="form.longitude"
            type="number"
            step="any"
            required
            class="form-input"
            @input="handleInput"
          />
        </div>
      </div>

      <div class="form-group">
        <label>Required marshals</label>
        <input
          v-model.number="form.requiredMarshals"
          type="number"
          min="1"
          required
          class="form-input"
          @input="handleInput"
        />
      </div>
    </form>

    <!-- Action buttons -->
    <template #actions>
      <button type="button" @click="handleClose" class="btn btn-secondary">
        Cancel
      </button>
      <button type="button" @click="handleSubmit" class="btn btn-primary">
        Add {{ termsLower.checkpoint }}
      </button>
    </template>
  </BaseModal>
</template>

<script setup>
import { ref, defineProps, defineEmits, watch } from 'vue';
import BaseModal from '../../BaseModal.vue';
import { useTerminology } from '../../../composables/useTerminology';

const { termsLower } = useTerminology();

const props = defineProps({
  show: {
    type: Boolean,
    default: false,
  },
  locationData: {
    type: Object,
    default: () => ({
      name: '',
      description: '',
      what3Words: '',
      latitude: 0,
      longitude: 0,
      requiredMarshals: 1,
      useCustomTimes: false,
      startTime: '',
      endTime: '',
    }),
  },
  isDirty: {
    type: Boolean,
    default: false,
  },
});

const emit = defineEmits(['close', 'submit', 'update:isDirty']);

const form = ref({ ...props.locationData });

watch(() => props.locationData, (newVal) => {
  form.value = { ...newVal };
}, { deep: true });

watch(() => props.show, (newVal) => {
  if (!newVal) {
    // Reset form when modal closes
    form.value = {
      name: '',
      description: '',
      what3Words: '',
      latitude: 0,
      longitude: 0,
      requiredMarshals: 1,
      useCustomTimes: false,
      startTime: '',
      endTime: '',
    };
  }
});

const isValidWhat3Words = (value) => {
  if (!value) return true;

  // Must be 3 words separated by . or /
  const parts = value.includes('.') ? value.split('.') : value.split('/');
  if (parts.length !== 3) return false;

  // Each word must be 1-20 lowercase letters
  const wordRegex = /^[a-z]{1,20}$/;
  return parts.every(part => wordRegex.test(part));
};

const formatDateTimeLocal = (date) => {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  const hours = String(date.getHours()).padStart(2, '0');
  const minutes = String(date.getMinutes()).padStart(2, '0');
  return `${year}-${month}-${day}T${hours}:${minutes}`;
};

const handleCustomTimesToggle = () => {
  if (!form.value.useCustomTimes) {
    // Clear times when disabling custom times
    form.value.startTime = '';
    form.value.endTime = '';
  }
  handleInput();
};

const handleInput = () => {
  emit('update:isDirty', true);
};

const handleSubmit = () => {
  if (form.value.name && form.value.latitude !== 0 && form.value.longitude !== 0) {
    // Convert local datetime strings back to UTC DateTime objects
    const formData = {
      ...form.value,
      startTime: form.value.useCustomTimes && form.value.startTime
        ? new Date(form.value.startTime).toISOString()
        : null,
      endTime: form.value.useCustomTimes && form.value.endTime
        ? new Date(form.value.endTime).toISOString()
        : null,
    };

    emit('submit', formData);
  }
};

const handleClose = () => {
  emit('close');
};
</script>

<style scoped>
.instruction {
  color: var(--text-secondary);
  margin-bottom: 1rem;
}

.location-set-notice {
  background: var(--success-bg);
  border: 1px solid var(--success-border);
  color: var(--success-text);
  padding: 0.75rem;
  border-radius: 4px;
  margin-bottom: 1.5rem;
  text-align: center;
  font-weight: 500;
}

.form-group {
  margin-bottom: 1.5rem;
}

.form-group label {
  display: block;
  margin-bottom: 0.5rem;
  font-weight: 500;
  color: var(--text-dark);
}

.form-input {
  width: 100%;
  padding: 0.5rem;
  border: 1px solid var(--border-medium);
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

.checkbox-group {
  margin-bottom: 0.5rem;
}

.checkbox-group .checkbox-label {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  cursor: pointer;
  font-weight: normal;
  padding: 0.5rem 0;
  margin-bottom: 0;
}

.checkbox-group .checkbox-label input[type="checkbox"] {
  width: 1.125rem;
  height: 1.125rem;
  cursor: pointer;
  flex-shrink: 0;
  margin: 0;
}

.checkbox-group .checkbox-label span {
  line-height: 1.4;
}

.form-help {
  display: block;
  color: var(--text-secondary);
  font-size: 0.85rem;
  margin-top: 0.25rem;
  font-weight: normal;
}

.custom-times-section {
  margin-left: 1.5rem;
  padding-left: 1rem;
  border-left: 3px solid var(--brand-primary);
  background: var(--bg-secondary);
  padding: 1rem;
  border-radius: 4px;
  margin-top: 0.5rem;
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
  background: var(--btn-primary-bg);
  color: var(--btn-primary-text);
}

.btn-primary:hover {
  background: var(--btn-primary-hover);
}

.btn-secondary {
  background: var(--btn-secondary-bg);
  color: var(--btn-secondary-text);
}

.btn-secondary:hover {
  background: var(--btn-secondary-hover);
}
</style>
