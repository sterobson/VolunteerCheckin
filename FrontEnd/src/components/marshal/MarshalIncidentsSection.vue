<template>
  <div class="accordion-section" v-if="incidents.length > 0 || loading">
    <button
      class="accordion-header"
      :class="{ active: isExpanded }"
      @click="$emit('toggle')"
    >
      <span class="accordion-title">
        <span class="section-icon" v-html="getIcon('incidents')"></span>
        Your {{ incidents.length === 1 ? 'incident' : 'incidents' }}{{ incidents.length > 1 ? ` (${incidents.length})` : '' }}
      </span>
      <span class="accordion-icon">{{ isExpanded ? '−' : '+' }}</span>
    </button>
    <div v-if="isExpanded" class="accordion-content">
      <div v-if="loading" class="loading-state">
        Loading incidents...
      </div>
      <div v-else-if="incidents.length === 0" class="empty-state">
        <p>No incidents to show.</p>
      </div>
      <div v-else class="incidents-list">
        <IncidentCard
          v-for="incident in incidents"
          :key="incident.incidentId"
          :incident="incident"
          @select="$emit('select-incident', incident)"
        />
      </div>
    </div>
  </div>
</template>

<script setup>
import { defineProps, defineEmits } from 'vue';
import { getIcon } from '../../utils/icons';
import IncidentCard from '../IncidentCard.vue';

defineProps({
  incidents: {
    type: Array,
    required: true,
  },
  loading: {
    type: Boolean,
    default: false,
  },
  isExpanded: {
    type: Boolean,
    default: false,
  },
});

defineEmits(['toggle', 'select-incident']);
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
.incidents-list {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.loading-state {
  text-align: center;
  padding: 1rem;
  color: var(--text-secondary);
}

.empty-state {
  text-align: center;
  padding: 1rem;
  color: var(--text-secondary);
}

.empty-state p {
  margin: 0;
}
</style>
