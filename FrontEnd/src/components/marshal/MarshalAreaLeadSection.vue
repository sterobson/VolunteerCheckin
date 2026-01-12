<template>
  <div v-if="isAreaLead" class="accordion-section">
    <button
      class="accordion-header"
      :class="{ active: isExpanded }"
      @click="$emit('toggle')"
    >
      <span class="accordion-title">
        <span class="section-icon" v-html="getIcon('area')"></span>
        Your {{ areas.length === 1 ? termsLower.area : termsLower.areas }}{{ areas.length > 1 ? ` (${areas.length})` : '' }}
      </span>
      <span class="accordion-icon">{{ isExpanded ? '−' : '+' }}</span>
    </button>
    <div v-if="isExpanded" class="accordion-content area-lead-accordion-content">
      <!-- Area filter pills (only show if more than one area) -->
      <div v-if="areas.length > 1" class="area-filter-pills">
        <button
          v-for="area in areas"
          :key="area.areaId"
          class="area-filter-pill"
          :class="{ selected: selectedFilters.has(area.areaId) }"
          :style="selectedFilters.has(area.areaId) ? { backgroundColor: area.color || '#667eea', borderColor: area.color || '#667eea' } : {}"
          @click="$emit('toggle-filter', area.areaId)"
        >
          {{ area.name }}
        </button>
      </div>
      <AreaLeadSection
        ref="areaLeadRef"
        :event-id="eventId"
        :area-ids="filteredAreaIds"
        :marshal-id="marshalId"
        :route="route"
        @checklist-updated="$emit('checklist-updated')"
      />
    </div>
  </div>
</template>

<script setup>
import { defineProps, defineEmits, ref, defineExpose } from 'vue';
import { getIcon } from '../../utils/icons';
import { useTerminology } from '../../composables/useTerminology';
import AreaLeadSection from '../AreaLeadSection.vue';

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
  areas: {
    type: Array,
    default: () => [],
  },
  selectedFilters: {
    type: Set,
    default: () => new Set(),
  },
  filteredAreaIds: {
    type: Array,
    default: () => [],
  },
  eventId: {
    type: String,
    required: true,
  },
  marshalId: {
    type: String,
    default: '',
  },
  route: {
    type: Array,
    default: () => [],
  },
});

defineEmits(['toggle', 'toggle-filter', 'checklist-updated']);

const areaLeadRef = ref(null);

defineExpose({
  areaLeadRef,
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
.area-lead-accordion-content {
  padding: 1rem;
}

.area-filter-pills {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
  margin-bottom: 1rem;
}

.area-filter-pill {
  padding: 0.4rem 0.75rem;
  border-radius: 999px;
  border: 2px solid var(--border-light);
  background: var(--card-bg);
  font-size: 0.85rem;
  font-weight: 500;
  color: var(--text-dark);
  cursor: pointer;
  transition: all 0.2s;
}

.area-filter-pill:hover {
  background: var(--bg-hover);
}

.area-filter-pill.selected {
  color: white;
  border-color: transparent;
}
</style>
