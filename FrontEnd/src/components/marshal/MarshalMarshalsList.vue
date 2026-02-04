<template>
  <div v-if="isAreaLead && marshals.length > 0" class="accordion-section">
    <button
      class="accordion-header"
      :class="{ active: isExpanded }"
      @click="$emit('toggle')"
    >
      <span class="accordion-title">
        <span class="section-icon" v-html="getIcon('marshal')"></span>
        Your {{ marshals.length === 1 ? termsLower.person : termsLower.people }}<span v-if="marshals.length > 1" class="header-count"> ({{ marshals.length }})</span>
      </span>
      <span class="accordion-icon">{{ isExpanded ? '−' : '+' }}</span>
    </button>
    <div v-if="isExpanded" class="accordion-content area-lead-marshals-content">
      <div class="area-lead-marshals-list">
        <div
          v-for="marshal in marshals"
          :key="marshal.marshalId"
          class="area-lead-marshal-item"
        >
          <button
            class="area-lead-marshal-header"
            :class="{ active: expandedMarshalId === marshal.marshalId, 'is-checked-in': marshal.isCheckedIn }"
            @click="$emit('toggle-marshal', marshal.marshalId)"
          >
            <div class="marshal-header-info">
              <div class="marshal-name-row">
                <span class="marshal-name">{{ marshal.name }}</span>
                <span class="check-status-badge" :class="{ 'checked-in': marshal.isCheckedIn }">
                  {{ marshal.isCheckedIn ? '✓' : '○' }}
                </span>
              </div>
              <div class="marshal-meta">
                <span class="marshal-checkpoint">
                  {{ formatMarshalCheckpoints(marshal.checkpoints) }}
                </span>
                <span v-if="getTaskCounts(marshal).total > 0" class="marshal-task-count">
                  {{ getTaskCounts(marshal).completed }} / {{ getTaskCounts(marshal).total }} {{ termsLower.checklists }} complete
                </span>
              </div>
            </div>
            <span class="accordion-icon">{{ expandedMarshalId === marshal.marshalId ? '−' : '+' }}</span>
          </button>

          <div v-if="expandedMarshalId === marshal.marshalId" class="area-lead-marshal-content">
            <!-- Check-in and QR code actions (not for yourself) -->
            <div v-if="marshal.marshalId !== currentMarshalId" class="marshal-actions-row">
              <CheckInToggleButton
                :is-checked-in="marshal.isCheckedIn"
                :check-in-time="marshal.checkInTime"
                :check-in-method="marshal.checkInMethod"
                :checked-in-by="marshal.checkedInBy"
                :marshal-name="marshal.marshalName"
                :is-loading="checkingInMarshalId === marshal.id"
                @toggle="$emit('check-in', marshal)"
              />
              <button
                class="btn btn-secondary btn-qr-icon"
                @click.stop="$emit('show-qr', marshal)"
                title="Show QR code for magic link"
              >
                <svg xmlns="http://www.w3.org/2000/svg" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                  <rect x="3" y="3" width="7" height="7"></rect>
                  <rect x="14" y="3" width="7" height="7"></rect>
                  <rect x="3" y="14" width="7" height="7"></rect>
                  <rect x="14" y="14" width="3" height="3"></rect>
                  <rect x="19" y="14" width="2" height="2"></rect>
                  <rect x="14" y="19" width="2" height="2"></rect>
                  <rect x="19" y="19" width="2" height="2"></rect>
                </svg>
              </button>
            </div>

            <!-- Contact details -->
            <div v-if="marshal.email || marshal.phoneNumber" class="marshal-contact-section">
              <a v-if="marshal.phoneNumber" :href="`tel:${marshal.phoneNumber}`" class="contact-link">
                {{ marshal.phoneNumber }}
              </a>
              <a v-if="marshal.email" :href="`mailto:${marshal.email}`" class="contact-link">
                {{ marshal.email }}
              </a>
            </div>

            <!-- Tasks -->
            <div v-if="getProcessedTasks(marshal).length > 0" class="marshal-tasks-section">
              <div class="tasks-label">{{ terms.checklists }}</div>
              <div class="tasks-list">
                <div
                  v-for="task in getProcessedTasks(marshal)"
                  :key="`${task.itemId}-${task.completionContextType || task.contextType}-${task.completionContextId || task.contextId}`"
                  class="task-item"
                  :class="{ 'task-completed': task.isCompleted }"
                >
                  <input
                    type="checkbox"
                    :checked="task.isCompleted"
                    :disabled="savingTask"
                    @change="$emit('toggle-task', task, marshal)"
                  />
                  <div class="task-content">
                    <span class="task-text">{{ task.text }}</span>
                    <div v-if="task.needsDisambiguation && task.contextDisplayName" class="task-context">
                      {{ task.contextDisplayName }}
                    </div>
                    <div v-if="task.isCompleted && (task.completedByActorName || task.completedAt)" class="task-completion-info">
                      <span v-if="getCompletionText(task)" class="completion-by">{{ getCompletionText(task) }}</span>
                      <span v-if="task.completedAt" class="completion-time">{{ formatDateTime(task.completedAt) }}</span>
                    </div>
                  </div>
                </div>
              </div>
            </div>

            <div v-else class="no-tasks-message">
              No {{ termsLower.checklists }} assigned.
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
// defineProps, defineEmits are compiler macros - no import needed
import { getIcon } from '../../utils/icons';
import { useTerminology } from '../../composables/useTerminology';
import { useEventTimeZone } from '../../composables/useEventTimeZone';
import { sortTasks } from '../../utils/sortingHelpers';
import CheckInToggleButton from '../common/CheckInToggleButton.vue';

