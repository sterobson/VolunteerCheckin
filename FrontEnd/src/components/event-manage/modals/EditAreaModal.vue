<template>
  <BaseModal
    :show="show"
    :title="area && area.id ? `Edit ${termsLower.area}: ${area.name}` : `Create new ${termsLower.area}`"
    size="large"
    :confirm-on-close="true"
    :is-dirty="isDirty"
    @close="handleClose"
  >
    <!-- Tabs in header -->
    <template #tab-header>
      <TabHeader
        v-model="activeTab"
        :tabs="availableTabs"
      />
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
      <h3 class="section-title">{{ terms.area }} contacts</h3>

      <div class="contacts-actions">
        <button @click="handleAddAreaContact" class="btn btn-primary btn-full">
          Add contact for this {{ termsLower.area }}
        </button>
      </div>

      <!-- Show existing contacts scoped to this area -->
      <div v-if="areaContacts.length > 0" class="area-contacts-list">
        <h4>Contacts visible in this {{ termsLower.area }} ({{ areaContacts.length }})</h4>
        <div
          v-for="contact in areaContacts"
          :key="contact.contactId"
          class="contact-item"
          @click="$emit('edit-contact', contact)"
        >
          <div class="contact-info">
            <strong>{{ contact.name }}</strong>
            <span v-if="contact.role" class="contact-role-badge">{{ formatRoleName(contact.role) }}</span>
            <div v-if="contact.phone || contact.email" class="contact-details">
              <span v-if="contact.phone">{{ contact.phone }}</span>
              <span v-if="contact.email">{{ contact.email }}</span>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- Checkpoints Tab (only when editing) -->
    <div v-if="activeTab === 'checkpoints' && isExistingArea" class="tab-content">
      <h3 class="section-title">{{ terms.checkpoints }} in {{ area?.name }} ({{ areaCheckpoints.length }})</h3>

      <div v-if="areaCheckpoints.length === 0" class="empty-state">
        <p>No {{ termsLower.checkpoints }} in this {{ termsLower.area }} yet.</p>
      </div>

      <div v-else class="checkpoints-list">
        <div
          v-for="checkpoint in sortedAreaCheckpoints"
          :key="checkpoint.id"
          class="checkpoint-card"
          @click="$emit('select-checkpoint', checkpoint)"
        >
          <div class="checkpoint-header">
            <div class="checkpoint-name-section">
              <h4>{{ checkpoint.name }}</h4>
              <p v-if="checkpoint.description" class="checkpoint-description">
                {{ checkpoint.description }}
              </p>
            </div>
            <div class="checkpoint-stats">
              <div class="stat-badge" :class="getCheckInStatusClass(checkpoint)">
                {{ getCheckInStatusText(checkpoint) }}
              </div>
            </div>
          </div>

          <div class="checkpoint-details">
            <div class="detail-row">
              <span class="detail-label">{{ terms.people }}:</span>
              <span class="detail-value">
                {{ getCheckpointAssignments(checkpoint.id).length }} / {{ checkpoint.requiredMarshals }}
                <span v-if="getCheckpointAssignments(checkpoint.id).length < checkpoint.requiredMarshals" class="warning-text">
                  ({{ checkpoint.requiredMarshals - getCheckpointAssignments(checkpoint.id).length }} needed)
                </span>
              </span>
            </div>

            <div class="detail-row">
              <span class="detail-label">Checked in:</span>
              <span class="detail-value">
                {{ getCheckedInCount(checkpoint) }} / {{ getCheckpointAssignments(checkpoint.id).length }}
              </span>
            </div>

            <div class="detail-row" v-if="checkpointChecklistStatus[checkpoint.id]">
              <span class="detail-label">Tasks:</span>
              <span class="detail-value" :class="getChecklistStatusClass(checkpoint.id)">
                {{ getChecklistStatusText(checkpoint.id) }}
              </span>
            </div>
            <div class="detail-row" v-else-if="loadingChecklistStatus[checkpoint.id]">
              <span class="detail-label">Tasks:</span>
              <span class="detail-value loading-text">Loading...</span>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- Checkpoints placeholder when creating -->
    <div v-if="activeTab === 'checkpoints' && !isExistingArea" class="placeholder-message">
      <p>{{ terms.checkpoints }} will be automatically assigned based on boundaries after the {{ termsLower.area }} is created.</p>
    </div>

    <!-- Checklists Tab (only when editing) -->
    <CheckpointChecklistView
      v-if="activeTab === 'checklists' && isExistingArea"
      ref="checklistTabRef"
      v-model="checklistChanges"
      :event-id="eventId"
      :area-id="area.id"
      :locations="allLocations"
      :areas="areas"
      :assignments="areaAssignments"
      @change="handleChecklistChange"
    />

    <!-- Checklists placeholder when creating -->
    <div v-if="activeTab === 'checklists' && !isExistingArea" class="placeholder-message">
      <p>{{ terms.checklists }} can be assigned after the {{ termsLower.area }} is created.</p>
    </div>

    <!-- Notes Tab (only when editing) -->
    <NotesView
      v-if="activeTab === 'notes' && isExistingArea"
      :event-id="eventId"
      :area-id="area?.id"
      :all-notes="notes"
      :locations="allLocations"
      :areas="areas"
      :assignments="assignments"
    />

    <!-- Notes placeholder when creating -->
    <div v-if="activeTab === 'notes' && !isExistingArea" class="placeholder-message">
      <p>Notes can be viewed after the {{ termsLower.area }} is created.</p>
    </div>

    <!-- Custom footer with left and right aligned buttons -->
    <template #footer>
      <div class="custom-footer">
        <button
          v-if="isExistingArea && !form.isDefault"
          type="button"
          @click="handleDelete"
          class="btn btn-danger"
        >
          Delete {{ termsLower.area }}
        </button>
        <div v-else></div>
        <button type="button" @click="handlePrimaryAction" class="btn btn-primary">
          {{ isExistingArea ? 'Save changes' : createButtonText }}
        </button>
      </div>
    </template>
  </BaseModal>
