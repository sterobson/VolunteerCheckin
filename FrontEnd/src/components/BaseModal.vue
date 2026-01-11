<template>
  <Transition name="modal" appear>
    <div
      v-if="show"
      class="base-modal-overlay"
      :style="{ zIndex: zIndex }"
      @click.self="handleOverlayClick"
    >
      <div
        class="base-modal"
        :class="[
          `base-modal--${size}`,
          `base-modal--${variant}`,
        ]"
      >
      <!-- Fixed Header -->
      <div class="base-modal-header">
        <div class="base-modal-header-content">
          <slot name="header">
            <h2 v-if="title" class="base-modal-title">{{ title }}</h2>
          </slot>
        </div>
        <button
          v-if="showCloseButton"
          @click="handleClose"
          class="base-modal-close"
          type="button"
          aria-label="Close"
        >
          ✕
        </button>
      </div>

      <!-- Optional Tab Header (fixed, below title) -->
      <div v-if="$slots['tab-header']" class="base-modal-tab-header">
        <slot name="tab-header"></slot>
      </div>

      <!-- Scrollable Content Area -->
      <div class="base-modal-body" ref="bodyRef">
        <div class="base-modal-body-content" ref="contentRef">
          <slot></slot>
        </div>
      </div>

      <!-- Fixed Footer -->
      <div v-if="$slots.footer || $slots.actions" class="base-modal-footer">
        <slot name="footer">
          <div v-if="$slots.actions" class="base-modal-actions">
            <slot name="actions"></slot>
          </div>
        </slot>
      </div>
      </div>

      <!-- Nested confirm modal for unsaved changes -->
      <ConfirmModal
        v-if="confirmOnClose && !isConfirmModalRefactored"
        :show="showConfirmClose"
        title="Unsaved changes"
        message="You have unsaved changes. Are you sure you want to close?"
        @confirm="handleConfirmCloseConfirm"
        @cancel="handleConfirmCloseCancel"
      />
    </div>
  </Transition>
</template>

<script setup>
import { ref, defineProps, defineEmits, onMounted, onUnmounted, watch, nextTick } from 'vue';
import ConfirmModal from './ConfirmModal.vue';

// Get animation duration from CSS variable (default 0.3s)
const getAnimationDuration = () => {
  const root = document.documentElement;
  const duration = getComputedStyle(root).getPropertyValue('--modal-resize-duration').trim();
  if (duration.endsWith('s')) {
    return parseFloat(duration) * 1000;
  }
  return 300; // Default fallback
};

const props = defineProps({
  show: {
    type: Boolean,
    required: true,
  },
  title: {
    type: String,
    default: '',
  },
  size: {
    type: String,
    default: 'medium',
    validator: (value) => ['small', 'medium', 'large'].includes(value),
  },
  variant: {
    type: String,
    default: 'center',
    validator: (value) => ['center', 'sidebar'].includes(value),
  },
  zIndex: {
    type: Number,
    default: 1000,
  },
  closeOnOverlay: {
    type: Boolean,
    default: true,
  },
  showCloseButton: {
    type: Boolean,
    default: true,
  },
  confirmOnClose: {
    type: Boolean,
    default: false,
  },
  isDirty: {
    type: Boolean,
    default: false,
  },
});

const emit = defineEmits(['close']);

const showConfirmClose = ref(false);
const isConfirmModalRefactored = ref(false); // Will be set to true once ConfirmModal uses BaseModal to prevent infinite loop

// Refs for height animation
const bodyRef = ref(null);
const contentRef = ref(null);
let mutationObserver = null;
let isAnimating = false;
let lastContentHeight = 0;

