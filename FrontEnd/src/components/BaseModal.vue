<template>
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
        <slot name="header">
          <h2 v-if="title" class="base-modal-title">{{ title }}</h2>
        </slot>
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
      <div class="base-modal-body">
        <slot></slot>
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
</template>

<script setup>
import { ref, defineProps, defineEmits } from 'vue';
import ConfirmModal from './ConfirmModal.vue';

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
  width: 90%;
  position: relative;
}

/* Size Variants */
.base-modal--small {
  max-width: 500px;
}

.base-modal--medium {
  max-width: 600px;
}

.base-modal--large {
  max-width: 800px;
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
  padding: 2rem 2rem 1rem 2rem;
  position: relative;
  border-bottom: 1px solid var(--border-color);
}

.base-modal-title {
  margin: 0;
  padding-right: 3rem; /* Space for close button */
  color: var(--text-primary);
  font-size: 1.25rem;
  font-weight: 600;
}

.base-modal-close {
  position: absolute;
  top: 1rem;
  right: 1rem;
  background: none;
  border: none;
  font-size: 1.5rem;
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
  overflow-x: hidden;
  padding: 1.5rem 2rem;
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
</style>
