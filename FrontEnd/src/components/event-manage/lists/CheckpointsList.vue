<template>
  <div class="checkpoints-tab">
    <!-- Row 1: Action buttons + status pills -->
    <div class="tab-header">
      <div class="button-group">
        <button @click="$emit('add-checkpoint-manually')" class="btn btn-primary">
          Add {{ termsLower.checkpoint }}
        </button>
        <button @click="$emit('import-checkpoints')" class="btn btn-secondary">
          Import CSV
        </button>
      </div>
      <div class="status-pills" v-if="locations.length > 0">
        <StatusPill
          variant="neutral"
          :active="activeFilter === 'all'"
          @click="setFilter('all')"
        >
          {{ locations.length }} total
        </StatusPill>
        <!-- Staffing status pills -->
        <StatusPill
          v-if="fullCount > 0 && fullCount < locations.length"
          variant="success"
          :active="activeFilter === 'full'"
          @click="setFilter('full')"
        >
          {{ fullCount }} fully staffed
        </StatusPill>
        <StatusPill
          v-if="partialCount > 0 && partialCount < locations.length"
          variant="warning"
          :active="activeFilter === 'partial'"
          @click="setFilter('partial')"
        >
          {{ partialCount }} partially staffed
        </StatusPill>
        <StatusPill
          v-if="missingCount > 0 && missingCount < locations.length"
          variant="danger"
          :active="activeFilter === 'missing'"
          @click="setFilter('missing')"
        >
          {{ missingCount }} unstaffed
        </StatusPill>
        <!-- Check-in status pills -->
        <StatusPill
          v-if="fullyCheckedInCount > 0 && fullyCheckedInCount < locations.length"
          variant="success"
          :active="activeFilter === 'checkin-full'"
          @click="setFilter('checkin-full')"
        >
          {{ fullyCheckedInCount }} fully checked in
        </StatusPill>
        <StatusPill
          v-if="partiallyCheckedInCount > 0 && partiallyCheckedInCount < locations.length"
          variant="warning"
          :active="activeFilter === 'checkin-partial'"
          @click="setFilter('checkin-partial')"
        >
          {{ partiallyCheckedInCount }} partially checked in
        </StatusPill>
        <StatusPill
          v-if="notCheckedInCount > 0 && notCheckedInCount < locations.length"
          variant="danger"
          :active="activeFilter === 'checkin-none'"
          @click="setFilter('checkin-none')"
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
          :placeholder="`Search by name, description, ${termsLower.area}, or ${termsLower.person}...`"
        />
      </div>
    </div>

    <!-- Row 3: Content -->
    <CheckpointsGrid
      :checkpoints="sortedLocations"
      :areas="areas"
      :empty-message="emptyMessage"
      @select="$emit('select-location', $event)"
    />
  </div>
</template>

<script setup>
import { defineProps, defineEmits, computed, ref } from 'vue';
import { alphanumericCompare } from '../../../utils/sortUtils';
import { useTerminology } from '../../../composables/useTerminology';
import StatusPill from '../../StatusPill.vue';
import CheckpointsGrid from '../CheckpointsGrid.vue';

const { termsLower } = useTerminology();

const props = defineProps({
  locations: {
    type: Array,
    required: true,
  },
  areas: {
    type: Array,
    default: () => [],
  },
});

defineEmits(['add-checkpoint-manually', 'import-checkpoints', 'select-location']);

const searchQuery = ref('');
const activeFilter = ref('all');

const setFilter = (filter) => {
  activeFilter.value = filter;
};

// Staffing status pill counts (based on assignments)
const getAssignedCount = (loc) => (loc.assignments || []).length;
const getCheckedInCount = (loc) => (loc.assignments || []).filter(a => a.isCheckedIn).length;

const fullCount = computed(() => {
  return props.locations.filter(loc => getAssignedCount(loc) >= loc.requiredMarshals && loc.requiredMarshals > 0).length;
});

const partialCount = computed(() => {
  return props.locations.filter(loc => getAssignedCount(loc) > 0 && getAssignedCount(loc) < loc.requiredMarshals).length;
});

const missingCount = computed(() => {
  return props.locations.filter(loc => getAssignedCount(loc) === 0 && loc.requiredMarshals > 0).length;
});

// Check-in status pill counts (based on whether assigned marshals have checked in)
const fullyCheckedInCount = computed(() => {
  return props.locations.filter(loc => {
    const assigned = getAssignedCount(loc);
    const checkedIn = getCheckedInCount(loc);
    return assigned > 0 && checkedIn >= assigned;
  }).length;
});

