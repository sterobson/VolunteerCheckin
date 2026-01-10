<template>
  <div class="areas-tab">
    <!-- Row 1: Action buttons + status pills -->
    <div class="tab-header">
      <div class="button-group">
        <button @click="$emit('add-area')" class="btn btn-primary">
          Add {{ termsLower.area }}
        </button>
      </div>
      <div class="status-pills" v-if="areas.length > 0">
        <StatusPill
          variant="neutral"
          :active="false"
        >
          {{ areas.length }} {{ areas.length === 1 ? termsLower.area : termsLower.areas }}
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
          :placeholder="`Search by ${termsLower.area} name or ${termsLower.checkpoint} name...`"
        />
      </div>
    </div>

    <!-- Row 3: Content -->
    <AreasGrid
      :areas="sortedAreas"
      :checkpoints="checkpoints"
      :contacts="contacts"
      :event-people-term="eventPeopleTerm"
      :event-checkpoint-term="eventCheckpointTerm"
      :empty-message="emptyMessage"
      @select="$emit('select-area', $event)"
    />
  </div>
</template>

<script setup>
import { defineProps, defineEmits, computed, ref } from 'vue';
import { alphanumericCompare } from '../../../utils/sortUtils';
import { useTerminology } from '../../../composables/useTerminology';
import StatusPill from '../../StatusPill.vue';
import AreasGrid from '../AreasGrid.vue';

const { terms, termsLower } = useTerminology();

const props = defineProps({
  areas: {
    type: Array,
    required: true,
  },
  checkpoints: {
    type: Array,
    default: () => [],
  },
  contacts: {
    type: Array,
    default: () => [],
  },
  eventPeopleTerm: {
    type: String,
    default: 'Marshals',
  },
  eventCheckpointTerm: {
    type: String,
    default: 'Checkpoints',
  },
});

defineEmits(['add-area', 'select-area']);

const searchQuery = ref('');

// Get checkpoints belonging to an area
const getCheckpointsForArea = (areaId) => {
  return props.checkpoints.filter(c => {
    const areaIds = c.areaIds || c.AreaIds || [];
    return areaIds.includes(areaId) || c.areaId === areaId;
  });
};

// Search filtering - searches area name + checkpoint names + checkpoint descriptions
const filteredAreas = computed(() => {
  if (!searchQuery.value.trim()) {
    return props.areas;
  }

  const searchTerms = searchQuery.value.toLowerCase().trim().split(/\s+/);

  return props.areas.filter(area => {
    // Build searchable text from area and its checkpoints
    const areaCheckpoints = getCheckpointsForArea(area.id);
    const checkpointText = areaCheckpoints
      .map(c => `${c.name || ''} ${c.description || ''}`)
      .join(' ');

    const searchableText = `${area.name || ''} ${area.description || ''} ${checkpointText}`.toLowerCase();

    // All search terms must match
    return searchTerms.every(term => searchableText.includes(term));
  });
});

const sortedAreas = computed(() => {
  return [...filteredAreas.value].sort((a, b) => {
    // Sort by display order first
    if (a.displayOrder !== b.displayOrder) {
      return a.displayOrder - b.displayOrder;
    }

    // Then by name (alphanumeric)
    return alphanumericCompare(a.name, b.name);
  });
});

// Empty message based on search
const emptyMessage = computed(() => {
  if (searchQuery.value.trim()) {
    return `No ${termsLower.value.areas} match your search.`;
  }
  return `No ${termsLower.value.areas} yet. Click "Add ${termsLower.value.area}" to create one.`;
});
</script>

<style scoped>
.areas-tab {
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
