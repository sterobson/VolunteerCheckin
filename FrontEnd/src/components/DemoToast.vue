<template>
  <Transition name="demo-toast">
    <div v-if="show" class="demo-toast">
      <p class="demo-toast-title">{{ title }}</p>
      <p class="demo-toast-body">{{ body }}</p>
      <div class="demo-toast-actions">
        <button ref="gotItBtn" class="btn btn-primary btn-small" @click="$emit('dismiss')">Got it</button>
        <button class="demo-toast-hide-btn" @click="$emit('hide-all')">Hide all</button>
      </div>
    </div>
  </Transition>
</template>

<script setup>
import { ref, watch, nextTick } from 'vue';

defineProps({
  show: { type: Boolean, default: false },
  title: { type: String, default: '' },
  body: { type: String, default: '' },
});

defineEmits(['dismiss', 'hide-all']);

const gotItBtn = ref(null);

watch(() => gotItBtn.value, (btn) => {
  if (btn) {
    nextTick(() => btn.focus());
  }
});
</script>

<style scoped>
.demo-toast {
  position: fixed;
  bottom: 1.5rem;
  right: 1.5rem;
  z-index: 900;
  max-width: 360px;
  width: calc(100% - 3rem);
  background: var(--card-bg);
  border: 1px solid var(--border-color);
  border-radius: 8px;
  padding: 1rem 1.25rem;
  box-shadow: var(--shadow-md);
}

.demo-toast-title {
  font-weight: 600;
  font-size: 0.95rem;
  color: var(--text-primary);
  margin: 0 0 0.375rem 0;
}

.demo-toast-body {
  font-size: 0.875rem;
  color: var(--text-secondary);
  margin: 0 0 0.875rem 0;
  line-height: 1.45;
}

.demo-toast-actions {
  display: flex;
  align-items: center;
  gap: 1rem;
}

.demo-toast-hide-btn {
  background: none;
  border: none;
  color: var(--text-secondary);
  font-size: 0.8rem;
  cursor: pointer;
  padding: 0;
  text-decoration: underline;
  text-underline-offset: 2px;
}

.demo-toast-hide-btn:hover {
  color: var(--text-primary);
}

/* Transition */
.demo-toast-enter-active {
  transition: opacity 0.25s ease, transform 0.25s ease;
}

.demo-toast-leave-active {
  transition: opacity 0.15s ease, transform 0.15s ease;
}

.demo-toast-enter-from {
  opacity: 0;
  transform: translateY(12px);
}

.demo-toast-leave-to {
  opacity: 0;
  transform: translateY(12px);
}

@media (max-width: 480px) {
  .demo-toast {
    right: 0.75rem;
    bottom: 0.75rem;
    max-width: calc(100% - 1.5rem);
  }
}
</style>
