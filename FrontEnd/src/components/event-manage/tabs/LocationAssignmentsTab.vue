<template>
  <div class="tab-content">
    <!-- Read-only notice when opened from a marshal -->
    <div v-if="readOnly" class="read-only-notice">
      Viewing from {{ personTermLower }} - {{ peopleTermLower }} cannot be modified here.
    </div>

    <div v-if="!readOnly" class="form-group">
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

    <CardsGrid
      :is-empty="assignments.length === 0"
      :empty-message="`No ${peopleTermLower} assigned yet.`"
      :empty-hint="`Use the button below to assign ${peopleTermLower} to this ${checkpointTermLower}.`"
    >
      <MarshalAssignmentCard
        v-for="assignment in sortedAssignments"
        :key="assignment.id"
        :assignment="assignment"
        :effective-check-in-status="getEffectiveCheckInStatus(assignment)"
        :has-unsaved-changes="pendingCheckInChanges.has(assignment.id)"
        :is-marked-for-removal="isAssignmentMarkedForRemoval(assignment.id)"
        :read-only="readOnly"
        :location="form"
        @click="handleSelectMarshal"
        @toggle-check-in="handleToggleCheckIn"
        @remove="handleMarkForRemoval"
        @undo-remove="handleUndoRemoval"
      />
    </CardsGrid>

    <!-- Assign marshal button -->
    <div v-if="!readOnly" class="assign-section">
      <button
        type="button"
        class="btn btn-primary assign-btn"
        @click="$emit('open-assign-modal')"
      >
        + Assign {{ personTermLower }}...
      </button>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, defineProps, defineEmits } from 'vue';
import { useCheckInManagement } from '../../../composables/useCheckInManagement';
import CardsGrid from '../../common/CardsGrid.vue';
import MarshalAssignmentCard from '../MarshalAssignmentCard.vue';

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
  checkpointTerm: {
    type: String,
    default: 'Checkpoint',
  },
  readOnly: {
    type: Boolean,
    default: false,
  },
});

// Computed lowercase versions
const peopleTermLower = computed(() => props.peopleTerm.toLowerCase());
const personTermLower = computed(() => props.personTerm.toLowerCase());
const checkpointTermLower = computed(() => props.checkpointTerm.toLowerCase());

const emit = defineEmits(['update:form', 'input', 'remove-assignment', 'open-assign-modal', 'select-marshal']);

// Use check-in management composable
const {
  pendingCheckInChanges,
  getEffectiveCheckInStatus,
  handleToggleCheckIn: toggleCheckIn,
} = useCheckInManagement(() => emit('input'));

const markedForRemoval = ref(new Set());

const totalAssignments = computed(() => {
  return props.assignments?.length || 0;
});

const sortedAssignments = computed(() => {
  if (!props.assignments) return [];
  return [...props.assignments].sort((a, b) => {
    return (a.marshalName || '').localeCompare(b.marshalName || '', undefined, { sensitivity: 'base' });
  });
});

// Check if an assignment is marked for removal
const isAssignmentMarkedForRemoval = (assignmentId) => {
  return markedForRemoval.value.has(assignmentId);
};

// Handle clicking on a marshal to view their details
const handleSelectMarshal = (assignment) => {
  if (!assignment.isPending) {
    emit('select-marshal', assignment);
  }
};

// Handle toggle check-in
const handleToggleCheckIn = (assignment) => {
  toggleCheckIn(assignment);
};

// Handle marking for removal (pending assignments are removed immediately)
const handleMarkForRemoval = (assignment) => {
  if (assignment.isPending) {
    // For pending assignments, remove immediately
    emit('remove-assignment', assignment);
  } else {
    // For existing assignments, mark for removal on save
    markedForRemoval.value.add(assignment.id);
    emit('input');
  }
};

// Handle undo removal
const handleUndoRemoval = (assignment) => {
  markedForRemoval.value.delete(assignment.id);
  emit('input');
};

const handleNumberInput = (field, value) => {
  emit('update:form', { ...props.form, [field]: Number(value) });
  emit('input');
};

// Expose check-in management functions for parent
defineExpose({
  pendingCheckInChanges,
  clearPendingChanges: () => {
    pendingCheckInChanges.value.clear();
    markedForRemoval.value.clear();
  },
  getPendingChanges: () => {
    return Array.from(pendingCheckInChanges.value.entries()).map(([assignmentId, shouldBeCheckedIn]) => ({
      assignmentId,
      shouldBeCheckedIn,
    }));
  },
  getMarkedForRemoval: () => {
    return Array.from(markedForRemoval.value);
  },
});
</script>

<style scoped>
.tab-content {
  padding-top: 0.5rem;
  width: max-content;
  min-width: 100%;
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

.assign-section {
  margin-top: 1.5rem;
}

.btn {
  padding: 0.5rem 1rem;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  font-size: 0.9rem;
  transition: background-color 0.2s;
}

.btn-primary {
  background: var(--accent-primary);
  color: var(--btn-primary-text);
}

.btn-primary:hover {
  background: var(--accent-primary-hover);
}

.assign-btn {
  width: 100%;
}

.read-only-notice {
  padding: 0.75rem 1rem;
  background: var(--bg-tertiary);
  border: 1px solid var(--border-color);
  border-radius: 6px;
  color: var(--text-secondary);
  font-size: 0.9rem;
  font-style: italic;
  margin-bottom: 1rem;
}
</style>
