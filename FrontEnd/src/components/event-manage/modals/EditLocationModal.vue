<template>
  <BaseModal
    :show="show"
    :title="`Edit checkpoint: ${location?.name || ''}`"
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
          :class="{ active: activeTab === 'marshals' }"
          @click="activeTab = 'marshals'"
          type="button"
        >
          Marshals
        </button>
      </div>
    </template>

    <!-- Details Tab -->
    <div v-if="activeTab === 'details'" class="tab-content">
      <form @submit.prevent="handleSave">
        <div class="form-group">
          <label>Name</label>
          <input
            v-model="form.name"
            type="text"
            required
            class="form-input"
            @input="handleInput"
          />
        </div>

        <div class="form-group">
          <label>Description (optional)</label>
          <input
            v-model="form.description"
            type="text"
            class="form-input"
            @input="handleInput"
          />
        </div>

        <div class="form-group">
          <label>What3Words (optional)</label>
          <input
            v-model="form.what3Words"
            type="text"
            class="form-input"
            placeholder="e.g. filled.count.soap or filled/count/soap"
            @input="handleInput"
          />
          <small v-if="form.what3Words && !isValidWhat3Words(form.what3Words)" class="form-error">
            Invalid format. Must be word.word.word or word/word/word (lowercase letters only, 1-20 characters each)
          </small>
        </div>

        <div class="form-group">
          <label class="checkbox-label">
            <input
              v-model="form.useCustomTimes"
              type="checkbox"
              @change="handleCustomTimesToggle"
            />
            Use custom date/time for this checkpoint
          </label>
          <small class="form-help">
            By default, marshals are expected during the event date/time. Enable this to set a specific time range.
          </small>
        </div>

        <div v-if="form.useCustomTimes" class="custom-times-section">
          <div class="form-group">
            <label>Start Date & Time (optional)</label>
            <input
              v-model="form.startTime"
              type="datetime-local"
              class="form-input"
              @input="handleInput"
            />
            <small class="form-help">
              When marshal should arrive at this checkpoint
            </small>
          </div>

          <div class="form-group">
            <label>End Date & Time (optional)</label>
            <input
              v-model="form.endTime"
              type="datetime-local"
              class="form-input"
              @input="handleInput"
            />
            <small class="form-help">
              When marshal can leave this checkpoint
            </small>
          </div>
        </div>

        <div class="form-row">
          <div class="form-group">
            <label>Latitude</label>
            <input
              v-model.number="form.latitude"
              type="number"
              step="any"
              required
              class="form-input"
              :disabled="isMoving"
              placeholder="e.g., 51.505123"
              @input="handleInput"
            />
          </div>

          <div class="form-group">
            <label>Longitude</label>
            <input
              v-model.number="form.longitude"
              type="number"
              step="any"
              required
              class="form-input"
              :disabled="isMoving"
              placeholder="e.g., -0.091234"
              @input="handleInput"
            />
          </div>
        </div>

        <div class="form-group">
          <button
            type="button"
            @click="handleMoveLocation"
            class="btn btn-secondary"
            :class="{ 'btn-primary': isMoving }"
          >
            {{ isMoving ? 'Click on map to set new location...' : 'Move checkpoint...' }}
          </button>
        </div>
      </form>
    </div>

    <!-- Marshals Tab -->
    <div v-if="activeTab === 'marshals'" class="tab-content">
      <div class="form-group">
        <label>Required marshals</label>
        <input
          v-model.number="form.requiredMarshals"
          type="number"
          min="1"
          required
          class="form-input"
          @input="handleInput"
        />
      </div>

      <h3 class="section-title">Assigned marshals ({{ totalAssignments }})</h3>
      <div class="assignments-list">
        <!-- Existing assignments -->
        <div
          v-for="assignment in assignments"
          :key="assignment.id"
          class="assignment-item"
          :class="{ 'checked-in': getEffectiveCheckInStatus(assignment) }"
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
              <strong>{{ assignment.marshalName }}</strong>
              <span v-if="pendingCheckInChanges.has(assignment.id)" class="pending-badge">
                (unsaved)
              </span>
            </div>
            <span v-if="getEffectiveCheckInStatus(assignment)" class="check-in-info">
              <template v-if="assignment.isCheckedIn && !pendingCheckInChanges.has(assignment.id)">
                {{ formatTime(assignment.checkInTime) }}
                <span class="check-in-method">({{ assignment.checkInMethod }})</span>
              </template>
              <template v-else>
                Will be checked in on save
              </template>
            </span>
          </div>
          <div class="assignment-actions">
            <button
              @click="handleToggleCheckIn(assignment)"
              class="btn btn-small"
              :class="getEffectiveCheckInStatus(assignment) ? 'btn-danger' : 'btn-secondary'"
            >
              {{ getEffectiveCheckInStatus(assignment) ? 'Undo' : 'Check in' }}
            </button>
            <button
              @click="handleRemoveAssignment(assignment)"
              class="btn btn-small btn-danger"
            >
              Remove
            </button>
          </div>
        </div>

        <!-- Assign Button -->
        <button @click="handleAssignMarshal" class="btn btn-secondary btn-full" style="margin-top: 1rem;">
          Assign...
        </button>
      </div>
    </div>

    <!-- Custom footer with left and right aligned buttons -->
    <template #footer>
      <div class="custom-footer">
        <button
          type="button"
          @click="handleDelete"
          class="btn btn-danger"
        >
          Delete checkpoint
        </button>
        <button type="button" @click="handleSave" class="btn btn-primary">
          Save changes
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
  location: {
    type: Object,
    default: null,
  },
  assignments: {
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
  'move-location',
  'toggle-check-in',
  'remove-assignment',
  'assign-marshal',
  'update:isDirty',
]);

