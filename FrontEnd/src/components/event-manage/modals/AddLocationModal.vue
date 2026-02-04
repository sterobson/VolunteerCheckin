<template>
  <BaseModal
    :show="show"
    :title="`Add ${termsLower.checkpoint}`"
    size="medium"
    :confirm-on-close="true"
    :is-dirty="isDirty"
    @close="handleClose"
  >
    <!-- Form -->
    <form @submit.prevent="handleSubmit">
      <div class="form-group">
        <label>Name</label>
        <input
          v-model="formData.name"
          type="text"
          required
          class="form-input"
          @input="handleInput"
        />
      </div>

      <div class="form-group">
        <label>Description</label>
        <input
          v-model="formData.description"
          type="text"
          class="form-input"
          @input="handleInput"
        />
      </div>

      <div class="form-row">
        <div class="form-group" style="flex: 1;">
          <label>Longitude</label>
          <input
            v-model.number="formData.longitude"
            type="number"
            step="0.00001"
            required
            class="form-input"
            @input="handleInput"
          />
        </div>

        <div class="form-group" style="flex: 1;">
          <label>Latitude</label>
          <input
            v-model.number="formData.latitude"
            type="number"
            step="0.00001"
            required
            class="form-input"
            @input="handleInput"
          />
        </div>

        <div class="form-group" style="flex: 0 0 auto;">
          <label>&nbsp;</label>
          <button
            type="button"
            class="icon-btn"
            title="Set location on map"
            @click="$emit('set-on-map')"
          >
            📍
          </button>
        </div>
      </div>

      <div class="form-group">
        <label>Required {{ termsLower.people }}</label>
        <input
          v-model.number="formData.requiredMarshals"
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

const { terms, termsLower } = useTerminology();

const props = defineProps({
  show: {
    type: Boolean,
    default: false,
  },
  location: {
    type: Object,
    default: () => ({
      name: '',
      description: '',
      latitude: 0,
      longitude: 0,
      requiredMarshals: 1,
      what3Words: '',
    }),
  },
  isDirty: {
    type: Boolean,
    default: false,
  },
});

const emit = defineEmits(['close', 'save', 'set-on-map', 'update:isDirty', 'update:location']);

const formData = ref({
  name: '',
  description: '',
  latitude: 0,
  longitude: 0,
  requiredMarshals: 1,
  what3Words: '',
});

// Watch for location changes to update form
watch(
  () => props.location,
  (newLocation) => {
    if (newLocation) {
      formData.value = { ...newLocation };
    }
  },
  { immediate: true, deep: true }
);

const handleInput = () => {
  emit('update:isDirty', true);
  // Emit form data changes back to parent
  emit('update:location', { ...formData.value });
};

const handleSubmit = () => {
  emit('save', formData.value);
};

const handleClose = () => {
  emit('close');
};
</script>

<style scoped>
.form-group {
  margin-bottom: 1rem;
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
  font-size: 1rem;
  background: var(--input-bg);
  color: var(--text-primary);
}

.form-input:focus {
  outline: none;
  border-color: var(--input-focus-border);
}

.form-row {
  display: flex;
  gap: 1rem;
}

.form-error {
  color: var(--danger);
  font-size: 0.85rem;
  margin-top: 0.25rem;
  display: block;
}

.input-with-icon {
  display: flex;
  gap: 0.5rem;
  align-items: stretch;
}

.input-with-icon .form-input {
  flex: 1;
}

.icon-btn {
  padding: 0.5rem 0.75rem;
  border: 1px solid var(--brand-primary);
  background: var(--card-bg);
  border-radius: 4px;
  cursor: pointer;
  font-size: 1.2rem;
  transition: all 0.2s;
  flex-shrink: 0;
  height: fit-content;
}

.icon-btn:hover {
  background: var(--brand-primary);
  transform: scale(1.05);
}

.form-row .form-group:last-child {
  display: flex;
  flex-direction: column;
  justify-content: flex-end;
}

.btn {
  padding: 0.5rem 1rem;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  font-size: 1rem;
  font-weight: 500;
  transition: background-color 0.2s;
}

.btn-secondary {
  background: var(--btn-secondary-bg);
  color: var(--btn-secondary-text);
}

.btn-secondary:hover {
  background: var(--btn-secondary-hover);
}

.btn-primary {
  background: var(--btn-primary-bg);
  color: var(--btn-primary-text);
}

.btn-primary:hover {
  background: var(--btn-primary-hover);
}
</style>