</template>

<script setup>
import { ref, computed, defineProps, defineEmits, watch } from 'vue';
import BaseModal from '../../BaseModal.vue';
import TabHeader from '../../TabHeader.vue';
import CheckpointChecklistView from '../../CheckpointChecklistView.vue';
import NotesView from '../../NotesView.vue';
import { AREA_COLORS, DEFAULT_AREA_COLOR, getNextAvailableColor } from '../../../constants/areaColors';
import { checklistApi } from '../../../services/api';
import { useTerminology } from '../../../composables/useTerminology';

const { terms, termsLower } = useTerminology();

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
  eventId: {
    type: String,
    required: true,
  },
  allLocations: {
    type: Array,
    default: () => [],
  },
  assignments: {
    type: Array,
    default: () => [],
  },
  notes: {
    type: Array,
    default: () => [],
  },
  contacts: {
    type: Array,
    default: () => [],
  },
});

const emit = defineEmits([
  'close',
  'save',
  'delete',
  'draw-boundary',
  'update:isDirty',
  'select-checkpoint',
  'add-area-contact',
  'edit-contact',
]);

const activeTab = ref('details');
const checklistTabRef = ref(null);
const checklistChanges = ref([]);
const checkpointChecklistStatus = ref({});
const loadingChecklistStatus = ref({});

const form = ref({
  name: '',
  description: '',
  color: DEFAULT_AREA_COLOR,
  displayOrder: 0,
  isDefault: false,
  polygon: [],
});

// Computed property for whether this is an existing area
const isExistingArea = computed(() => {
  return props.area && props.area.id;
});

