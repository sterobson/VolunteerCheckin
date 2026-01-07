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
  color: var(--text-primary);
}

.form-input {
  width: 100%;
  padding: 0.5rem;
  border: 1px solid var(--border-color);
  border-radius: 4px;
  font-size: 0.9rem;
  box-sizing: border-box;
  background: var(--input-bg);
  color: var(--text-primary);
}

.section-title {
  margin: 0 0 1rem 0;
  font-size: 1rem;
  color: var(--text-primary);
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
  background: var(--bg-tertiary);
  border-radius: 8px;
  border: 1px solid var(--border-color);
  gap: 1rem;
}

@media (max-width: 640px) {
  .assignment-item {
    flex-direction: column;
    align-items: stretch;
  }
}

.assignment-item.checked-in {
  background: var(--success-bg);
  border-color: var(--success-border);
}

.assignment-item.is-pending {
  background: var(--warning-bg-light);
  border-color: var(--warning);
  border-style: dashed;
}

.pending-indicator {
  color: var(--warning);
}

.assignment-item.empty-assignment {
  background: transparent;
  border: 2px dashed var(--border-color);
  cursor: pointer;
  justify-content: center;
  transition: all 0.2s;
}

.assignment-item.empty-assignment:hover {
  border-color: var(--accent-primary);
  background: var(--bg-tertiary);
}

.empty-assignment-content {
  font-size: 1.5rem;
  color: var(--accent-primary);
}

.assignment-item.empty-assignment:hover .empty-assignment-content {
  color: var(--accent-primary-hover);
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
  color: var(--text-secondary);
}

.check-in-method {
  color: var(--text-muted);
}

.pending-badge {
  color: var(--warning-orange);
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
  background: var(--accent-primary);
  color: var(--btn-primary-text);
}

.btn-primary:hover {
  background: var(--accent-primary-hover);
}

.btn-secondary {
  background: var(--btn-secondary-bg);
  color: var(--btn-secondary-text);
}

.btn-secondary:hover {
  background: var(--btn-secondary-hover);
}

.btn-danger {
  background: var(--danger);
  color: var(--btn-primary-text);
}

.btn-danger:hover {
  background: var(--danger-hover);
}

.assign-section {
  margin-top: 1.5rem;
  padding: 1rem;
  background: var(--bg-tertiary);
  border-radius: 8px;
  border: 1px solid var(--border-color);
}

.assign-btn {
  width: 100%;
}
</style>
