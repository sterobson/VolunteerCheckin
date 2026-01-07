<template>
  <div class="incidents-tab">
    <div class="incidents-tab-header">
      <div class="header-left">
        <h2>Incidents</h2>
        <div class="incidents-summary" v-if="incidents.length > 0">
          <span class="summary-item open" v-if="openCount > 0">{{ openCount }} open</span>
          <span class="summary-item in-progress" v-if="inProgressCount > 0">{{ inProgressCount }} in progress</span>
          <span class="summary-item resolved" v-if="resolvedCount > 0">{{ resolvedCount }} resolved</span>
        </div>
      </div>
      <button @click="$emit('report-incident')" class="btn btn-primary">
        Report incident
      </button>
    </div>

    <!-- Filters -->
    <div class="filters-section">
      <div class="filter-group">
        <h4>Filter by status:</h4>
        <div class="status-filters">
          <label
            v-for="status in statuses"
            :key="status.value"
            class="filter-checkbox"
          >
            <input
              type="checkbox"
              :checked="filterStatuses.includes(status.value)"
              @change="toggleStatus(status.value)"
            />
            <span class="status-dot" :class="status.value"></span>
            {{ status.label }}
          </label>
        </div>
      </div>

      <div class="filter-group">
        <h4>Filter by severity:</h4>
        <div class="severity-filters">
          <label
            v-for="severity in severities"
            :key="severity.value"
            class="filter-checkbox"
          >
            <input
              type="checkbox"
              :checked="filterSeverities.includes(severity.value)"
              @change="toggleSeverity(severity.value)"
            />
            <span class="severity-dot" :class="severity.value"></span>
            {{ severity.label }}
          </label>
        </div>
      </div>

      <button v-if="hasActiveFilters" @click="clearFilters" class="btn btn-secondary btn-small">
        Clear filters
      </button>
    </div>

    <!-- Incidents List -->
    <div class="incidents-list">
      <div v-if="loading" class="loading-state">
        Loading incidents...
      </div>

      <div v-else-if="filteredIncidents.length === 0" class="empty-state">
        <p>{{ hasActiveFilters ? 'No incidents match the selected filters.' : 'No incidents reported yet.' }}</p>
      </div>

      <IncidentCard
        v-for="incident in sortedIncidents"
        :key="incident.incidentId"
        :incident="incident"
        @select="$emit('select-incident', incident)"
      />
    </div>
  </div>
</template>

<script setup>
import { ref, computed, defineProps, defineEmits } from 'vue';
import IncidentCard from '../../components/IncidentCard.vue';

const props = defineProps({
  incidents: {
    type: Array,
    required: true,
  },
  areas: {
    type: Array,
    default: () => [],
  },
  loading: {
    type: Boolean,
    default: false,
  },
});

const emit = defineEmits(['select-incident', 'report-incident']);

const statuses = [
  { value: 'open', label: 'Open' },
  { value: 'acknowledged', label: 'Acknowledged' },
  { value: 'in_progress', label: 'In Progress' },
  { value: 'resolved', label: 'Resolved' },
  { value: 'closed', label: 'Closed' },
];

const severities = [
  { value: 'critical', label: 'Critical' },
  { value: 'high', label: 'High' },
  { value: 'medium', label: 'Medium' },
  { value: 'low', label: 'Low' },
];

const filterStatuses = ref(['open', 'acknowledged', 'in_progress', 'resolved', 'closed']);
const filterSeverities = ref(['critical', 'high', 'medium', 'low']);

const hasActiveFilters = computed(() => {
  return filterStatuses.value.length < 5 || filterSeverities.value.length < 4;
});

const openCount = computed(() => {
  return props.incidents.filter(i => i.status === 'open').length;
});

const inProgressCount = computed(() => {
  return props.incidents.filter(i => i.status === 'in_progress' || i.status === 'acknowledged').length;
});

const resolvedCount = computed(() => {
  return props.incidents.filter(i => i.status === 'resolved' || i.status === 'closed').length;
});

const toggleStatus = (status) => {
  const index = filterStatuses.value.indexOf(status);
  if (index >= 0) {
    filterStatuses.value.splice(index, 1);
  } else {
    filterStatuses.value.push(status);
  }
};

