<template>
  <div class="notes-tab">
    <div class="notes-tab-header">
      <h2>Notes</h2>
      <div class="button-group">
        <button @click="$emit('add-note')" class="btn btn-primary">
          Add note
        </button>
      </div>
    </div>

    <!-- Filters -->
    <div class="filters-section">
      <div class="filter-group">
        <h4>Filter by priority:</h4>
        <div class="priority-filters">
          <label
            v-for="priority in priorities"
            :key="priority.value"
            class="filter-checkbox"
          >
            <input
              type="checkbox"
              :checked="filterPriorities.includes(priority.value)"
              @change="togglePriority(priority.value)"
            />
            <span class="priority-dot" :class="priority.value.toLowerCase()"></span>
            {{ priority.label }}
          </label>
        </div>
      </div>

      <div class="filter-group">
        <h4>Filter by area:</h4>
        <label class="filter-checkbox">
          <input
            type="checkbox"
            :checked="showAllAreas"
            @change="toggleAllAreas"
          />
          All areas
        </label>
        <div v-if="!showAllAreas" class="checkbox-dropdown">
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

      <button v-if="hasActiveFilters" @click="clearFilters" class="btn btn-secondary btn-small">
        Clear filters
      </button>
    </div>

    <!-- Notes List -->
    <div class="notes-list">
      <div v-if="filteredNotes.length === 0" class="empty-state">
        <p>{{ hasActiveFilters ? 'No notes match the selected filters.' : 'No notes yet. Create one to get started!' }}</p>
      </div>

      <div
        v-for="note in sortedNotes"
        :key="note.noteId"
        class="note-card"
        :class="{ pinned: note.isPinned }"
        @click="$emit('select-note', note)"
      >
        <div class="note-header">
          <div class="note-title-row">
            <span v-if="note.isPinned" class="pin-icon" title="Pinned">📌</span>
            <span class="priority-indicator" :class="note.priority?.toLowerCase() || 'normal'"></span>
            <strong>{{ note.title }}</strong>
          </div>
          <div class="note-meta">
            <span v-if="note.category" class="category-badge">{{ note.category }}</span>
            <span class="created-info">{{ formatRelativeTime(note.createdAt) }}</span>
          </div>
        </div>
        <div v-if="note.content" class="note-preview">
          {{ truncateContent(note.content) }}
        </div>
        <div class="note-scopes">
          <span
            v-for="(config, index) in note.scopeConfigurations"
            :key="index"
            class="scope-badge"
          >
            {{ formatScopeConfig(config) }}
          </span>
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
  notes: {
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
});

const emit = defineEmits(['add-note', 'select-note']);

const priorities = [
  { value: 'Urgent', label: 'Urgent' },
  { value: 'High', label: 'High' },
  { value: 'Normal', label: 'Normal' },
  { value: 'Low', label: 'Low' },
];

const showAllAreas = ref(true);
const filterAreaIds = ref([]);
const filterPriorities = ref(['Urgent', 'High', 'Normal', 'Low']);

const sortedAreas = computed(() => {
  return [...props.areas].sort((a, b) => {
    if (a.displayOrder !== b.displayOrder) {
      return a.displayOrder - b.displayOrder;
    }
    return alphanumericCompare(a.name, b.name);
  });
});

const hasActiveFilters = computed(() => {
  return !showAllAreas.value || filterPriorities.value.length < 4;
});

