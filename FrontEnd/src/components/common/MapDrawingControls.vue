<template>
  <div class="drawing-controls" v-if="visible">
    <div class="point-count">{{ pointCount }} point{{ pointCount !== 1 ? 's' : '' }}</div>
    <div class="undo-redo-buttons">
      <button
        @click="$emit('undo')"
        class="btn-drawing undo-btn"
        :disabled="!canUndo"
        title="Undo last point (Ctrl+Z)"
      >
        ↩ Undo
      </button>
      <button
        @click="$emit('redo')"
        class="btn-drawing redo-btn"
        :disabled="!canRedo"
        title="Redo point (Ctrl+Y)"
      >
        Redo ↪
      </button>
    </div>
  </div>
</template>

<script setup>
import { computed, onMounted, onUnmounted, defineProps, defineEmits } from 'vue';

const props = defineProps({
  pointCount: {
    type: Number,
    default: 0,
  },
  canUndo: {
    type: Boolean,
    default: false,
  },
  canRedo: {
    type: Boolean,
    default: false,
  },
  show: {
    type: Boolean,
    default: true,
  },
});

const emit = defineEmits(['undo', 'redo']);

const visible = computed(() => props.show && props.pointCount > 0);

// Keyboard shortcuts
const handleKeydown = (e) => {
  if (!visible.value) return;

  const isMac = navigator.platform.toUpperCase().indexOf('MAC') >= 0;
  const modifier = isMac ? e.metaKey : e.ctrlKey;

  if (modifier && e.key === 'z' && !e.shiftKey) {
    e.preventDefault();
    if (props.canUndo) {
      emit('undo');
    }
  } else if (modifier && (e.key === 'y' || (e.key === 'z' && e.shiftKey))) {
    e.preventDefault();
    if (props.canRedo) {
      emit('redo');
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
  background: var(--shadow-overlay, rgba(0, 0, 0, 0.7));
  color: var(--btn-primary-text, white);
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
  box-shadow: var(--shadow-md);
  min-width: 90px;
  background: var(--card-bg);
  color: var(--text-dark);
}

.btn-drawing:hover:not(:disabled) {
  background: var(--bg-hover);
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
