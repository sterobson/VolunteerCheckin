<template>
  <div class="tab-content">
    <h3 class="section-title">Assigned {{ termsLower.checkpoints }} ({{ totalAssignments }})</h3>
    <div class="assignments-list">
      <!-- Existing assignments -->
      <div
        v-for="assignment in assignments"
        :key="assignment.id"
        class="assignment-item"
        :class="{
          'checked-in': !assignment.isPending && getEffectiveCheckInStatus(assignment),
          'is-pending': assignment.isPending
        }"
      >
        <div class="assignment-info">
          <div class="assignment-header">
            <span
              v-if="!assignment.isPending"
              class="status-indicator"
              :style="{ color: getStatusColor(assignment) }"
              :title="getStatusText(assignment)"
            >
              {{ getStatusIcon(assignment) }}
            </span>
            <span v-else class="status-indicator pending-indicator" title="Will be assigned on save">
              ⏳
            </span>
            <strong>{{ assignment.locationName }}</strong>
            <span v-if="assignment.isPending" class="pending-badge">
              (will be assigned on save)
            </span>
            <span v-else-if="pendingCheckInChanges.has(assignment.id)" class="pending-badge">
              (unsaved)
            </span>
          </div>
          <span v-if="!assignment.isPending && getEffectiveCheckInStatus(assignment)" class="check-in-info">
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
            v-if="!assignment.isPending"
            @click="handleToggleCheckIn(assignment)"
            class="btn btn-small"
            :class="getEffectiveCheckInStatus(assignment) ? 'btn-danger' : 'btn-secondary'"
          >
            {{ getEffectiveCheckInStatus(assignment) ? 'Undo' : 'Check in' }}
          </button>
          <button
            @click="$emit('remove-assignment', assignment)"
            class="btn btn-small btn-danger"
          >
            Remove
          </button>
        </div>
      </div>

      <!-- Assign to checkpoint -->
      <div v-if="sortedAvailableLocations.length > 0" class="form-group" style="margin-top: 1.5rem;">
        <label>Assign to {{ termsLower.checkpoint }}</label>
        <select v-model="selectedLocationId" class="form-input" @change="handleSelectionChange">
          <option value="">Select a {{ termsLower.checkpoint }}...</option>
          <option
            v-for="location in sortedAvailableLocations"
            :key="location.id"
            :value="location.id"
          >
            {{ location.name }}
          </option>
        </select>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, defineProps, defineEmits } from 'vue';
import { useCheckInManagement } from '../../../composables/useCheckInManagement';
import { useTerminology } from '../../../composables/useTerminology';

const { termsLower } = useTerminology();

const props = defineProps({
  assignments: {
    type: Array,
    default: () => [],
  },
  availableLocations: {
    type: Array,
    default: () => [],
  },
  isNewMarshal: {
    type: Boolean,
    default: false,
  },
});

const emit = defineEmits(['input', 'remove-assignment', 'assign-to-location']);

// Use check-in management composable
const {
  pendingCheckInChanges,
  getEffectiveCheckInStatus,
  handleToggleCheckIn,
  getStatusIcon,
  getStatusColor,
  getStatusText,
  formatTime,
} = useCheckInManagement(() => emit('input'));

const selectedLocationId = ref('');

const totalAssignments = computed(() => {
  return props.assignments?.length || 0;
});

// Sort locations alphabetically with natural number sorting
const sortedAvailableLocations = computed(() => {
  const sorted = [...props.availableLocations];
  sorted.sort((a, b) => {
    return a.name.localeCompare(b.name, undefined, { numeric: true, sensitivity: 'base' });
  });
  return sorted;
});

// Auto-assign when selection changes
const handleSelectionChange = () => {
  if (selectedLocationId.value) {
    emit('assign-to-location', selectedLocationId.value);
    selectedLocationId.value = '';
  }
};

// Expose check-in management functions for parent
defineExpose({
  pendingCheckInChanges,
  clearPendingChanges: () => {
    pendingCheckInChanges.value.clear();
    selectedLocationId.value = '';
  },
  getPendingChanges: () => {
    return Array.from(pendingCheckInChanges.value.entries()).map(([assignmentId, shouldBeCheckedIn]) => ({
      assignmentId,
      shouldBeCheckedIn,
    }));
  },
});
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
  font-family: inherit;
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

.assignment-item.is-pending {
  background: #fff8e6;
  border-color: #ffc107;
  border-style: dashed;
}

.pending-indicator {
  color: #ffc107;
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
