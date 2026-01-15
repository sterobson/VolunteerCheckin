<template>
  <teleport to="body">
    <div v-if="show" class="fullscreen-map-overlay" @touchmove.prevent.self>
      <!-- Compact header at top -->
      <div class="fullscreen-map-header" :class="{ compact: mode === 'view' }" :style="headerStyle">
        <div class="context-info">
          <h3 :style="{ color: headerTextColor }">{{ title }}</h3>
          <p v-if="description" :style="{ color: headerTextColor, opacity: 0.85 }">{{ description }}</p>
        </div>
        <div class="action-buttons">
          <!-- Custom actions slot (appears before standard buttons) -->
          <slot name="actions"></slot>

          <template v-if="mode === 'view'">
            <button type="button" @click.stop="handleClose" class="btn btn-close-x" :style="closeButtonStyle" title="Close">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" width="20" height="20">
                <path d="M18 6L6 18M6 6l12 12"/>
              </svg>
            </button>
          </template>
          <template v-else>
            <button type="button" @click="handleCancel" class="btn btn-cancel">
              Cancel
            </button>
            <button
              type="button"
              @click="handleDone"
              class="btn btn-done"
              :disabled="!canComplete"
            >
              Done
            </button>
          </template>
        </div>
      </div>

      <!-- Banner slot (for contextual messages like selection mode) -->
      <div v-if="$slots.banner" class="fullscreen-map-banner">
        <slot name="banner"></slot>
      </div>

      <!-- Fullscreen map container -->
      <div class="fullscreen-map-container">
        <slot></slot>
      </div>

      <!-- Footer actions slot (for buttons below the map) -->
      <div v-if="$slots.footer" class="fullscreen-map-footer">
        <slot name="footer"></slot>
      </div>
    </div>
  </teleport>
</template>

<script setup>
import { computed, onMounted, onUnmounted, watch, defineProps, defineEmits } from 'vue';

const props = defineProps({
  show: {
    type: Boolean,
    required: true,
  },
  mode: {
    type: String,
    default: 'view',
    validator: (value) => ['view', 'select-point', 'draw-polygon', 'edit-polygon'].includes(value),
  },
  title: {
    type: String,
    default: '',
  },
  description: {
    type: String,
    default: '',
  },
  canComplete: {
    type: Boolean,
    default: false,
  },
  // Header customization
  headerStyle: {
    type: Object,
    default: () => ({}),
  },
  headerTextColor: {
    type: String,
    default: '',
  },
});

// Computed style for close button to match header text color
const closeButtonStyle = computed(() => {
  if (props.headerTextColor) {
    return { color: props.headerTextColor };
  }
  return {};
});

const emit = defineEmits(['done', 'cancel', 'close']);

const handleDone = () => {
  emit('done');
};

const handleCancel = () => {
  emit('cancel');
};

const handleClose = () => {
  emit('close');
};

// Handle escape key
const handleKeydown = (event) => {
  if (event.key === 'Escape' && props.show) {
    if (props.mode === 'view') {
      handleClose();
    } else {
      handleCancel();
    }
  }
};

// Prevent body scroll when modal is open
watch(() => props.show, (newShow) => {
  if (newShow) {
    document.body.style.overflow = 'hidden';
    document.documentElement.style.overflow = 'hidden';
  } else {
    document.body.style.overflow = '';
    document.documentElement.style.overflow = '';
  }
}, { immediate: true });

onMounted(() => {
  document.addEventListener('keydown', handleKeydown);
});

onUnmounted(() => {
  document.removeEventListener('keydown', handleKeydown);
  document.body.style.overflow = '';
  document.documentElement.style.overflow = '';
});
</script>

<style scoped>
.fullscreen-map-overlay {
  position: fixed;
  inset: 0;
  z-index: 2000;
  background: var(--card-bg);
  display: flex;
  flex-direction: column;
  overflow: hidden;
  touch-action: none;
  overscroll-behavior: contain;
  /* Handle safe area insets on mobile devices */
  padding-bottom: env(safe-area-inset-bottom, 0);
}

.fullscreen-map-header {
  flex-shrink: 0;
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 1.5rem 2rem;
  background: var(--card-bg);
  border-bottom: 2px solid var(--border-light);
  box-shadow: var(--shadow-md);
  position: relative;
  z-index: 10;
  touch-action: none;
}

/* Compact header for view mode */
.fullscreen-map-header.compact {
  padding: 0.5rem 1rem;
  border-bottom: 1px solid var(--border-light);
  box-shadow: none;
}

.fullscreen-map-header.compact .context-info {
  overflow: hidden;
  min-width: 0;
}

.fullscreen-map-header.compact .context-info h3 {
  font-size: 1rem;
  margin: 0;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.fullscreen-map-header.compact .context-info p {
  display: none;
}

.context-info h3 {
  margin: 0 0 0.25rem 0;
  font-size: 1.25rem;
  color: var(--text-dark);
  font-weight: 600;
}

.context-info p {
  margin: 0;
  color: var(--text-secondary);
  font-size: 0.9rem;
}

.action-buttons {
  display: flex;
  gap: 1rem;
  flex-shrink: 0;
}

.fullscreen-map-container {
  flex: 1 1 0;
  min-height: 0;
  position: relative;
  overflow: hidden;
  touch-action: auto;
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
  background-color: var(--btn-secondary-bg);
  color: var(--btn-secondary-text);
}

.btn-cancel:hover {
  background-color: var(--btn-secondary-hover);
}

.btn-done {
  background-color: var(--btn-primary-bg);
  color: var(--btn-primary-text);
}

.btn-done:hover:not(:disabled) {
  background-color: var(--btn-primary-hover);
}

.btn-done:disabled {
  background-color: var(--disabled-bg);
  cursor: not-allowed;
  opacity: 0.6;
}

.btn-close-x {
  background: rgba(0, 0, 0, 0.1);
  padding: 0.5rem;
  color: var(--text-dark);
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: 50%;
  min-width: 36px;
  min-height: 36px;
  cursor: pointer;
  touch-action: manipulation;
  -webkit-tap-highlight-color: transparent;
}

.btn-close-x:hover,
.btn-close-x:active {
  background: rgba(0, 0, 0, 0.2);
}

.fullscreen-map-header.compact .btn-close-x {
  padding: 0.35rem;
  min-width: 32px;
  min-height: 32px;
}

/* Banner styles */
.fullscreen-map-banner {
  flex-shrink: 0;
  background: linear-gradient(135deg, var(--info) 0%, var(--info-dark, #1976D2) 100%);
  color: white;
  padding: 0.75rem 1rem;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 0.75rem;
  font-size: 0.9rem;
}

/* Footer styles */
.fullscreen-map-footer {
  flex-shrink: 0;
  padding: 1rem;
  background: var(--card-bg);
  border-top: 1px solid var(--border-light);
  display: flex;
  justify-content: center;
  gap: 1rem;
}

/* Mobile responsive */
@media (max-width: 768px) {
  .fullscreen-map-header {
    flex-direction: column;
    gap: 1rem;
    padding: 1rem;
  }

  /* Keep compact header horizontal on mobile */
  .fullscreen-map-header.compact {
    flex-direction: row;
    gap: 0.5rem;
    padding: 0.5rem 1rem;
  }

  .fullscreen-map-header.compact .context-info {
    text-align: left;
    width: auto;
  }

  .fullscreen-map-header.compact .action-buttons {
    width: auto;
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
