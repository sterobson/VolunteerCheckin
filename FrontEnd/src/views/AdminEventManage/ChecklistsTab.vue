<template>
  <div class="checklists-tab">
    <div class="checklists-tab-header">
      <div class="button-group">
        <button @click="$emit('add-checklist-item')" class="btn btn-primary">
          Add {{ termsLower.checklist }}
        </button>
      </div>
    </div>

    <!-- Filters -->
    <div class="filters-section">
      <div class="search-group">
        <input
          v-model="searchQuery"
          type="text"
          class="search-input"
          :placeholder="`Search ${termsLower.checklists}...`"
        />
      </div>
    </div>

    <!-- Checklist Items List -->
    <div class="checklist-items-list">
      <div v-if="filteredItems.length === 0" class="empty-state">
        <p>{{ searchQuery ? `No ${termsLower.checklists} match your search.` : `No ${termsLower.checklists} yet. Create one to get started!` }}</p>
      </div>

      <div
        v-for="item in sortedItems"
        :key="item.itemId"
        class="checklist-item-card"
      >
        <div class="checklist-item-content">
          <div class="checklist-item-title" @click="$emit('select-checklist-item', item, 'details')">
            <strong>{{ item.text }}</strong>
          </div>
          <div class="checklist-item-scopes">
            <span
              v-for="(config, index) in item.scopeConfigurations"
              :key="index"
              class="scope-badge"
              @click="$emit('select-checklist-item', item, 'visibility')"
            >
              {{ formatScopeConfig(config) }}
            </span>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, defineProps, defineEmits } from 'vue';
import { alphanumericCompare } from '../../utils/sortUtils';
import { useTerminology } from '../../composables/useTerminology';

const { terms, termsLower } = useTerminology();

const props = defineProps({
  checklistItems: {
    type: Array,
    required: true,
  },
  areas: {
    type: Array,
    default: () => [],
  },
  locations: {
    type: Array,
    default: () => [],
  },
  marshals: {
    type: Array,
    default: () => [],
  },
  completionReport: {
    type: Object,
    default: null,
  },
});

const emit = defineEmits(['add-checklist-item', 'select-checklist-item']);

const searchQuery = ref('');

// Search filtering - searches item text
const filteredItems = computed(() => {
  if (!searchQuery.value.trim()) {
    return props.checklistItems;
  }

  const searchTerms = searchQuery.value.toLowerCase().trim().split(/\s+/);

  return props.checklistItems.filter(item => {
    const searchableText = (item.text || '').toLowerCase();

    // All search terms must match
    return searchTerms.every(term => searchableText.includes(term));
  });
});

const sortedItems = computed(() => {
  return [...filteredItems.value].sort((a, b) => {
    if (a.displayOrder !== b.displayOrder) {
      return a.displayOrder - b.displayOrder;
    }
    return alphanumericCompare(a.text, b.text);
  });
});

const formatScope = (scope) => {
  const scopeMap = {
    'Everyone': 'Everyone',
    'EveryoneInAreas': `Everyone in ${termsLower.value.areas}`,
    'EveryoneAtCheckpoints': `Everyone at ${termsLower.value.checkpoints}`,
    'SpecificPeople': `Specific ${termsLower.value.people}`,
    'OnePerArea': `One per ${termsLower.value.area}`,
    'OnePerCheckpoint': `One per ${termsLower.value.checkpoint}`,
    'EveryAreaLead': `Every ${termsLower.value.area} lead`,
    'OneLeadPerArea': `One lead per ${termsLower.value.area}`,
    'AreaLead': `${terms.value.area} lead`, // Legacy support
  };
  return scopeMap[scope] || scope;
};

const formatScopeConfig = (config) => {
  if (!config) return '';

  const scopeName = formatScope(config.scope);

  if (config.itemType === null) {
    return scopeName;
  }

  const ids = config.ids || [];

  // Handle sentinel values
  if (ids.includes('ALL_MARSHALS')) {
    return `${scopeName} (Everyone)`;
  }
  if (ids.includes('ALL_AREAS')) {
    return `${scopeName} (All ${termsLower.value.areas})`;
  }
  if (ids.includes('ALL_CHECKPOINTS')) {
    return `${scopeName} (All ${termsLower.value.checkpoints})`;
  }

  const count = ids.length;
  if (count === 0) {
    return scopeName;
  }

  return `${scopeName} (${count})`;
};

const getAreaNames = (areaIds) => {
  return areaIds.map(id => {
    const area = props.areas.find(a => a.id === id);
    return area ? area.name : id;
  });
};

const getLocationNames = (locationIds) => {
  return locationIds.map(id => {
    const location = props.locations.find(l => l.id === id);
    return location ? location.name : id;
  });
};

const getMarshalNames = (marshalIds) => {
  return marshalIds.map(id => {
    const marshal = props.marshals.find(m => m.id === id);
    return marshal ? marshal.name : id;
  });
};

const formatDateTime = (dateString) => {
  if (!dateString) return '';
  const date = new Date(dateString);
  return date.toLocaleString();
};

const getCompletionSummary = (item) => {
  if (!props.completionReport) return null;

  const itemCompletion = props.completionReport.completionsByItem?.find(
    c => c.itemId === item.itemId
  );

  if (!itemCompletion) return null;

  const count = itemCompletion.completionCount || 0;
  return count > 0 ? `Completed ${count} time(s)` : null;
};

const getIncompleteDetails = (item) => {
  // TODO: Implement logic to show which areas/checkpoints/people haven't completed
  // This will require additional API endpoint or data structure
  return null;
};
</script>

<style scoped>
.checklists-tab {
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
}

.checklists-tab-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 1rem;
}

.checklists-tab-header h2 {
  margin: 0;
  font-size: 1.5rem;
  color: var(--text-primary);
}

.button-group {
  display: flex;
  gap: 0.75rem;
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

.empty-state {
  text-align: center;
  padding: 3rem 1rem;
  color: var(--text-muted);
  font-style: italic;
}

.checklist-items-list {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.checklist-item-card {
  padding: 0.75rem 1rem;
  background: var(--card-bg);
  border: 1px solid var(--border-color);
  border-radius: 6px;
  transition: all 0.2s;
}

.checklist-item-content {
  display: flex;
  flex-direction: row;
  gap: 1rem;
  align-items: flex-start;
}

.checklist-item-title {
  flex: 1;
  min-width: 0;
  cursor: pointer;
  transition: color 0.2s;
}

.checklist-item-title:hover strong {
  color: var(--accent-primary);
}

.checklist-item-title strong {
  font-size: 0.95rem;
  color: var(--text-primary);
  word-wrap: break-word;
}

.checklist-item-scopes {
  display: flex;
  gap: 0.5rem;
  flex-wrap: wrap;
  max-width: 67%;
  flex-shrink: 0;
  justify-content: flex-end;
}

.scope-badge {
  padding: 0.25rem 0.7rem;
  background: var(--accent-primary);
  color: white;
  border-radius: 12px;
  font-size: 0.75rem;
  font-weight: 500;
  white-space: nowrap;
  cursor: pointer;
  transition: background-color 0.2s;
}

.scope-badge:hover {
  background: var(--accent-primary-hover);
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

.btn-primary {
  background: var(--accent-primary);
  color: white;
}

.btn-primary:hover {
  background: var(--accent-primary-hover);
}

.btn-secondary {
  background: var(--btn-secondary-bg);
  color: white;
}

.btn-secondary:hover {
  background: var(--btn-secondary-hover);
}
</style>
