<template>
  <div class="roles-tab">
    <div class="tab-header">
      <div class="button-group">
        <button @click="$emit('add-role')" class="btn btn-primary">
          Add role
        </button>
      </div>
      <div class="status-pills" v-if="roleDefinitions.length > 0">
        <StatusPill variant="neutral">
          {{ roleDefinitions.length }} {{ roleDefinitions.length === 1 ? 'role' : 'roles' }}
        </StatusPill>
        <StatusPill v-if="totalUsageCount > 0" variant="success">
          {{ totalUsageCount }} {{ totalUsageCount === 1 ? 'assignment' : 'assignments' }}
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
          placeholder="Search by name or notes..."
        />
      </div>
    </div>

    <!-- Roles List -->
    <div class="roles-list">
      <div v-if="filteredRoles.length === 0" class="empty-state">
        <p>{{ emptyMessage }}</p>
      </div>

      <DraggableList
        v-else
        :items="sortedRoles"
        item-key="roleId"
        :disabled="!!searchQuery"
        @reorder="handleReorder"
      >
        <template #item="{ element: role }">
          <div class="role-card">
            <DragHandle v-if="!searchQuery" />
            <div class="role-content" @click="$emit('select-role', role)">
              <div class="role-header">
                <div class="role-name-row">
                  <strong>{{ role.name }}</strong>
                </div>
                <div class="role-usage">
                  <span
                    class="usage-badge"
                    :class="{ 'has-usage': role.usageCount > 0 }"
                  >
                    {{ role.usageCount }} {{ role.usageCount === 1 ? 'person' : 'people' }}
                  </span>
                </div>
              </div>
              <div v-if="role.notes" class="role-notes">
                {{ truncateNotes(role.notes) }}
              </div>
              <div v-else class="role-notes empty">
                No notes
              </div>
            </div>
          </div>
        </template>
      </DraggableList>
    </div>
  </div>
</template>

<script setup>
import { ref, computed } from 'vue';
import StatusPill from '../../components/StatusPill.vue';
import DraggableList from '../../components/common/DraggableList.vue';
import DragHandle from '../../components/common/DragHandle.vue';

const props = defineProps({
  roleDefinitions: {
    type: Array,
    required: true,
  },
});

const emit = defineEmits(['add-role', 'select-role', 'reorder']);

const searchQuery = ref('');

// Total usage count across all roles
const totalUsageCount = computed(() => {
  return props.roleDefinitions.reduce((sum, role) => sum + (role.usageCount || 0), 0);
});

// Normalize text for searching
const normalizeText = (text) => {
  if (!text) return '';
  return text.toLowerCase().replace(/[.,\-_'"()]/g, ' ').replace(/\s+/g, ' ').trim();
};

// Truncate notes for preview
const truncateNotes = (notes) => {
  if (!notes) return '';
  const maxLength = 150;
  if (notes.length <= maxLength) return notes;
  return notes.substring(0, maxLength) + '...';
};

// Search filtering
const filteredRoles = computed(() => {
  if (!searchQuery.value.trim()) {
    return props.roleDefinitions;
  }

  const normalizedQuery = normalizeText(searchQuery.value);
  const searchTerms = normalizedQuery.split(' ').filter(t => t.length > 0);

  return props.roleDefinitions.filter(role => {
    const searchableText = normalizeText(`${role.name || ''} ${role.notes || ''}`);
    return searchTerms.every(term => searchableText.includes(term));
  });
});

// Sort by display order, then alphabetically by name (natural sorting)
const sortedRoles = computed(() => {
  return [...filteredRoles.value].sort((a, b) => {
    // By display order first
    const orderA = a.displayOrder || 0;
    const orderB = b.displayOrder || 0;
    if (orderA !== orderB) {
      return orderA - orderB;
    }

    // Then alphabetically by name with natural sorting
    return (a.name || '').localeCompare(b.name || '', undefined, { numeric: true, sensitivity: 'base' });
  });
});

// Empty message based on filters
const emptyMessage = computed(() => {
  if (searchQuery.value.trim()) {
    return 'No roles match your search.';
  }
  return 'No roles defined yet. Create one to get started!';
});

const handleReorder = ({ changes }) => {
  emit('reorder', changes);
};
</script>

<style scoped>
.roles-tab {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.tab-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  flex-wrap: wrap;
  gap: 1rem;
}

.status-pills {
  display: flex;
  gap: 0.5rem;
  flex-wrap: wrap;
}

.button-group {
  display: flex;
  gap: 0.75rem;
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

.btn-primary {
  background: var(--accent-primary);
  color: white;
}

.btn-primary:hover {
  background: var(--btn-primary-hover);
}

.filters-section {
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

.roles-list {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.empty-state {
  text-align: center;
  padding: 3rem 1rem;
  color: var(--text-muted);
  font-style: italic;
}

.role-card {
  display: flex;
  align-items: flex-start;
  gap: 0.5rem;
  padding: 1rem 1.25rem;
  background: var(--card-bg);
  border: 1px solid var(--border-color);
  border-radius: 8px;
  transition: all 0.2s;
}

.role-card:hover {
  border-color: var(--accent-primary);
  box-shadow: var(--shadow-md);
}

.role-content {
  flex: 1;
  cursor: pointer;
  min-width: 0;
}

.role-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: 1rem;
  margin-bottom: 0.5rem;
}

.role-name-row {
  display: flex;
  align-items: center;
  gap: 0.75rem;
}

.role-name-row strong {
  font-size: 1rem;
  color: var(--text-primary);
}

.role-usage {
  flex-shrink: 0;
}

.usage-badge {
  padding: 0.2rem 0.6rem;
  background: var(--bg-tertiary);
  color: var(--text-muted);
  border-radius: 12px;
  font-size: 0.75rem;
  font-weight: 500;
}

.usage-badge.has-usage {
  background: var(--success-bg);
  color: var(--success-color);
}

.role-notes {
  font-size: 0.85rem;
  color: var(--text-secondary);
  line-height: 1.4;
}

.role-notes.empty {
  color: var(--text-muted);
  font-style: italic;
}

@media (max-width: 768px) {
  .role-header {
    flex-direction: column;
    gap: 0.5rem;
  }

  .role-usage {
    order: -1;
  }
}
</style>
