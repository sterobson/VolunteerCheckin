<template>
  <BaseModal
    :show="show"
    :title="isEditing ? 'Edit event' : 'Create new event'"
    size="large"
    @close="handleClose"
  >
    <!-- Tabs in header -->
    <template #tab-header>
      <div class="tabs">
        <button
          type="button"
          @click="switchTab('basic')"
          class="tab-button"
          :class="{ active: activeTab === 'basic' }"
        >
          Basic details
        </button>
        <button
          type="button"
          @click="switchTab('emergency')"
          class="tab-button"
          :class="{ active: activeTab === 'emergency' }"
        >
          Emergency contacts
        </button>
      </div>
    </template>

    <!-- Tab content -->
    <form @submit.prevent="handleSubmit">
      <!-- Basic details Tab -->
      <div v-show="activeTab === 'basic'" class="tab-content">
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
              v-for="tz in TIME_ZONES"
              :key="tz.value"
              :value="tz.value"
            >
              {{ tz.label }}
            </option>
          </select>
        </div>
      </div>

      <!-- Emergency contacts tab -->
      <div v-show="activeTab === 'emergency'" class="tab-content">
        <div class="emergency-contacts-section">
          <div class="section-header-small">
            <h3>Emergency contacts</h3>
            <button
              type="button"
              @click="addEmergencyContact"
              class="btn btn-small btn-primary"
            >
              Add contact
            </button>
          </div>

          <div v-if="form.emergencyContacts.length === 0" class="empty-state">
            <p>No emergency contacts added yet. Click "Add contact" to add one.</p>
          </div>

          <div
            v-for="(contact, index) in form.emergencyContacts"
            :key="index"
            class="emergency-contact-card"
          >
            <div class="card-header">
              <h4>Contact {{ index + 1 }}</h4>
              <button
                type="button"
                @click="removeEmergencyContact(index)"
                class="btn-remove"
              >
                ✕
              </button>
            </div>

            <div class="form-group">
              <label>Name *</label>
              <input
                v-model="contact.name"
                type="text"
                required
                class="form-input"
                placeholder="Contact person name"
              />
            </div>

            <div class="form-group">
              <label>Phone *</label>
              <input
                v-model="contact.phone"
                type="tel"
                required
                class="form-input"
                placeholder="+1 (555) 123-4567"
              />
            </div>

            <div class="form-group">
              <label>Additional details</label>
              <textarea
                v-model="contact.details"
                rows="2"
                class="form-input"
                placeholder="Role, availability, or other important information..."
              ></textarea>
            </div>
          </div>
        </div>
      </div>
    </form>

    <!-- Action buttons -->
    <template #actions>
      <button type="button" @click="handleClose" class="btn btn-secondary">
        Cancel
      </button>
      <button type="button" @click="handleSubmit" class="btn btn-primary">
        {{ isEditing ? 'Update' : 'Create' }}
      </button>
    </template>
  </BaseModal>
</template>

<script setup>
import { ref, defineProps, defineEmits, watch } from 'vue';
import BaseModal from '../../BaseModal.vue';
import { useTabs } from '../../../composables/useTabs';
import { TIME_ZONES } from '../../../constants/timeZones';

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
      timeZoneId: 'UTC',
      emergencyContacts: [],
    }),
  },
  isEditing: {
    type: Boolean,
    default: false,
  },
});

const emit = defineEmits(['close', 'submit']);

// Use composable for tab management
const { activeTab, switchTab, resetTab } = useTabs('basic', ['basic', 'emergency']);

const form = ref({ ...props.eventData });

watch(() => props.eventData, (newVal) => {
  form.value = { ...newVal };
  // Ensure emergencyContacts is always an array
  if (!form.value.emergencyContacts) {
    form.value.emergencyContacts = [];
  }
}, { deep: true });

watch(() => props.show, (newVal) => {
  if (newVal) {
    // Reset to basic tab when opening
    resetTab();
  }
});

const addEmergencyContact = () => {
  form.value.emergencyContacts.push({
    name: '',
    phone: '',
    details: '',
  });
};

const removeEmergencyContact = (index) => {
  form.value.emergencyContacts.splice(index, 1);
};

const handleSubmit = () => {
  emit('submit', { ...form.value });
};

const handleClose = () => {
  emit('close');
};
</script>

<style scoped>
/* Global styles now in src/styles/common.css */
/* Only component-specific styles remain here */

.tabs {
  display: flex;
  gap: 0.5rem;
  padding: 0.5rem 0;
}

.tab-button {
  padding: 0.5rem 1rem;
  border: none;
  background: transparent;
  color: var(--text-secondary);
  cursor: pointer;
  font-size: 0.9rem;
  border-bottom: 2px solid transparent;
  transition: all 0.2s;
}

.tab-button:hover {
  color: var(--text-dark);
}

.tab-button.active {
  color: var(--accent-primary);
  border-bottom-color: var(--accent-primary);
  font-weight: 500;
}

.tab-content {
  padding-top: 0.5rem;
}

.emergency-contacts-section {
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
}

.section-header-small {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.section-header-small h3 {
  margin: 0;
  font-size: 1rem;
  color: var(--text-dark);
}

.emergency-contact-card {
  padding: 1.5rem;
  background: var(--bg-secondary);
  border-radius: 8px;
  border: 1px solid var(--border-color);
}

.card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 1rem;
}

.card-header h4 {
  margin: 0;
  font-size: 0.95rem;
  color: var(--text-dark);
}

.btn-remove {
  background: none;
  border: none;
  color: var(--danger);
  cursor: pointer;
  font-size: 1.25rem;
  padding: 0.25rem 0.5rem;
  border-radius: 4px;
  transition: background-color 0.2s;
}

.btn-remove:hover {
  background: var(--danger-bg);
}
</style>
