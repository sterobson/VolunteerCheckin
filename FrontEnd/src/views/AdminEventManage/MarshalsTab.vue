<template>
  <div class="marshals-tab">
    <div class="marshals-tab-header">
      <h2>{{ terms.people }} management</h2>
      <div class="button-group">
        <button @click="$emit('add-marshal')" class="btn btn-primary">
          Add {{ terms.person.toLowerCase() }}
        </button>
        <button @click="$emit('import-marshals')" class="btn btn-secondary">
          Import CSV
        </button>
      </div>
    </div>

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
                <td>{{ getLocationName(assignment.locationId) }}</td>
                <td @click.stop>
                  <select
                    :value="getAssignmentStatusValue(assignment)"
                    @change="$emit('change-assignment-status', { assignment, status: $event.target.value })"
                    class="status-select hide-on-mobile"
                    :class="getStatusClass(assignment)"
                  >
                    <option value="not-checked-in">Not checked-in</option>
                    <option value="checked-in">Checked-in</option>
                    <option value="admin-checked-in">Admin checked-in</option>
                    <option value="wrong-location">Checked-in but not at correct location</option>
                  </select>
                  <span class="status-icon show-on-mobile" :class="getStatusClass(assignment)">
                    {{ getStatusIcon(assignment) }}
                  </span>
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
              <td style="color: #999; font-style: italic;">No {{ terms.checkpoint.toLowerCase() }} assigned</td>
              <td>
                <span class="hide-on-mobile">-</span>
                <span class="status-icon show-on-mobile" style="color: #999;">-</span>
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
  </div>
</template>

<script setup>
import { ref, computed, defineProps, defineEmits } from 'vue';
import { sortAlphanumeric } from '../../utils/sortingHelpers';
import { getStatusIcon } from '../../utils/statusHelpers';
import { useTerminology } from '../../composables/useTerminology';

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
  'change-assignment-status',
]);

const sortBy = ref('name');
const sortOrder = ref('asc');

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

const getLocationName = (locationId) => {
  const location = props.locations.find(l => l.id === locationId);
  return location ? location.name : 'Unknown';
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

const sortedMarshals = computed(() => {
  const sorted = [...props.marshals];

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

const getAssignmentStatusValue = (assignment) => {
  if (assignment.isAdminCheckedIn) return 'admin-checked-in';
  if (assignment.isWrongLocation) return 'wrong-location';
  if (assignment.isCheckedIn) return 'checked-in';
  return 'not-checked-in';
};

const getStatusClass = (assignment) => {
  if (assignment.isCheckedIn) return 'status-checked-in';
  return 'status-not-checked-in';
};
</script>

<style scoped>
.marshals-tab {
  width: 100%;
}

.marshals-tab-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 2rem;
}

.marshals-tab-header h2 {
  margin: 0;
  font-size: 1.5rem;
  color: var(--text-primary);
}

.button-group {
  display: flex;
  gap: 1rem;
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

.status-select {
  padding: 0.25rem 0.5rem;
  border: 1px solid var(--input-border);
  border-radius: 4px;
  font-size: 0.85rem;
  cursor: pointer;
  background: var(--input-bg);
  color: var(--text-primary);
}

.status-select.status-checked-in {
  color: var(--accent-success);
  border-color: var(--accent-success);
}

.status-select.status-not-checked-in {
  color: var(--text-secondary);
}

.status-icon {
  font-size: 1.25rem;
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
