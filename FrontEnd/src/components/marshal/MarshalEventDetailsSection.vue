<template>
  <div class="accordion-section" v-if="eventDate || description">
    <button
      class="accordion-header"
      :class="{ active: isExpanded }"
      @click="$emit('toggle')"
    >
      <span class="accordion-title">
        <span class="section-icon" v-html="getIcon('event')"></span>
        Event details
      </span>
      <span class="accordion-icon">{{ isExpanded ? '−' : '+' }}</span>
    </button>
    <div v-if="isExpanded" class="accordion-content">
      <div class="event-details">
        <div v-if="eventDate" class="event-detail-row">
          <span class="detail-label">Date and time</span>
          <span class="detail-value">{{ formatEventDateTime(eventDate) }}</span>
        </div>
        <div v-if="description" class="event-description">
          <span class="detail-label">Description</span>
          <p class="detail-value">{{ description }}</p>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { defineProps, defineEmits } from 'vue';
import { getIcon } from '../../utils/icons';
import { useEventTimeZone } from '../../composables/useEventTimeZone';

const props = defineProps({
  eventDate: {
    type: String,
    default: null,
  },
  description: {
    type: String,
    default: '',
  },
  isExpanded: {
    type: Boolean,
    default: false,
  },
  timeZoneId: {
    type: String,
    default: 'UTC',
  },
});

defineEmits(['toggle']);

// Use event timezone for formatting
const { formatEventDateTime } = useEventTimeZone(props.timeZoneId);
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
.event-details {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.event-detail-row {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.event-description {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.detail-label {
  font-size: 0.85rem;
  font-weight: 500;
  color: var(--text-secondary);
}

.detail-value {
  font-size: 0.95rem;
  color: var(--text-dark);
}

.event-description .detail-value {
  margin: 0;
  white-space: pre-wrap;
  line-height: 1.5;
}
</style>
