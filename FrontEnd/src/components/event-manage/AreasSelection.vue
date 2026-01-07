<template>
  <div class="areas-selection">
    <div class="areas-selection-header" v-if="areas.length > 1">
      <button
        @click="toggleAll"
        class="toggle-all-btn"
      >
        {{ allSelected ? 'Deselect all' : 'Select all' }}
      </button>
    </div>
    <div class="areas-list">
      <label class="area-checkbox" v-for="area in sortedAreas" :key="area.id">
        <input
          type="checkbox"
          :checked="selectedAreaIds.includes(area.id)"
          @change="handleToggle(area.id)"
        />
        <span class="area-color-dot" :style="{ backgroundColor: area.color || '#667eea' }"></span>
        {{ area.name }}
      </label>
    </div>
  </div>
</template>

<script setup>
import { computed } from 'vue';
import { alphanumericCompare } from '../../utils/sortUtils';

const props = defineProps({
  areas: {
    type: Array,
    default: () => [],
  },
  selectedAreaIds: {
    type: Array,
    default: () => [],
  },
});

const emit = defineEmits(['update:selectedAreaIds']);

const sortedAreas = computed(() => {
  return [...props.areas].sort((a, b) => {
    if (a.displayOrder !== b.displayOrder) {
      return a.displayOrder - b.displayOrder;
    }
    return alphanumericCompare(a.name, b.name);
  });
});

const allSelected = computed(() => {
  return props.areas.length > 0 && props.selectedAreaIds.length === props.areas.length;
});

const handleToggle = (areaId) => {
  const newSelection = [...props.selectedAreaIds];
  const index = newSelection.indexOf(areaId);

  if (index >= 0) {
    newSelection.splice(index, 1);
  } else {
    newSelection.push(areaId);
  }

  emit('update:selectedAreaIds', newSelection);
};

const toggleAll = () => {
  if (allSelected.value) {
    emit('update:selectedAreaIds', []);
  } else {
    emit('update:selectedAreaIds', props.areas.map(a => a.id));
  }
};
</script>

<style scoped>
.areas-selection {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.areas-selection-header {
  display: flex;
  justify-content: flex-start;
  padding-bottom: 0.5rem;
  border-bottom: 1px solid var(--border-light);
}

.toggle-all-btn {
  padding: 0.4rem 0.75rem;
  background: var(--bg-hover);
  border: 1px solid var(--border-medium);
  border-radius: 4px;
  cursor: pointer;
  font-size: 0.8rem;
  color: var(--text-dark);
  transition: background-color 0.2s;
  font-weight: 500;
}

.toggle-all-btn:hover {
  background: var(--border-light);
}

.areas-list {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  max-height: 300px;
  overflow-y: auto;
  padding-right: 0.5rem;
}

.areas-list::-webkit-scrollbar {
  width: 6px;
}

.areas-list::-webkit-scrollbar-track {
  background: var(--bg-secondary);
  border-radius: 3px;
}

.areas-list::-webkit-scrollbar-thumb {
  background: var(--text-light);
  border-radius: 3px;
}

.areas-list::-webkit-scrollbar-thumb:hover {
  background: var(--text-darker);
}

.area-checkbox {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  font-size: 0.9rem;
  cursor: pointer;
  color: var(--text-dark);
  padding: 0.25rem 0;
}

.area-checkbox input[type="checkbox"] {
  cursor: pointer;
  width: 1rem;
  height: 1rem;
  flex-shrink: 0;
}

.area-color-dot {
  width: 12px;
  height: 12px;
  border-radius: 50%;
  display: inline-block;
  flex-shrink: 0;
}
</style>
