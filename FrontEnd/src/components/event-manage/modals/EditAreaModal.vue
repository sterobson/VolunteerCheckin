<template>
  <BaseModal
    :show="show"
    :title="area && area.id ? `Edit area: ${area.name}` : 'Create new area'"
    size="large"
    :confirm-on-close="true"
    :is-dirty="isDirty"
    @close="handleClose"
  >
    <!-- Tabs in header -->
    <template #tab-header>
      <div class="tabs">
        <button
          class="tab-button"
          :class="{ active: activeTab === 'details' }"
          @click="activeTab = 'details'"
          type="button"
        >
          Details
        </button>
        <button
          class="tab-button"
          :class="{ active: activeTab === 'boundary' }"
          @click="activeTab = 'boundary'"
          type="button"
        >
          Boundary
        </button>
        <button
          class="tab-button"
          :class="{ active: activeTab === 'contacts' }"
          @click="activeTab = 'contacts'"
          type="button"
        >
          Contacts
        </button>
      </div>
    </template>

    <!-- Details Tab -->
    <div v-if="activeTab === 'details'" class="tab-content">
      <form @submit.prevent="handleSave">
        <div class="form-group">
          <label>Name *</label>
          <input
            v-model="form.name"
            type="text"
            required
            class="form-input"
            :disabled="form.isDefault"
            @input="handleInput"
          />
        </div>

        <div class="form-group">
          <label>Description</label>
          <textarea
            v-model="form.description"
            class="form-input"
            rows="3"
            @input="handleInput"
          ></textarea>
        </div>

        <div class="form-group">
          <label>Color *</label>
          <div class="color-picker">
            <div
              v-for="colorOption in AREA_COLORS"
              :key="colorOption.hex"
              class="color-swatch"
              :style="{ backgroundColor: colorOption.hex }"
              :class="{ selected: form.color === colorOption.hex }"
              :title="colorOption.name"
              @click="selectColor(colorOption.hex)"
            />
          </div>
        </div>
      </form>
    </div>

    <!-- Boundary Tab -->
    <div v-if="activeTab === 'boundary'" class="tab-content">
      <div class="boundary-section">
        <div class="instructions-card">
          <p>
            <strong>Draw a polygon on the map to define this area's boundary</strong>
          </p>
          <p>
            Click "Draw on Map" to activate drawing mode. Click on the map to add vertices,
            and double-click to complete the polygon.
          </p>
        </div>

        <div class="boundary-actions">
          <button @click="handleDrawBoundary" class="btn btn-primary btn-full">
            Draw on Map
          </button>
          <button
            v-if="form.polygon && form.polygon.length > 0"
            @click="clearBoundary"
            class="btn btn-danger btn-full"
          >
            Clear Boundary
          </button>
        </div>

      </div>
    </div>

    <!-- Contacts Tab -->
    <div v-if="activeTab === 'contacts'" class="tab-content">
      <h3 class="section-title">Area contacts ({{ form.contacts.length }})</h3>
      <button @click="showMarshalSelector = true" class="btn btn-secondary btn-full">
        Add contact
      </button>

      <div class="contacts-list">
        <div
          v-for="(contact, index) in form.contacts"
          :key="index"
          class="contact-item"
        >
          <div class="contact-info">
            <strong>{{ contact.marshalName }}</strong>
            <div class="contact-role">
              <label>Role:</label>
              <select
                v-model="contact.role"
                class="role-select"
                @change="handleInput"
              >
                <option value="Lead">Lead</option>
                <option value="Deputy">Deputy</option>
                <option value="Support">Support</option>
              </select>
            </div>
          </div>
          <button
            @click="removeContact(index)"
            class="btn btn-small btn-danger"
          >
            Remove
          </button>
        </div>

        <div v-if="form.contacts.length === 0" class="empty-state">
          <p>No contacts assigned. Click "Add contact" to assign a marshal.</p>
        </div>
      </div>
    </div>

    <!-- Custom footer with left and right aligned buttons -->
    <template #footer>
      <div class="custom-footer">
        <button
          v-if="area && area.id && !form.isDefault"
          type="button"
          @click="handleDelete"
          class="btn btn-danger"
        >
          Delete area
        </button>
        <div v-else></div>
        <button type="button" @click="handleSave" class="btn btn-primary">
          {{ area && area.id ? 'Save changes' : 'Create area' }}
        </button>
      </div>
    </template>
  </BaseModal>

  <!-- Child Modals -->
  <MarshalSelectorModal
    :show="showMarshalSelector"
    :areaName="form.name"
    :marshals="marshals"
    :existingContacts="form.contacts"
    @close="showMarshalSelector = false"
    @assign="handleAddContact"
  />