// All tabs always available
const availableTabs = computed(() => [
  { value: 'details', label: 'Details', icon: 'details' },
  { value: 'boundary', label: 'Boundary', icon: 'area' },
  { value: 'contacts', label: 'Contacts', icon: 'marshal' },
  { value: 'checkpoints', label: terms.value.checkpoints, icon: 'checkpoint' },
  { value: 'checklists', label: terms.value.checklists, icon: 'checklist' },
  { value: 'notes', label: 'Notes', icon: 'notes' },
]);

// Get current tab index and check if on last tab
const currentTabIndex = computed(() => {
  return availableTabs.value.findIndex(tab => tab.value === activeTab.value);
});

const isLastTab = computed(() => {
  return currentTabIndex.value === availableTabs.value.length - 1;
});

const nextTab = computed(() => {
  if (isLastTab.value) return null;
  return availableTabs.value[currentTabIndex.value + 1];
});

// Button text for create mode - shows "Add [next tab]..." or "Create [area]"
const createButtonText = computed(() => {
  if (isLastTab.value) {
    return `Create ${termsLower.value.area}`;
  }
  return `Add ${nextTab.value.label.toLowerCase()}...`;
});

// Compute assignments for checkpoints in this area
const areaAssignments = computed(() => {
  if (!props.area || !props.area.id) return [];

  // Get all checkpoints in this area
  const checkpointIds = props.checkpoints
    .filter(cp => cp.areaIds && cp.areaIds.includes(props.area.id))
    .map(cp => cp.id);

  // Get all assignments for those checkpoints
  return props.assignments.filter(a => checkpointIds.includes(a.locationId));
});

// Get checkpoints in this area
const areaCheckpoints = computed(() => {
  if (!props.area || !props.area.id) return [];
  return props.checkpoints.filter(cp => cp.areaIds && cp.areaIds.includes(props.area.id));
});

// Sort checkpoints alphanumerically
const sortedAreaCheckpoints = computed(() => {
  const sorted = [...areaCheckpoints.value];
  sorted.sort((a, b) => {
    // Extract numbers from names for natural sorting
    const aMatch = a.name.match(/^(\d+)/);
    const bMatch = b.name.match(/^(\d+)/);

    if (aMatch && bMatch) {
      const aNum = parseInt(aMatch[1]);
      const bNum = parseInt(bMatch[1]);
      if (aNum !== bNum) return aNum - bNum;
    }

    return a.name.localeCompare(b.name, undefined, { numeric: true, sensitivity: 'base' });
  });
  return sorted;
});

// Get contacts scoped to this area
const areaContacts = computed(() => {
  if (!props.area || !props.area.id) return [];

  return props.contacts.filter(contact => {
    if (!contact.scopeConfigurations) return false;

    return contact.scopeConfigurations.some(config => {
      // Check if this contact is visible to everyone in specific areas and this area is included
      if (config.scope === 'EveryoneInAreas' && config.itemType === 'Area') {
        // ALL_AREAS means all areas
        if (config.ids?.includes('ALL_AREAS')) return true;
        // Check if this specific area is in the list
        return config.ids?.includes(props.area.id);
      }
      return false;
    });
  });
});

// Get assignments for a specific checkpoint
const getCheckpointAssignments = (checkpointId) => {
  return props.assignments.filter(a => a.locationId === checkpointId);
};

// Get number of checked in marshals for a checkpoint
const getCheckedInCount = (checkpoint) => {
  const assignments = getCheckpointAssignments(checkpoint.id);
  return assignments.filter(a => a.isCheckedIn).length;
};

// Get check-in status class
const getCheckInStatusClass = (checkpoint) => {
  const assignments = getCheckpointAssignments(checkpoint.id);
  if (assignments.length === 0) return 'status-none';

  const checkedInCount = assignments.filter(a => a.isCheckedIn).length;
  if (checkedInCount === 0) return 'status-none';
  if (checkedInCount === assignments.length) return 'status-complete';
  return 'status-partial';
};

