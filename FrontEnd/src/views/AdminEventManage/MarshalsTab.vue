<template>
  <div class="marshals-tab">
    <!-- Row 1: Action buttons + status pills -->
    <div class="tab-header">
      <div class="button-group">
        <button @click="$emit('add-marshal')" class="btn btn-primary">
          Add {{ terms.person.toLowerCase() }}
        </button>
        <button @click="$emit('import-marshals')" class="btn btn-secondary">
          Import CSV
        </button>
      </div>
      <div class="status-pills" v-if="marshals.length > 0">
        <StatusPill
          variant="neutral"
          :active="activeFilter === 'all'"
          @click="setFilter('all')"
        >
          {{ marshals.length }} total
        </StatusPill>
        <StatusPill
          v-if="unassignedCount > 0 && unassignedCount < marshals.length"
          variant="warning"
          :active="activeFilter === 'unassigned'"
          @click="setFilter('unassigned')"
        >
          {{ unassignedCount }} unassigned
        </StatusPill>
        <StatusPill
          v-if="checkedInCount > 0 && checkedInCount < marshals.length"
          variant="success"
          :active="activeFilter === 'checked-in'"
          @click="setFilter('checked-in')"
        >
          {{ checkedInCount }} checked in
        </StatusPill>
        <StatusPill
          v-if="notCheckedInCount > 0 && notCheckedInCount < marshals.length"
          variant="danger"
          :active="activeFilter === 'not-checked-in'"
          @click="setFilter('not-checked-in')"
        >
          {{ notCheckedInCount }} not checked in
        </StatusPill>
      </div>
    </div>

    <!-- Row 2: Filters -->
    <div class="filters-section">
      <div class="search-group">
        <input
          v-model="searchQuery"
          type="text"
          class="search-input"
          placeholder="Search by name, checkpoint, or description..."
        />
      </div>
    </div>

    <!-- Row 3: Content -->
    <div class="marshals-grid">
      <table class="marshals-table">
        <thead>
          <tr>
            <th @click="changeSortColumn('name')" class="sortable">
              Name
              <span class="sort-indicator" v-if="sortBy === 'name'">
                {{ sortOrder === 'asc' ? '▲' : '▼' }}
              </span>
            </th>
            <th @click="changeSortColumn('checkpoint')" class="sortable">
              {{ terms.checkpoint }}
              <span class="sort-indicator" v-if="sortBy === 'checkpoint'">
                {{ sortOrder === 'asc' ? '▲' : '▼' }}
              </span>
            </th>
            <th @click="changeSortColumn('status')" class="sortable">
              Status
              <span class="sort-indicator" v-if="sortBy === 'status'">
                {{ sortOrder === 'asc' ? '▲' : '▼' }}
              </span>
            </th>
            <th @click="changeSortColumn('lastActive')" class="sortable hide-on-mobile">
              Last active
              <span class="sort-indicator" v-if="sortBy === 'lastActive'">
                {{ sortOrder === 'asc' ? '▲' : '▼' }}
              </span>
            </th>
          </tr>
        </thead>
        <tbody>
          <template v-for="marshal in sortedMarshals" :key="marshal.id">
            <template v-if="getMarshalAssignments(marshal.id).length > 0">
              <tr
                v-for="(assignment, index) in getMarshalAssignments(marshal.id)"
                :key="`${marshal.id}-${assignment.id}`"
                class="marshal-row"
                @click="$emit('select-marshal', marshal)"
              >
                <td v-if="index === 0" :rowspan="getMarshalAssignments(marshal.id).length">
                  {{ marshal.name }}
                </td>
                <td class="checkpoint-cell">
                  <div class="checkpoint-name">{{ getLocationName(assignment.locationId) }}</div>
                  <div v-if="getLocationDescription(assignment.locationId)" class="checkpoint-description">
                    {{ getLocationDescription(assignment.locationId) }}
                  </div>
                </td>
                <td @click.stop class="check-in-cell">
                  <CheckInToggleButton
                    :is-checked-in="assignment.isCheckedIn"
                    :check-in-time="assignment.checkInTime"
                    :check-in-method="assignment.checkInMethod"
                    :checked-in-by="assignment.checkedInBy"
                    :marshal-name="getMarshalName(assignment.marshalId)"
                    :is-loading="checkingInAssignmentId === assignment.id"
                    :distance="getCheckInDistance(assignment)"
                    @toggle="handleToggleCheckIn(assignment)"
                    @distance-click="handleDistanceClick(assignment)"
                  />
                </td>
                <td v-if="index === 0" :rowspan="getMarshalAssignments(marshal.id).length" class="hide-on-mobile last-active-cell">
                  <span v-if="marshal.lastAccessedDate" class="last-active-text">
                    {{ formatRelativeTime(marshal.lastAccessedDate) }}
                  </span>
                  <span v-else class="last-active-never">Never</span>
                </td>
              </tr>
            </template>
            <tr v-else class="marshal-row" @click="$emit('select-marshal', marshal)">
              <td>{{ marshal.name }}</td>
              <td class="no-checkpoint-text">No {{ terms.checkpoint.toLowerCase() }} assigned</td>
              <td>
                <span class="hide-on-mobile">-</span>
                <span class="status-icon show-on-mobile no-checkpoint-text">-</span>
              </td>
              <td class="hide-on-mobile last-active-cell">
                <span v-if="marshal.lastAccessedDate" class="last-active-text">
                  {{ formatRelativeTime(marshal.lastAccessedDate) }}
                </span>
                <span v-else class="last-active-never">Never</span>
              </td>
            </tr>
          </template>
        </tbody>
      </table>
    </div>

    <!-- Check-in location map modal -->
    <CheckInLocationMapModal
      :show="showMapModal"
      :marshal-name="mapModalMarshalName"
      :checkpoint-name="mapModalCheckpointName"
      :check-in-location="mapModalCheckInLocation"
      :checkpoint-location="mapModalCheckpointLocation"
      :distance="mapModalDistance"
      @close="closeMapModal"
    />

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
import { sortAlphanumeric } from '../../utils/sortingHelpers';
import { calculateDistance } from '../../utils/coordinateUtils';
import { useTerminology } from '../../composables/useTerminology';
import StatusPill from '../../components/StatusPill.vue';
import CheckInToggleButton from '../../components/common/CheckInToggleButton.vue';
import CheckInLocationMapModal from '../../components/common/CheckInLocationMapModal.vue';
import ConfirmModal from '../../components/ConfirmModal.vue';

