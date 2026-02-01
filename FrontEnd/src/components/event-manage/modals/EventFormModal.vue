<template>
  <BaseModal
    :show="show"
    :title="isEditing ? 'Edit event' : 'Create new event'"
    size="large"
    @close="handleCloseAttempt"
  >
    <form @submit.prevent="handleSubmit">
      <div class="form-group">
        <label>Event name *</label>
        <input
          v-model="form.name"
          type="text"
          required
          class="form-input"
        />
      </div>

      <div class="form-group">
        <label>Description</label>
        <textarea
          v-model="form.description"
          rows="3"
          class="form-input"
        ></textarea>
      </div>

      <div class="form-group">
        <label>Event date *</label>
        <input
          v-model="form.eventDate"
          type="datetime-local"
          required
          class="form-input"
        />
      </div>

      <div class="form-group">
        <label>Time zone *</label>
        <select
          v-model="form.timeZoneId"
          required
          class="form-input"
        >
          <option
            v-for="tz in timeZoneOptions"
            :key="tz.value"
            :value="tz.value"
          >
            {{ tz.label }}
          </option>
        </select>
      </div>
    </form>

    <!-- Action buttons -->
    <template #actions>
      <button type="button" @click="handleCloseAttempt" class="btn btn-secondary">
        Cancel
      </button>
      <button type="button" @click="handleSubmit" class="btn btn-success">
        Save
      </button>
    </template>
  </BaseModal>

  <!-- Unsaved changes confirmation -->
  <div v-if="showUnsavedConfirm" class="modal-overlay" @click.self="cancelClose">
    <div class="confirm-modal">
      <h3>Unsaved changes</h3>
      <p>You have unsaved changes. What would you like to do?</p>
      <div class="confirm-actions">
        <button @click="cancelClose" class="btn btn-secondary">Cancel</button>
        <button @click="discardChanges" class="btn btn-danger">Discard</button>
        <button @click="saveAndClose" class="btn btn-success">Save</button>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, watch, computed } from 'vue';
import BaseModal from '../../BaseModal.vue';
import { getTimeZonesForDate, DEFAULT_TIME_ZONE } from '../../../constants/timeZones';

const props = defineProps({
  show: {
    type: Boolean,
    default: false,
  },
  eventData: {
    type: Object,
    default: () => ({
      name: '',
      description: '',
      eventDate: '',
      timeZoneId: DEFAULT_TIME_ZONE,
    }),
  },
  isEditing: {
    type: Boolean,
    default: false,
  },
});

const emit = defineEmits(['close', 'submit']);

const form = ref({ ...props.eventData });
const initialFormState = ref(JSON.stringify(props.eventData));
const showUnsavedConfirm = ref(false);

// Compute timezone options based on the selected event date
const timeZoneOptions = computed(() => getTimeZonesForDate(form.value.eventDate));

// Check if form has been modified
const isDirty = computed(() => {
  return JSON.stringify(form.value) !== initialFormState.value;
});

watch(() => props.eventData, (newVal) => {
  form.value = { ...newVal };
  initialFormState.value = JSON.stringify(newVal);
}, { deep: true });

// Reset initial state when modal opens
watch(() => props.show, (newShow) => {
  if (newShow) {
    initialFormState.value = JSON.stringify(form.value);
    showUnsavedConfirm.value = false;
  }
});

const handleSubmit = () => {
  emit('submit', { ...form.value });
};

// Called when user attempts to close (X button, click outside, Cancel button)
const handleCloseAttempt = () => {
  if (isDirty.value) {
    showUnsavedConfirm.value = true;
  } else {
    emit('close');
  }
};

// Cancel the close attempt, return to form
const cancelClose = () => {
  showUnsavedConfirm.value = false;
};

// Discard changes and close
const discardChanges = () => {
  showUnsavedConfirm.value = false;
  form.value = JSON.parse(initialFormState.value);
  emit('close');
};

// Save and close
const saveAndClose = () => {
  showUnsavedConfirm.value = false;
  handleSubmit();
};
</script>

<style scoped>
/* Component uses global styles from src/styles/common.css */

.modal-overlay {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background: var(--modal-overlay);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 3000;
}

.confirm-modal {
  background: var(--modal-bg);
  color: var(--text-primary);
  padding: 2rem;
  border-radius: 12px;
  max-width: 500px;
  width: 90%;
  box-shadow: var(--shadow-lg);
}

.confirm-modal h3 {
  margin: 0 0 1rem 0;
  color: var(--text-primary);
  font-size: 1.25rem;
}

.confirm-modal p {
  margin: 0 0 1.5rem 0;
  color: var(--text-secondary);
  line-height: 1.5;
}

.confirm-actions {
  display: flex;
  gap: 1rem;
  justify-content: flex-end;
}

.btn {
  padding: 0.5rem 1.5rem;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  font-size: 0.9rem;
  font-weight: 500;
  transition: background-color 0.2s;
}

.btn-success {
  background: var(--accent-success);
  color: white;
}

.btn-success:hover {
  background: var(--accent-success-hover, #16a34a);
}

.btn-danger {
  background: #ef4444;
  color: white;
}

.btn-danger:hover {
  background: #dc2626;
}

.btn-secondary {
  background: var(--bg-tertiary);
  color: var(--text-primary);
}

.btn-secondary:hover {
  background: var(--bg-hover);
}
</style>
