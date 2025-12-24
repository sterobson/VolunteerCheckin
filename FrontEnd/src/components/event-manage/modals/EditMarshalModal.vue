<template>
  <BaseModal
    :show="show"
    :title="isEditing ? 'Edit marshal' : 'Add marshal'"
    size="large"
    :confirm-on-close="true"
    :is-dirty="isDirty"
    @close="handleClose"
  >
    <!-- Tabs in header (only show when editing) -->
    <template v-if="isEditing" #tab-header>
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
          :class="{ active: activeTab === 'checkpoints' }"
          @click="activeTab = 'checkpoints'"
          type="button"
        >
          Checkpoints
        </button>
      </div>
    </template>

    <!-- Details Tab (or whole content when creating) -->
    <div v-if="activeTab === 'details' || !isEditing" class="tab-content">
      <div class="form-group">
        <label>Name *</label>
        <input
          v-model="form.name"
          type="text"
          required
          class="form-input"
          @input="handleInput"
        />
      </div>

      <div class="form-group">
        <label>Email</label>
        <input
          v-model="form.email"
          type="email"
          class="form-input"
          @input="handleInput"
        />
      </div>

      <div class="form-group">
        <label>Phone number</label>
        <input
          v-model="form.phoneNumber"
          type="tel"
          class="form-input"
          @input="handleInput"
        />
      </div>

      <div class="form-group">
        <label>Notes</label>
        <textarea
          v-model="form.notes"
          class="form-input"
          rows="3"
          placeholder="e.g., Needs to leave by 11am"
          @input="handleInput"
        ></textarea>
      </div>
    </div>

    <!-- Checkpoints Tab (only when editing) -->
    <div v-if="activeTab === 'checkpoints' && isEditing" class="tab-content">
      <h3 class="section-title">Assigned checkpoints ({{ totalAssignments }})</h3>
      <div class="assignments-list">
        <!-- Existing assignments -->
        <div
          v-for="assignment in assignments"
          :key="assignment.id"
          class="assignment-item"
          :class="{ 'checked-in': assignment.isCheckedIn }"
        >
          <div class="assignment-info">
            <div class="assignment-header">
              <span
                class="status-indicator"
                :style="{ color: getStatusColor(assignment) }"
                :title="getStatusText(assignment)"
              >
                {{ getStatusIcon(assignment) }}
              </span>
              <strong>{{ assignment.locationName }}</strong>
            </div>
            <span v-if="assignment.isCheckedIn" class="check-in-info">
              {{ formatTime(assignment.checkInTime) }}
              <span class="check-in-method">({{ assignment.checkInMethod }})</span>
            </span>
          </div>
          <div class="assignment-actions">
            <button
              @click="handleToggleCheckIn(assignment)"
              class="btn btn-small"
              :class="assignment.isCheckedIn ? 'btn-danger' : 'btn-secondary'"
            >
              {{ assignment.isCheckedIn ? 'Undo' : 'Check in' }}
            </button>
            <button
              @click="handleRemoveAssignment(assignment)"
              class="btn btn-small btn-danger"
            >
              Remove
            </button>
          </div>
        </div>

        <!-- Assign to checkpoint -->
        <div class="form-group" style="margin-top: 1.5rem;">
          <label>Assign to checkpoint</label>
          <select v-model="selectedLocationId" class="form-input">
            <option value="">Select a checkpoint...</option>
            <option
              v-for="location in availableLocations"
              :key="location.id"
              :value="location.id"
            >
              {{ location.name }}
            </option>
          </select>
          <button
            @click="handleAssignToLocation"
            class="btn btn-secondary btn-full"
            style="margin-top: 0.5rem;"
            :disabled="!selectedLocationId"
          >
            Assign to checkpoint
          </button>
        </div>
      </div>
    </div>

    <!-- Custom footer with left and right aligned buttons -->
    <template #footer>
      <div class="custom-footer">
        <button
          v-if="isEditing"
          type="button"
          @click="handleDelete"
          class="btn btn-danger"
        >
          Delete marshal
        </button>
        <div v-else></div>
        <button type="button" @click="handleSave" class="btn btn-primary">
          {{ isEditing ? 'Save changes' : 'Add marshal' }}
        </button>
      </div>
    </template>
  </BaseModal>
</template>

<script setup>
import { ref, computed, defineProps, defineEmits, watch } from 'vue';
import BaseModal from '../../BaseModal.vue';

const props = defineProps({
  show: {
    type: Boolean,
    default: false,
  },
  marshal: {
    type: Object,
    default: null,
  },
  assignments: {
    type: Array,
    default: () => [],
  },
  availableLocations: {
    type: Array,
    default: () => [],
  },
  isEditing: {
    type: Boolean,
    default: false,
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
  'toggle-check-in',
  'remove-assignment',
  'assign-to-location',
  'update:isDirty',
]);

const activeTab = ref('details');
const selectedLocationId = ref('');
const form = ref({
  name: '',
  email: '',
  phoneNumber: '',
  notes: '',
});

const totalAssignments = computed(() => {
  return props.assignments?.length || 0;
});

watch(() => props.marshal, (newVal) => {
  if (newVal) {
    form.value = {
      name: newVal.name || '',
      email: newVal.email || '',
      phoneNumber: newVal.phoneNumber || '',
      notes: newVal.notes || '',
    };
  } else {
    form.value = {
      name: '',
      email: '',
      phoneNumber: '',
      notes: '',
    };
  }
}, { immediate: true, deep: true });

watch(() => props.show, (newVal) => {
  if (newVal) {
    activeTab.value = 'details';
    selectedLocationId.value = '';
  }
});

const handleInput = () => {
  emit('update:isDirty', true);
};

const handleSave = () => {
  emit('save', { ...form.value });
};

const handleDelete = () => {
  emit('delete');
};

const handleToggleCheckIn = (assignment) => {
  emit('toggle-check-in', assignment);
};

const handleRemoveAssignment = (assignment) => {
  emit('remove-assignment', assignment);
};

const handleAssignToLocation = () => {
  if (selectedLocationId.value) {
    emit('assign-to-location', selectedLocationId.value);
    selectedLocationId.value = '';
  }
};

const handleClose = () => {
  emit('close');
};

const getStatusIcon = (assignment) => {
  if (assignment.isCheckedIn) return '✓';
  return '○';
};

const getStatusColor = (assignment) => {
  if (assignment.isCheckedIn) return '#28a745';
  return '#666';
};

const getStatusText = (assignment) => {
  if (assignment.isCheckedIn) return 'Checked in';
  return 'Not checked in';
};

const formatTime = (timeString) => {
  if (!timeString) return '';
  const date = new Date(timeString);
  return date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
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
  font-family: inherit;
}

textarea.form-input {
  resize: vertical;
}

.section-title {
  margin: 0 0 1rem 0;
  font-size: 1rem;
  color: #333;
}

.assignments-list {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.assignment-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 1rem;
  background: #f8f9fa;
  border-radius: 8px;
  border: 1px solid #dee2e6;
}

.assignment-item.checked-in {
  background: #d4edda;
  border-color: #c3e6cb;
}

.assignment-info {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.assignment-header {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.status-indicator {
  font-size: 1.1rem;
  font-weight: bold;
}

.check-in-info {
  font-size: 0.85rem;
  color: #666;
}

.check-in-method {
  color: #999;
}

.assignment-actions {
  display: flex;
  gap: 0.5rem;
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

.btn:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}
</style>