const { terms } = useTerminology();

const props = defineProps({
  marshals: {
    type: Array,
    required: true,
  },
  assignments: {
    type: Array,
    required: true,
  },
  locations: {
    type: Array,
    required: true,
  },
});

const emit = defineEmits([
  'add-marshal',
  'import-marshals',
  'select-marshal',
  'delete-marshal',
  'toggle-check-in',
]);

const checkingInAssignmentId = ref(null);

// Map modal state
const showMapModal = ref(false);
const selectedAssignmentForMap = ref(null);

const setCheckingIn = (assignmentId) => {
  checkingInAssignmentId.value = assignmentId;
};

const handleDistanceClick = (assignment) => {
  selectedAssignmentForMap.value = assignment;
  showMapModal.value = true;
};

const closeMapModal = () => {
  showMapModal.value = false;
  selectedAssignmentForMap.value = null;
};

// Computed values for map modal
const mapModalMarshalName = computed(() => {
  if (!selectedAssignmentForMap.value) return '';
  return getMarshalName(selectedAssignmentForMap.value.marshalId);
});

const mapModalCheckpointName = computed(() => {
  if (!selectedAssignmentForMap.value) return '';
  return getLocationName(selectedAssignmentForMap.value.locationId);
});

const mapModalCheckInLocation = computed(() => {
  if (!selectedAssignmentForMap.value) return null;
  const { checkInLatitude, checkInLongitude } = selectedAssignmentForMap.value;
  if (!checkInLatitude || !checkInLongitude) return null;
  return { lat: checkInLatitude, lng: checkInLongitude };
});

const mapModalCheckpointLocation = computed(() => {
  if (!selectedAssignmentForMap.value) return null;
  const location = getLocation(selectedAssignmentForMap.value.locationId);
  if (!location?.latitude || !location?.longitude) return null;
  return { lat: location.latitude, lng: location.longitude };
});

const mapModalDistance = computed(() => {
  if (!selectedAssignmentForMap.value) return null;
  return getCheckInDistance(selectedAssignmentForMap.value);
});

// Checkout confirmation state
const showCheckoutConfirm = ref(false);
const pendingCheckoutAssignment = ref(null);

const handleToggleCheckIn = (assignment) => {
  // If checking out (already checked in), show confirmation
  if (assignment.isCheckedIn) {
    pendingCheckoutAssignment.value = assignment;
    showCheckoutConfirm.value = true;
  } else {
    // Checking in - proceed directly
    emit('toggle-check-in', assignment);
  }
};

const confirmCheckout = () => {
  if (pendingCheckoutAssignment.value) {
    emit('toggle-check-in', pendingCheckoutAssignment.value);
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
  const marshalName = getMarshalName(pendingCheckoutAssignment.value.marshalId);
  return `Are you sure you want to undo ${marshalName}'s check-in?`;
});

defineExpose({ setCheckingIn });

