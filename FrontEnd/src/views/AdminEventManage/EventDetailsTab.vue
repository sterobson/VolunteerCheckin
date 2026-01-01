<template>
  <div class="event-details-tab">
    <div class="accordion">
      <!-- Event Information Section -->
      <div class="accordion-section">
        <button
          class="accordion-header"
          :class="{ active: expandedSection === 'info' }"
          @click="toggleSection('info')"
        >
          <span class="accordion-title">Event Information</span>
          <span class="accordion-icon">{{ expandedSection === 'info' ? '−' : '+' }}</span>
        </button>
        <div v-if="expandedSection === 'info'" class="accordion-content">
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
      </div>

      <!-- Administrators Section -->
      <div class="accordion-section">
        <button
          class="accordion-header"
          :class="{ active: expandedSection === 'admins' }"
          @click="toggleSection('admins')"
        >
          <span class="accordion-title">Administrators ({{ admins.length }})</span>
          <span class="accordion-icon">{{ expandedSection === 'admins' ? '−' : '+' }}</span>
        </button>
        <div v-if="expandedSection === 'admins'" class="accordion-content">
          <AdminsList
            :admins="admins"
            @add-admin="$emit('add-admin')"
            @remove-admin="$emit('remove-admin', $event)"
          />
        </div>
      </div>

      <!-- Terminology Settings Section -->
      <div class="accordion-section">
        <button
          class="accordion-header"
          :class="{ active: expandedSection === 'terminology' }"
          @click="toggleSection('terminology')"
        >
          <span class="accordion-title">Terminology Settings</span>
          <span class="accordion-icon">{{ expandedSection === 'terminology' ? '−' : '+' }}</span>
        </button>
        <div v-if="expandedSection === 'terminology'" class="accordion-content">
          <p class="section-description">
            Customize how things are named throughout the application.
          </p>

          <div class="terminology-grid">
            <div class="term-group">
              <label>{{ terms.people }} are called</label>
              <select
                v-model="localForm.peopleTerm"
                class="form-input"
                @change="emitFormChange"
              >
                <option v-for="option in terminologyOptions.people" :key="option" :value="option">
                  {{ option }}
                </option>
              </select>
            </div>

            <div class="term-group">
              <label>{{ terms.checkpoints }} are called</label>
              <select
                v-model="localForm.checkpointTerm"
                class="form-input"
                @change="emitFormChange"
              >
                <option v-for="option in terminologyOptions.checkpoint" :key="option" :value="option">
                  {{ option }}
                </option>
              </select>
            </div>

            <div class="term-group">
              <label>{{ terms.areas }} are called</label>
              <select
                v-model="localForm.areaTerm"
                class="form-input"
                @change="emitFormChange"
              >
                <option v-for="option in terminologyOptions.area" :key="option" :value="option">
                  {{ option }}
                </option>
              </select>
            </div>

            <div class="term-group">
              <label>{{ terms.checklists }} are called</label>
              <select
                v-model="localForm.checklistTerm"
                class="form-input"
                @change="emitFormChange"
              >
                <option v-for="option in terminologyOptions.checklist" :key="option" :value="option">
                  {{ option }}
                </option>
              </select>
            </div>
          </div>

          <div class="form-actions">
            <button @click="handleSubmit" class="btn btn-primary" :disabled="!formDirty">
              Save Changes
            </button>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, watch, defineProps, defineEmits } from 'vue';
import { TIME_ZONES } from '../../constants/timeZones';
import { terminologyOptions, useTerminology } from '../../composables/useTerminology';
import AdminsList from '../../components/event-manage/lists/AdminsList.vue';

const { terms } = useTerminology();

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
const expandedSection = ref(null);

const toggleSection = (section) => {
  expandedSection.value = expandedSection.value === section ? null : section;
};

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

.accordion {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.accordion-section {
  background: white;
  border-radius: 8px;
  box-shadow: 0 2px 4px rgba(0, 0, 0, 0.1);
  overflow: hidden;
}

.accordion-header {
  width: 100%;
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 1.25rem 1.5rem;
  background: white;
  border: none;
  cursor: pointer;
  text-align: left;
  font-size: 1.1rem;
  font-weight: 600;
  color: #333;
  transition: background 0.2s;
}

.accordion-header:hover {
  background: #f8f9fa;
}

.accordion-header.active {
  background: #f0f4ff;
  color: #667eea;
}

.accordion-title {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.accordion-icon {
  font-size: 1.5rem;
  font-weight: 300;
  color: #667eea;
}

.accordion-content {
  padding: 1.5rem;
  border-top: 1px solid #e0e0e0;
}

.event-details-form {
  display: flex;
  flex-direction: column;
  gap: 0;
}

.form-actions {
  margin-top: 1.5rem;
  padding-top: 1rem;
  border-top: 1px solid #dee2e6;
}

.section-description {
  color: #666;
  font-size: 0.9rem;
  margin: 0 0 1.5rem 0;
}

.terminology-grid {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: 1.5rem;
}

.term-group {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.term-group label {
  font-weight: 500;
  color: #333;
  font-size: 0.9rem;
}

@media (max-width: 600px) {
  .terminology-grid {
    grid-template-columns: 1fr;
  }

  .accordion-header {
    padding: 1rem;
  }

  .accordion-content {
    padding: 1rem;
  }
}
</style>
