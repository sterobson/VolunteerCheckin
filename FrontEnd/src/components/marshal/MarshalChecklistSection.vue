<template>
  <div class="accordion-section">
    <button
      class="accordion-header"
      :class="{ active: isExpanded }"
      @click="$emit('toggle')"
    >
      <span class="accordion-title">
        <span class="section-icon" v-html="getIcon('checklist')"></span>
        Your {{ checklistTerm }} ({{ completedCount }} of {{ totalCount }} complete)
      </span>
      <span class="accordion-icon">{{ isExpanded ? '−' : '+' }}</span>
    </button>
    <div v-if="isExpanded" class="accordion-content">
      <div v-if="loading" class="loading">Loading checklist...</div>
      <div v-else-if="error" class="error">{{ error }}</div>
      <div v-else-if="totalCount === 0" class="empty-state">
        <p>No {{ checklistTerm }} for you.</p>
      </div>

      <!-- Area leads see two sections: Your jobs and Your area's jobs -->
      <template v-else-if="isAreaLead">
        <!-- Your jobs section (simple list) -->
        <div class="checklist-section">
          <h4 class="checklist-section-title">Your jobs</h4>
          <div v-if="myItems.length === 0" class="empty-state small">
            <p>No jobs assigned to you.</p>
          </div>
          <div v-else class="my-jobs-list">
            <div
              v-for="item in myItems"
              :key="`${item.itemId}_${item.completionContextType}_${item.completionContextId}`"
              class="my-job-item"
            >
              <input
                type="checkbox"
                :checked="item.isCompleted"
                :disabled="saving"
                @change="$emit('toggle-item', item)"
              />
              <span class="my-job-text" :class="{ completed: item.isCompleted }">
                {{ item.text }}
              </span>
            </div>
          </div>
        </div>

        <!-- Your area's jobs section -->
        <div v-if="areaItems.length > 0" class="checklist-section">
          <h4 class="checklist-section-title">Your {{ areaTerm }}'s jobs</h4>
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
                <span class="completion-text">
                  Completed by {{ item.completedByActorName || 'Unknown' }}
                </span>
                <span class="completion-time">
                  {{ formatDateTime(item.completedAt) }}
                </span>
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
                    :checked="item.isCompleted"
                    :disabled="!item.canBeCompletedByMe || saving"
                    @change="$emit('toggle-item', item)"
                  />
                </div>
                <div class="item-content">
                  <div class="item-text" :class="{ 'text-completed': item.isCompleted }">{{ item.text }}</div>
                  <div v-if="item.isCompleted" class="completion-info">
                    <span class="completion-text">
                      {{ item.completedByActorName || 'Unknown' }}
                    </span>
                    <span class="completion-time">
                      {{ formatDateTime(item.completedAt) }}
                    </span>
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
import GroupedTasksList from '../event-manage/GroupedTasksList.vue';

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
  checklistTerm: {
    type: String,
    default: 'checklists',
  },
  areaTerm: {
    type: String,
    default: 'area',
  },
  isAreaLead: {
    type: Boolean,
    default: false,
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

const totalCount = computed(() => props.visibleItems.length);

const formatDateTime = (dateString) => {
  if (!dateString) return '';
  return new Date(dateString).toLocaleString();
};
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
  align-items: center;
  gap: 0.75rem;
  padding: 0.5rem;
  background: var(--bg-muted);
  border-radius: 6px;
}

.my-job-item input[type="checkbox"] {
  width: 18px;
  height: 18px;
  cursor: pointer;
}

.my-job-text {
  flex: 1;
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
