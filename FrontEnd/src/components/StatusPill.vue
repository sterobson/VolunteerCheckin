<template>
  <button
    class="status-pill"
    :class="[variant, { active }]"
    @click="$emit('click')"
  >
    <slot></slot>
  </button>
</template>

<script setup>
import { defineProps, defineEmits } from 'vue';

defineProps({
  variant: {
    type: String,
    default: 'neutral',
    validator: (value) => [
      'neutral',
      'success',
      'warning',
      'danger',
      'info',
      'purple',
    ].includes(value),
  },
  active: {
    type: Boolean,
    default: false,
  },
});

defineEmits(['click']);
</script>

<style scoped>
.status-pill {
  padding: 0.25rem 0.75rem;
  border-radius: 12px;
  font-size: 0.8rem;
  font-weight: 500;
  border: none;
  cursor: pointer;
  font-family: inherit;
  line-height: inherit;
  transition: box-shadow 0.2s;
}

.status-pill.active {
  box-shadow: inset 0 0 0 2px var(--text-primary);
}

.status-pill.neutral {
  background: var(--bg-tertiary);
  color: var(--text-secondary);
}

.status-pill.success {
  background: var(--status-success-bg);
  color: var(--accent-success);
}

.status-pill.warning {
  background: var(--status-warning-bg);
  color: var(--warning-text, #92400e);
}

.status-pill.danger {
  background: var(--status-danger-bg);
  color: var(--accent-danger);
}

.status-pill.info {
  background: var(--status-open-bg, #e0f2fe);
  color: var(--status-open, #0369a1);
}

.status-pill.purple {
  background: var(--status-in-progress-bg, #f3e8ff);
  color: var(--status-in-progress, #7c3aed);
}
</style>
