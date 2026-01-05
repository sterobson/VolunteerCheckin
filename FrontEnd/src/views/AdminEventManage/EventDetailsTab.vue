<template>
  <div class="event-details-tab">
    <div class="event-info-card">
      <form @submit.prevent="handleSubmit" class="event-details-form">
        <div class="form-group">
          <label>Event name</label>
          <input
            v-model="localForm.name"
            type="text"
            required
            class="form-input"
            @input="emitFormChange"
          />
        </div>

        <div class="form-group">
          <label>Description</label>
          <textarea
            v-model="localForm.description"
            rows="3"
            class="form-input"
            @input="emitFormChange"
          ></textarea>
        </div>

        <div class="form-row">
          <div class="form-group">
            <label>Event date & time</label>
            <input
              v-model="localForm.eventDate"
              type="datetime-local"
              required
              class="form-input"
              @input="emitFormChange"
            />
          </div>

          <div class="form-group">
            <label>Time zone</label>
            <select
              v-model="localForm.timeZoneId"
              required
              class="form-input"
              @change="emitFormChange"
            >
              <option
                v-for="tz in TIME_ZONES"
                :key="tz.value"
                :value="tz.value"
              >
                {{ tz.label }}
              </option>
            </select>
          </div>
        </div>

        <div class="form-actions">
          <button type="submit" class="btn btn-primary" :disabled="!formDirty">
            Save Changes
          </button>
        </div>
      </form>
    </div>
  </div>
</template>

<script setup>
import { ref, watch, defineProps, defineEmits } from 'vue';
import { TIME_ZONES } from '../../constants/timeZones';

const props = defineProps({
  eventData: {
    type: Object,
    required: true,
  },
  formDirty: {
    type: Boolean,
    default: false,
  },
});

const emit = defineEmits([
  'submit',
  'update:formDirty',
]);

const localForm = ref({ ...props.eventData });

watch(() => props.eventData, (newVal) => {
  localForm.value = { ...newVal };
}, { deep: true });

const emitFormChange = () => {
  emit('update:formDirty', true);
};

const handleSubmit = () => {
  emit('submit', { ...localForm.value });
};
</script>

<style scoped>
.event-details-tab {
  width: 100%;
}

.event-info-card {
  background: white;
  border-radius: 8px;
  box-shadow: 0 2px 4px rgba(0, 0, 0, 0.1);
  padding: 1.5rem;
}

.event-details-form {
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
}

.form-row {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 1.5rem;
}

.form-group {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.form-group label {
  font-weight: 500;
  color: #333;
  font-size: 0.9rem;
}

.form-input {
  width: 100%;
  padding: 0.5rem;
  border: 1px solid #ddd;
  border-radius: 4px;
  font-size: 0.9rem;
  box-sizing: border-box;
}

textarea.form-input {
  resize: vertical;
}

.form-actions {
  margin-top: 0.5rem;
  padding-top: 1rem;
  border-top: 1px solid #dee2e6;
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

.btn-primary:disabled {
  background: #ccc;
  cursor: not-allowed;
}

@media (max-width: 600px) {
  .form-row {
    grid-template-columns: 1fr;
  }

  .event-info-card {
    padding: 1rem;
  }
}
</style>
