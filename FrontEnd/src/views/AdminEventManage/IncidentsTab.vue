<template>
  <div class="incidents-tab">
    <!-- Row 1: Action buttons + status pills -->
    <div class="tab-header">
      <div class="button-group">
        <button @click="$emit('report-incident')" class="btn btn-primary">
          Report incident
        </button>
      </div>
      <div class="status-pills" v-if="incidents.length > 0">
        <StatusPill
          variant="neutral"
          :active="activeFilter === 'all'"
          @click="setFilter('all')"
        >
          {{ incidents.length }} total
        </StatusPill>
        <StatusPill
          v-if="openCount > 0"
          variant="info"
          :active="activeFilter === 'open'"
          @click="setFilter('open')"
        >
          {{ openCount }} open
        </StatusPill>
        <StatusPill
          v-if="inProgressCount > 0"
          variant="purple"
          :active="activeFilter === 'in-progress'"
          @click="setFilter('in-progress')"
        >
          {{ inProgressCount }} in progress
        </StatusPill>
        <StatusPill
          v-if="resolvedCount > 0"
          variant="success"
          :active="activeFilter === 'resolved'"
          @click="setFilter('resolved')"
        >
          {{ resolvedCount }} resolved
        </StatusPill>
        <StatusPill
          v-if="closedCount > 0"
          variant="neutral"
          :active="activeFilter === 'closed'"
          @click="setFilter('closed')"
        >
          {{ closedCount }} closed
        </StatusPill>
      </div>
    </div>

    <!-- Filters -->
    <div class="filters-section">
      <div class="search-group">
        <input
          v-model="searchQuery"
          type="text"
          class="search-input"
          placeholder="Search by title, description, location, area, or person..."
        />
      </div>
    </div>

    <!-- Incidents List -->
    <div class="incidents-list">
      <div v-if="loading" class="loading-state">
        Loading incidents...
      </div>

      <div v-else-if="filteredIncidents.length === 0" class="empty-state">
        <p>{{ (searchQuery || activeFilter !== 'all') ? 'No incidents match your search or filter.' : 'No incidents reported yet.' }}</p>
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
import StatusPill from '../../components/StatusPill.vue';

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

const searchQuery = ref('');
const activeFilter = ref('all');

const setFilter = (filter) => {
  activeFilter.value = filter;
};

// Status counts
const openCount = computed(() => {
  return props.incidents.filter(i => i.status === 'open').length;
});

const inProgressCount = computed(() => {
  return props.incidents.filter(i => i.status === 'in_progress' || i.status === 'acknowledged').length;
});

const resolvedCount = computed(() => {
  return props.incidents.filter(i => i.status === 'resolved').length;
});

const closedCount = computed(() => {
  return props.incidents.filter(i => i.status === 'closed').length;
});

// Filtering by status pill and search query
const filteredIncidents = computed(() => {
  let result = props.incidents;

  // Filter by status pill
  if (activeFilter.value === 'open') {
    result = result.filter(i => i.status === 'open');
  } else if (activeFilter.value === 'in-progress') {
    result = result.filter(i => i.status === 'in_progress' || i.status === 'acknowledged');
  } else if (activeFilter.value === 'resolved') {
    result = result.filter(i => i.status === 'resolved');
  } else if (activeFilter.value === 'closed') {
    result = result.filter(i => i.status === 'closed');
  }

  // Filter by search query
  if (searchQuery.value.trim()) {
    const searchTerms = searchQuery.value.toLowerCase().trim().split(/\s+/);

    result = result.filter(incident => {
      // Build searchable text from all relevant fields
      const checkpointText = `${incident.context?.checkpoint?.name || ''} ${incident.context?.checkpoint?.description || ''}`;
      const areaText = incident.area?.areaName || '';
      const peopleText = [
        incident.reportedBy?.name || '',
        ...(incident.context?.peopleAtCheckpoint || []).map(p => p.name || ''),
      ].join(' ');
      const notesText = (incident.updates || []).map(u => u.note || '').join(' ');

      const searchableText = `${incident.title || ''} ${incident.description || ''} ${checkpointText} ${areaText} ${peopleText} ${notesText}`.toLowerCase();

      // All search terms must match
      return searchTerms.every(term => searchableText.includes(term));
    });
  }

  return result;
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

.status-pills {
  display: flex;
  gap: 0.5rem;
  flex-wrap: wrap;
}

.btn-primary {
  background: var(--accent-primary);
  color: white;
}

.btn-primary:hover {
  background: var(--accent-primary-hover);
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
  padding: 0.5rem 1rem;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  font-size: 0.9rem;
  font-weight: 500;
  transition: background-color 0.2s;
}

@media (max-width: 768px) {
  .tab-header {
    flex-direction: column;
    align-items: flex-start;
  }

  .button-group {
    width: 100%;
    flex-direction: column;
  }

  .button-group button {
    width: 100%;
  }
}
</style>
