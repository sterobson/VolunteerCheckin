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

      <!-- Marshal tier selector (only shown when creating, not editing) -->
      <div v-if="!isEditing && showMarshalTier" class="form-group">
        <label>{{ peopleTerm }} tier *</label>
        <p class="tier-description">
          Base price is &pound;10 and includes up to 10 {{ peopleTermLower }}. Additional {{ peopleTermLower }} are 50p each.
        </p>
        <div class="tier-options">
          <label
            v-for="tier in tierOptions"
            :key="tier.value"
            class="tier-option"
            :class="{ selected: form.marshalTier === tier.value }"
          >
            <input
              type="radio"
              v-model="form.marshalTier"
              :value="tier.value"
              name="marshalTier"
            />
            <span class="tier-label">{{ tier.label }}</span>
            <span class="tier-price">{{ tier.price }}</span>
          </label>
        </div>
        <div class="custom-tier" v-if="form.marshalTier === 'custom'">
          <input
            v-model.number="form.customMarshalTier"
            type="number"
            min="10"
            step="5"
            class="form-input"
            placeholder="Enter number of marshals"
          />
          <span v-if="customTierPrice" class="custom-price">{{ customTierPrice }}</span>
        </div>
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
  showMarshalTier: {
    type: Boolean,
    default: false,
  },
  peopleTerm: {
    type: String,
    default: 'marshals',
  },
});

const emit = defineEmits(['close', 'submit']);

const form = ref({
  ...props.eventData,
  marshalTier: 10,
  customMarshalTier: 25,
});
const initialFormState = ref(JSON.stringify({ ...props.eventData, marshalTier: 10, customMarshalTier: 25 }));
const showUnsavedConfirm = ref(false);

const peopleTermLower = computed(() => props.peopleTerm.toLowerCase());

const tierOptions = [
  { value: 10, label: '10', price: '£10' },
  { value: 25, label: '25', price: '£17.50' },
  { value: 50, label: '50', price: '£30' },
  { value: 100, label: '100', price: '£55' },
  { value: 'custom', label: 'Custom', price: '' },
];

const customTierPrice = computed(() => {
  const count = form.value.customMarshalTier;
  if (!count || count < 10) return '';
  const base = 1000;
  const extra = Math.max(0, count - 10) * 50;
  const total = (base + extra) / 100;
  return `£${total.toFixed(2)}`;
});

// Compute timezone options based on the selected event date
const timeZoneOptions = computed(() => getTimeZonesForDate(form.value.eventDate));

// Check if form has been modified
const isDirty = computed(() => {
  return JSON.stringify(form.value) !== initialFormState.value;
});

watch(() => props.eventData, (newVal) => {
  form.value = { ...newVal, marshalTier: form.value.marshalTier, customMarshalTier: form.value.customMarshalTier };
  initialFormState.value = JSON.stringify(form.value);
}, { deep: true });

// Reset initial state when modal opens
watch(() => props.show, (newShow) => {
  if (newShow) {
    initialFormState.value = JSON.stringify(form.value);
    showUnsavedConfirm.value = false;
  }
});

const handleSubmit = () => {
  const data = { ...form.value };
  // Resolve the actual marshal tier number
  if (props.showMarshalTier) {
    data.resolvedMarshalTier = data.marshalTier === 'custom'
      ? Math.max(10, data.customMarshalTier || 10)
      : data.marshalTier;
  }
  emit('submit', data);
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

.tier-description {
  font-size: 0.85rem;
  color: var(--text-secondary);
  margin-bottom: 0.75rem;
}

.tier-options {
  display: flex;
  gap: 0.5rem;
  flex-wrap: wrap;
}

.tier-option {
  display: flex;
  flex-direction: column;
  align-items: center;
  padding: 0.75rem 1rem;
  border: 2px solid var(--border-color, #e0e0e0);
  border-radius: 8px;
  cursor: pointer;
  transition: all 0.2s;
  flex: 1;
  min-width: 70px;
  text-align: center;
}

.tier-option:hover {
  border-color: #667eea;
}

.tier-option.selected {
  border-color: #667eea;
  background: rgba(102, 126, 234, 0.08);
}

.tier-option input[type="radio"] {
  display: none;
}

.tier-label {
  font-weight: 600;
  font-size: 1rem;
  color: var(--text-primary);
}

.tier-price {
  font-size: 0.8rem;
  color: var(--text-secondary);
  margin-top: 0.25rem;
}

.custom-tier {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  margin-top: 0.75rem;
}

.custom-tier .form-input {
  max-width: 200px;
}

.custom-price {
  font-weight: 600;
  color: var(--text-primary);
}
</style>
