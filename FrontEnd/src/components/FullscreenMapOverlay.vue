<template>
  <teleport to="body">
    <div v-if="show" class="fullscreen-map-overlay">
      <!-- Fixed header at top -->
      <div class="fullscreen-map-header">
        <div class="context-info">
          <h3>{{ contextTitle }}</h3>
          <p v-if="contextDescription">{{ contextDescription }}</p>
        </div>
        <div class="action-buttons">
          <button @click="handleCancel" class="btn btn-cancel">
            Cancel
          </button>
          <button
            @click="handleDone"
            class="btn btn-done"
            :disabled="!canComplete"
          >
            Done
          </button>
        </div>
      </div>

      <!-- Fullscreen map container -->
      <div class="fullscreen-map-container">
        <MapView
          ref="mapViewRef"
          :drawingMode="drawingMode"
          v-bind="$attrs"
          @map-click="$emit('map-click', $event)"
          @polygon-complete="$emit('polygon-complete', $event)"
          @polygon-drawing="handlePolygonDrawing"
          @location-click="$emit('location-click', $event)"
          @area-click="$emit('area-click', $event)"
        />

        <!-- Drawing controls overlay - shown when in draw-area mode -->
        <div v-if="drawingMode && polygonPointCount > 0" class="drawing-controls">
          <div class="point-count">{{ polygonPointCount }} point{{ polygonPointCount !== 1 ? 's' : '' }}</div>
          <div class="undo-redo-buttons">
            <button
              @click="handleUndo"
              class="btn-drawing undo-btn"
              :disabled="!canUndoPoints"
              title="Undo last point"
            >
              ↩ Undo
            </button>
            <button
              @click="handleRedo"
              class="btn-drawing redo-btn"
              :disabled="!canRedoPoints"
              title="Redo point"
            >
              Redo ↪
            </button>
          </div>
        </div>
      </div>
    </div>
  </teleport>
</template>

<script setup>
import { ref, computed, onMounted, onUnmounted, watch } from 'vue';
import MapView from './MapView.vue';

// Disable inheriting attrs on root element so we can pass them to MapView
defineOptions({
  inheritAttrs: false
});

const props = defineProps({
  show: {
    type: Boolean,
    required: true,
  },
  mode: {
    type: String,
    validator: (value) => ['place-checkpoint', 'move-checkpoint', 'draw-area', 'add-multiple'].includes(value),
  },
  contextTitle: {
    type: String,
    default: '',
  },
  contextDescription: {
    type: String,
    default: '',
  },
  canComplete: {
    type: Boolean,
    default: false,
  },
  drawingMode: {
    type: Boolean,
    default: false,
  },
});

const emit = defineEmits(['done', 'cancel', 'map-click', 'polygon-complete', 'polygon-drawing', 'location-click', 'area-click']);

const mapViewRef = ref(null);
const polygonPointCount = ref(0);
const canUndoPoints = ref(false);
const canRedoPoints = ref(false);

const handleDone = () => {
  emit('done');
};

const handleCancel = () => {
  emit('cancel');
};

// Handle polygon drawing updates from MapView
const handlePolygonDrawing = (points) => {
  polygonPointCount.value = points?.length || 0;
  updateUndoRedoState();
  emit('polygon-drawing', points);
};

// Update undo/redo button states
const updateUndoRedoState = () => {
  if (mapViewRef.value) {
    canUndoPoints.value = mapViewRef.value.canUndo();
    canRedoPoints.value = mapViewRef.value.canRedo();
  }
};

// Undo last polygon point
const handleUndo = () => {
  if (mapViewRef.value) {
    mapViewRef.value.undoPolygonPoint();
    updateUndoRedoState();
  }
};

// Redo polygon point
const handleRedo = () => {
  if (mapViewRef.value) {
    mapViewRef.value.redoPolygonPoint();
    updateUndoRedoState();
  }
};

// Reset state when overlay is hidden
watch(() => props.show, (newShow) => {
  if (!newShow) {
    polygonPointCount.value = 0;
    canUndoPoints.value = false;
    canRedoPoints.value = false;
  }
});