const activeTab = ref('details');
const isMoving = ref(false);
const form = ref({
  name: '',
  description: '',
  what3Words: '',
  latitude: 0,
  longitude: 0,
  requiredMarshals: 1,
  useCustomTimes: false,
  startTime: '',
  endTime: '',
});

// Track local check-in status changes (assignmentId -> newStatus)
const pendingCheckInChanges = ref(new Map());

const totalAssignments = computed(() => {
  return props.assignments?.length || 0;
});

// Get the effective check-in status (considering pending changes)
const getEffectiveCheckInStatus = (assignment) => {
  if (pendingCheckInChanges.value.has(assignment.id)) {
    return pendingCheckInChanges.value.get(assignment.id);
  }
  return assignment.isCheckedIn;
};

watch(() => props.location, (newVal) => {
  if (newVal) {
    // Convert UTC times to local datetime strings for datetime-local inputs
    let startTimeLocal = '';
    let endTimeLocal = '';

    if (newVal.startTime) {
      const startDate = new Date(newVal.startTime);
      startTimeLocal = formatDateTimeLocal(startDate);
    }

    if (newVal.endTime) {
      const endDate = new Date(newVal.endTime);
      endTimeLocal = formatDateTimeLocal(endDate);
    }

    form.value = {
      name: newVal.name || '',
      description: newVal.description || '',
      what3Words: newVal.what3Words || '',
      latitude: newVal.latitude || 0,
      longitude: newVal.longitude || 0,
      requiredMarshals: newVal.requiredMarshals || 1,
      useCustomTimes: !!(newVal.startTime || newVal.endTime),
      startTime: startTimeLocal,
      endTime: endTimeLocal,
    };
  }
}, { immediate: true, deep: true });

watch(() => props.show, (newVal) => {
  if (newVal) {
    activeTab.value = 'details';
    isMoving.value = false;
    pendingCheckInChanges.value.clear();
  }
});

const isValidWhat3Words = (value) => {
  if (!value) return true;
  const parts = value.includes('.') ? value.split('.') : value.split('/');
  if (parts.length !== 3) return false;
  const wordRegex = /^[a-z]{1,20}$/;
  return parts.every(part => wordRegex.test(part));
};

const formatDateTimeLocal = (date) => {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  const hours = String(date.getHours()).padStart(2, '0');
  const minutes = String(date.getMinutes()).padStart(2, '0');
  return `${year}-${month}-${day}T${hours}:${minutes}`;
};

const handleCustomTimesToggle = () => {
  if (!form.value.useCustomTimes) {
    // Clear times when disabling custom times
    form.value.startTime = '';
    form.value.endTime = '';
  }
  handleInput();
};

const handleInput = () => {
  emit('update:isDirty', true);
};

const handleMoveLocation = () => {
  isMoving.value = !isMoving.value;
  emit('move-location', isMoving.value);
};

const handleSave = () => {
  // Convert local datetime strings back to UTC DateTime objects
  const formData = {
    ...form.value,
    startTime: form.value.useCustomTimes && form.value.startTime
      ? new Date(form.value.startTime).toISOString()
      : null,
    endTime: form.value.useCustomTimes && form.value.endTime
      ? new Date(form.value.endTime).toISOString()
      : null,
    // Include pending check-in changes
    checkInChanges: Array.from(pendingCheckInChanges.value.entries()).map(([assignmentId, shouldBeCheckedIn]) => ({
      assignmentId,
      shouldBeCheckedIn,
    })),
  };

  emit('save', formData);
};

const handleDelete = () => {
  emit('delete');
};

const handleToggleCheckIn = (assignment) => {
  const currentStatus = getEffectiveCheckInStatus(assignment);
  const newStatus = !currentStatus;

  // If toggling back to original status, remove from pending changes
  if (newStatus === assignment.isCheckedIn) {
    pendingCheckInChanges.value.delete(assignment.id);
  } else {
    // Otherwise, track the pending change
    pendingCheckInChanges.value.set(assignment.id, newStatus);
  }

  // Mark form as dirty
  emit('update:isDirty', true);
};

const handleRemoveAssignment = (assignment) => {
  emit('remove-assignment', assignment);
};

const handleAssignMarshal = () => {
  emit('assign-marshal');
};

const handleClose = () => {
  emit('close');
};

const getStatusIcon = (assignment) => {
  if (getEffectiveCheckInStatus(assignment)) return '✓';
  return '✗';
};

const getStatusColor = (assignment) => {
  if (getEffectiveCheckInStatus(assignment)) return '#28a745';
  return '#dc3545';
};

const getStatusText = (assignment) => {
  if (getEffectiveCheckInStatus(assignment)) return 'Checked in';
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
}

.form-error {
  display: block;
  color: #dc3545;
  font-size: 0.85rem;
  margin-top: 0.25rem;
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

.form-help {
  display: block;
  color: #666;
  font-size: 0.85rem;
  margin-top: 0.25rem;
  font-weight: normal;
}

.custom-times-section {
  margin-left: 1.5rem;
  padding-left: 1rem;
  border-left: 3px solid #007bff;
  background: #f8f9fa;
  padding: 1rem;
  border-radius: 4px;
  margin-top: 0.5rem;
}

.form-row {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 1rem;
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
  gap: 1rem;
}

@media (max-width: 640px) {
  .assignment-item {
    flex-direction: column;
    align-items: stretch;
  }
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

.pending-badge {
  color: #ff8c00;
  font-size: 0.85rem;
  font-weight: 600;
  font-style: italic;
}

.assignment-actions {
  display: flex;
  gap: 0.5rem;
  flex-shrink: 0;
}

@media (max-width: 640px) {
  .assignment-actions {
    width: 100%;
  }

  .assignment-actions .btn {
    flex: 1;
  }
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
