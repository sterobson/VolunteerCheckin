<template>
  <div class="accordion-section">
    <button
      class="accordion-header"
      :class="{ active: isExpanded }"
      @click="$emit('toggle')"
    >
      <span class="accordion-title">
        <span class="section-icon" v-html="getIcon('checklist')"></span>
        <template v-if="isAreaLead">
          Your {{ termsLower.checklists }} <span class="header-count">(You: {{ myOutstandingCount }}, Your {{ areaTermForHeader }}: {{ areaOutstandingCount }})</span>
        </template>
        <template v-else>
          Your {{ termsLower.checklists }} <span class="header-count">({{ visibleOutstandingCount }})</span>
        </template>
      </span>
      <span class="accordion-icon">{{ isExpanded ? '−' : '+' }}</span>
    </button>
    <div v-if="isExpanded" class="accordion-content">
      <div v-if="loading" class="loading">Loading checklist...</div>
      <div v-else-if="error" class="error">{{ error }}</div>
      <div v-else-if="totalCount === 0 && (!isAreaLead || areaItems.length === 0)" class="empty-state">
        <p>No {{ termsLower.checklists }} for you.</p>
      </div>

      <!-- Area leads see two sections: Your jobs and Your area's jobs -->
      <template v-else-if="isAreaLead">
        <!-- Your jobs section (simple list) -->
        <div class="checklist-section">
          <h4 class="checklist-section-title">Your {{ termsLower.checklists }}</h4>
          <div v-if="sortedMyItems.length === 0" class="empty-state small">
            <p>No {{ termsLower.checklists }} assigned to you.</p>
          </div>
          <div v-else class="my-jobs-list">
            <div
              v-for="item in sortedMyItems"
              :key="`${item.itemId}_${item.completionContextType}_${item.completionContextId}`"
              class="my-job-item"
              :class="{ 'item-completed': item.isCompleted }"
            >
              <input
                type="checkbox"
                class="disable-on-load"
                :checked="item.isCompleted"
                :disabled="saving || !item.canBeCompletedByMe"
                @change="$emit('toggle-item', item)"
              />
              <div class="my-job-content">
                <span class="my-job-text" :class="{ completed: item.isCompleted }">
                  {{ item.text }}
                </span>
                <div v-if="item.needsDisambiguation && item.contextDisplayName" class="context-info">
                  {{ item.contextDisplayName }}
                </div>
                <div v-if="item.isCompleted && item.completedByActorName" class="completion-info">
                  <span class="completion-text">{{ getCompletionText(item) }}</span>
                  <span class="completion-time">{{ formatDateTime(item.completedAt) }}</span>
                </div>
              </div>
            </div>
          </div>
        </div>

        <!-- Your area's jobs section -->
        <div v-if="areaItems.length > 0" class="checklist-section">
          <h4 class="checklist-section-title">Your {{ areaPossessive }} {{ termsLower.checklists }}</h4>
          <GroupedTasksList
            :items="areaItemsWithLocalState"
            :locations="locations"
            :areas="areas"
            :marshals="marshals"
            @toggle-complete="(item) => $emit('toggle-item', item)"
          />
        </div>
      </template>

      <!-- Non-leads see simple list or grouped by checkpoint -->
      <template v-else>
        <!-- Ungrouped view (when single checkpoint or no grouping needed) -->
        <div v-if="effectiveGroupBy === 'none'" class="checklist-items">
          <div
            v-for="item in visibleItems"
            :key="`${item.itemId}_${item.completionContextType}_${item.completionContextId}`"
            class="checklist-item"
            :class="{ 'item-completed': item.isCompleted }"
          >
            <div class="item-checkbox">
              <input
                type="checkbox"
                class="disable-on-load"
                :checked="item.isCompleted"
                :disabled="!item.canBeCompletedByMe || saving"
                @change="$emit('toggle-item', item)"
              />
            </div>
            <div class="item-content">
              <div class="item-text" :class="{ 'text-completed': item.isCompleted }">{{ item.text }}</div>
              <div v-if="getContextName(item)" class="item-context">
                {{ getContextName(item) }}
              </div>
              <div v-if="item.isCompleted" class="completion-info">
                <span class="completion-text">{{ getCompletionText(item) }}</span>
                <span class="completion-time">{{ formatDateTime(item.completedAt) }}</span>
              </div>
            </div>
          </div>
        </div>

        <!-- Grouped by checkpoint view (for non-leads with multiple checkpoints) -->
        <div v-else class="checklist-groups">
          <div
            v-for="group in groupedItems"
            :key="group.key"
            class="checklist-group"
          >
            <button
              class="checklist-group-header"
              :class="{ expanded: expandedGroup === group.key, 'all-complete': group.completedCount === group.items.length }"
              @click="$emit('toggle-group', group.key)"
            >
              <span class="group-title">{{ group.name }}</span>
              <span class="group-status">
                <span class="group-count">{{ group.completedCount }}/{{ group.items.length }}</span>
                <span class="group-expand-icon">{{ expandedGroup === group.key ? '−' : '+' }}</span>
              </span>
            </button>
            <div v-if="expandedGroup === group.key" class="checklist-group-items">
              <div
                v-for="item in group.items"
                :key="`${item.itemId}_${item.completionContextType}_${item.completionContextId}`"
                class="checklist-item"
                :class="{ 'item-completed': item.isCompleted }"
              >
                <div class="item-checkbox">
                  <input
                    type="checkbox"
                    class="disable-on-load"
                    :checked="item.isCompleted"
                    :disabled="!item.canBeCompletedByMe || saving"
                    @change="$emit('toggle-item', item)"
                  />
                </div>
                <div class="item-content">
                  <div class="item-text" :class="{ 'text-completed': item.isCompleted }">{{ item.text }}</div>
                  <div v-if="item.isCompleted" class="completion-info">
                    <span class="completion-text">{{ getCompletionText(item) }}</span>
                    <span class="completion-time">{{ formatDateTime(item.completedAt) }}</span>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>
      </template>
    </div>
  </div>
