<template>
  <div v-if="isAreaLead && marshals.length > 0" class="accordion-section">
    <button
      class="accordion-header"
      :class="{ active: isExpanded }"
      @click="$emit('toggle')"
    >
      <span class="accordion-title">
        <span class="section-icon" v-html="getIcon('marshal')"></span>
        Your {{ marshals.length === 1 ? termsLower.person : termsLower.people }}{{ marshals.length > 1 ? ` (${marshals.length})` : '' }}
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
                <span v-if="marshal.totalTaskCount > 0" class="marshal-task-count">
                  {{ marshal.completedTaskCount }} / {{ marshal.totalTaskCount }} {{ termsLower.checklists }} complete
                </span>
              </div>
            </div>
            <span class="accordion-icon">{{ expandedMarshalId === marshal.marshalId ? '−' : '+' }}</span>
          </button>

          <div v-if="expandedMarshalId === marshal.marshalId" class="area-lead-marshal-content">
            <!-- Check-in status -->
            <div class="marshal-checkin-section">
              <CheckInToggleButton
                :is-checked-in="marshal.isCheckedIn"
                :check-in-time="marshal.checkInTime"
                :check-in-method="marshal.checkInMethod"
                :checked-in-by="marshal.checkedInBy"
                :marshal-name="marshal.marshalName"
                :is-loading="checkingInMarshalId === marshal.id"
                @toggle="$emit('check-in', marshal)"
              />
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
            <div v-if="marshal.allTasks.length > 0" class="marshal-tasks-section">
              <div class="tasks-label">{{ terms.checklists }}</div>
              <div class="tasks-list">
                <div
                  v-for="task in marshal.allTasks"
                  :key="`${task.itemId}-${task.contextId}`"
                  class="task-item"
                  :class="{ 'task-completed': task.isCompleted }"
                >
                  <input
                    type="checkbox"
                    :checked="task.isCompleted"
                    :disabled="savingTask"
                    @change="$emit('toggle-task', task, marshal)"
                  />
                  <span class="task-text">{{ task.text }}</span>
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
import { defineProps, defineEmits } from 'vue';
import { getIcon } from '../../utils/icons';
import { useTerminology } from '../../composables/useTerminology';
import CheckInToggleButton from '../common/CheckInToggleButton.vue';

const { terms, termsLower } = useTerminology();

defineProps({
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
});

defineEmits(['toggle', 'toggle-marshal', 'check-in', 'toggle-task']);

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
  align-items: center;
  gap: 0.5rem;
  padding: 0.4rem 0.5rem;
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
  cursor: pointer;
}

.task-text {
  flex: 1;
  color: var(--text-dark);
}

.task-completed .task-text {
  text-decoration: line-through;
  color: var(--text-secondary);
}

.no-tasks-message {
  font-size: 0.85rem;
  color: var(--text-secondary);
  font-style: italic;
}
</style>