// Handle escape key
const handleKeydown = (event) => {
  if (event.key === 'Escape' && props.show) {
    handleCancel();
  }
  // Handle Ctrl+Z for undo and Ctrl+Y or Ctrl+Shift+Z for redo
  if (props.show && props.drawingMode) {
    if ((event.ctrlKey || event.metaKey) && event.key === 'z' && !event.shiftKey) {
      event.preventDefault();
      handleUndo();
    } else if ((event.ctrlKey || event.metaKey) && (event.key === 'y' || (event.key === 'z' && event.shiftKey))) {
      event.preventDefault();
      handleRedo();
    }
  }
};

onMounted(() => {
  document.addEventListener('keydown', handleKeydown);
});

onUnmounted(() => {
  document.removeEventListener('keydown', handleKeydown);
});
</script>

<style scoped>
.fullscreen-map-overlay {
  position: fixed;
  inset: 0;
  z-index: 2000;
  background: white;
  display: flex;
  flex-direction: column;
}

.fullscreen-map-header {
  flex-shrink: 0;
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 1.5rem 2rem;
  background: white;
  border-bottom: 2px solid #e0e0e0;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.1);
}

.context-info h3 {
  margin: 0 0 0.25rem 0;
  font-size: 1.25rem;
  color: #333;
  font-weight: 600;
}

.context-info p {
  margin: 0;
  color: #666;
  font-size: 0.9rem;
}

.action-buttons {
  display: flex;
  gap: 1rem;
  flex-shrink: 0;
}

.fullscreen-map-container {
  flex: 1;
  position: relative;
  overflow: hidden;
}

.btn {
  padding: 0.5rem 1.5rem;
  border: none;
  border-radius: 4px;
  font-size: 1rem;
  cursor: pointer;
  transition: all 0.2s ease;
  font-weight: 500;
}

.btn-cancel {
  background-color: #6c757d;
  color: white;
}

.btn-cancel:hover {
  background-color: #5a6268;
}

.btn-done {
  background-color: #007bff;
  color: white;
}

.btn-done:hover:not(:disabled) {
  background-color: #0056b3;
}

.btn-done:disabled {
  background-color: #ccc;
  cursor: not-allowed;
  opacity: 0.6;
}

/* Mobile responsive */
@media (max-width: 768px) {
  .fullscreen-map-header {
    flex-direction: column;
    gap: 1rem;
    padding: 1rem;
  }

  .context-info {
    text-align: center;
    width: 100%;
  }

  .action-buttons {
    width: 100%;
    justify-content: center;
  }

  .btn {
    flex: 1;
    max-width: 150px;
  }
}

/* Drawing controls overlay */
.drawing-controls {
  position: absolute;
  bottom: 2rem;
  left: 50%;
  transform: translateX(-50%);
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 0.5rem;
  z-index: 1000;
}

.point-count {
  background: rgba(0, 0, 0, 0.7);
  color: white;
  padding: 0.5rem 1rem;
  border-radius: 20px;
  font-size: 0.9rem;
  font-weight: 500;
}

.undo-redo-buttons {
  display: flex;
  gap: 0.5rem;
}

.btn-drawing {
  padding: 0.75rem 1.25rem;
  border: none;
  border-radius: 8px;
  font-size: 1rem;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.2s ease;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.2);
  min-width: 90px;
}

.undo-btn {
  background: white;
  color: #333;
}

.undo-btn:hover:not(:disabled) {
  background: #f0f0f0;
}

.redo-btn {
  background: white;
  color: #333;
}

.redo-btn:hover:not(:disabled) {
  background: #f0f0f0;
}

.btn-drawing:disabled {
  opacity: 0.4;
  cursor: not-allowed;
}

@media (max-width: 768px) {
  .drawing-controls {
    bottom: 1rem;
  }

  .btn-drawing {
    padding: 0.6rem 1rem;
    font-size: 0.9rem;
    min-width: 80px;
  }
}
</style>
