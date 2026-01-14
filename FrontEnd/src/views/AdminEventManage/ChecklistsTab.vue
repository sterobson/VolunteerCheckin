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
          <ScopedAssignmentPills
            class="checklist-item-scopes"
            :scope-configurations="item.scopeConfigurations"
            :areas="areas"
            :locations="locations"
            :marshals="marshals"
            @click="$emit('select-checklist-item', item, 'visibility')"
          />
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, defineProps, defineEmits } from 'vue';
import { alphanumericCompare } from '../../utils/sortUtils';
import ScopedAssignmentPills from '../../components/common/ScopedAssignmentPills.vue';
import { useTerminology } from '../../composables/useTerminology';

const { termsLower } = useTerminology();

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
  flex: 1;
  min-width: 0;
  justify-content: flex-end;
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
