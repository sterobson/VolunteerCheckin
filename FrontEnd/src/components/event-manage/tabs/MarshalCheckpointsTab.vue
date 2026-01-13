<template>
  <div class="tab-content">
    <!-- Read-only notice when opened from a checkpoint -->
    <div v-if="lockedCheckpointId" class="read-only-notice">
      Viewing from {{ termsLower.checkpoint }} - {{ termsLower.checkpoints }} cannot be modified here.
    </div>

    <h3 class="section-title">Assigned {{ termsLower.checkpoints }} ({{ totalAssignments }})</h3>

    <CardsGrid
      :is-empty="assignments.length === 0"
      :empty-message="`No ${termsLower.checkpoints} assigned yet.`"
      :empty-hint="lockedCheckpointId ? '' : `Use the dropdown below to assign this ${termsLower.person} to ${termsLower.checkpoints}.`"
    >
      <MarshalCheckpointCard
        v-for="assignment in sortedAssignments"
        :key="assignment.id"
        :assignment="assignment"
        :location="getLocation(assignment.locationId)"
        :areas="areas"
        :effective-check-in-status="getEffectiveCheckInStatus(assignment)"
        :has-unsaved-changes="pendingCheckInChanges.has(assignment.id)"
        :is-locked="!!lockedCheckpointId"
        :is-marked-for-removal="isAssignmentMarkedForRemoval(assignment.id)"
        @click="handleSelectCheckpoint"
        @toggle-check-in="handleToggleCheckIn"
        @remove="handleMarkForRemoval"
        @undo-remove="handleUndoRemoval"
        @distance-click="$emit('distance-click', $event)"
      />
    </CardsGrid>

    <!-- Assign to checkpoint (hidden when locked) -->
    <div v-if="!lockedCheckpointId && sortedAvailableLocations.length > 0" class="form-group" style="margin-top: 1.5rem;">
      <label>Assign to {{ termsLower.checkpoint }}</label>
      <select v-model="selectedLocationId" class="form-input" @change="handleSelectionChange">
        <option value="">Select a {{ termsLower.checkpoint }}...</option>
        <option
          v-for="location in sortedAvailableLocations"
          :key="location.id"
          :value="location.id"
        >
          {{ location.name }}<template v-if="location.description"> - {{ location.description }}</template>
        </option>
      </select>
    </div>

    <!-- Checkout confirmation modal -->
    <ConfirmModal
      :show="showCheckoutConfirm"
      title="Confirm check-out"
      :message="checkoutConfirmMessage"
      confirm-text="Check out"
      :is-danger="true"
      @confirm="confirmCheckout"
      @cancel="cancelCheckout"
    />
  </div>
</template>

<script setup>
import { ref, computed, defineProps, defineEmits } from 'vue';
import { useCheckInManagement } from '../../../composables/useCheckInManagement';
import { useTerminology } from '../../../composables/useTerminology';
import CardsGrid from '../../common/CardsGrid.vue';
import MarshalCheckpointCard from '../MarshalCheckpointCard.vue';
import ConfirmModal from '../../ConfirmModal.vue';

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
  allLocations: {
    type: Array,
    default: () => [],
  },
  areas: {
    type: Array,
    default: () => [],
  },
  isNewMarshal: {
    type: Boolean,
    default: false,
  },
  lockedCheckpointId: {
    type: String,
    default: null,
  },
});

const emit = defineEmits(['input', 'remove-assignment', 'assign-to-location', 'select-checkpoint', 'distance-click']);

// Use check-in management composable
const {
  pendingCheckInChanges,
  getEffectiveCheckInStatus,
  handleToggleCheckIn: toggleCheckIn,
} = useCheckInManagement(() => emit('input'));

const selectedLocationId = ref('');
const markedForRemoval = ref(new Set());

const totalAssignments = computed(() => {
  return props.assignments?.length || 0;
});

// Get location object for an assignment
const getLocation = (locationId) => {
  if (!props.allLocations) return null;
  return props.allLocations.find(l => l.id === locationId) || null;
};

// Sort assignments by location name
const sortedAssignments = computed(() => {
  return [...props.assignments].sort((a, b) => {
    return (a.locationName || '').localeCompare(b.locationName || '', undefined, { numeric: true, sensitivity: 'base' });
  });
});

// Check if an assignment is for the locked checkpoint
const isLockedCheckpoint = (assignment) => {
  return props.lockedCheckpointId && assignment.locationId === props.lockedCheckpointId;
};

// Check if an assignment is marked for removal
const isAssignmentMarkedForRemoval = (assignmentId) => {
  return markedForRemoval.value.has(assignmentId);
};

// Handle clicking on a checkpoint to view its details
const handleSelectCheckpoint = (assignment) => {
  if (!assignment.isPending) {
    emit('select-checkpoint', assignment);
  }
};

// Checkout confirmation state
const showCheckoutConfirm = ref(false);
const pendingCheckoutAssignment = ref(null);

// Handle toggle check-in
const handleToggleCheckIn = (assignment) => {
  // Check if this is a checkout (currently checked in, either actually or pending)
  const isCurrentlyCheckedIn = getEffectiveCheckInStatus(assignment);

  if (isCurrentlyCheckedIn) {
    // Show confirmation for checkout
    pendingCheckoutAssignment.value = assignment;
    showCheckoutConfirm.value = true;
  } else {
    // Checking in - proceed directly
    toggleCheckIn(assignment);
  }
};

const confirmCheckout = () => {
  if (pendingCheckoutAssignment.value) {
    toggleCheckIn(pendingCheckoutAssignment.value);
  }
  showCheckoutConfirm.value = false;
  pendingCheckoutAssignment.value = null;
};

const cancelCheckout = () => {
  showCheckoutConfirm.value = false;
  pendingCheckoutAssignment.value = null;
};

const checkoutConfirmMessage = computed(() => {
  if (!pendingCheckoutAssignment.value) return '';
  const marshalName = pendingCheckoutAssignment.value.marshalName || 'this marshal';
  return `Are you sure you want to undo ${marshalName}'s check-in?`;
});

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
    markedForRemoval.value.clear();
    selectedLocationId.value = '';
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

@media (max-width: 640px) {
  .tab-content {
    width: 100%;
  }
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
  font-family: inherit;
  background: var(--input-bg);
  color: var(--text-primary);
}

.section-title {
  margin: 0 0 1rem 0;
  font-size: 1rem;
  color: var(--text-primary);
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