// Animate content height when it changes
const animateHeightChange = () => {
  if (!contentRef.value || !bodyRef.value || isAnimating) return;

  const newHeight = contentRef.value.scrollHeight;
  const oldHeight = lastContentHeight;

  // Skip animation if heights are the same or this is the first render
  if (oldHeight === newHeight || lastContentHeight === 0) {
    lastContentHeight = newHeight;
    return;
  }

  isAnimating = true;

  // Set explicit height on content wrapper to animate from
  contentRef.value.style.height = `${oldHeight}px`;
  contentRef.value.style.overflow = 'hidden';

  // Force reflow
  contentRef.value.offsetHeight;

  // Animate to new height
  contentRef.value.style.transition = `height var(--modal-resize-duration, 0.3s) ease`;
  contentRef.value.style.height = `${newHeight}px`;

  // After animation, reset to auto
  const duration = getAnimationDuration();
  setTimeout(() => {
    if (contentRef.value) {
      contentRef.value.style.height = '';
      contentRef.value.style.overflow = '';
      contentRef.value.style.transition = '';
    }
    isAnimating = false;
    lastContentHeight = newHeight;
  }, duration);
};

// Setup mutation observer to detect content changes (more reliable than ResizeObserver for DOM changes)
const setupMutationObserver = () => {
  if (!bodyRef.value || typeof MutationObserver === 'undefined') return;

  // Store initial height
  if (contentRef.value) {
    lastContentHeight = contentRef.value.scrollHeight;
  }

  mutationObserver = new MutationObserver(() => {
    // Debounce to avoid multiple triggers
    nextTick(() => {
      animateHeightChange();
    });
  });

  mutationObserver.observe(bodyRef.value, {
    childList: true,
    subtree: true,
    attributes: true,
    characterData: true,
  });
};

const cleanupMutationObserver = () => {
  if (mutationObserver) {
    mutationObserver.disconnect();
    mutationObserver = null;
  }
};

onMounted(() => {
  nextTick(() => {
    if (props.show) {
      setupMutationObserver();
    }
  });
});

onUnmounted(() => {
  cleanupMutationObserver();
});

// Setup/cleanup observer when modal opens/closes
watch(() => props.show, (newVal) => {
  if (newVal) {
    nextTick(() => {
      lastContentHeight = 0; // Reset on open
      setupMutationObserver();
    });
  } else {
    cleanupMutationObserver();
  }
});

const handleOverlayClick = () => {
  if (props.closeOnOverlay) {
    handleClose();
  }
};

const handleClose = () => {
  if (props.confirmOnClose && props.isDirty) {
    showConfirmClose.value = true;
  } else {
    emit('close');
  }
};

const handleConfirmCloseConfirm = () => {
  showConfirmClose.value = false;
  emit('close');
};

const handleConfirmCloseCancel = () => {
  showConfirmClose.value = false;
};
</script>

<style scoped>
/* Overlay */
.base-modal-overlay {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background: var(--modal-overlay);
  display: flex;
  align-items: center;
  justify-content: center;
}

/* Modal Container - Flexbox Layout */
.base-modal {
  background: var(--modal-bg);
  color: var(--text-primary);
  border-radius: 12px;
  box-shadow: var(--shadow-lg);
  display: flex;
  flex-direction: column;
  max-height: 90vh;
  width: 95%;
  position: relative;
  overflow: hidden;
}

/* Size Variants */
.base-modal--small {
  max-width: 500px;
}

.base-modal--medium {
  max-width: 600px;
}

.base-modal--large {
  width: fit-content;
  min-width: min(400px, 95vw);
  max-width: 95vw;
}

/* Variant: Center (default) */
.base-modal--center {
  /* Already centered by overlay flex layout */
}

/* Variant: Sidebar */
.base-modal--sidebar {
  position: fixed;
  top: 0;
  right: 0;
  bottom: 0;
  width: 400px;
  max-width: 90vw;
  max-height: 100vh;
  margin: 0;
  border-radius: 0;
  height: 100vh;
}

/* Fixed Header */
.base-modal-header {
  flex-shrink: 0;
  padding: 1rem 1rem 1rem 1.5rem;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 1rem;
  border-bottom: 1px solid var(--border-color);
}

