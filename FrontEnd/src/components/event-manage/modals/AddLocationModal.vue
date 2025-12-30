<template>
  <BaseModal
    :show="show"
    title="Add checkpoint"
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

      <div class="form-group">
        <label>What3Words (optional)</label>
        <input
          v-model="formData.what3Words"
          type="text"
          class="form-input"
          placeholder="e.g. filled.count.soap or filled/count/soap"
          @input="handleInput"
        />
        <small v-if="formData.what3Words && !isValidWhat3Words(formData.what3Words)" class="form-error">
          Invalid format. Must be word.word.word or word/word/word (lowercase letters only, 1-20 characters each)
        </small>
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
        <label>Required marshals</label>
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
        Add checkpoint
      </button>
    </template>
  </BaseModal>
</template>

<script setup>
import { ref, defineProps, defineEmits, watch } from 'vue';
import BaseModal from '../../BaseModal.vue';
import { isValidWhat3Words } from '../../../utils/validators';

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
  if (!isValidWhat3Words(formData.value.what3Words) && formData.value.what3Words) {
    alert('Invalid What3Words format.');
    return;
  }
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
  color: #333;
}

.form-input {
  width: 100%;
  padding: 0.5rem;
  border: 1px solid #ddd;
  border-radius: 4px;
  font-size: 1rem;
}

.form-input:focus {
  outline: none;
  border-color: #007bff;
}

.form-row {
  display: flex;
  gap: 1rem;
}

.form-error {
  color: #dc3545;
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
  border: 1px solid #007bff;
  background: white;
  border-radius: 4px;
  cursor: pointer;
  font-size: 1.2rem;
  transition: all 0.2s;
  flex-shrink: 0;
  height: fit-content;
}

.icon-btn:hover {
  background: #007bff;
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
  background: #6c757d;
  color: white;
}

.btn-secondary:hover {
  background: #5a6268;
}

.btn-primary {
  background: #007bff;
  color: white;
}

.btn-primary:hover {
  background: #0056b3;
}
</style>
