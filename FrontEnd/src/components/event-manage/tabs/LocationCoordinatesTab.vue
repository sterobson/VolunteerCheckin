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

      <div class="form-row">
        <div class="form-group">
          <label>Latitude</label>
          <input
            :value="form.latitude"
            @input="handleNumberInput('latitude', $event.target.value)"
            type="number"
            step="any"
            required
            class="form-input"
            :disabled="isMoving"
            placeholder="e.g., 51.505123"
          />
        </div>

        <div class="form-group">
          <label>Longitude</label>
          <input
            :value="form.longitude"
            @input="handleNumberInput('longitude', $event.target.value)"
            type="number"
            step="any"
            required
            class="form-input"
            :disabled="isMoving"
            placeholder="e.g., -0.091234"
          />
        </div>
      </div>

      <div class="form-group">
        <button
          type="button"
          @click="$emit('move-location')"
          class="btn btn-secondary"
          :class="{ 'btn-primary': isMoving }"
        >
          {{ isMoving ? 'Click on map to set new location...' : 'Move checkpoint...' }}
        </button>
      </div>
    </form>
  </div>
</template>

<script setup>
import { defineProps, defineEmits } from 'vue';

const props = defineProps({
  form: {
    type: Object,
    required: true,
  },
  isMoving: {
    type: Boolean,
    default: false,
  },
});

const emit = defineEmits(['update:form', 'input', 'save', 'move-location']);

const handleInput = (field, value) => {
  emit('update:form', { ...props.form, [field]: value });
  emit('input');
};

const handleNumberInput = (field, value) => {
  emit('update:form', { ...props.form, [field]: Number(value) });
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
  color: #333;
}

.form-input {
  width: 100%;
  padding: 0.5rem;
  border: 1px solid #ddd;
  border-radius: 4px;
  font-size: 0.9rem;
  box-sizing: border-box;
}

.form-error {
  display: block;
  color: #dc3545;
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
  background: #007bff;
  color: white;
}

.btn-primary:hover {
  background: #0056b3;
}

.btn-secondary {
  background: #6c757d;
  color: white;
}

.btn-secondary:hover {
  background: #545b62;
}
</style>