</template>

<script setup>
import { defineProps, defineEmits, computed } from 'vue';
import { getIcon } from '../../utils/icons';
import { useTerminology } from '../../composables/useTerminology';
import { sortTasks } from '../../utils/sortingHelpers';
import GroupedTasksList from '../event-manage/GroupedTasksList.vue';

const { terms, termsLower } = useTerminology();

const props = defineProps({
  loading: {
    type: Boolean,
    default: false,
  },
  error: {
    type: String,
    default: '',
  },
  isExpanded: {
    type: Boolean,
    default: false,
  },
  isAreaLead: {
    type: Boolean,
    default: false,
  },
  ledAreaCount: {
    type: Number,
    default: 1,
  },
  myItems: {
    type: Array,
    default: () => [],
  },
  areaItems: {
    type: Array,
    default: () => [],
  },
  areaItemsWithLocalState: {
    type: Array,
    default: () => [],
  },
  visibleItems: {
    type: Array,
    default: () => [],
  },
  groupedItems: {
    type: Array,
    default: () => [],
  },
  completedCount: {
    type: Number,
    default: 0,
  },
  saving: {
    type: Boolean,
    default: false,
  },
  effectiveGroupBy: {
    type: String,
    default: 'none',
  },
  expandedGroup: {
    type: String,
    default: null,
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
  getContextName: {
    type: Function,
    default: () => '',
  },
});

defineEmits(['toggle', 'toggle-item', 'toggle-group']);

// Area term based on count (singular vs plural)
const areaTermForHeader = computed(() => {
  return props.ledAreaCount === 1 ? termsLower.value.area : termsLower.value.areas;
});

// Possessive form of area term: "zone's" (singular) vs "zones'" (plural)
const areaPossessive = computed(() => {
  if (props.ledAreaCount === 1) {
    return `${termsLower.value.area}'s`;
  }
  // Plural possessive - add apostrophe after the s
  return `${termsLower.value.areas}'`;
});

// Shared scope types where one person completes for the whole group
const sharedScopes = ['OnePerCheckpoint', 'OnePerArea', 'OneLeadPerArea'];

// For the header count, properly count shared vs individual tasks
// - Shared scopes: count by unique context (checkpoint/area)
// - Other scopes: count each item
const totalCount = computed(() => {
  let count = 0;
  const countedContexts = new Set();

  for (const item of props.myItems) {
    if (sharedScopes.includes(item.matchedScope) && item.completionContextId) {
      // For shared scopes, count unique contexts
      const key = `${item.itemId}_${item.completionContextId}`;
      if (!countedContexts.has(key)) {
        countedContexts.add(key);
        count++;
      }
    } else {
      // For other scopes, count each item
      count++;
    }
  }
  return count;
});

const myCompletedCount = computed(() => {
  let count = 0;
  const countedContexts = new Set();

  for (const item of props.myItems) {
    if (sharedScopes.includes(item.matchedScope) && item.completionContextId) {
      // For shared scopes, count unique completed contexts
      const key = `${item.itemId}_${item.completionContextId}`;
      if (!countedContexts.has(key)) {
        countedContexts.add(key);
        if (item.isCompleted) {
          count++;
        }
      }
    } else {
      // For other scopes, count completed items
      if (item.isCompleted) {
        count++;
      }
    }
  }
  return count;
});

// Outstanding (incomplete) tasks for "Your jobs" (area lead view)
const myOutstandingCount = computed(() => {
  return totalCount.value - myCompletedCount.value;
});

// Outstanding (incomplete) tasks for non-area-lead view (based on visibleItems)
const visibleOutstandingCount = computed(() => {
  let incompleteCount = 0;
  const countedContexts = new Set();

  for (const item of props.visibleItems) {
    if (sharedScopes.includes(item.matchedScope) && item.completionContextId) {
      // For shared scopes, count unique incomplete contexts
      const key = `${item.itemId}_${item.completionContextId}`;
      if (!countedContexts.has(key)) {
        countedContexts.add(key);
        if (!item.isCompleted) {
          incompleteCount++;
        }
      }
    } else {
      // For other scopes, count incomplete items
      if (!item.isCompleted) {
        incompleteCount++;
      }
    }
  }
  return incompleteCount;
});

// Outstanding tasks for "Your area's jobs" - count by unique context for shared scopes
const areaOutstandingCount = computed(() => {
  let count = 0;
  const countedContexts = new Set();

  for (const item of props.areaItems) {
    if (sharedScopes.includes(item.matchedScope) && item.completionContextId) {
      // For shared scopes, count unique incomplete contexts
      const key = `${item.itemId}_${item.completionContextId}`;
      if (!countedContexts.has(key)) {
        countedContexts.add(key);
        // Check if ANY item in this context is completed
        const contextCompleted = props.areaItems.some(i =>
          i.itemId === item.itemId &&
          i.completionContextId === item.completionContextId &&
          i.isCompleted
        );
        if (!contextCompleted) {
          count++;
        }
      }
    } else {
      // For other scopes, count incomplete items
      if (!item.isCompleted) {
        count++;
      }
    }
  }
  return count;
});

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
  return item.completedByActorName || 'Unknown';
};

