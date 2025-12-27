<template>
  <div class="event-details-tab">
    <div class="details-container">
      <div class="details-section">
        <h2>Event Information</h2>
        <form @submit.prevent="handleSubmit" class="event-details-form">
          <div class="form-group">
            <label>Event Name</label>
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

          <div class="form-group">
            <label>Event Date & Time</label>
            <input
              v-model="localForm.eventDate"
              type="datetime-local"
              required
              class="form-input"
              @input="emitFormChange"
            />
          </div>

          <div class="form-group">
            <label>Time Zone</label>
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

          <div class="form-actions">
            <button type="submit" class="btn btn-primary" :disabled="!formDirty">
              Save Changes
            </button>
          </div>
        </form>
      </div>

      <div class="details-section">
        <AdminsList
          :admins="admins"
          @add-admin="$emit('add-admin')"
          @remove-admin="$emit('remove-admin', $event)"
        />
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, watch, defineProps, defineEmits } from 'vue';
import { TIME_ZONES } from '../../constants/timeZones';
import AdminsList from '../../components/event-manage/lists/AdminsList.vue';

const props = defineProps({
  eventData: {
    type: Object,
    required: true,
  },
  admins: {
    type: Array,
    default: () => [],
  },
  formDirty: {
    type: Boolean,
    default: false,
  },
});

const emit = defineEmits([
  'submit',
  'add-admin',
  'remove-admin',
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

.details-container {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 2rem;
}

.details-section {
  background: white;
  border-radius: 8px;
  padding: 2rem;
  box-shadow: 0 2px 4px rgba(0, 0, 0, 0.1);
}

.details-section h2 {
  margin: 0 0 1.5rem 0;
  font-size: 1.25rem;
  color: #333;
  padding-bottom: 1rem;
  border-bottom: 1px solid #dee2e6;
}

.event-details-form {
  display: flex;
  flex-direction: column;
  gap: 0;
}

.form-actions {
  margin-top: 1rem;
  padding-top: 1rem;
  border-top: 1px solid #dee2e6;
}

@media (max-width: 1024px) {
  .details-container {
    grid-template-columns: 1fr;
  }
}
</style>