.base-modal-header-content {
  flex: 1;
  min-width: 0;
}

.base-modal-title {
  margin: 0;
  color: var(--text-primary);
  font-size: 1.25rem;
  font-weight: 600;
}

.base-modal-close {
  flex-shrink: 0;
  background: none;
  border: none;
  font-size: 1.25rem;
  cursor: pointer;
  color: var(--text-secondary);
  width: 32px;
  height: 32px;
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: 4px;
  transition: background-color 0.2s, color 0.2s;
}

.base-modal-close:hover {
  background: var(--bg-hover);
  color: var(--text-primary);
}

/* Optional Tab Header (below title, above body) */
.base-modal-tab-header {
  flex-shrink: 0;
  padding: 0 2rem;
  border-bottom: 1px solid var(--border-color);
}

/* Scrollable Body */
.base-modal-body {
  flex: 1;
  overflow-y: auto;
  overflow-x: auto;
  padding: 1.5rem 2rem;
  width: max-content;
  min-width: 100%;
}

/* Inner content wrapper for height animation */
.base-modal-body-content {
  width: max-content;
  min-width: 100%;
}

/* Fixed Footer */
.base-modal-footer {
  flex-shrink: 0;
  padding: 1.5rem 2rem 2rem 2rem;
  border-top: 1px solid var(--border-color);
  background: var(--modal-bg);
}

.base-modal-actions {
  display: flex;
  gap: 1rem;
  justify-content: flex-end;
}

/* Sidebar variant adjustments */
.base-modal--sidebar .base-modal-overlay {
  justify-content: flex-end;
  align-items: stretch;
}

/* Modal transition - overlay fades while modal slides */
.modal-enter-active,
.modal-leave-active {
  transition: opacity var(--modal-open-duration, 0.5s) ease;
}

.modal-enter-active .base-modal,
.modal-leave-active .base-modal {
  transition: transform var(--modal-open-duration, 0.5s) ease;
}

.modal-enter-from,
.modal-leave-to {
  opacity: 0;
}

.modal-enter-from .base-modal,
.modal-leave-to .base-modal {
  transform: translateY(100vh);
}

/* Cards grid sizing inside modals - push modal wider based on item count */
.base-modal--large :deep(.cards-grid) {
  max-width: 95vw;
}

/* 2 items: 280*2 + 12 gap = 572px */
.base-modal--large :deep(.cards-grid:has(:nth-child(2):last-child)) {
  min-width: min(572px, 95vw);
}

/* 3 items: 280*3 + 24 gaps = 864px */
.base-modal--large :deep(.cards-grid:has(:nth-child(3):last-child)) {
  min-width: min(864px, 95vw);
}

/* 4 items: 280*4 + 36 gaps = 1156px */
.base-modal--large :deep(.cards-grid:has(:nth-child(4):last-child)) {
  min-width: min(1156px, 95vw);
}

/* 5 items: 280*5 + 48 gaps = 1448px */
.base-modal--large :deep(.cards-grid:has(:nth-child(5):last-child)) {
  min-width: min(1448px, 95vw);
}

/* 6+ items: request width for 6 columns */
.base-modal--large :deep(.cards-grid:has(:nth-child(6))) {
  min-width: min(1740px, 95vw);
}

@media (max-width: 640px) {
  .base-modal--large :deep(.cards-grid),
  .base-modal--large :deep(.cards-grid:has(:nth-child(2):last-child)),
  .base-modal--large :deep(.cards-grid:has(:nth-child(3):last-child)),
  .base-modal--large :deep(.cards-grid:has(:nth-child(4):last-child)),
  .base-modal--large :deep(.cards-grid:has(:nth-child(5):last-child)),
  .base-modal--large :deep(.cards-grid:has(:nth-child(6))) {
    min-width: 0;
  }
}
</style>
