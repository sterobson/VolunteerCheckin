<template>
  <div class="grouped-tasks-list">
    <div v-if="groupedItems.length === 0" class="empty-state">
      <p>No tasks available.</p>
    </div>

    <DraggableList
      v-else
      :items="groupedItems"
      item-key="itemId"
      :disabled="!allowReorder"
      :show-order-overlay="allowReorder"
      @reorder="handleReorder"
    >
      <template #item="{ element: group }">
        <div
          class="task-group"
          :class="{ 'all-completed': group.allCompleted, 'has-modified': group.hasModified }"
        >
        <div class="task-group-header-row">
          <DragHandle v-if="allowReorder" class="task-drag-handle" />
          <button
            type="button"
            class="task-group-header"
            @click="toggleGroup(group.itemId)"
          >
          <div class="task-header-content">
            <div class="task-checkbox-area">
              <span class="task-status-icon" :class="getStatusClass(group)">
                {{ getStatusIcon(group) }}
              </span>
            </div>
            <div class="task-info">
              <div class="task-name">{{ group.text }}</div>
              <div class="task-summary">
                <span class="completion-count" :class="{ 'all-done': group.allCompleted }">
                  {{ group.completedCount }}/{{ group.totalCount }} completed
                </span>
                <span v-if="group.hasModified" class="modified-badge">
                  Modified
                </span>
              </div>
            </div>
            <div class="task-scope">
              <ScopedAssignmentPills
                v-if="group.items[0]?.scopeConfigurations?.length"
                :scope-configurations="group.items[0].scopeConfigurations"
                :areas="areas"
                :locations="locations"
                :marshals="marshals"
                :max-expanded-items="3"
              />
              <span v-else class="scope-pill" :title="getScopeTooltip(group.items[0])">
                {{ getScopeLabel(group.items[0]) }}
              </span>
            </div>
          </div>
          <span class="expand-arrow">{{ expandedGroups.has(group.itemId) ? '▲' : '▼' }}</span>
          </button>
        </div>

        <div v-if="expandedGroups.has(group.itemId)" class="task-group-details">
          <!-- For shared scopes with sub-groups (by checkpoint/area), show nested structure -->
          <template v-if="group.subGroups && group.subGroups.length > 0">
            <div
              v-for="subGroup in group.subGroups"
              :key="subGroup.contextId"
              class="task-sub-group"
            >
              <div class="sub-group-header">
                {{ subGroup.contextName }}
              </div>
              <div
                v-for="item in subGroup.items"
                :key="`${item.itemId}-${item.completionContextId}-${item.contextOwnerMarshalId}`"
                class="task-detail-item"
                :class="{
                  'item-completed': item.localIsCompleted,
                  'item-modified': item.isModified
                }"
              >
                <div class="detail-checkbox">
                  <!-- Use radio buttons when context not yet completed (only one person can complete) -->
                  <!-- Use checkboxes when completed (so completer can uncomplete) -->
                  <input
                    v-if="!subGroup.isContextCompleted"
                    type="radio"
                    :name="`shared-${item.itemId}-${subGroup.contextId}`"
                    :checked="item.localIsCompleted"
                    :disabled="!item.canBeCompletedByMe"
                    @change="$emit('toggle-complete', item)"
                  />
                  <input
                    v-else
                    type="checkbox"
                    :checked="item.localIsCompleted"
                    :disabled="!item.canBeCompletedByMe && !item.localIsCompleted"
                    @change="$emit('toggle-complete', item)"
                  />
                </div>
                <div class="detail-content">
                  <div class="detail-context">
                    {{ item.contextOwnerName || 'Unknown marshal' }}
                  </div>
                  <div v-if="item.localIsCompleted" class="detail-completion">
                    <span v-if="item.isModified && !item.isCompleted" class="pending-text">
                      Will be marked as completed
                    </span>
                    <template v-else>
                      <span class="completion-text">{{ getCompletionText(item) }}</span>
                      <span class="completion-time">{{ formatDateTime(item.completedAt) }}</span>
                    </template>
                  </div>
                  <div v-else-if="item.isModified && item.isCompleted" class="detail-uncomplete">
                    <span class="pending-text">Will be marked as incomplete</span>
                  </div>
                  <div v-else-if="!item.canBeCompletedByMe" class="detail-disabled">
                    Already completed by someone else
                  </div>
                </div>
              </div>
            </div>
          </template>

          <!-- For non-shared scopes, show flat list -->
          <template v-else>
            <div
              v-for="item in group.items"
              :key="`${item.itemId}-${item.completionContextId}`"
              class="task-detail-item"
              :class="{
                'item-completed': item.localIsCompleted,
                'item-modified': item.isModified
              }"
            >
              <div class="detail-checkbox">
                <input
                  type="checkbox"
                  :checked="item.localIsCompleted"
                  :disabled="!item.canBeCompletedByMe && !item.localIsCompleted"
                  @change="$emit('toggle-complete', item)"
                />
              </div>
              <div class="detail-content">
                <div class="detail-context">
                  {{ getContextLabel(item) }}
                </div>
                <div v-if="item.localIsCompleted" class="detail-completion">
                  <span v-if="item.isModified && !item.isCompleted" class="pending-text">
                    Will be marked as completed
                  </span>
                  <template v-else>
                    <span class="completion-text">{{ getCompletionText(item) }}</span>
                    <span class="completion-time">{{ formatDateTime(item.completedAt) }}</span>
                  </template>
                </div>
                <div v-else-if="item.isModified && item.isCompleted" class="detail-uncomplete">
                  <span class="pending-text">Will be marked as incomplete</span>
                </div>
                <div v-else-if="!item.canBeCompletedByMe" class="detail-disabled">
                  Already completed by someone else
                </div>
              </div>
            </div>
          </template>
        </div>
      </div>
      </template>
    </DraggableList>
  </div>
