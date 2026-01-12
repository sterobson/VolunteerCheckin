<template>
  <div class="hero-header">
    <HeroMapAnimation :animate="shouldAnimate" />
    <div v-if="title" class="hero-overlay" :class="{ 'no-animate': !shouldAnimate }">
      <h1>{{ title }}</h1>
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted } from 'vue';
import HeroMapAnimation from './HeroMapAnimation.vue';

defineProps({
  title: {
    type: String,
    default: '',
  },
});

const shouldAnimate = ref(false);

onMounted(() => {
  // Use window-level flag - survives SPA navigation, resets on F5/new tab
  if (!window.__heroAnimationPlayed) {
    // First time showing hero in this page load - animate
    shouldAnimate.value = true;
    window.__heroAnimationPlayed = true;
  } else {
    // Already played animation in this page load - skip
    shouldAnimate.value = false;
  }
});
</script>

<style scoped>
@import url('https://fonts.googleapis.com/css2?family=Pacifico&display=swap');

.hero-header {
  position: relative;
  width: 100%;
  max-width: 400px;
  margin: 0 auto 1rem;
}

.hero-overlay {
  position: absolute;
  top: 0%;
  left: 50%;
  transform: translate(-50%, 0) scale(0);
  text-align: center;
  z-index: 10;
  pointer-events: none;
  width: 90%;
  opacity: 0;
  animation: popIn 0.5s ease-out 1s forwards;
}

.hero-overlay.no-animate {
  transform: translate(-50%, 0) scale(1);
  opacity: 1;
  animation: none;
}

@keyframes popIn {
  0% {
    transform: translate(-50%, 0) scale(0);
    opacity: 0;
  }
  70% {
    transform: translate(-50%, 0) scale(1.1);
    opacity: 1;
  }
  100% {
    transform: translate(-50%, 0) scale(1);
    opacity: 1;
  }
}

.hero-overlay h1 {
  font-family: 'Pacifico', cursive;
  font-size: 2.5rem;
  font-weight: 400;
  margin: 0;
  color: white;
  text-shadow:
    0 4px 8px rgba(0, 0, 0, 0.8),
    0 2px 4px rgba(0, 0, 0, 0.8);
}
</style>