const partiallyCheckedInCount = computed(() => {
  return props.locations.filter(loc => {
    const assigned = getAssignedCount(loc);
    const checkedIn = getCheckedInCount(loc);
    return assigned > 0 && checkedIn > 0 && checkedIn < assigned;
  }).length;
});

const notCheckedInCount = computed(() => {
  return props.locations.filter(loc => {
    const assigned = getAssignedCount(loc);
    const checkedIn = getCheckedInCount(loc);
    return assigned > 0 && checkedIn === 0;
  }).length;
});

// Helper to get area name for search
const getAreaName = (areaId) => {
  const area = props.areas.find((a) => a.id === areaId);
  return area ? area.name : null;
};

// Filtering by status pill and search query
const filteredLocations = computed(() => {
  let result = props.locations;

  // Filter by staffing status pill (based on assignments)
  if (activeFilter.value === 'full') {
    result = result.filter(loc => getAssignedCount(loc) >= loc.requiredMarshals && loc.requiredMarshals > 0);
  } else if (activeFilter.value === 'partial') {
    result = result.filter(loc => getAssignedCount(loc) > 0 && getAssignedCount(loc) < loc.requiredMarshals);
  } else if (activeFilter.value === 'missing') {
    result = result.filter(loc => getAssignedCount(loc) === 0 && loc.requiredMarshals > 0);
  }
  // Filter by check-in status pill
  else if (activeFilter.value === 'checkin-full') {
    result = result.filter(loc => {
      const assigned = getAssignedCount(loc);
      const checkedIn = getCheckedInCount(loc);
      return assigned > 0 && checkedIn >= assigned;
    });
  } else if (activeFilter.value === 'checkin-partial') {
    result = result.filter(loc => {
      const assigned = getAssignedCount(loc);
      const checkedIn = getCheckedInCount(loc);
      return assigned > 0 && checkedIn > 0 && checkedIn < assigned;
    });
  } else if (activeFilter.value === 'checkin-none') {
    result = result.filter(loc => {
      const assigned = getAssignedCount(loc);
      const checkedIn = getCheckedInCount(loc);
      return assigned > 0 && checkedIn === 0;
    });
  }

  // Filter by search query
  if (searchQuery.value.trim()) {
    const searchTerms = searchQuery.value.toLowerCase().trim().split(/\s+/);

    result = result.filter(location => {
      // Build searchable text
      const areaIds = location.areaIds || location.AreaIds || [];
      const areaNames = areaIds.map(id => getAreaName(id) || '').join(' ');
      const marshalNames = (location.assignments || []).map(a => a.marshalName || '').join(' ');

      const searchableText = `${location.name || ''} ${location.description || ''} ${areaNames} ${marshalNames}`.toLowerCase();

      // All search terms must match
      return searchTerms.every(term => searchableText.includes(term));
    });
  }

  return result;
});

const sortedLocations = computed(() => {
  return [...filteredLocations.value].sort((a, b) => alphanumericCompare(a.name, b.name));
});

// Empty message based on filters
const emptyMessage = computed(() => {
  const hasSearch = searchQuery.value.trim();

  if (activeFilter.value === 'full') {
    if (hasSearch) {
      return `No fully staffed ${termsLower.value.checkpoints} match your search.`;
    }
    return `No ${termsLower.value.checkpoints} are fully staffed.`;
  }
  if (activeFilter.value === 'partial') {
    if (hasSearch) {
      return `No partially staffed ${termsLower.value.checkpoints} match your search.`;
    }
    return `No ${termsLower.value.checkpoints} are partially staffed.`;
  }
  if (activeFilter.value === 'missing') {
    if (hasSearch) {
      return `No unstaffed ${termsLower.value.checkpoints} match your search.`;
    }
    return `No ${termsLower.value.checkpoints} are unstaffed.`;
  }
  if (activeFilter.value === 'checkin-full') {
    if (hasSearch) {
      return `No fully checked in ${termsLower.value.checkpoints} match your search.`;
    }
    return `No ${termsLower.value.checkpoints} are fully checked in.`;
  }
  if (activeFilter.value === 'checkin-partial') {
    if (hasSearch) {
      return `No partially checked in ${termsLower.value.checkpoints} match your search.`;
    }
    return `No ${termsLower.value.checkpoints} are partially checked in.`;
  }
  if (activeFilter.value === 'checkin-none') {
    if (hasSearch) {
      return `No ${termsLower.value.checkpoints} without check-ins match your search.`;
    }
    return `No ${termsLower.value.checkpoints} are without check-ins.`;
  }
  if (hasSearch) {
    return `No ${termsLower.value.checkpoints} match your search.`;
  }
  return `No ${termsLower.value.checkpoints} yet. Add one to get started!`;
});
</script>

<style scoped>
.checkpoints-tab {
  width: 100%;
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
</style>