// Get check-in status text
const getCheckInStatusText = (checkpoint) => {
  const assignments = getCheckpointAssignments(checkpoint.id);
  if (assignments.length === 0) return `No ${termsLower.value.people}`;

  const checkedInCount = assignments.filter(a => a.isCheckedIn).length;
  if (checkedInCount === 0) return 'Not checked in';
  if (checkedInCount === assignments.length) return 'Fully checked in';
  return `Partially checked in (${checkedInCount}/${assignments.length})`;
};

// Get checklist status class
const getChecklistStatusClass = (checkpointId) => {
  const status = checkpointChecklistStatus.value[checkpointId];
  if (!status) return '';

  if (status.total === 0) return '';
  if (status.completed === status.total) return 'status-complete';
  if (status.completed === 0) return 'status-none';
  return 'status-partial';
};

// Get checklist status text
const getChecklistStatusText = (checkpointId) => {
  const status = checkpointChecklistStatus.value[checkpointId];
  if (!status) return '';

  if (status.total === 0) return 'No tasks';
  if (status.completed === status.total) return 'All tasks completed';
  return `${status.completed}/${status.total} completed`;
};

// Lazy load checklist status when checkpoints tab is viewed
const loadChecklistStatus = async (checkpointId) => {
  if (checkpointChecklistStatus.value[checkpointId] || loadingChecklistStatus.value[checkpointId]) {
    return; // Already loaded or loading
  }

  loadingChecklistStatus.value[checkpointId] = true;

  try {
    const response = await checklistApi.getCheckpointChecklist(props.eventId, checkpointId);
    const items = response.data || [];

    checkpointChecklistStatus.value[checkpointId] = {
      total: items.length,
      completed: items.filter(item => item.isCompleted).length,
    };
  } catch (error) {
    console.error('Failed to load checklist status for checkpoint:', checkpointId, error);
    checkpointChecklistStatus.value[checkpointId] = { total: 0, completed: 0 };
  } finally {
    loadingChecklistStatus.value[checkpointId] = false;
  }
};

watch(() => props.area, (newVal) => {
  if (newVal) {
    // Parse PolygonJson from backend
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
    };
  }
}, { immediate: true, deep: true });

watch(() => props.show, (newVal) => {
  if (newVal) {
    activeTab.value = 'details';
    checklistChanges.value = [];
    checkpointChecklistStatus.value = {};
    loadingChecklistStatus.value = {};
    // Reset form when opening for a new area
    if (!isExistingArea.value) {
      form.value = {
        name: '',
        description: '',
        color: getNextAvailableColor(props.areas),
        displayOrder: props.areas.length,
        isDefault: false,
        polygon: [],
      };
    }
    // Clear pending changes in checklist tab if it exists
    if (checklistTabRef.value?.resetLocalState) {
      checklistTabRef.value.resetLocalState();
    }
  }
});

