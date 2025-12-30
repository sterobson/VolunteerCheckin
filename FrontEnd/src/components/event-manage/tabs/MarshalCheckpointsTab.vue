<template>
  <div class="tab-content">
    <h3 class="section-title">Assigned checkpoints ({{ totalAssignments }})</h3>
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
            <strong>{{ assignment.locationName }}</strong>
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
            @click="$emit('remove-assignment', assignment)"
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
</template>

<script setup>
import { ref, computed, defineProps, defineEmits } from 'vue';
import { useCheckInManagement } from '../../../composables/useCheckInManagement';

const props = defineProps({
  assignments: {
    type: Array,
    default: () => [],
  },
  availableLocations: {
    type: Array,
    default: () => [],
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

const handleAssignToLocation = () => {
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
