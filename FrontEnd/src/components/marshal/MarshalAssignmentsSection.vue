<template>
  <div class="accordion-section">
    <button
      class="accordion-header"
      :class="{ active: isExpanded }"
      @click="$emit('toggle')"
    >
      <span class="accordion-title">
        <span class="section-icon" v-html="getIcon('checkpoint')"></span>
        Your {{ assignments.length === 1 ? termsLower.checkpoint : termsLower.checkpoints }}<span v-if="assignments.length > 1" class="header-count"> ({{ assignments.length }})</span>
      </span>
      <span class="accordion-icon">{{ isExpanded ? '−' : '+' }}</span>
    </button>
    <div v-if="isExpanded" class="accordion-content assignments-accordion-content">
      <div v-if="assignments.length === 0" class="empty-state">
        <p>You don't have any {{ termsLower.checkpoint }} assignments yet.</p>
      </div>
      <div v-else class="checkpoint-accordion">
        <CheckpointCard
          v-for="assign in assignments"
          :key="assign.id"
          :ref="(el) => setCheckpointRef(assign.id, el)"
          :assignment="assign"
          :is-expanded="expandedCheckpointId === assign.id"
          :all-locations="allLocations"
          :route="route"
          :route-color="routeColor"
          :route-style="routeStyle"
          :route-weight="routeWeight"
          :user-location="userLocation"
          :toolbar-actions="getToolbarActions(assign.id)"
          :has-dynamic-assignment="hasDynamicAssignment"
          :current-marshal-id="currentMarshalId"
          :current-marshal-name="currentMarshalName"
          :is-checking-in="checkingInId === assign.id"
          :check-in-error="checkingInAssignmentId === assign.id ? checkInError : ''"
          :checking-in-marshal-id="checkingInMarshalId"
          :is-area-lead-for-checkpoint="isAreaLeadForAreas(assign.areaIds)"
          :expanded-marshal-id="expandedMarshalId"
          :notes="getNotesForCheckpoint(assign.locationId)"
          :updating-location="updatingLocation"
          :auto-update-enabled="autoUpdateEnabled"
          :area-lead-area-ids="areaLeadAreaIds"
          :all-assignment-location-ids="allAssignmentLocationIds"
          @toggle="$emit('toggle-checkpoint', assign.id)"
          @map-click="(e) => $emit('map-click', e)"
          @location-click="(e) => $emit('location-click', e)"
          @action-click="(e) => $emit('action-click', assign, e)"
          @visibility-change="(e) => $emit('visibility-change', assign.id, e)"
          @update-location="$emit('update-location', assign)"
          @check-in="$emit('check-in', assign)"
          @toggle-marshal="(id) => $emit('toggle-marshal', id)"
          @marshal-check-in="(m) => $emit('marshal-check-in', m, assign.locationId)"
          @toggle-auto-update="$emit('toggle-auto-update', assign)"
          @show-marshal-qr="(m) => $emit('show-marshal-qr', m)"
        />
      </div>
    </div>
  </div>
</template>

<script setup>
import { defineProps, defineEmits, defineExpose, computed } from 'vue';
import { getIcon } from '../../utils/icons';
import { useTerminology } from '../../composables/useTerminology';
import CheckpointCard from './CheckpointCard.vue';

const { terms, termsLower } = useTerminology();