</template>

<script setup>
import { ref, computed } from 'vue';
import { useTerminology } from '../../composables/useTerminology';
import ScopedAssignmentPills from '../common/ScopedAssignmentPills.vue';
import DraggableList from '../common/DraggableList.vue';
import DragHandle from '../common/DragHandle.vue';

const { terms, termsLower } = useTerminology();

const props = defineProps({
  items: {
    type: Array,
    required: true,
  },
  locations: {
    type: Array,
    default: () => [],
  },
  areas: {
    type: Array,
    default: () => [],
  },
  marshals: {
    type: Array,
    default: () => [],
  },
  allowReorder: {
    type: Boolean,
    default: false,
  },
});

const emit = defineEmits(['toggle-complete', 'reorder']);

const expandedGroups = ref(new Set());

// Helper to get checkpoint/area name for sub-grouping (with description, truncated)
const getContextName = (contextType, contextId) => {
  if (contextType === 'Checkpoint') {
    const location = props.locations.find(l => l.id === contextId);
    if (!location) return `${terms.value.checkpoint} (unknown)`;
    if (location.description) {
      // Truncate description if too long
      const maxDescLength = 40;
      const desc = location.description.length > maxDescLength
        ? location.description.substring(0, maxDescLength) + '...'
        : location.description;
      return `${location.name} - ${desc}`;
    }
    return location.name;
  }
  if (contextType === 'Area') {
    const area = props.areas.find(a => a.id === contextId);
    return area?.name || `${terms.value.area} (unknown)`;
  }
  return contextId;
};

// Helper to get sort key for checkpoint/area (just the name for proper sorting)
const getContextSortKey = (contextType, contextId) => {
  if (contextType === 'Checkpoint') {
    const location = props.locations.find(l => l.id === contextId);
    return location?.name || '';
  }
  if (contextType === 'Area') {
    const area = props.areas.find(a => a.id === contextId);
    return area?.name || '';
  }
  return contextId;
};