const { terms, termsLower } = useTerminology();

const props = defineProps({
  isAreaLead: {
    type: Boolean,
    default: false,
  },
  isExpanded: {
    type: Boolean,
    default: false,
  },
  marshals: {
    type: Array,
    default: () => [],
  },
  expandedMarshalId: {
    type: String,
    default: null,
  },
  checkingInMarshalId: {
    type: String,
    default: null,
  },
  savingTask: {
    type: Boolean,
    default: false,
  },
  currentMarshalId: {
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
  timeZoneId: {
    type: String,
    default: 'UTC',
  },
});

// Use event timezone for formatting
const { formatDateTime } = useEventTimeZone(() => props.timeZoneId);

defineEmits(['toggle', 'toggle-marshal', 'check-in', 'toggle-task', 'show-qr']);

const formatMarshalCheckpoints = (checkpoints) => {
  if (!checkpoints || checkpoints.length === 0) return '';
  const formatCheckpoint = (c) => {
    if (c.description) {
      return `${c.name} - ${c.description}`;
    }
    return c.name;
  };
  if (checkpoints.length === 1) return formatCheckpoint(checkpoints[0]);
  return checkpoints.map(formatCheckpoint).join(', ');
};

// Get display name for a context (checkpoint with description, or area name)
const getContextDisplayName = (task, marshal) => {
  const contextType = task.completionContextType || task.contextType;
  const contextId = task.completionContextId || task.contextId;

  if (contextType === 'Checkpoint' && contextId) {
    // First try to get from marshal's checkpoints (has description)
    const marshalCheckpoint = marshal.checkpoints?.find(c => c.checkpointId === contextId);
    if (marshalCheckpoint) {
      if (marshalCheckpoint.description) {
        const maxDescLength = 50;
        const desc = marshalCheckpoint.description.length > maxDescLength
          ? marshalCheckpoint.description.substring(0, maxDescLength) + '...'
          : marshalCheckpoint.description;
        return `${marshalCheckpoint.name} - ${desc}`;
      }
      return marshalCheckpoint.name;
    }
    // Fallback to locations prop
    const location = props.locations.find(l => l.id === contextId);
    if (location) {
      if (location.description) {
        const maxDescLength = 50;
        const desc = location.description.length > maxDescLength
          ? location.description.substring(0, maxDescLength) + '...'
          : location.description;
        return `${location.name} - ${desc}`;
      }
      return location.name;
    }
  }
  if (contextType === 'Area' && contextId) {
    const area = props.areas.find(a => a.id === contextId);
    if (area) {
      return area.name;
    }
  }
  return '';
};

// Get sort key for context (just the name for proper sorting)
const getContextSortKey = (task, marshal) => {
  const contextType = task.completionContextType || task.contextType;
  const contextId = task.completionContextId || task.contextId;

  if (contextType === 'Checkpoint' && contextId) {
    const marshalCheckpoint = marshal.checkpoints?.find(c => c.checkpointId === contextId);
    if (marshalCheckpoint) return marshalCheckpoint.name || '';
    const location = props.locations.find(l => l.id === contextId);
    return location?.name || '';
  }
  if (contextType === 'Area' && contextId) {
    const area = props.areas.find(a => a.id === contextId);
    return area?.name || '';
  }
  return '';
};

// Process tasks for a marshal: dedupe, sort, and add disambiguation
const getProcessedTasks = (marshal) => {
  if (!marshal.allTasks || marshal.allTasks.length === 0) return [];

  // Deduplicate by itemId + contextType + contextId
  // If duplicates exist, prefer the completed version (merge completion state)
  const taskMap = new Map();
  for (const task of marshal.allTasks) {
    const contextType = task.completionContextType || task.contextType;
    const contextId = task.completionContextId || task.contextId;
    const key = `${task.itemId}_${contextType}_${contextId}`;

    if (!taskMap.has(key)) {
      taskMap.set(key, { ...task });
    } else {
      // Merge: if any instance is completed, mark as completed
      const existing = taskMap.get(key);
      if (task.isCompleted && !existing.isCompleted) {
        taskMap.set(key, {
          ...existing,
          isCompleted: true,
          completedAt: task.completedAt || existing.completedAt,
          completedByActorName: task.completedByActorName || existing.completedByActorName,
          contextOwnerName: task.contextOwnerName || existing.contextOwnerName,
        });
      }
    }
  }
  const uniqueTasks = Array.from(taskMap.values());

  // Count occurrences of each task text
  const textCounts = new Map();
  for (const task of uniqueTasks) {
    textCounts.set(task.text, (textCounts.get(task.text) || 0) + 1);
  }

  // Sort by displayOrder, then text, then context name
  const sorted = sortTasks(uniqueTasks, (task) => getContextSortKey(task, marshal));

  // Add disambiguation info
  return sorted.map(task => ({
    ...task,
    needsDisambiguation: textCounts.get(task.text) > 1,
    contextDisplayName: getContextDisplayName(task, marshal),
  }));
};

// Format completion text (who completed it, on behalf of whom)
const getCompletionText = (task) => {
  if (task.completedByActorName && task.contextOwnerName &&
      task.completedByActorName !== task.contextOwnerName) {
    return `${task.completedByActorName} on behalf of ${task.contextOwnerName}`;
  }
  return task.completedByActorName || '';
};

// formatDateTime provided by useEventTimeZone composable

// Get task counts based on deduped tasks
const getTaskCounts = (marshal) => {
  const tasks = getProcessedTasks(marshal);
  const total = tasks.length;
  const completed = tasks.filter(t => t.isCompleted).length;
  return { total, completed };
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
.area-lead-marshals-content {
  padding: 0.5rem;
}

.area-lead-marshals-list {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.area-lead-marshal-item {
  background: var(--bg-secondary);
  border: 1px solid var(--border-light);
  border-radius: 10px;
  overflow: hidden;
}

.area-lead-marshal-header {
  width: 100%;
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  padding: 0.75rem 1rem;
  background: var(--bg-secondary);
  border: none;
  cursor: pointer;
  text-align: left;
  transition: background 0.2s;
  gap: 0.5rem;
}

.area-lead-marshal-header:hover {
  background: var(--bg-hover);
}

.area-lead-marshal-header.active {
  background: var(--bg-tertiary);
  border-bottom: 1px solid var(--border-light);
}

.area-lead-marshal-header.is-checked-in {
  background: var(--checked-in-bg);
}

.area-lead-marshal-header.is-checked-in.active {
  background: var(--success-bg-light);
}

.marshal-header-info {
  flex: 1;
  min-width: 0;
}

.marshal-name-row {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  margin-bottom: 0.25rem;
}

.marshal-name {
  font-size: 0.95rem;
  font-weight: 600;
  color: var(--text-dark);
}

.check-status-badge {
  font-size: 0.85rem;
  color: var(--text-secondary);
}

.check-status-badge.checked-in {
  color: var(--checked-in-text);
}

.marshal-meta {
  display: flex;
  flex-direction: column;
  gap: 0.15rem;
  font-size: 0.8rem;
  color: var(--text-secondary);
}

.marshal-checkpoint {
  color: var(--text-secondary);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.marshal-task-count {
  color: var(--text-muted);
}

.area-lead-marshal-content {
  padding: 1rem;
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.marshal-checkin-section {
  /* CheckInToggleButton has its own styling */
}

.marshal-contact-section {
  display: flex;
  flex-wrap: wrap;
  gap: 0.75rem;
}

.contact-link {
  color: var(--brand-primary);
  text-decoration: none;
  font-size: 0.9rem;
}

.contact-link:hover {
  text-decoration: underline;
}

.marshal-tasks-section {
  /* Task section styles */
}

.tasks-label {
  font-size: 0.85rem;
  font-weight: 600;
  color: var(--text-dark);
  margin-bottom: 0.5rem;
}

.tasks-list {
  display: flex;
  flex-direction: column;
  gap: 0.4rem;
}

.task-item {
  display: flex;
  align-items: flex-start;
  gap: 0.5rem;
  padding: 0.5rem;
  background: var(--bg-muted);
  border-radius: 6px;
  font-size: 0.85rem;
}

.task-item.task-completed {
  background: var(--success-bg-light);
}

.task-item input[type="checkbox"] {
  width: 16px;
  height: 16px;
  margin-top: 0.1rem;
  cursor: pointer;
  flex-shrink: 0;
}

.task-content {
  flex: 1;
  min-width: 0;
}

.task-text {
  color: var(--text-dark);
}

.task-completed .task-text {
  text-decoration: line-through;
  color: var(--text-secondary);
}

.task-context {
  font-size: 0.8rem;
  color: var(--text-secondary);
  margin-top: 0.15rem;
}

.task-completion-info {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
  font-size: 0.75rem;
  color: var(--success-dark);
  margin-top: 0.25rem;
}

.completion-by {
  color: var(--success-dark);
}

.completion-time {
  color: var(--text-muted);
}

.no-tasks-message {
  font-size: 0.85rem;
  color: var(--text-secondary);
  font-style: italic;
}

.marshal-actions-row {
  display: flex;
  align-items: center;
  gap: 1.5rem;
}

.btn-qr-icon {
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 0.5rem;
  border-radius: 6px;
  border: 1px solid var(--border-light);
  background: var(--bg-muted);
  color: var(--text-dark);
  cursor: pointer;
  transition: background 0.2s, border-color 0.2s, color 0.2s;
}

.btn-qr-icon:hover {
  background: var(--bg-hover);
  border-color: var(--brand-primary);
  color: var(--brand-primary);
}

.btn-qr-icon svg {
  display: block;
}
</style>
