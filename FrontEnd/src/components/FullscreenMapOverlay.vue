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
          v-bind="$attrs"
          @map-click="$emit('map-click', $event)"
          @polygon-complete="$emit('polygon-complete', $event)"
          @polygon-drawing="$emit('polygon-drawing', $event)"
          @location-click="$emit('location-click', $event)"
          @area-click="$emit('area-click', $event)"
        />
      </div>
    </div>
  </teleport>
</template>

<script setup>
import { onMounted, onUnmounted } from 'vue';
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
});

const emit = defineEmits(['done', 'cancel', 'map-click', 'polygon-complete', 'polygon-drawing', 'location-click', 'area-click']);

const handleDone = () => {
  emit('done');
};

const handleCancel = () => {
  emit('cancel');
};

// Handle escape key
const handleKeydown = (event) => {
  if (event.key === 'Escape' && props.show) {
    handleCancel();
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
</style>
