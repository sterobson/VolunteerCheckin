<template>
  <div class="checklists-tab">
    <div class="checklists-tab-header">
      <h2>{{ terms.checklists }}</h2>
      <div class="button-group">
        <button @click="$emit('add-checklist-item')" class="btn btn-primary">
          Add {{ termsLower.checklist }}
        </button>
      </div>
    </div>

    <!-- Filters -->
    <div class="filters-section">
      <div class="filter-group">
        <h4>Filter by {{ termsLower.area }}:</h4>
        <label class="filter-checkbox">
          <input
            type="checkbox"
            :checked="showAllAreas"
            @change="toggleAllAreas"
          />
          All {{ termsLower.areas }}
        </label>
        <div v-if="!showAllAreas" class="checkbox-dropdown">
          <div class="checkbox-dropdown-header">
            <button @click="toggleAllAreasSelection" class="toggle-all-btn">
              {{ allAreasSelected ? 'Deselect all' : 'Select all' }}
            </button>
          </div>
          <div class="checkbox-list">
            <label
              v-for="area in sortedAreas"
              :key="area.id"
              class="checkbox-item"
            >
              <input
                type="checkbox"
                :checked="filterAreaIds.includes(area.id)"
                @change="toggleArea(area.id)"
              />
              <span class="area-color-dot" :style="{ backgroundColor: area.color || '#667eea' }"></span>
              {{ area.name }}
            </label>
          </div>
        </div>
      </div>

      <div class="filter-group">
        <h4>Filter by {{ termsLower.checkpoint }}:</h4>
        <label class="filter-checkbox">
          <input
            type="checkbox"
            :checked="showAllCheckpoints"
            @change="toggleAllCheckpoints"
          />
          All {{ termsLower.checkpoints }}
        </label>
        <div v-if="!showAllCheckpoints" class="checkbox-dropdown">
          <div class="checkbox-dropdown-header">
            <button @click="toggleAllCheckpointsSelection" class="toggle-all-btn">
              {{ allCheckpointsSelected ? 'Deselect all' : 'Select all' }}
            </button>
          </div>
          <div class="checkbox-list">
            <label
              v-for="location in sortedLocations"
              :key="location.id"
              class="checkbox-item"
            >
              <input
                type="checkbox"
                :checked="filterLocationIds.includes(location.id)"
                @change="toggleCheckpoint(location.id)"
              />
              {{ location.name }}
            </label>
          </div>
        </div>
      </div>

      <div class="filter-group">
        <h4>Filter by {{ termsLower.person }}:</h4>
        <label class="filter-checkbox">
          <input
            type="checkbox"
            :checked="showAllPeople"
            @change="toggleAllPeople"
          />
          All {{ termsLower.people }}
        </label>
        <div v-if="!showAllPeople" class="checkbox-dropdown">
          <div class="checkbox-dropdown-header">
            <button @click="toggleAllPeopleSelection" class="toggle-all-btn">
              {{ allPeopleSelected ? 'Deselect all' : 'Select all' }}
            </button>
          </div>
          <div class="checkbox-list">
            <label
              v-for="marshal in sortedMarshals"
              :key="marshal.id"
              class="checkbox-item"
            >
              <input
                type="checkbox"
                :checked="filterMarshalIds.includes(marshal.id)"
                @change="togglePerson(marshal.id)"
              />
              {{ marshal.name }}
            </label>
          </div>
        </div>
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

const showAllAreas = ref(true);
const showAllCheckpoints = ref(true);
const showAllPeople = ref(true);
const filterAreaIds = ref([]);
const filterLocationIds = ref([]);
const filterMarshalIds = ref([]);

const sortedAreas = computed(() => {
  return [...props.areas].sort((a, b) => {
    if (a.displayOrder !== b.displayOrder) {
      return a.displayOrder - b.displayOrder;
    }
    return alphanumericCompare(a.name, b.name);
  });
});

const sortedLocations = computed(() => {
  return [...props.locations].sort((a, b) => alphanumericCompare(a.name, b.name));
});

const sortedMarshals = computed(() => {
  return [...props.marshals].sort((a, b) => alphanumericCompare(a.name, b.name));
});

const allAreasSelected = computed(() => {
  return props.areas.length > 0 && filterAreaIds.value.length === props.areas.length;
});

