<template>
  <div class="tab-content">
    <div class="form-group">
      <label>Required {{ peopleTermLower }}</label>
      <input
        :value="form.requiredMarshals"
        @input="handleNumberInput('requiredMarshals', $event.target.value)"
        type="number"
        min="1"
        required
        class="form-input"
      />
    </div>

    <h3 class="section-title">Assigned {{ peopleTermLower }} ({{ totalAssignments }})</h3>
    <div class="assignments-list">
      <!-- Existing assignments -->
      <div
        v-for="assignment in sortedAssignments"
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
            <strong>{{ assignment.marshalName }}</strong>
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

      <!-- Assign marshal button -->
      <div class="assign-section">
        <button
          type="button"
          class="btn btn-primary assign-btn"
          @click="$emit('open-assign-modal')"
        >
          + Assign {{ personTermLower }}...
        </button>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, defineProps, defineEmits } from 'vue';
import { useCheckInManagement } from '../../../composables/useCheckInManagement';

const props = defineProps({
  form: {
    type: Object,
    required: true,
  },
  assignments: {
    type: Array,
    default: () => [],
  },
  peopleTerm: {
    type: String,
    default: 'Marshals',
  },
  personTerm: {
    type: String,
    default: 'Marshal',
  },
});

// Computed lowercase versions
const peopleTermLower = computed(() => props.peopleTerm.toLowerCase());
const personTermLower = computed(() => props.personTerm.toLowerCase());

const emit = defineEmits(['update:form', 'input', 'remove-assignment', 'open-assign-modal']);

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

const totalAssignments = computed(() => {
  return props.assignments?.length || 0;
});

const sortedAssignments = computed(() => {
  if (!props.assignments) return [];
  return [...props.assignments].sort((a, b) => {
    const nameA = a.marshalName?.toLowerCase() || '';
    const nameB = b.marshalName?.toLowerCase() || '';
    return nameA.localeCompare(nameB);
  });
});

const handleNumberInput = (field, value) => {
  emit('update:form', { ...props.form, [field]: Number(value) });
  emit('input');
};

// Expose check-in management functions for parent
defineExpose({
  pendingCheckInChanges,
  clearPendingChanges: () => {
    pendingCheckInChanges.value.clear();
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

.assignment-item.empty-assignment {
  background: transparent;
  border: 2px dashed #dee2e6;
  cursor: pointer;
  justify-content: center;
  transition: all 0.2s;
}

.assignment-item.empty-assignment:hover {
  border-color: #007bff;
  background: #f8f9fa;
}

.empty-assignment-content {
  font-size: 1.5rem;
  color: #007bff;
}

.assignment-item.empty-assignment:hover .empty-assignment-content {
  color: #0056b3;
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

.assign-section {
  margin-top: 1.5rem;
  padding: 1rem;
  background: #f8f9fa;
  border-radius: 8px;
  border: 1px solid #e2e8f0;
}

.assign-btn {
  width: 100%;
}
</style>