const toggleAllAreas = () => {
  showAllAreas.value = !showAllAreas.value;
  if (!showAllAreas.value && filterAreaIds.value.length === 0) {
    filterAreaIds.value = props.areas.map(a => a.id);
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

const togglePriority = (priority) => {
  const index = filterPriorities.value.indexOf(priority);
  if (index >= 0) {
    filterPriorities.value.splice(index, 1);
  } else {
    filterPriorities.value.push(priority);
  }
};

const clearFilters = () => {
  showAllAreas.value = true;
  filterAreaIds.value = [];
  filterPriorities.value = ['Urgent', 'High', 'Normal', 'Low'];
};

const filteredNotes = computed(() => {
  let notes = props.notes;

  // Filter by priority
  if (filterPriorities.value.length < 4) {
    notes = notes.filter(note => filterPriorities.value.includes(note.priority || 'Normal'));
  }

  // Filter by area
  if (!showAllAreas.value && filterAreaIds.value.length > 0) {
    notes = notes.filter(note => {
      if (!note.scopeConfigurations) return false;
      return note.scopeConfigurations.some(config =>
        config.itemType === 'Area' && filterAreaIds.value.some(areaId => config.ids.includes(areaId))
      );
    });
  }

  return notes;
});

const sortedNotes = computed(() => {
  const priorityOrder = { Urgent: 0, High: 1, Normal: 2, Low: 3 };

  return [...filteredNotes.value].sort((a, b) => {
    // Pinned first
    if (a.isPinned !== b.isPinned) {
      return a.isPinned ? -1 : 1;
    }

    // Then by priority
    const priorityA = priorityOrder[a.priority] ?? 2;
    const priorityB = priorityOrder[b.priority] ?? 2;
    if (priorityA !== priorityB) {
      return priorityA - priorityB;
    }

    // Then by display order
    if (a.displayOrder !== b.displayOrder) {
      return a.displayOrder - b.displayOrder;
    }

    // Then by title
    return alphanumericCompare(a.title, b.title);
  });
});

const formatScope = (scope) => {
  const scopeMap = {
    'Everyone': 'Everyone',
    'EveryoneInAreas': `Everyone in ${termsLower.value.areas}`,
    'EveryoneAtCheckpoints': `Everyone at ${termsLower.value.checkpoints}`,
    'SpecificPeople': `Specific ${termsLower.value.people}`,
    'EveryAreaLead': `Every ${termsLower.value.area} lead`,
    'OnePerCheckpoint': `One per ${termsLower.value.checkpoint}`,
    'OnePerArea': `One per ${termsLower.value.area}`,
    'OneLeadPerArea': `One lead per ${termsLower.value.area}`,
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

const truncateContent = (content) => {
  if (!content) return '';
  const maxLength = 150;
  if (content.length <= maxLength) return content;
  return content.substring(0, maxLength) + '...';
};

const formatRelativeTime = (dateString) => {
  if (!dateString) return '';
  const date = new Date(dateString);
  const now = new Date();
  const diffMs = now - date;
  const diffMins = Math.floor(diffMs / 60000);
  const diffHours = Math.floor(diffMs / 3600000);
  const diffDays = Math.floor(diffMs / 86400000);

  if (diffMins < 1) return 'Just now';
  if (diffMins < 60) return `${diffMins}m ago`;
  if (diffHours < 24) return `${diffHours}h ago`;
  if (diffDays < 7) return `${diffDays}d ago`;

  return date.toLocaleDateString();
};
</script>

<style scoped>
.notes-tab {
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
}

.notes-tab-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 1rem;
}

.notes-tab-header h2 {
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

.priority-filters {
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

.priority-dot {
  width: 10px;
  height: 10px;
  border-radius: 50%;
}

.priority-dot.urgent { background: var(--priority-urgent); }
.priority-dot.high { background: var(--priority-high); }
.priority-dot.normal { background: var(--priority-normal); }
.priority-dot.low { background: var(--priority-low); }

.checkbox-dropdown {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
  margin-left: 1.5rem;
}

.checkbox-list {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  max-height: 200px;
  overflow-y: auto;
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

.notes-list {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.note-card {
  padding: 1rem 1.25rem;
  background: var(--card-bg);
  border: 1px solid var(--border-color);
  border-radius: 8px;
  cursor: pointer;
  transition: all 0.2s;
}

.note-card:hover {
  border-color: var(--accent-primary);
  box-shadow: var(--shadow-md);
}

.note-card.pinned {
  border-left: 4px solid var(--accent-primary);
  background: var(--bg-secondary);
}

.note-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: 1rem;
  margin-bottom: 0.5rem;
}

.note-title-row {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.pin-icon {
  font-size: 0.9rem;
}

.priority-indicator {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  flex-shrink: 0;
}

.priority-indicator.urgent { background: var(--priority-urgent); }
.priority-indicator.high { background: var(--priority-high); }
.priority-indicator.normal { background: var(--priority-normal); }
.priority-indicator.low { background: var(--priority-low); }

.note-title-row strong {
  font-size: 1rem;
  color: var(--text-primary);
}

.note-meta {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  flex-shrink: 0;
}

.category-badge {
  padding: 0.2rem 0.6rem;
  background: var(--bg-tertiary);
  color: var(--text-secondary);
  border-radius: 12px;
  font-size: 0.75rem;
  font-weight: 500;
}

.created-info {
  font-size: 0.8rem;
  color: var(--text-muted);
}

.note-preview {
  font-size: 0.9rem;
  color: var(--text-secondary);
  line-height: 1.4;
  margin-bottom: 0.75rem;
}

.note-scopes {
  display: flex;
  gap: 0.5rem;
  flex-wrap: wrap;
}

.scope-badge {
  padding: 0.2rem 0.6rem;
  background: var(--accent-primary);
  color: white;
  border-radius: 12px;
  font-size: 0.7rem;
  font-weight: 500;
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
  background: var(--btn-secondary-bg);
  color: white;
}

.btn-secondary:hover {
  background: var(--btn-secondary-hover);
}

@media (max-width: 768px) {
  .filters-section {
    flex-direction: column;
  }

  .filter-group {
    width: 100%;
  }

  .note-header {
    flex-direction: column;
    gap: 0.5rem;
  }

  .note-meta {
    order: -1;
  }
}
</style>