const allCheckpointsSelected = computed(() => {
  return props.locations.length > 0 && filterLocationIds.value.length === props.locations.length;
});

const allPeopleSelected = computed(() => {
  return props.marshals.length > 0 && filterMarshalIds.value.length === props.marshals.length;
});

const hasActiveFilters = computed(() => {
  return !showAllAreas.value || !showAllCheckpoints.value || !showAllPeople.value;
});

const toggleAllAreas = () => {
  showAllAreas.value = !showAllAreas.value;
  if (!showAllAreas.value && filterAreaIds.value.length === 0) {
    filterAreaIds.value = props.areas.map(a => a.id);
  }
};

const toggleAllCheckpoints = () => {
  showAllCheckpoints.value = !showAllCheckpoints.value;
  if (!showAllCheckpoints.value && filterLocationIds.value.length === 0) {
    filterLocationIds.value = props.locations.map(l => l.id);
  }
};

const toggleAllPeople = () => {
  showAllPeople.value = !showAllPeople.value;
  if (!showAllPeople.value && filterMarshalIds.value.length === 0) {
    filterMarshalIds.value = props.marshals.map(m => m.id);
  }
};

const toggleArea = (areaId) => {
  const index = filterAreaIds.value.indexOf(areaId);
  if (index >= 0) {
    filterAreaIds.value.splice(index, 1);
  } else {
    filterAreaIds.value.push(areaId);
  }
};

const toggleCheckpoint = (locationId) => {
  const index = filterLocationIds.value.indexOf(locationId);
  if (index >= 0) {
    filterLocationIds.value.splice(index, 1);
  } else {
    filterLocationIds.value.push(locationId);
  }
};

const togglePerson = (marshalId) => {
  const index = filterMarshalIds.value.indexOf(marshalId);
  if (index >= 0) {
    filterMarshalIds.value.splice(index, 1);
  } else {
    filterMarshalIds.value.push(marshalId);
  }
};

const toggleAllAreasSelection = () => {
  if (allAreasSelected.value) {
    filterAreaIds.value = [];
  } else {
    filterAreaIds.value = props.areas.map(a => a.id);
  }
};

const toggleAllCheckpointsSelection = () => {
  if (allCheckpointsSelected.value) {
    filterLocationIds.value = [];
  } else {
    filterLocationIds.value = props.locations.map(l => l.id);
  }
};

const toggleAllPeopleSelection = () => {
  if (allPeopleSelected.value) {
    filterMarshalIds.value = [];
  } else {
    filterMarshalIds.value = props.marshals.map(m => m.id);
  }
};

const clearFilters = () => {
  showAllAreas.value = true;
  showAllCheckpoints.value = true;
  showAllPeople.value = true;
  filterAreaIds.value = [];
  filterLocationIds.value = [];
  filterMarshalIds.value = [];
};

// Helper to get checkpoints for a marshal
const getMarshalCheckpoints = (marshalId) => {
  const marshal = props.marshals.find(m => m.id === marshalId);
  if (!marshal || !marshal.assignments) return [];
  return marshal.assignments.map(a => a.locationId);
};

// Helper to get areas for checkpoints
const getAreasForCheckpoints = (checkpointIds) => {
  const areaSet = new Set();
  checkpointIds.forEach(checkpointId => {
    const location = props.locations.find(l => l.id === checkpointId);
    if (location && location.areaIds) {
      location.areaIds.forEach(areaId => areaSet.add(areaId));
    }
  });
  return Array.from(areaSet);
};