</template>

<script setup>
import { ref, computed, defineProps, defineEmits, watch } from 'vue';
import BaseModal from '../../BaseModal.vue';
import MarshalSelectorModal from './MarshalSelectorModal.vue';
import { AREA_COLORS, DEFAULT_AREA_COLOR, getNextAvailableColor } from '../../../constants/areaColors';

const props = defineProps({
  show: {
    type: Boolean,
    default: false,
  },
  area: {
    type: Object,
    default: null,
  },
  checkpoints: {
    type: Array,
    default: () => [],
  },
  marshals: {
    type: Array,
    default: () => [],
  },
  areas: {
    type: Array,
    default: () => [],
  },
  isDirty: {
    type: Boolean,
    default: false,
  },
});

const emit = defineEmits([
  'close',
  'save',
  'delete',
  'draw-boundary',
  'update:isDirty',
]);

const activeTab = ref('details');
const showMarshalSelector = ref(false);

const form = ref({
  name: '',
  description: '',
  color: DEFAULT_AREA_COLOR,
  displayOrder: 0,
  isDefault: false,
  polygon: [],
  contacts: [],
});

watch(() => props.area, (newVal) => {
  if (newVal) {
    // Parse ContactsJson and PolygonJson from backend
    const contacts = newVal.contacts
      ? (typeof newVal.contacts === 'string' ? JSON.parse(newVal.contacts) : newVal.contacts)
      : [];
    const polygon = newVal.polygon
      ? (typeof newVal.polygon === 'string' ? JSON.parse(newVal.polygon) : newVal.polygon)
      : [];

    form.value = {
      name: newVal.name || '',
      description: newVal.description || '',
      color: newVal.color || DEFAULT_AREA_COLOR,
      displayOrder: newVal.displayOrder || 0,
      isDefault: newVal.isDefault || false,
      polygon: polygon,
      contacts: contacts,
    };
  } else {
    // Creating new area
    form.value = {
      name: '',
      description: '',
      color: getNextAvailableColor(props.areas),
      displayOrder: props.areas.length,
      isDefault: false,
      polygon: [],
      contacts: [],
    };
  }
}, { immediate: true, deep: true });

watch(() => props.show, (newVal) => {
  if (newVal) {
    activeTab.value = 'details';
    showMarshalSelector.value = false;
  }
});

const handleInput = () => {
  emit('update:isDirty', true);
};

const selectColor = (colorHex) => {
  form.value.color = colorHex;
  handleInput();
};

const handleDrawBoundary = () => {
  // Emit current form data so parent can preserve it while drawing
  emit('draw-boundary', form.value);
};

const clearBoundary = () => {
  form.value.polygon = [];
  handleInput();
};

const handleAddContact = (contact) => {
  form.value.contacts.push(contact);
  showMarshalSelector.value = false;
  handleInput();
};

const removeContact = (index) => {
  form.value.contacts.splice(index, 1);
  handleInput();
};

const handleSave = () => {
  // For create: EventId, Name, Description, Color, Contacts (array), Polygon (array or null)
  // For update: Name, Description, Color, Contacts (array), Polygon (array or null), DisplayOrder
  const formData = props.area ? {
    // Update request
    name: form.value.name,
    description: form.value.description,
    color: form.value.color,
    contacts: form.value.contacts,
    polygon: form.value.polygon.length > 0 ? form.value.polygon : null,
    displayOrder: form.value.displayOrder,
  } : {
    // Create request
    name: form.value.name,
    description: form.value.description,
    color: form.value.color,
    contacts: form.value.contacts,
    polygon: form.value.polygon.length > 0 ? form.value.polygon : null,
  };

  emit('save', formData);
};

