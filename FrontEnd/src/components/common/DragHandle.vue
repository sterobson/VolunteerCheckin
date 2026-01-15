<template>
  <span class="drag-handle" :class="{ disabled }" :title="disabled ? '' : 'Drag to reorder'">
    <svg
      xmlns="http://www.w3.org/2000/svg"
      width="16"
      height="16"
      viewBox="0 0 16 16"
      fill="currentColor"
    >
      <circle cx="5" cy="3" r="1.5" />
      <circle cx="11" cy="3" r="1.5" />
      <circle cx="5" cy="8" r="1.5" />
      <circle cx="11" cy="8" r="1.5" />
      <circle cx="5" cy="13" r="1.5" />
      <circle cx="11" cy="13" r="1.5" />
    </svg>
  </span>
</template>

<script setup>
defineProps({
  disabled: {
    type: Boolean,
    default: false,
  },
});
</script>

<style>
/* Not scoped - class must be accessible to vuedraggable handle selector */
.drag-handle {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-width: 24px;
  min-height: 24px;
  padding: 4px;
  color: var(--text-muted);
  cursor: grab;
  touch-action: none;
  user-select: none;
  transition: color 0.15s ease;
  flex-shrink: 0;
}

.drag-handle:hover:not(.disabled) {
  color: var(--text-primary);
}

.drag-handle:active:not(.disabled) {
  cursor: grabbing;
}

.drag-handle.disabled {
  cursor: not-allowed;
  opacity: 0.4;
}

/* Larger touch target on mobile */
@media (max-width: 768px) {
  .drag-handle {
    min-width: 44px;
    min-height: 44px;
    padding: 10px;
  }
}
</style>
