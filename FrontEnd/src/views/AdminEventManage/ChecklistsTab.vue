<template>
  <div class="checklists-tab">
    <div class="checklists-tab-header">
      <h2>Checklists</h2>
      <div class="button-group">
        <button @click="$emit('add-checklist-item')" class="btn btn-primary">
          Add checklist item
        </button>
      </div>
    </div>

    <!-- Filters -->
    <div class="filters-section">
      <div class="filter-group">
        <label>Filter by area:</label>
        <select v-model="filterAreaId" class="filter-select">
          <option value="">All areas</option>
          <option v-for="area in areas" :key="area.id" :value="area.id">
            {{ area.name }}
          </option>
        </select>
      </div>

      <div class="filter-group">
        <label>Filter by checkpoint:</label>
        <select v-model="filterLocationId" class="filter-select">
          <option value="">All checkpoints</option>
          <option v-for="location in locations" :key="location.id" :value="location.id">
            {{ location.name }}
          </option>
        </select>
      </div>

      <div class="filter-group">
        <label>Filter by person:</label>
        <select v-model="filterMarshalId" class="filter-select">
          <option value="">All people</option>
          <option v-for="marshal in marshals" :key="marshal.id" :value="marshal.id">
            {{ marshal.name }}
          </option>
        </select>
      </div>

      <button v-if="hasActiveFilters" @click="clearFilters" class="btn btn-secondary btn-small">
        Clear filters
      </button>
    </div>

    <!-- Checklist Items List -->
    <div class="checklist-items-list">
      <div v-if="filteredItems.length === 0" class="empty-state">
        <p>{{ hasActiveFilters ? 'No checklist items match the selected filters.' : 'No checklist items yet. Create one to get started!' }}</p>
      </div>

      <div
        v-for="item in sortedItems"
        :key="item.itemId"
        class="checklist-item-card"
        @click="$emit('select-checklist-item', item)"
      >
        <div class="checklist-item-header">
          <div class="checklist-item-title">
            <strong>{{ item.text }}</strong>
          </div>
          <div class="checklist-item-scopes">
            <span
              v-for="(config, index) in item.scopeConfigurations"
              :key="index"
              class="scope-badge"
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

const filterAreaId = ref('');
const filterLocationId = ref('');
const filterMarshalId = ref('');

const hasActiveFilters = computed(() => {
  return filterAreaId.value || filterLocationId.value || filterMarshalId.value;
});

const clearFilters = () => {
  filterAreaId.value = '';
  filterLocationId.value = '';
  filterMarshalId.value = '';
};

const filteredItems = computed(() => {
  let items = props.checklistItems;

  if (filterAreaId.value) {
    items = items.filter(item => {
      if (!item.scopeConfigurations) return false;
      return item.scopeConfigurations.some(config =>
        config.itemType === 'Area' && config.ids.includes(filterAreaId.value)
      );
    });
  }

  if (filterLocationId.value) {
    items = items.filter(item => {
      if (!item.scopeConfigurations) return false;
      return item.scopeConfigurations.some(config =>
        config.itemType === 'Checkpoint' && config.ids.includes(filterLocationId.value)
      );
    });
  }

  if (filterMarshalId.value) {
    items = items.filter(item => {
      if (!item.scopeConfigurations) return false;
      return item.scopeConfigurations.some(config =>
        config.itemType === 'Marshal' && config.ids.includes(filterMarshalId.value)
      );
    });
  }

  return items;
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
    'EveryoneInAreas': 'Everyone in areas',
    'EveryoneAtCheckpoints': 'Everyone at checkpoints',
    'SpecificPeople': 'Specific people',
    'OnePerArea': 'One per area',
    'OnePerCheckpoint': 'One per checkpoint',
    'AreaLead': 'Area lead',
  };
  return scopeMap[scope] || scope;
};

const formatScopeConfig = (config) => {
  if (!config) return '';

  const scopeName = formatScope(config.scope);

  if (config.itemType === null) {
    return scopeName;
  }

  const count = config.ids?.length || 0;
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
  color: #333;
}

.button-group {
  display: flex;
  gap: 0.75rem;
}

.filters-section {
  display: flex;
  flex-wrap: wrap;
  gap: 1rem;
  padding: 1rem;
  background: #f8f9fa;
  border-radius: 8px;
  align-items: flex-end;
}

.filter-group {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  min-width: 200px;
}

.filter-group label {
  font-size: 0.9rem;
  font-weight: 500;
  color: #333;
}

.filter-select {
  padding: 0.5rem;
  border: 1px solid #ddd;
  border-radius: 4px;
  font-size: 0.9rem;
  background: white;
}

.empty-state {
  text-align: center;
  padding: 3rem 1rem;
  color: #999;
  font-style: italic;
}

.checklist-items-list {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.checklist-item-card {
  padding: 0.75rem 1rem;
  background: white;
  border: 1px solid #e0e0e0;
  border-radius: 6px;
  cursor: pointer;
  transition: all 0.2s;
}

.checklist-item-card:hover {
  border-color: #667eea;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.1);
}

.checklist-item-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 1rem;
}

.checklist-item-title {
  display: flex;
  align-items: center;
  flex: 1;
  min-width: 0;
}

.checklist-item-title strong {
  font-size: 0.95rem;
  color: #333;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.checklist-item-scopes {
  display: flex;
  gap: 0.5rem;
  flex-wrap: wrap;
  flex-shrink: 0;
}

.scope-badge {
  padding: 0.25rem 0.7rem;
  background: #667eea;
  color: white;
  border-radius: 12px;
  font-size: 0.75rem;
  font-weight: 500;
  white-space: nowrap;
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

.btn-primary {
  background: #007bff;
  color: white;
}

.btn-primary:hover {
  background: #0056b3;
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
}
</style>