// Get display name for a context (checkpoint with description, or area name)
const getContextDisplayName = (item) => {
  if (item.completionContextType === 'Checkpoint' && item.completionContextId) {
    const location = props.locations.find(l => l.id === item.completionContextId);
    if (location) {
      if (location.description) {
        // Truncate description if too long
        const maxDescLength = 50;
        const desc = location.description.length > maxDescLength
          ? location.description.substring(0, maxDescLength) + '...'
          : location.description;
        return `${location.name} - ${desc}`;
      }
      return location.name;
    }
  }
  if (item.completionContextType === 'Area' && item.completionContextId) {
    const area = props.areas.find(a => a.id === item.completionContextId);
    if (area) {
      return area.name;
    }
  }
  return '';
};

// Get sort key for context (just the name for proper sorting)
const getContextSortKey = (item) => {
  if (item.completionContextType === 'Checkpoint' && item.completionContextId) {
    const location = props.locations.find(l => l.id === item.completionContextId);
    return location?.name || '';
  }
  if (item.completionContextType === 'Area' && item.completionContextId) {
    const area = props.areas.find(a => a.id === item.completionContextId);
    return area?.name || '';
  }
  return '';
};

// Sort myItems and identify which need disambiguation (same text appears multiple times)
const sortedMyItems = computed(() => {
  if (!props.myItems || props.myItems.length === 0) return [];

  // Count occurrences of each task text
  const textCounts = new Map();
  for (const item of props.myItems) {
    textCounts.set(item.text, (textCounts.get(item.text) || 0) + 1);
  }

  // Sort by displayOrder, then text, then context name
  const sorted = sortTasks(props.myItems, getContextSortKey);

  // Mark items that need disambiguation
  return sorted.map(item => ({
    ...item,
    needsDisambiguation: textCounts.get(item.text) > 1,
    contextDisplayName: getContextDisplayName(item),
  }));
});
</script>

<style scoped>
/* Accordion styles */
.accordion-section {
  background: var(--card-bg);
  border-radius: 12px;
  box-shadow: var(--shadow-sm);
  overflow: hidden;
  margin-bottom: 0.5rem;
}

.accordion-header {
  width: 100%;
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 0.5rem;
  padding: 1.25rem 1.5rem;
  background: var(--card-bg);
  border: none;
  cursor: pointer;
  text-align: left;
  font-size: 1rem;
  font-weight: 600;
  color: var(--text-dark);
  transition: background 0.2s;
}

