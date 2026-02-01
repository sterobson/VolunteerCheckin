<template>
  <BaseModal
    :show="show"
    :z-index="zIndex"
    title="Assign checkpoints to layers"
    size="medium"
    @close="$emit('cancel')"
  >
    <div class="batch-layer-selection">
      <p class="description">
        Choose how to assign {{ checkpointCount }} {{ checkpointCount === 1 ? checkpointTerm : checkpointTermPlural }} to layers.
      </p>

      <div class="selection-options">
        <!-- Auto-detect option -->
        <label class="option-row" :class="{ selected: mode === 'auto-detect' }">
          <input
            type="radio"
            v-model="mode"
            value="auto-detect"
            name="layer-mode"
          />
          <div class="option-content">
            <span class="option-label">Auto-detect from routes</span>
            <span class="option-description">
              Assign each {{ checkpointTerm.toLowerCase() }} to layers whose routes pass within
              <select
                v-model="autoDetectDistance"
                class="distance-select inline"
                @click.stop
              >
                <option :value="25">25m</option>
                <option :value="50">50m</option>
                <option :value="100">100m</option>
                <option :value="200">200m</option>
                <option :value="500">500m</option>
              </select>
            </span>
          </div>
        </label>

        <!-- All layers option -->
        <label class="option-row" :class="{ selected: mode === 'all' }">
          <input
            type="radio"
            v-model="mode"
            value="all"
            name="layer-mode"
          />
          <div class="option-content">
            <span class="option-label">All layers</span>
            <span class="option-description">
              {{ checkpointTermPlural }} will appear on all layers
            </span>
          </div>
        </label>

        <!-- Specific layers option -->
        <label class="option-row" :class="{ selected: mode === 'specific' }">
          <input
            type="radio"
            v-model="mode"
            value="specific"
            name="layer-mode"
          />
          <div class="option-content">
            <span class="option-label">Specific layers</span>
            <span class="option-description">
              Choose which layers to assign all {{ checkpointTermPlural.toLowerCase() }} to
            </span>
          </div>
        </label>

        <!-- Layer checkboxes (shown when specific is selected) -->
        <div v-if="mode === 'specific'" class="layer-checkboxes">
          <label
            v-for="layer in sortedLayers"
            :key="layer.id"
            class="layer-checkbox-row"
          >
            <input
              type="checkbox"
              :checked="selectedLayerIds.includes(layer.id)"
              @change="toggleLayer(layer.id)"
            />
            <span
              class="layer-color-dot"
              :style="{ backgroundColor: layer.routeColor || '#3388ff' }"
            ></span>
            <span class="layer-name">{{ layer.name }}</span>
          </label>
        </div>
      </div>
    </div>

    <template #actions>
      <button type="button" class="btn btn-secondary" @click="$emit('cancel')">
        Cancel
      </button>
      <button
        type="button"
        class="btn btn-primary"
        :disabled="mode === 'specific' && selectedLayerIds.length === 0"
        @click="handleConfirm"
      >
        Add {{ checkpointTermPlural.toLowerCase() }}
      </button>
    </template>
  </BaseModal>
</template>

<script setup>
import { ref, computed, watch } from 'vue';
import BaseModal from '../../BaseModal.vue';
import { useTerminology } from '../../../composables/useTerminology';

const { terms } = useTerminology();

const props = defineProps({
  show: {
    type: Boolean,
    default: false,
  },
  zIndex: {
    type: Number,
    default: 1000,
  },
  layers: {
    type: Array,
    default: () => [],
  },
  checkpointCount: {
    type: Number,
    default: 0,
  },
});

const emit = defineEmits(['confirm', 'cancel']);

const checkpointTerm = computed(() => terms.value.checkpoint || 'Checkpoint');
const checkpointTermPlural = computed(() => terms.value.checkpoints || 'Checkpoints');

// Selection mode: 'auto-detect', 'all', or 'specific'
const mode = ref('auto-detect');
const autoDetectDistance = ref(25);
const selectedLayerIds = ref([]);

// Sort layers by display order
const sortedLayers = computed(() => {
  return [...props.layers].sort((a, b) => (a.displayOrder || 0) - (b.displayOrder || 0));
});

// Reset state when modal opens
watch(() => props.show, (newShow) => {
  if (newShow) {
    mode.value = 'auto-detect';
    autoDetectDistance.value = 25;
    selectedLayerIds.value = [];
  }
});

const toggleLayer = (layerId) => {
  const index = selectedLayerIds.value.indexOf(layerId);
  if (index === -1) {
    selectedLayerIds.value.push(layerId);
  } else {
    selectedLayerIds.value.splice(index, 1);
  }
};

const handleConfirm = () => {
  emit('confirm', {
    mode: mode.value,
    autoDetectDistance: autoDetectDistance.value,
    selectedLayerIds: mode.value === 'specific' ? [...selectedLayerIds.value] : null,
  });
};
</script>

<style scoped>
.batch-layer-selection {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.description {
  color: var(--text-secondary);
  margin: 0;
}

.selection-options {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.option-row {
  display: flex;
  align-items: flex-start;
  gap: 0.75rem;
  padding: 0.75rem;
  border: 1px solid var(--border-color);
  border-radius: 6px;
  cursor: pointer;
  transition: border-color 0.2s, background-color 0.2s;
}

.option-row:hover {
  background: var(--bg-hover);
}

.option-row.selected {
  border-color: var(--brand-primary);
  background: var(--bg-tertiary);
}

.option-row input[type="radio"] {
  margin-top: 0.25rem;
  flex-shrink: 0;
}

.option-content {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.option-label {
  font-weight: 500;
  color: var(--text-primary);
}

.option-description {
  font-size: 0.85rem;
  color: var(--text-secondary);
}

.distance-select.inline {
  padding: 0.125rem 0.375rem;
  border: 1px solid var(--border-color);
  border-radius: 4px;
  background: var(--input-bg);
  color: var(--text-primary);
  font-size: 0.85rem;
  cursor: pointer;
}

.layer-checkboxes {
  margin-left: 1.5rem;
  margin-top: 0.5rem;
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  padding: 0.75rem;
  background: var(--bg-secondary);
  border-radius: 6px;
}

.layer-checkbox-row {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  cursor: pointer;
}

.layer-checkbox-row input[type="checkbox"] {
  flex-shrink: 0;
}

.layer-color-dot {
  width: 12px;
  height: 12px;
  border-radius: 50%;
  flex-shrink: 0;
}

.layer-name {
  color: var(--text-primary);
}

.btn {
  padding: 0.5rem 1rem;
  border: none;
  border-radius: 6px;
  font-size: 0.9rem;
  font-weight: 500;
  cursor: pointer;
  transition: background 0.2s;
}

.btn-secondary {
  background: var(--btn-secondary-bg);
  color: var(--btn-secondary-text);
}

.btn-secondary:hover {
  background: var(--btn-secondary-hover);
}

.btn-primary {
  background: var(--btn-primary-bg);
  color: var(--btn-primary-text);
}

.btn-primary:hover:not(:disabled) {
  background: var(--btn-primary-hover);
}

.btn-primary:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}
</style>