// Helper to check if a marshal matches a scope configuration
const marshalMatchesScope = (marshalId, config) => {
  if (!config) return false;

  const checkpointIds = getMarshalCheckpoints(marshalId);
  const areaIds = getAreasForCheckpoints(checkpointIds);

  // SpecificPeople
  if (config.scope === 'SpecificPeople' && config.itemType === 'Marshal') {
    return config.ids.includes(marshalId) || config.ids.includes('ALL_MARSHALS');
  }

  // EveryoneAtCheckpoints
  if (config.scope === 'EveryoneAtCheckpoints' && config.itemType === 'Checkpoint') {
    return config.ids.includes('ALL_CHECKPOINTS') ||
           checkpointIds.some(id => config.ids.includes(id));
  }

  // OnePerCheckpoint
  if (config.scope === 'OnePerCheckpoint' && config.itemType === 'Checkpoint') {
    return config.ids.includes('ALL_CHECKPOINTS') ||
           checkpointIds.some(id => config.ids.includes(id));
  }

  // OnePerCheckpoint filtered by areas
  if (config.scope === 'OnePerCheckpoint' && config.itemType === 'Area') {
    return config.ids.includes('ALL_AREAS') ||
           areaIds.some(id => config.ids.includes(id));
  }

  // EveryoneInAreas
  if (config.scope === 'EveryoneInAreas' && config.itemType === 'Area') {
    return config.ids.includes('ALL_AREAS') ||
           areaIds.some(id => config.ids.includes(id));
  }

  // OnePerArea
  if (config.scope === 'OnePerArea' && config.itemType === 'Area') {
    return config.ids.includes('ALL_AREAS') ||
           areaIds.some(id => config.ids.includes(id));
  }

  // EveryAreaLead and OneLeadPerArea - would need area lead data
  // For now, just check if areas match (assuming we don't have area lead info here)
  if ((config.scope === 'EveryAreaLead' || config.scope === 'OneLeadPerArea') && config.itemType === 'Area') {
    return config.ids.includes('ALL_AREAS') ||
           areaIds.some(id => config.ids.includes(id));
  }

  return false;
};

const filteredItems = computed(() => {
  let items = props.checklistItems;

  if (!showAllAreas.value && filterAreaIds.value.length > 0) {
    items = items.filter(item => {
      if (!item.scopeConfigurations) return false;
      return item.scopeConfigurations.some(config =>
        config.itemType === 'Area' && filterAreaIds.value.some(areaId => config.ids.includes(areaId))
      );
    });
  }

  if (!showAllCheckpoints.value && filterLocationIds.value.length > 0) {
    items = items.filter(item => {
      if (!item.scopeConfigurations) return false;
      return item.scopeConfigurations.some(config =>
        config.itemType === 'Checkpoint' && filterLocationIds.value.some(locationId => config.ids.includes(locationId))
      );
    });
  }

  if (!showAllPeople.value && filterMarshalIds.value.length > 0) {
    items = items.filter(item => {
      if (!item.scopeConfigurations) return false;
      // Item matches if ANY of the selected marshals would see it
      return filterMarshalIds.value.some(marshalId =>
        item.scopeConfigurations.some(config => marshalMatchesScope(marshalId, config))
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

.checkbox-dropdown {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
  margin-left: 1.5rem;
}

.checkbox-dropdown-header {
  display: flex;
  justify-content: flex-start;
  padding-bottom: 0.5rem;
  border-bottom: 1px solid var(--border-color);
}

.toggle-all-btn {
  padding: 0.4rem 0.75rem;
  background: var(--bg-tertiary);
  border: 1px solid var(--border-color);
  border-radius: 4px;
  cursor: pointer;
  font-size: 0.8rem;
  color: var(--text-primary);
  transition: background-color 0.2s;
  font-weight: 500;
}

.toggle-all-btn:hover {
  background: var(--bg-secondary);
}

.checkbox-list {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  max-height: 300px;
  overflow-y: auto;
  padding-right: 0.5rem;
}

.checkbox-list::-webkit-scrollbar {
  width: 6px;
}

.checkbox-list::-webkit-scrollbar-track {
  background: #f1f1f1;
  border-radius: 3px;
}

.checkbox-list::-webkit-scrollbar-thumb {
  background: #888;
  border-radius: 3px;
}

.checkbox-list::-webkit-scrollbar-thumb:hover {
  background: #555;
}

.checkbox-item {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  font-size: 0.9rem;
  cursor: pointer;
  color: var(--text-primary);
  padding: 0.25rem 0;
}

.checkbox-item input[type="checkbox"] {
  cursor: pointer;
  width: 1rem;
  height: 1rem;
  flex-shrink: 0;
}

.area-color-dot {
  width: 12px;
  height: 12px;
  border-radius: 50%;
  display: inline-block;
  flex-shrink: 0;
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

.btn-small {
  padding: 0.4rem 0.8rem;
  font-size: 0.85rem;
}

.btn-primary {
  background: var(--accent-primary);
  color: white;
}

.btn-primary:hover {
  background: var(--accent-primary-hover);
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

  .checkbox-list {
    max-height: 200px;
  }
}
</style>