.accordion-header:hover {
  background: var(--bg-secondary);
}

.accordion-header.active {
  background: var(--brand-primary-bg);
  color: var(--brand-primary);
}

.accordion-title {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.header-count {
  font-style: italic;
  opacity: 0.6;
}

.section-icon {
  display: flex;
  align-items: center;
  justify-content: center;
  color: var(--brand-primary);
}

.section-icon :deep(svg) {
  width: 18px;
  height: 18px;
}

.accordion-icon {
  font-size: 1.5rem;
  font-weight: 300;
  color: var(--brand-primary);
}

.accordion-content {
  padding: 1rem 1.5rem 1.5rem;
  border-top: 1px solid var(--border-light);
}

/* Component-specific styles */
.loading,
.empty-state {
  text-align: center;
  padding: 1rem;
  color: var(--text-secondary);
}

.empty-state.small {
  padding: 0.5rem;
  font-size: 0.9rem;
}

.empty-state p {
  margin: 0;
}

.error {
  padding: 1rem;
  background: var(--danger-bg-lighter);
  color: var(--danger);
  border-radius: 6px;
}

.checklist-section {
  margin-bottom: 1.5rem;
}

.checklist-section:last-child {
  margin-bottom: 0;
}

.checklist-section-title {
  font-size: 0.95rem;
  font-weight: 600;
  color: var(--text-dark);
  margin: 0 0 0.75rem 0;
  padding-bottom: 0.5rem;
  border-bottom: 1px solid var(--border-light);
}

.my-jobs-list {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.my-job-item {
  display: flex;
  align-items: flex-start;
  gap: 0.75rem;
  padding: 0.5rem;
  background: var(--bg-muted);
  border-radius: 6px;
}

.my-job-item.item-completed {
  background: var(--success-bg-light);
}

.my-job-item input[type="checkbox"] {
  width: 18px;
  height: 18px;
  cursor: pointer;
  margin-top: 0.1rem;
}

.my-job-content {
  flex: 1;
  min-width: 0;
}

.my-job-text {
  font-size: 0.9rem;
  color: var(--text-dark);
}

.my-job-text.completed {
  text-decoration: line-through;
  color: var(--text-secondary);
}

.checklist-items {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.checklist-item {
  display: flex;
  align-items: flex-start;
  gap: 0.75rem;
  padding: 0.75rem;
  background: var(--bg-muted);
  border-radius: 8px;
}

.checklist-item.item-completed {
  background: var(--success-bg-light);
}

.item-checkbox {
  flex-shrink: 0;
  padding-top: 0.1rem;
}

.item-checkbox input[type="checkbox"] {
  width: 18px;
  height: 18px;
  cursor: pointer;
}

.item-content {
  flex: 1;
  min-width: 0;
}

.item-text {
  font-size: 0.9rem;
  color: var(--text-dark);
  word-break: break-word;
}

.item-text.text-completed {
  text-decoration: line-through;
  color: var(--text-secondary);
}

.item-context {
  font-size: 0.8rem;
  color: var(--text-secondary);
  margin-top: 0.25rem;
}

.context-info {
  font-size: 0.8rem;
  color: var(--text-secondary);
  margin-top: 0.15rem;
}

.completion-info {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
  font-size: 0.75rem;
  color: var(--success-dark);
  margin-top: 0.25rem;
}

.checklist-groups {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.checklist-group {
  background: var(--card-bg);
  border-radius: 8px;
  overflow: hidden;
  border: 1px solid var(--border-light);
}

.checklist-group-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  width: 100%;
  padding: 0.75rem 1rem;
  background: var(--bg-secondary);
  border: none;
  cursor: pointer;
  font-size: 0.9rem;
  font-weight: 500;
  color: var(--text-dark);
  text-align: left;
  transition: background 0.2s;
}

.checklist-group-header:hover {
  background: var(--bg-muted);
}

.checklist-group-header.expanded {
  border-bottom: 1px solid var(--border-light);
}

.checklist-group-header.all-complete {
  background: var(--success-bg-light);
}

.group-title {
  flex: 1;
}

.group-status {
  display: flex;
  align-items: center;
  gap: 0.75rem;
}

.group-count {
  font-size: 0.85rem;
  color: var(--text-secondary);
}

.group-expand-icon {
  font-size: 1.1rem;
  color: var(--text-secondary);
}

.checklist-group-items {
  padding: 0.5rem;
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}
</style>