const sortBy = ref('name');
const sortOrder = ref('asc');
const searchQuery = ref('');
const activeFilter = ref('all');

const setFilter = (filter) => {
  activeFilter.value = filter;
};

const changeSortColumn = (column) => {
  if (sortBy.value === column) {
    sortOrder.value = sortOrder.value === 'asc' ? 'desc' : 'asc';
  } else {
    sortBy.value = column;
    sortOrder.value = 'asc';
  }
};

const getMarshalAssignments = (marshalId) => {
  return props.assignments.filter(a => a.marshalId === marshalId);
};

// Status pill counts
const checkedInMarshalIds = computed(() => {
  return new Set(
    props.assignments.filter(a => a.isCheckedIn).map(a => a.marshalId)
  );
});

const assignedMarshalIds = computed(() => {
  return new Set(props.assignments.map(a => a.marshalId));
});

const checkedInCount = computed(() => {
  return checkedInMarshalIds.value.size;
});

const unassignedCount = computed(() => {
  return props.marshals.filter(m => !assignedMarshalIds.value.has(m.id)).length;
});

const notCheckedInCount = computed(() => {
  // Marshals who are assigned but not checked in
  return props.marshals.filter(m =>
    assignedMarshalIds.value.has(m.id) && !checkedInMarshalIds.value.has(m.id)
  ).length;
});

const getLocation = (locationId) => {
  return props.locations.find(l => l.id === locationId);
};

const getLocationName = (locationId) => {
  const location = getLocation(locationId);
  return location ? location.name : 'Unknown';
};

const getLocationDescription = (locationId) => {
  const location = getLocation(locationId);
  return location ? location.description : '';
};

const getMarshalName = (marshalId) => {
  const marshal = props.marshals.find(m => m.id === marshalId);
  return marshal ? marshal.name : '';
};

/**
 * Calculate distance from check-in location to checkpoint
 * Returns null if GPS data is not available
 */
const getCheckInDistance = (assignment) => {
  const { checkInLatitude, checkInLongitude, locationId } = assignment;
  const location = getLocation(locationId);

  if (!checkInLatitude || !checkInLongitude || !location?.latitude || !location?.longitude) {
    return null;
  }

  return calculateDistance(
    checkInLatitude,
    checkInLongitude,
    location.latitude,
    location.longitude
  );
};

const formatRelativeTime = (dateString) => {
  if (!dateString) return '';
  const date = new Date(dateString);
  const now = new Date();
  const diffMs = now - date;
  const diffMins = Math.floor(diffMs / 60000);
  const diffHours = Math.floor(diffMins / 60);
  const diffDays = Math.floor(diffHours / 24);

  if (diffMins < 1) return 'just now';
  if (diffMins < 60) return `${diffMins}m ago`;
  if (diffHours < 24) return `${diffHours}h ago`;
  if (diffDays === 1) return 'yesterday';
  if (diffDays < 7) return `${diffDays}d ago`;
  return date.toLocaleDateString();
};

// Filtering - by status pill and search query
const filteredMarshals = computed(() => {
  let result = props.marshals;

  // Filter by status pill
  if (activeFilter.value === 'unassigned') {
    result = result.filter(m => !assignedMarshalIds.value.has(m.id));
  } else if (activeFilter.value === 'checked-in') {
    result = result.filter(m => checkedInMarshalIds.value.has(m.id));
  } else if (activeFilter.value === 'not-checked-in') {
    result = result.filter(m =>
      assignedMarshalIds.value.has(m.id) && !checkedInMarshalIds.value.has(m.id)
    );
  }

  // Filter by search query
  if (searchQuery.value.trim()) {
    const searchTerms = searchQuery.value.toLowerCase().trim().split(/\s+/);

    result = result.filter(marshal => {
      // Build searchable text: marshal name + all checkpoint names + descriptions
      const assignments = getMarshalAssignments(marshal.id);
      const checkpointTexts = assignments.map(a => {
        const location = getLocation(a.locationId);
        if (!location) return '';
        return `${location.name || ''} ${location.description || ''}`;
      }).join(' ');

      const searchableText = `${marshal.name} ${checkpointTexts}`.toLowerCase();

      // All search terms must match
      return searchTerms.every(term => searchableText.includes(term));
    });
  }

  return result;
});

