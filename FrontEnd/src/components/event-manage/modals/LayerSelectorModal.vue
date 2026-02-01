<template>
  <BaseModal
    :show="show"
    title="Layers"
    size="small"
    :z-index="zIndex"
    @close="$emit('close')"
  >
    <div class="layer-selector-content">
      <p class="help-text">
        Choose how this {{ checkpointTerm.toLowerCase() }} is assigned to layers.
      </p>

      <!-- Auto-detect radio -->
      <label class="mode-option" :class="{ selected: mode === 'auto' }">
        <input
          type="radio"
          :checked="mode === 'auto'"
          name="layer-mode"
          @change="handleModeChange('auto')"
        />
        <div class="mode-content">
          <span class="mode-title">Auto-detect from routes</span>
          <span class="mode-description">
            Automatically assigned to layers with routes within 25m
          </span>
        </div>
      </label>

      <!-- All layers radio -->
      <label class="mode-option" :class="{ selected: mode === 'all' }">
        <input
          type="radio"
          :checked="mode === 'all'"
          name="layer-mode"
          @change="handleModeChange('all')"
        />
        <div class="mode-content">
          <span class="mode-title">All layers</span>
          <span class="mode-description">
            Appears on all layers, including future ones
          </span>
        </div>
      </label>

      <!-- Specific layers radio -->
      <label class="mode-option" :class="{ selected: mode === 'specific' }">
        <input
          type="radio"
          :checked="mode === 'specific'"
          name="layer-mode"
          @change="handleModeChange('specific')"
        />
        <div class="mode-content">
          <span class="mode-title">Specific layers</span>
          <span class="mode-description">
            Manually select which layers
          </span>
        </div>
      </label>

      <!-- Layer checkboxes (only shown when mode === 'specific') -->
      <div v-if="mode === 'specific'" class="layer-list">
        <label
          v-for="layer in sortedLayers"
          :key="layer.id"
          class="checkbox-label"
        >
          <input
            type="checkbox"
            :checked="isLayerSelected(layer.id)"
            @change="handleLayerToggle(layer.id)"
          />
          <span class="layer-color-dot" :style="{ backgroundColor: layer.routeColor || '#3388ff' }"></span>
          <span>{{ layer.name }}</span>
        </label>
        <div v-if="sortedLayers.length === 0" class="no-layers-message">
          No layers available
        </div>
      </div>
    </div>

    <template #actions>
      <button @click="$emit('close')" class="btn btn-primary" type="button">Done</button>
    </template>
  </BaseModal>
</template>

<script setup>
import { computed, defineProps, defineEmits } from 'vue';
import BaseModal from '../../BaseModal.vue';

const props = defineProps({
  show: {
    type: Boolean,
    default: false,
  },
  layers: {
    type: Array,
    default: () => [],
  },
  layerAssignmentMode: {
    type: String,
    default: 'auto', // 'auto' | 'all' | 'specific'
  },
  selectedLayerIds: {
    type: Array,
    default: null, // null = all layers or auto-detected
  },
  checkpointTerm: {
    type: String,
    default: 'checkpoint',
  },
  zIndex: {
    type: Number,
    default: 1100,
  },
});

const emit = defineEmits(['close', 'update:layer-assignment']);

// Compute current mode from props
const mode = computed(() => {
  return props.layerAssignmentMode || 'auto';
});

const sortedLayers = computed(() => {
  return [...props.layers].sort((a, b) => (a.displayOrder || 0) - (b.displayOrder || 0));
});

const isLayerSelected = (layerId) => {
  return (props.selectedLayerIds || []).includes(layerId);
};

const handleModeChange = (newMode) => {
  if (newMode === 'specific') {
    // When switching to specific, select all layers initially
    emit('update:layer-assignment', {
      mode: 'specific',
      layerIds: props.layers.map(layer => layer.id),
    });
  } else {
    // For auto and all modes, layerIds is null (backend calculates for auto)
    emit('update:layer-assignment', {
      mode: newMode,
      layerIds: null,
    });
  }
};

const handleLayerToggle = (layerId) => {
  const currentIds = props.selectedLayerIds || [];
  let newIds;

  if (currentIds.includes(layerId)) {
    newIds = currentIds.filter(id => id !== layerId);
  } else {
    newIds = [...currentIds, layerId];
  }

  // Ensure at least one layer is selected in specific mode
  if (newIds.length === 0) {
    // Switch to all layers if none selected
    emit('update:layer-assignment', {
      mode: 'all',
      layerIds: null,
    });
  } else {
    emit('update:layer-assignment', {
      mode: 'specific',
      layerIds: newIds,
    });
  }
};
</script>

<style scoped>
.layer-selector-content {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.help-text {
  margin: 0;
  padding: 0.75rem;
  background: var(--bg-secondary);
  border-left: 3px solid var(--brand-primary);
  font-size: 0.85rem;
  color: var(--text-secondary);
  border-radius: 4px;
}

.mode-option {
  display: flex;
  align-items: flex-start;
  gap: 0.75rem;
  cursor: pointer;
  padding: 0.75rem;
  font-size: 0.9rem;
  border-radius: 6px;
  border: 2px solid var(--border-medium);
  transition: all 0.15s;
  background: var(--card-bg);
}

.mode-option:hover {
  border-color: var(--brand-primary);
  background: var(--bg-secondary);
}

.mode-option.selected {
  border-color: var(--brand-primary);
  background: var(--info-bg);
}

.mode-option input[type="radio"] {
  cursor: pointer;
  width: 1.1rem;
  height: 1.1rem;
  flex-shrink: 0;
  margin-top: 0.15rem;
}

.mode-content {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.mode-title {
  font-weight: 500;
  color: var(--text-primary);
}

.mode-description {
  font-size: 0.8rem;
  color: var(--text-secondary);
  line-height: 1.4;
}

.layer-list {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
  padding: 0.5rem;
  border: 1px solid var(--border-medium);
  border-radius: 4px;
  background: var(--card-bg);
  max-height: 200px;
  overflow-y: auto;
  margin-top: 0.5rem;
}

.checkbox-label {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  cursor: pointer;
  padding: 0.5rem;
  font-size: 0.9rem;
  border-radius: 4px;
  transition: background-color 0.15s;
}

.checkbox-label:hover {
  background: var(--bg-secondary);
}

.checkbox-label input[type="checkbox"] {
  cursor: pointer;
  width: 1.1rem;
  height: 1.1rem;
  flex-shrink: 0;
}

.layer-color-dot {
  width: 12px;
  height: 12px;
  border-radius: 50%;
  flex-shrink: 0;
}

.no-layers-message {
  padding: 0.75rem;
  text-align: center;
  color: var(--text-secondary);
  font-style: italic;
}

.btn {
  padding: 0.5rem 1rem;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  font-size: 0.9rem;
  transition: background-color 0.2s;
}

.btn-primary {
  background: var(--btn-primary-bg);
  color: var(--btn-primary-text);
}

.btn-primary:hover {
  background: var(--btn-primary-hover);
}
</style>