const toggleSeverity = (severity) => {
  const index = filterSeverities.value.indexOf(severity);
  if (index >= 0) {
    filterSeverities.value.splice(index, 1);
  } else {
    filterSeverities.value.push(severity);
  }
};

const clearFilters = () => {
  filterStatuses.value = ['open', 'acknowledged', 'in_progress', 'resolved', 'closed'];
  filterSeverities.value = ['critical', 'high', 'medium', 'low'];
};

const filteredIncidents = computed(() => {
  return props.incidents.filter(incident => {
    const statusMatch = filterStatuses.value.includes(incident.status);
    const severityMatch = filterSeverities.value.includes(incident.severity);
    return statusMatch && severityMatch;
  });
});

const sortedIncidents = computed(() => {
  return [...filteredIncidents.value].sort((a, b) => {
    // Sort by date, most recent first
    return new Date(b.incidentTime || b.createdAt) - new Date(a.incidentTime || a.createdAt);
  });
});
</script>

<style scoped>
.incidents-tab {
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
}

.incidents-tab-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: 1rem;
  margin-bottom: 1rem;
}

.header-left {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.incidents-tab-header h2 {
  margin: 0;
  font-size: 1.5rem;
  color: var(--text-primary);
}

.incidents-summary {
  display: flex;
  gap: 1rem;
}

.btn-primary {
  background: var(--accent-primary);
  color: white;
}

.btn-primary:hover {
  opacity: 0.9;
}

.summary-item {
  padding: 0.25rem 0.75rem;
  border-radius: 12px;
  font-size: 0.85rem;
  font-weight: 500;
}

.summary-item.open {
  background: #e3f2fd;
  color: #1565c0;
}

.summary-item.in-progress {
  background: #f3e5f5;
  color: #7b1fa2;
}

.summary-item.resolved {
  background: #e8f5e9;
  color: #2e7d32;
}

.filters-section {
  display: flex;
  flex-wrap: wrap;
  gap: 1.5rem;
  padding: 1rem;
  background: var(--bg-tertiary);
  border-radius: 8px;
  align-items: flex-start;
}

.filter-group {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
  min-width: 200px;
}

.filter-group h4 {
  margin: 0;
  font-size: 0.9rem;
  font-weight: 600;
  color: var(--text-primary);
}

.status-filters,
.severity-filters {
  display: flex;
  flex-wrap: wrap;
  gap: 0.75rem;
}

.filter-checkbox {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  font-size: 0.9rem;
  cursor: pointer;
  color: var(--text-primary);
}

.filter-checkbox input[type="checkbox"] {
  cursor: pointer;
  width: 1rem;
  height: 1rem;
  flex-shrink: 0;
}

.status-dot,
.severity-dot {
  width: 10px;
  height: 10px;
  border-radius: 50%;
}

/* Status dots */
.status-dot.open { background: #1565c0; }
.status-dot.acknowledged { background: #e65100; }
.status-dot.in_progress { background: #7b1fa2; }
.status-dot.resolved { background: #2e7d32; }
.status-dot.closed { background: #616161; }

/* Severity dots */
.severity-dot.critical { background: #dc3545; }
.severity-dot.high { background: #fd7e14; }
.severity-dot.medium { background: #ffc107; }
.severity-dot.low { background: #667eea; }

.loading-state,
.empty-state {
  text-align: center;
  padding: 3rem 1rem;
  color: var(--text-muted);
  font-style: italic;
}

.incidents-list {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.btn {
  padding: 0.6rem 1.2rem;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  font-size: 0.9rem;
  font-weight: 500;
  transition: background-color 0.2s;
}

.btn-small {
  padding: 0.4rem 0.8rem;
  font-size: 0.85rem;
}

.btn-secondary {
  background: #6c757d;
  color: white;
}

.btn-secondary:hover {
  background: #545b62;
}

@media (max-width: 768px) {
  .filters-section {
    flex-direction: column;
  }

  .filter-group {
    width: 100%;
  }

  .incidents-tab-header {
    flex-direction: column;
    align-items: flex-start;
  }
}
</style>