const sortedMarshals = computed(() => {
  const sorted = [...filteredMarshals.value];

  sorted.sort((a, b) => {
    let compareValue = 0;

    if (sortBy.value === 'name') {
      compareValue = a.name.localeCompare(b.name);
    } else if (sortBy.value === 'checkpoint') {
      const aAssignments = getMarshalAssignments(a.id);
      const bAssignments = getMarshalAssignments(b.id);
      const aCheckpoint = aAssignments.length > 0 ? getLocationName(aAssignments[0].locationId) : '';
      const bCheckpoint = bAssignments.length > 0 ? getLocationName(bAssignments[0].locationId) : '';
      compareValue = sortAlphanumeric(aCheckpoint, bCheckpoint);
    } else if (sortBy.value === 'status') {
      const aAssignments = getMarshalAssignments(a.id);
      const bAssignments = getMarshalAssignments(b.id);
      const aCheckedIn = aAssignments.length > 0 && aAssignments[0].isCheckedIn ? 1 : 0;
      const bCheckedIn = bAssignments.length > 0 && bAssignments[0].isCheckedIn ? 1 : 0;
      compareValue = bCheckedIn - aCheckedIn;
    } else if (sortBy.value === 'lastActive') {
      const aTime = a.lastAccessedDate ? new Date(a.lastAccessedDate).getTime() : 0;
      const bTime = b.lastAccessedDate ? new Date(b.lastAccessedDate).getTime() : 0;
      compareValue = bTime - aTime; // Most recent first by default
    }

    return sortOrder.value === 'asc' ? compareValue : -compareValue;
  });

  return sorted;
});
</script>

<style scoped>
.marshals-tab {
  width: 100%;
}

.no-checkpoint-text {
  color: var(--text-muted);
  font-style: italic;
}

.tab-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 1rem;
  flex-wrap: wrap;
  gap: 1rem;
}

.button-group {
  display: flex;
  gap: 0.75rem;
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
  color: white;
}

.btn-primary:hover {
  background: var(--accent-primary-hover);
}

.btn-secondary {
  background: var(--bg-tertiary);
  color: var(--text-primary);
}

.btn-secondary:hover {
  background: var(--bg-hover);
}

.status-pills {
  display: flex;
  gap: 0.5rem;
  flex-wrap: wrap;
}

.filters-section {
  margin-bottom: 1rem;
  padding: 0.75rem 1rem;
  background: var(--bg-tertiary);
  border-radius: 8px;
}

.search-group {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.search-input {
  flex: 1;
  max-width: 400px;
  padding: 0.5rem 0.75rem;
  border: 1px solid var(--input-border);
  border-radius: 4px;
  font-size: 0.9rem;
  background: var(--input-bg);
  color: var(--text-primary);
}

.search-input:focus {
  outline: none;
  border-color: var(--accent-primary);
}

.search-input::placeholder {
  color: var(--text-muted);
}

.marshals-grid {
  background: var(--card-bg);
  border-radius: 8px;
  box-shadow: var(--shadow-md);
  overflow-x: auto;
}

.marshals-table {
  width: 100%;
  border-collapse: collapse;
}

.marshals-table th,
.marshals-table td {
  padding: 1rem;
  text-align: left;
  border-bottom: 1px solid var(--border-color);
}

.marshals-table th {
  background: var(--table-header-bg);
  font-weight: 600;
  color: var(--text-secondary);
  font-size: 0.85rem;
  text-transform: uppercase;
  letter-spacing: 0.5px;
  position: sticky;
  top: 0;
  z-index: 10;
}

.marshals-table th.sortable {
  cursor: pointer;
  user-select: none;
  transition: background-color 0.2s;
}

.marshals-table th.sortable:hover {
  background: var(--bg-hover);
}

.sort-indicator {
  margin-left: 0.5rem;
  color: var(--accent-primary);
}

.marshal-row {
  cursor: pointer;
  transition: background-color 0.2s;
}

.marshal-row:hover {
  background: var(--table-row-hover);
}

.checkpoint-cell {
  max-width: 300px;
}

.checkpoint-name {
  font-weight: 500;
  color: var(--text-primary);
}

.checkpoint-description {
  font-size: 0.8rem;
  color: var(--text-muted);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  max-width: 100%;
}

.check-in-cell {
  min-width: 120px;
}

.check-in-cell :deep(.check-in-toggle-container) {
  align-items: flex-start;
}

.last-active-cell {
  font-size: 0.85rem;
}

.last-active-text {
  color: var(--text-secondary);
}

.last-active-never {
  color: var(--text-muted);
  font-style: italic;
}

.action-buttons {
  display: flex;
  gap: 0.5rem;
}

@media (max-width: 768px) {
  .hide-on-mobile {
    display: none;
  }

  .show-on-mobile {
    display: inline-block;
  }

  .marshals-tab-header {
    flex-direction: column;
    align-items: flex-start;
    gap: 1rem;
  }

  .button-group {
    width: 100%;
    flex-direction: column;
  }

  .button-group button {
    width: 100%;
  }
}

@media (min-width: 769px) {
  .show-on-mobile {
    display: none;
  }
}
</style>