const props = defineProps({
  isExpanded: {
    type: Boolean,
    default: false,
  },
  assignments: {
    type: Array,
    default: () => [],
  },
  expandedCheckpointId: {
    type: String,
    default: null,
  },
  allLocations: {
    type: Array,
    default: () => [],
  },
  route: {
    type: Array,
    default: () => [],
  },
  routeColor: {
    type: String,
    default: '',
  },
  routeStyle: {
    type: String,
    default: '',
  },
  routeWeight: {
    type: Number,
    default: null,
  },
  userLocation: {
    type: Object,
    default: null,
  },
  hasDynamicAssignment: {
    type: Boolean,
    default: false,
  },
  currentMarshalId: {
    type: String,
    default: '',
  },
  currentMarshalName: {
    type: String,
    default: '',
  },
  checkingInId: {
    type: String,
    default: null,
  },
  checkingInAssignmentId: {
    type: String,
    default: null,
  },
  checkInError: {
    type: String,
    default: '',
  },
  checkingInMarshalId: {
    type: String,
    default: null,
  },
  expandedMarshalId: {
    type: String,
    default: null,
  },
  updatingLocation: {
    type: Boolean,
    default: false,
  },
  autoUpdateEnabled: {
    type: Boolean,
    default: false,
  },
  areaLeadAreaIds: {
    type: Array,
    default: () => [],
  },
  getToolbarActions: {
    type: Function,
    default: () => [],
  },
  isAreaLeadForAreas: {
    type: Function,
    default: () => false,
  },
  getNotesForCheckpoint: {
    type: Function,
    default: () => [],
  },
});

defineEmits([
  'toggle',
  'toggle-checkpoint',
  'map-click',
  'location-click',
  'action-click',
  'visibility-change',
  'update-location',
  'check-in',
  'toggle-marshal',
  'marshal-check-in',
  'toggle-auto-update',
  'show-marshal-qr',
]);

// Computed list of all assignment location IDs for scope checking
const allAssignmentLocationIds = computed(() => {
  return props.assignments.map(a => a.locationId);
});

// Store refs to checkpoint cards
const checkpointRefs = {};

const setCheckpointRef = (id, el) => {
  if (el) {
    checkpointRefs[id] = el;
  } else {
    delete checkpointRefs[id];
  }
};

defineExpose({
  checkpointRefs,
});
</script>

<style scoped>
/* Accordion styles */
.accordion-section {
  background: var(--card-bg);
  border-radius: 12px;
  box-shadow: var(--shadow-sm);
  overflow: hidden;
  margin-bottom: 0.5rem;
}

.accordion-header {
  width: 100%;
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 0.5rem;
  padding: 1.25rem 1.5rem;
  background: var(--card-bg);
  border: none;
  cursor: pointer;
  text-align: left;
  font-size: 1rem;
  font-weight: 600;
  color: var(--text-dark);
  transition: background 0.2s;
}

.accordion-header:hover {
  background: var(--bg-secondary);
}

.accordion-header.active {
  background: var(--brand-primary-bg);
  color: var(--brand-primary);
}

.accordion-title {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.header-count {
  font-style: italic;
  opacity: 0.6;
}

.section-icon {
  display: flex;
  align-items: center;
  justify-content: center;
  color: var(--brand-primary);
}

.section-icon :deep(svg) {
  width: 18px;
  height: 18px;
}

.accordion-icon {
  font-size: 1.5rem;
  font-weight: 300;
  color: var(--brand-primary);
}

.accordion-content {
  padding: 1rem 1.5rem 1.5rem;
  border-top: 1px solid var(--border-light);
}

/* Component-specific styles */
.assignments-accordion-content {
  padding: 0.5rem;
}

.empty-state {
  text-align: center;
  padding: 1rem;
  color: var(--text-secondary);
}

.empty-state p {
  margin: 0;
}

.checkpoint-accordion {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

/* Wide screen layout - multi-column checkpoints */
@media (min-width: 1200px) {
  .checkpoint-accordion {
    display: grid;
    grid-template-columns: repeat(2, 1fr);
    gap: 0.75rem;
  }

  /* Expanded checkpoint spans full width */
  .checkpoint-accordion :deep(.checkpoint-accordion-section:has(.checkpoint-accordion-header.active)) {
    grid-column: 1 / -1;
  }
}

/* Extra wide screens - 3 columns */
@media (min-width: 1600px) {
  .checkpoint-accordion {
    grid-template-columns: repeat(3, 1fr);
  }
}
</style>
