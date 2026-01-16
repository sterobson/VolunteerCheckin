<template>
  <div class="accordion-section">
    <button
      class="accordion-header"
      :class="{ active: isExpanded }"
      @click="$emit('toggle')"
    >
      <span class="accordion-title">
        <span class="section-icon" v-html="getIcon('notes')"></span>
        Your {{ notes.length === 1 ? 'note' : 'notes' }}<span v-if="notes.length > 1" class="header-count"> ({{ notes.length }})</span>
      </span>
      <span class="accordion-icon">{{ isExpanded ? '−' : '+' }}</span>
    </button>
    <div v-if="isExpanded" class="accordion-content">
      <NotesView
        :event-id="eventId"
        :all-notes="notes"
        :locations="locations"
        :areas="areas"
        :assignments="assignments"
        :show-scope="false"
        @notes-changed="$emit('notes-changed')"
      />
    </div>
  </div>
</template>

<script setup>
import { defineProps, defineEmits } from 'vue';
import { getIcon } from '../../utils/icons';
import NotesView from '../NotesView.vue';

defineProps({
  eventId: {
    type: String,
    required: true,
  },
  notes: {
    type: Array,
    default: () => [],
  },
  locations: {
    type: Array,
    default: () => [],
  },
  areas: {
    type: Array,
    default: () => [],
  },
  assignments: {
    type: Array,
    default: () => [],
  },
  isExpanded: {
    type: Boolean,
    default: false,
  },
});

defineEmits(['toggle', 'notes-changed']);
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
</style>
