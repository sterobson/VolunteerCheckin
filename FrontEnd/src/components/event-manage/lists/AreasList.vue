<template>
  <div class="areas-tab">
    <!-- Row 1: Action buttons + status pills -->
    <div class="tab-header">
      <div class="button-group">
        <button @click="$emit('add-area')" class="btn btn-primary">
          Add {{ terms.area.toLowerCase() }}
        </button>
      </div>
      <div class="status-pills" v-if="areas.length > 0">
        <span class="status-pill">{{ areas.length }} {{ areas.length === 1 ? terms.area.toLowerCase() : terms.areas.toLowerCase() }}</span>
      </div>
    </div>

    <!-- Row 2: Filters -->
    <div class="filters-section">
      <div class="search-group">
        <input
          v-model="searchQuery"
          type="text"
          class="search-input"
          :placeholder="`Search by ${terms.area.toLowerCase()} name or ${terms.checkpoint.toLowerCase()} name...`"
        />
      </div>
    </div>

    <!-- Row 3: Content -->
    <div class="areas-list">
      <div
        v-for="area in sortedAreas"
        :key="area.id"
        class="area-item"
        :style="{ borderLeftColor: area.color || '#667eea' }"
        @click="$emit('select-area', area)"
      >
        <div class="area-info">
          <div class="area-header">
            <strong>{{ area.name }}</strong>
            <span v-if="area.isDefault" class="default-badge">Default</span>
          </div>
          <p v-if="area.description" class="area-description">
            {{ area.description }}
          </p>
          <div class="area-stats">
            <span class="stat-badge">
              {{ formatAreaCheckpointCount(area) }}
            </span>
            <span class="stat-badge">
              {{ getContactCount(area) }} contact{{ getContactCount(area) === 1 ? '' : 's' }}
            </span>
          </div>
        </div>
      </div>

      <div v-if="filteredAreas.length === 0" class="empty-state">
        <p>{{ searchQuery ? `No ${terms.areas.toLowerCase()} match your search.` : `No ${terms.areas.toLowerCase()} yet. Click "Add ${terms.area.toLowerCase()}" to create one.` }}</p>
      </div>
    </div>
  </div>
</template>

<script setup>
import { defineProps, defineEmits, computed, ref } from 'vue';
import { alphanumericCompare } from '../../../utils/sortUtils';
import { useTerminology, getSingularTerm, getPluralTerm } from '../../../composables/useTerminology';

const { terms } = useTerminology();

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

const getCheckpointCount = (area) => {
  if (area.checkpointCount !== undefined) {
    return area.checkpointCount;
  }
  // Fallback: count from checkpoints array
  return props.checkpoints.filter((c) => c.areaId === area.id).length;
};

const getContactCount = (area) => {
  // Count contacts from EventContacts that are scoped to this area
  return props.contacts.filter(contact => {
    if (!contact.scopeConfigurations) return false;

    return contact.scopeConfigurations.some(config => {
      // Check if this contact is visible to everyone in specific areas and this area is included
      if (config.scope === 'EveryoneInAreas' && config.itemType === 'Area') {
        // ALL_AREAS means all areas
        if (config.ids?.includes('ALL_AREAS')) return true;
        // Check if this specific area is in the list
        return config.ids?.includes(area.id);
      }
      return false;
    });
  }).length;
};

// Get the effective people term for an area
const getAreaPeopleTerm = (area) => {
  return area.peopleTerm || props.eventPeopleTerm || 'Marshals';
};

// Get the effective checkpoint term for an area (resolves "Person points" dynamically)
const getAreaCheckpointTerm = (area, count) => {
  const storedTerm = area.checkpointTerm || props.eventCheckpointTerm || 'Checkpoints';
  const peopleTerm = getAreaPeopleTerm(area);

  if (count === 1) {
    return getSingularTerm('checkpoint', storedTerm, peopleTerm);
  }
  return getPluralTerm('checkpoint', storedTerm, peopleTerm);
};

// Format checkpoint count with area-specific terminology (sentence case)
const formatAreaCheckpointCount = (area) => {
  const count = getCheckpointCount(area);
  const term = getAreaCheckpointTerm(area, count).toLowerCase();
  return `${count} ${term}`;
};
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

.status-pill {
  padding: 0.25rem 0.75rem;
  border-radius: 12px;
  font-size: 0.8rem;
  font-weight: 500;
  background: var(--bg-tertiary);
  color: var(--text-secondary);
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

.areas-list {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.area-item {
  padding: 1rem;
  border: 2px solid var(--border-color);
  border-left: 4px solid var(--accent-primary);
  border-radius: 6px;
  cursor: pointer;
  transition: all 0.3s;
  background: var(--card-bg);
}

.area-item:hover {
  border-color: var(--accent-primary);
  box-shadow: var(--shadow-md);
}

.area-info {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.area-header {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.area-header strong {
  font-size: 1rem;
  color: var(--text-primary);
}

.default-badge {
  padding: 0.2rem 0.5rem;
  background: var(--warning);
  border-radius: 4px;
  font-size: 0.7rem;
  font-weight: 600;
  color: var(--text-primary);
  text-transform: uppercase;
}

.area-description {
  margin: 0;
  color: var(--text-secondary);
  font-size: 0.9rem;
}

.area-stats {
  display: flex;
  gap: 0.5rem;
  flex-wrap: wrap;
}

.stat-badge {
  padding: 0.25rem 0.75rem;
  background: var(--bg-tertiary);
  border-radius: 12px;
  font-size: 0.75rem;
  color: var(--text-secondary);
  font-weight: 500;
}

.empty-state {
  text-align: center;
  padding: 2rem;
  color: var(--text-muted);
}

.empty-state p {
  margin: 0;
  font-size: 0.9rem;
}
</style>