const handleDelete = () => {
  emit('delete', props.area.id);
};

const handleClose = () => {
  emit('close');
};
</script>

<style scoped>
.tabs {
  display: flex;
  gap: 0.5rem;
  padding: 0.5rem 0;
}

.tab-button {
  padding: 0.5rem 1rem;
  border: none;
  background: transparent;
  color: #666;
  cursor: pointer;
  font-size: 0.9rem;
  border-bottom: 2px solid transparent;
  transition: all 0.2s;
}

.tab-button:hover {
  color: #333;
}

.tab-button.active {
  color: #007bff;
  border-bottom-color: #007bff;
  font-weight: 500;
}

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
  resize: vertical;
}

.form-input:disabled {
  background-color: #f5f5f5;
  cursor: not-allowed;
}

.form-help {
  display: block;
  color: #666;
  font-size: 0.85rem;
  margin-top: 0.25rem;
  font-weight: normal;
}

.checkbox-label {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  cursor: pointer;
  font-weight: 500;
  color: #333;
}

.checkbox-label input[type="checkbox"] {
  cursor: pointer;
  width: 1.1rem;
  height: 1.1rem;
}

.color-picker {
  display: flex;
  gap: 0.75rem;
  flex-wrap: wrap;
}

.color-swatch {
  width: 40px;
  height: 40px;
  border-radius: 50%;
  cursor: pointer;
  border: 3px solid transparent;
  transition: all 0.2s;
  box-shadow: 0 2px 4px rgba(0, 0, 0, 0.2);
}

.color-swatch:hover {
  transform: scale(1.1);
}

.color-swatch.selected {
  border-color: #333;
  box-shadow: 0 0 0 2px white, 0 0 0 4px #333;
}

.section-title {
  margin: 0 0 1rem 0;
  font-size: 1rem;
  color: #333;
}

.checkpoints-list,
.contacts-list {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
  margin-top: 1rem;
}

.checkpoint-item,
.contact-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 1rem;
  background: #f8f9fa;
  border-radius: 8px;
  border: 1px solid #dee2e6;
  gap: 1rem;
}

.checkpoint-info,
.contact-info {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
  flex: 1;
}

.checkpoint-description {
  font-size: 0.85rem;
  color: #666;
}

.checkpoint-meta {
  font-size: 0.8rem;
  color: #999;
}

.contact-role {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  margin-top: 0.25rem;
}

.contact-role label {
  font-size: 0.85rem;
  font-weight: normal;
  color: #666;
  margin: 0;
}

.role-select {
  padding: 0.25rem 0.5rem;
  border: 1px solid #ddd;
  border-radius: 4px;
  font-size: 0.85rem;
}

.boundary-section {
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
}

.instructions-card {
  background: #e7f3ff;
  padding: 1rem;
  border-radius: 8px;
  color: #0056b3;
}

.instructions-card p {
  margin: 0 0 0.5rem 0;
}

.instructions-card p:last-child {
  margin-bottom: 0;
}

.boundary-actions {
  display: flex;
  gap: 1rem;
}

.boundary-info {
  background: #d4edda;
  padding: 0.75rem;
  border-radius: 6px;
  border: 1px solid #c3e6cb;
  color: #155724;
  font-size: 0.9rem;
}

.empty-state {
  text-align: center;
  padding: 2rem;
  color: #999;
}

.empty-state p {
  margin: 0;
  font-size: 0.9rem;
}

.auto-assign-notice {
  background: #d4edda;
  border: 1px solid #c3e6cb;
  color: #155724;
  padding: 0.75rem 1rem;
  border-radius: 4px;
  margin-bottom: 1rem;
  font-size: 0.9rem;
  line-height: 1.5;
}

.custom-footer {
  display: flex;
  justify-content: space-between;
  align-items: center;
  width: 100%;
}

.btn {
  padding: 0.5rem 1rem;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  font-size: 0.9rem;
  transition: background-color 0.2s;
}

.btn-full {
  width: 100%;
}

.btn-small {
  padding: 0.375rem 0.75rem;
  font-size: 0.85rem;
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

.btn-danger {
  background: #dc3545;
  color: white;
}

.btn-danger:hover {
  background: #c82333;
}
</style>
