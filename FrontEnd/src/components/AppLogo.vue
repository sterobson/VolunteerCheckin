<template>
  <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 512 512" class="app-logo">
    <defs>
      <filter :id="filterId" x="-20%" y="-20%" width="140%" height="140%">
        <feDropShadow dx="0" dy="6" stdDeviation="8" flood-color="#000" flood-opacity="0.25"/>
      </filter>
    </defs>
    <!-- Blue disc (map marker) - stays still -->
    <ellipse cx="256" cy="400" rx="220" ry="70" fill="#667eea" :filter="`url(#${filterId})`"/>
    <ellipse cx="256" cy="394" rx="185" ry="55" fill="none" stroke="rgba(255,255,255,0.3)" stroke-width="4"/>
    <!-- Marshal figure - animated bounce on load -->
    <g :class="{ 'marshal-figure': animate }">
      <circle cx="256" cy="160" r="55" fill="currentColor"/>
      <path d="M365 380 v-30 a55 55 0 0 0-55-55 H202 a55 55 0 0 0-55 55 v30" fill="currentColor"/>
    </g>
  </svg>
</template>

<script setup>
import { computed } from 'vue';

const props = defineProps({
  size: {
    type: [Number, String],
    default: 48,
  },
  animate: {
    type: Boolean,
    default: false,
  },
});

// Generate unique filter ID to avoid conflicts when multiple logos are on the page
const filterId = computed(() => `discShadow-${Math.random().toString(36).substr(2, 9)}`);
</script>

<style scoped>
.app-logo {
  width: v-bind('typeof size === "number" ? size + "px" : size');
  height: v-bind('typeof size === "number" ? size + "px" : size');
  color: var(--text-primary);
}

.marshal-figure {
  animation: dropBounce 0.8s ease-out forwards;
  transform-origin: center center;
}

@keyframes dropBounce {
  0% {
    transform: translateY(-512px);
  }
  80% {
    transform: translateY(32px);
  }
  100% {
    transform: translateY(0);
  }
}
</style>
