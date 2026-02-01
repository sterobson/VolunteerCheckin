<template>
  <div class="layers-selection">
    <label class="show-all-checkbox" v-if="layers.length > 1">
      <input
        type="checkbox"
        :checked="allSelected"
        @change="handleShowAllChange($event.target.checked)"
      />
      <span>Show all</span>
    </label>
    <div class="layers-list">
      <label class="layer-checkbox" v-for="layer in sortedLayers" :key="layer.id">
        <input
          type="checkbox"
          :checked="selectedLayerIds.includes(layer.id)"
          @change="handleToggle(layer.id)"
        />
        <span class="layer-color-line" :style="getLayerLineStyle(layer)"></span>
        {{ layer.name }}
      </label>
    </div>
  </div>
</template>

<script setup>
import { computed } from 'vue';
import { alphanumericCompare } from '../../utils/sortUtils';

const props = defineProps({
  layers: {
    type: Array,
    default: () => [],
  },
  selectedLayerIds: {
    type: Array,
    default: () => [],
  },
});

const emit = defineEmits(['update:selectedLayerIds']);

const sortedLayers = computed(() => {
  return [...props.layers].sort((a, b) => {
    if (a.displayOrder !== b.displayOrder) {
      return a.displayOrder - b.displayOrder;
    }
    return alphanumericCompare(a.name, b.name);
  });
});

const allSelected = computed(() => {
  return props.layers.length > 0 && props.selectedLayerIds.length === props.layers.length;
});

const getLayerLineStyle = (layer) => {
  const color = layer.routeColor || '#3388ff';
  const style = layer.routeStyle || 'line';

  // Create a visual representation of the line style
  let backgroundStyle = color;

  if (style === 'dash' || style === 'dash-long' || style === 'dash-short' || style === 'dash-dense') {
    backgroundStyle = `repeating-linear-gradient(90deg, ${color} 0px, ${color} 4px, transparent 4px, transparent 6px)`;
  } else if (style === 'dot' || style === 'dot-sparse' || style === 'dot-dense') {
    backgroundStyle = `repeating-linear-gradient(90deg, ${color} 0px, ${color} 2px, transparent 2px, transparent 5px)`;
  } else if (style.includes('dash-dot')) {
    backgroundStyle = `repeating-linear-gradient(90deg, ${color} 0px, ${color} 4px, transparent 4px, transparent 6px, ${color} 6px, ${color} 8px, transparent 8px, transparent 11px)`;
  }

  return {
    background: backgroundStyle,
  };
};

const handleToggle = (layerId) => {
  const newSelection = [...props.selectedLayerIds];
  const index = newSelection.indexOf(layerId);

  if (index >= 0) {
    newSelection.splice(index, 1);
  } else {
    newSelection.push(layerId);
  }

  emit('update:selectedLayerIds', newSelection);
};

const handleShowAllChange = (checked) => {
  if (checked) {
    emit('update:selectedLayerIds', props.layers.map(l => l.id));
  } else {
    emit('update:selectedLayerIds', []);
  }
};
</script>

<style scoped>
.layers-selection {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.show-all-checkbox {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  font-size: 0.875rem;
  font-weight: 500;
  cursor: pointer;
  color: var(--text-primary);
  padding-bottom: 0.5rem;
  border-bottom: 1px solid var(--border-light);
  margin-bottom: 0.25rem;
}

.show-all-checkbox:hover {
  color: var(--accent-primary);
}

.show-all-checkbox input[type="checkbox"] {
  cursor: pointer;
  width: 1rem;
  height: 1rem;
  flex-shrink: 0;
}

.layers-list {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  max-height: 300px;
  overflow-y: auto;
  padding-right: 0.5rem;
}

.layers-list::-webkit-scrollbar {
  width: 6px;
}

.layers-list::-webkit-scrollbar-track {
  background: var(--bg-secondary);
  border-radius: 3px;
}

.layers-list::-webkit-scrollbar-thumb {
  background: var(--text-light);
  border-radius: 3px;
}

.layers-list::-webkit-scrollbar-thumb:hover {
  background: var(--text-darker);
}

.layer-checkbox {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  font-size: 0.9rem;
  cursor: pointer;
  color: var(--text-dark);
  padding: 0.25rem 0;
}

.layer-checkbox input[type="checkbox"] {
  cursor: pointer;
  width: 1rem;
  height: 1rem;
  flex-shrink: 0;
}

.layer-color-line {
  width: 20px;
  height: 4px;
  border-radius: 2px;
  display: inline-block;
  flex-shrink: 0;
}
</style>
