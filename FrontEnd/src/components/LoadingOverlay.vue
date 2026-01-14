<template>
  <Transition name="fade">
    <div v-if="showOverlay" class="loading-overlay">
      <div class="loading-content">
        <div class="spinner"></div>
        <p class="loading-message">{{ overlayMessage || 'Loading...' }}</p>
      </div>
    </div>
  </Transition>
</template>

<script setup>
import { useGlobalLoadingOverlay } from '../services/loadingOverlay';

const { showOverlay, overlayMessage } = useGlobalLoadingOverlay();
</script>

<style scoped>
.loading-overlay {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background: rgba(0, 0, 0, 0.5);
  display: flex;
  justify-content: center;
  align-items: center;
  z-index: 99999;
  backdrop-filter: blur(2px);
}

.loading-content {
  background: var(--card-bg, #ffffff);
  padding: 2rem 3rem;
  border-radius: 12px;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 1rem;
  box-shadow: 0 4px 20px rgba(0, 0, 0, 0.15);
}

.spinner {
  width: 40px;
  height: 40px;
  border: 3px solid var(--border-light, #e0e0e0);
  border-top-color: var(--brand-primary, #4f46e5);
  border-radius: 50%;
  animation: spin 0.8s linear infinite;
}

@keyframes spin {
  to {
    transform: rotate(360deg);
  }
}

.loading-message {
  margin: 0;
  font-size: 1rem;
  font-weight: 500;
  color: var(--text-dark, #1f2937);
}

/* Fade transition */
.fade-enter-active,
.fade-leave-active {
  transition: opacity 0.2s ease;
}

.fade-enter-from,
.fade-leave-to {
  opacity: 0;
}
</style>