// Watch for activeTab changes to lazy load checklist status
watch(activeTab, (newTab) => {
  if (newTab === 'checkpoints' && props.area && props.area.id) {
    // Lazy load checklist status for all checkpoints in this area
    areaCheckpoints.value.forEach(checkpoint => {
      setTimeout(() => loadChecklistStatus(checkpoint.id), 0);
    });
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

const handleAddAreaContact = () => {
  // Emit to parent to open contact modal with pre-configured area scope
  emit('add-area-contact', props.area);
};

const formatRoleName = (role) => {
  if (!role) return '';
  return role
    .replace(/([A-Z])/g, ' $1')
    .replace(/^./, str => str.toUpperCase())
    .trim();
};

const handleChecklistChange = (changes) => {
  checklistChanges.value = changes;
  handleInput();
};

const handlePrimaryAction = () => {
  if (isExistingArea.value) {
    // When editing, always save
    handleSave();
  } else if (isLastTab.value) {
    // When creating and on the last tab, save
    handleSave();
  } else {
    // When creating and not on the last tab, advance to next tab
    activeTab.value = nextTab.value.value;
  }
};

const handleSave = () => {
  // For create: EventId, Name, Description, Color, Polygon (array or null)
  // For update: Name, Description, Color, Polygon (array or null), DisplayOrder
  const formData = props.area ? {
    // Update request
    name: form.value.name,
    description: form.value.description,
    color: form.value.color,
    polygon: form.value.polygon.length > 0 ? form.value.polygon : null,
    displayOrder: form.value.displayOrder,
    // Include pending checklist changes
    checklistChanges: checklistChanges.value || [],
  } : {
    // Create request
    name: form.value.name,
    description: form.value.description,
    color: form.value.color,
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

.checkpoint-card {
  padding: 1rem;
  background: #f8f9fa;
  border: 1px solid #dee2e6;
  border-radius: 8px;
  cursor: pointer;
  transition: all 0.2s;
}

.checkpoint-card:hover {
  background: #e9ecef;
  border-color: #adb5bd;
  box-shadow: 0 2px 4px rgba(0, 0, 0, 0.1);
}

.checkpoint-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: 1rem;
  margin-bottom: 0.75rem;
}

.checkpoint-name-section {
  flex: 1;
  min-width: 0;
}

.checkpoint-name-section h4 {
  margin: 0;
  font-size: 1rem;
  color: #333;
}

.checkpoint-description {
  margin: 0.25rem 0 0 0;
  font-size: 0.85rem;
  color: #666;
}

.checkpoint-stats {
  flex-shrink: 0;
}

.stat-badge {
  padding: 0.375rem 0.75rem;
  border-radius: 12px;
  font-size: 0.75rem;
  font-weight: 600;
  white-space: nowrap;
}

.stat-badge.status-complete {
  background: #d4edda;
  color: #155724;
}

.stat-badge.status-partial {
  background: #fff3cd;
  color: #856404;
}

.stat-badge.status-none {
  background: #f8d7da;
  color: #721c24;
}

.checkpoint-details {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.detail-row {
  display: flex;
  justify-content: space-between;
  align-items: center;
  font-size: 0.875rem;
}

.detail-label {
  color: #666;
  font-weight: 500;
}

.detail-value {
  color: #333;
}

.detail-value.status-complete {
  color: #28a745;
  font-weight: 500;
}

.detail-value.status-partial {
  color: #ffc107;
  font-weight: 500;
}

.detail-value.status-none {
  color: #dc3545;
  font-weight: 500;
}

.detail-value.loading-text {
  color: #999;
  font-style: italic;
}

.warning-text {
  color: #dc3545;
  font-weight: 500;
}

/* Contacts section */
.contacts-actions {
  display: flex;
  gap: 1rem;
  margin-bottom: 1.5rem;
}

.area-contacts-list {
  margin-top: 1rem;
}

.area-contacts-list h4 {
  margin: 0 0 0.75rem 0;
  font-size: 0.9rem;
  color: #333;
}

.contact-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 0.75rem 1rem;
  background: #f8f9fa;
  border-radius: 8px;
  border: 1px solid #dee2e6;
  margin-bottom: 0.5rem;
  cursor: pointer;
  transition: all 0.2s;
}

.contact-item:hover {
  background: #e9ecef;
  border-color: #adb5bd;
}

.contact-info {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.contact-info strong {
  display: inline;
}

.contact-role-badge {
  display: inline-block;
  padding: 0.15rem 0.5rem;
  background: #e9ecef;
  color: #495057;
  border-radius: 12px;
  font-size: 0.75rem;
  font-weight: 500;
  margin-left: 0.5rem;
}

.contact-details {
  display: flex;
  gap: 1rem;
  font-size: 0.85rem;
  color: #666;
}

.placeholder-message {
  display: flex;
  align-items: center;
  justify-content: center;
  min-height: 150px;
  color: #666;
  font-style: italic;
  text-align: center;
  padding: 2rem;
}

.placeholder-message p {
  margin: 0;
}
</style>