// Group items by task text (itemId groups same task across contexts)
const groupedItems = computed(() => {
  const groups = new Map();

  for (const item of props.items) {
    if (!groups.has(item.itemId)) {
      groups.set(item.itemId, {
        itemId: item.itemId,
        text: item.text,
        displayOrder: item.displayOrder || 0,
        matchedScope: item.matchedScope,
        items: [],
        subGroups: null, // Will be populated for shared scopes
        totalCount: 0,
        completedCount: 0,
        allCompleted: false,
        hasModified: false,
        // Track unique contexts for shared scopes
        contextIds: new Set(),
        completedContextIds: new Set(),
      });
    }

    const group = groups.get(item.itemId);
    group.items.push(item);

    // For shared scopes, count by unique context (checkpoint/area), not by marshal
    const isSharedScope = sharedScopes.includes(item.matchedScope);
    if (isSharedScope && item.completionContextId) {
      group.contextIds.add(item.completionContextId);
      if (item.localIsCompleted) {
        group.completedContextIds.add(item.completionContextId);
      }
    } else {
      group.totalCount++;
      if (item.localIsCompleted) {
        group.completedCount++;
      }
    }

    if (item.isModified) {
      group.hasModified = true;
    }
  }

  // Finalize counts, create sub-groups, and sort items within each group
  for (const group of groups.values()) {
    const isSharedScope = sharedScopes.includes(group.matchedScope);

    if (isSharedScope) {
      // For shared scopes, use context-based counts
      group.totalCount = group.contextIds.size;
      group.completedCount = group.completedContextIds.size;

      // Create sub-groups by checkpoint/area
      const subGroupMap = new Map();
      for (const item of group.items) {
        const contextId = item.completionContextId;
        if (!subGroupMap.has(contextId)) {
          subGroupMap.set(contextId, {
            contextId,
            contextType: item.completionContextType,
            contextName: getContextName(item.completionContextType, contextId),
            sortKey: getContextSortKey(item.completionContextType, contextId),
            items: [],
            isContextCompleted: false, // Will be set to true if any item is completed
          });
        }
        const subGroup = subGroupMap.get(contextId);
        subGroup.items.push(item);
        // Track if this context has been completed by anyone
        if (item.localIsCompleted) {
          subGroup.isContextCompleted = true;
        }
      }

      // Sort sub-groups by checkpoint/area name (numeric sorting for "2 A" before "10 B")
      group.subGroups = Array.from(subGroupMap.values()).sort((a, b) =>
        a.sortKey.localeCompare(b.sortKey, undefined, { numeric: true, sensitivity: 'base' })
      );

      // Sort marshals within each sub-group alphabetically
      for (const subGroup of group.subGroups) {
        subGroup.items.sort((a, b) => {
          const nameA = a.contextOwnerName || '';
          const nameB = b.contextOwnerName || '';
          return nameA.localeCompare(nameB, undefined, { sensitivity: 'base' });
        });
      }
    } else {
      // For non-shared scopes, sort items by context name
      group.items.sort((a, b) => {
        const nameA = getContextLabel(a);
        const nameB = getContextLabel(b);
        return nameA.localeCompare(nameB, undefined, { numeric: true, sensitivity: 'base' });
      });
    }

    group.allCompleted = group.totalCount > 0 && group.completedCount === group.totalCount;
  }

  // Convert to array and sort by displayOrder, then task name
  return Array.from(groups.values()).sort((a, b) => {
    // First sort by displayOrder if available
    const orderA = a.displayOrder || 0;
    const orderB = b.displayOrder || 0;
    if (orderA !== orderB) return orderA - orderB;
    // Then by task name
    return a.text.localeCompare(b.text, undefined, { sensitivity: 'base' });
  });
});

const toggleGroup = (itemId) => {
  if (expandedGroups.value.has(itemId)) {
    expandedGroups.value.delete(itemId);
  } else {
    expandedGroups.value.add(itemId);
  }
  // Trigger reactivity
  expandedGroups.value = new Set(expandedGroups.value);
};

const handleReorder = ({ changes }) => {
  // Emit the reorder event with itemId -> displayOrder mapping
  emit('reorder', changes);
};

const getStatusClass = (group) => {
  if (group.hasModified) return 'status-modified';
  if (group.allCompleted) return 'status-completed';
  if (group.completedCount > 0) return 'status-partial';
  return 'status-pending';
};

const getStatusIcon = (group) => {
  if (group.allCompleted) return '✓';
  if (group.completedCount > 0) return '◐';
  return '✗';
};

const getScopeLabel = (item) => {
  const scopeMap = {
    'Everyone': 'Everyone',
    'SpecificPeople': 'Assigned individually',
    'EveryoneInAreas': `Per ${termsLower.value.area}`,
    'EveryoneAtCheckpoints': `Per ${termsLower.value.checkpoint}`,
    'OnePerCheckpoint': `One per ${termsLower.value.checkpoint}`,
    'OnePerArea': `One per ${termsLower.value.area}`,
    'EveryAreaLead': `${terms.value.area} leads`,
    'OneLeadPerArea': `One lead per ${termsLower.value.area}`,
  };
  return scopeMap[item.matchedScope] || item.matchedScope;
};

const getScopeTooltip = (item) => {
  const tooltips = {
    'Everyone': 'This task is for everyone at the event',
    'SpecificPeople': 'This task is specifically assigned to certain people',
    'EveryoneInAreas': `This task is for everyone in certain ${termsLower.value.areas}`,
    'EveryoneAtCheckpoints': `This task is for everyone at certain ${termsLower.value.checkpoints}`,
    'OnePerCheckpoint': `One person at the ${termsLower.value.checkpoint} needs to complete this`,
    'OnePerArea': `One person in the ${termsLower.value.area} needs to complete this`,
    'EveryAreaLead': `This task is for ${termsLower.value.area} leads only`,
    'OneLeadPerArea': `One ${termsLower.value.area} lead needs to complete this`,
  };
  return tooltips[item.matchedScope] || '';
};

// Shared scope types where one person completes for the whole group
const sharedScopes = ['OnePerCheckpoint', 'OnePerArea', 'OneLeadPerArea'];

const getContextLabel = (item) => {
  if (!item.completionContextType || !item.completionContextId) {
    return 'Everyone';
  }

  // For shared scope per-marshal items, show the marshal name
  if (sharedScopes.includes(item.matchedScope) && item.contextOwnerName) {
    return item.contextOwnerName;
  }

  if (item.completionContextType === 'Checkpoint') {
    // For linked tasks, show marshal name with checkpoint as secondary detail
    if (item.linksToCheckIn && item.contextOwnerName) {
      const location = props.locations.find(l => l.id === item.completionContextId);
      const checkpointName = location?.name || item.linkedCheckpointName;
      return checkpointName
        ? `${item.contextOwnerName} (${checkpointName})`
        : item.contextOwnerName;
    }
    // Regular checkpoint context - show checkpoint name
    const location = props.locations.find(l => l.id === item.completionContextId);
    if (!location) return `${terms.value.checkpoint} (unknown)`;
    return location.description
      ? `${location.name} - ${location.description}`
      : location.name;
  }

  if (item.completionContextType === 'Area') {
    const area = props.areas.find(a => a.id === item.completionContextId);
    return area ? area.name : `${terms.value.area} (unknown)`;
  }

  if (item.completionContextType === 'Personal') {
    if (item.contextOwnerName) {
      return item.contextOwnerName;
    }
    // Fall back to looking up from marshals prop
    if (item.completionContextId && props.marshals.length > 0) {
      const marshal = props.marshals.find(m =>
        m.id === item.completionContextId || m.marshalId === item.completionContextId
      );
      if (marshal) {
        return marshal.name || marshal.marshalName;
      }
    }
    return 'Assigned individually';
  }

  return item.completionContextType;
};

const formatDateTime = (dateString) => {
  if (!dateString) return '';
  const date = new Date(dateString);
  const now = new Date();
  const diffMs = now - date;
  const diffHours = diffMs / (1000 * 60 * 60);

  // If within 24 hours, just show time
  if (diffHours < 24 && diffHours >= 0) {
    return date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
  }
  return date.toLocaleString();
};

const getCompletionText = (item) => {
  if (item.completedByActorName && item.contextOwnerName &&
      item.completedByActorName !== item.contextOwnerName) {
    return `${item.completedByActorName} on behalf of ${item.contextOwnerName}`;
  }
  return item.completedByActorName || item.contextOwnerName || 'Unknown';
};
</script>

<style scoped>
.grouped-tasks-list {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.empty-state {
  text-align: center;
  padding: 2rem;
  color: var(--text-muted);
}

.task-groups {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.task-group {
  display: flex;
  flex-direction: column;
  border: 1px solid var(--border-light);
  border-radius: 8px;
  background: var(--card-bg);
  overflow: hidden;
}

.task-group-header-row {
  display: flex;
  align-items: stretch;
}

.task-drag-handle {
  display: flex;
  align-items: center;
  padding: 0 0.25rem 0 0.5rem;
  flex-shrink: 0;
}

.task-group.all-completed {
  opacity: 0.7;
}

.task-group.has-modified {
  border-color: var(--warning);
}

.task-group-header {
  flex: 1;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 1rem;
  padding: 0.75rem 1rem;
  background: transparent;
  border: none;
  cursor: pointer;
  text-align: left;
  font-family: inherit;
  transition: background-color 0.15s;
  min-width: 0;
}

.task-group-header:hover {
  background: var(--bg-secondary);
}

.task-header-content {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  flex: 1;
  min-width: 0;
}

.task-checkbox-area {
  flex-shrink: 0;
}

.task-status-icon {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 24px;
  height: 24px;
  border-radius: 50%;
  font-size: 0.9rem;
  font-weight: 600;
}

.task-status-icon.status-completed {
  background: var(--status-success-bg);
  color: var(--accent-success);
}

.task-status-icon.status-partial {
  background: var(--status-warning-bg);
  color: var(--warning-text, #92400e);
}

.task-status-icon.status-pending {
  background: var(--danger-bg-lighter, #fee2e2);
  color: var(--danger, #dc2626);
}

.task-status-icon.status-modified {
  background: var(--warning-bg-light);
  color: var(--warning-dark);
}

.task-info {
  flex: 1;
  min-width: 0;
}

.task-name {
  font-weight: 500;
  color: var(--text-primary);
  word-wrap: break-word;
}

.task-summary {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  margin-top: 0.25rem;
  font-size: 0.85rem;
}

.completion-count {
  color: var(--text-secondary);
}

.completion-count.all-done {
  color: var(--accent-success);
}

.modified-badge {
  padding: 0.125rem 0.375rem;
  background: var(--warning-bg-light);
  color: var(--warning-dark);
  border-radius: 4px;
  font-size: 0.75rem;
  font-weight: 500;
}

.task-scope {
  flex-shrink: 0;
  display: flex;
  justify-content: flex-end;
}

.scope-pill {
  display: inline-block;
  padding: 0.25rem 0.65rem;
  background: var(--info-bg);
  color: var(--info-blue);
  border-radius: 12px;
  font-size: 0.75rem;
  font-weight: 500;
  white-space: nowrap;
}

.expand-arrow {
  color: var(--text-muted);
  font-size: 0.75rem;
  flex-shrink: 0;
}

.task-group-details {
  border-top: 1px solid var(--border-light);
  background: var(--bg-secondary);
}

.task-detail-item {
  display: flex;
  gap: 0.75rem;
  padding: 0.625rem 1rem 0.625rem 2.5rem;
  border-bottom: 1px solid var(--border-light);
}

.task-detail-item:last-child {
  border-bottom: none;
}

.task-detail-item.item-completed {
  /* Don't use opacity on the whole row - it makes enabled checkboxes look disabled */
  /* Instead, style the text content to show completion */
}

.task-detail-item.item-completed .detail-content {
  opacity: 0.7;
}

/* Ensure checkbox remains fully visible and clickable even in completed items */
.task-detail-item.item-completed .detail-checkbox {
  opacity: 1;
}

.task-detail-item.item-modified {
  background: var(--warning-bg-lighter);
}

.detail-checkbox {
  flex-shrink: 0;
  padding-top: 0.125rem;
}

.detail-checkbox input[type="checkbox"],
.detail-checkbox input[type="radio"] {
  cursor: pointer;
  width: 1rem;
  height: 1rem;
}

.detail-checkbox input[type="checkbox"]:disabled,
.detail-checkbox input[type="radio"]:disabled {
  cursor: not-allowed;
  opacity: 0.5;
}

.detail-content {
  flex: 1;
  min-width: 0;
}

.detail-context {
  font-size: 0.9rem;
  color: var(--text-primary);
  font-weight: 500;
}

.detail-completion {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
  margin-top: 0.25rem;
  font-size: 0.8rem;
  color: var(--text-secondary);
}

.completion-text {
  color: var(--accent-success);
}

.completion-time {
  color: var(--text-muted);
}

.detail-uncomplete,
.detail-disabled {
  margin-top: 0.25rem;
  font-size: 0.8rem;
}

.detail-uncomplete {
  color: var(--danger);
}

.detail-disabled {
  color: var(--text-muted);
  font-style: italic;
}

.pending-text {
  color: var(--warning-dark);
  font-style: italic;
}

/* Sub-group styles for shared scope tasks */
.task-sub-group {
  border-bottom: 1px solid var(--border-light);
}

.task-sub-group:last-child {
  border-bottom: none;
}

.sub-group-header {
  padding: 0.5rem 1rem 0.5rem 1.5rem;
  font-size: 0.85rem;
  font-weight: 600;
  color: var(--text-secondary);
  background: var(--bg-muted);
  border-bottom: 1px solid var(--border-light);
}

.task-sub-group .task-detail-item {
  padding-left: 2rem;
}

.task-sub-group .task-detail-item:last-child {
  border-bottom: none;
}

@media (max-width: 640px) {
  .task-header-content {
    flex-wrap: wrap;
  }

  .task-scope {
    width: 100%;
    margin-left: calc(24px + 0.75rem);
    margin-top: 0.25rem;
  }

  .task-detail-item {
    padding-left: 1rem;
  }
}
</style>
