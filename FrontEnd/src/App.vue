<template>
  <TopNav v-if="showTopNav" />
  <router-view v-slot="{ Component, route }">
    <Transition :name="transitionName" mode="out-in">
      <component :is="Component" :key="route.path" />
    </Transition>
  </router-view>
  <LoadingOverlay />
</template>

<script setup>
import { ref, computed, watch, watchEffect } from 'vue';
import { useRouter } from 'vue-router';
import LoadingOverlay from './components/LoadingOverlay.vue';
import TopNav from './components/TopNav.vue';
import { useGlobalLoadingOverlay } from './services/loadingOverlay';

const router = useRouter();

// Add/remove body class for disabling inputs during long network operations
const { shouldDisableInputs } = useGlobalLoadingOverlay();
watchEffect(() => {
  if (shouldDisableInputs.value) {
    document.body.classList.add('has-network-activity');
  } else {
    document.body.classList.remove('has-network-activity');
  }
});
const transitionName = ref('');

// Define page order for slide direction (matches nav menu order)
const pageOrder = ['/', '/pricing', '/about', '/myevents'];

// Show TopNav only on marketing pages
const showTopNav = computed(() => pageOrder.includes(router.currentRoute.value.path));

watch(() => router.currentRoute.value, (to, from) => {
  if (!from?.path) {
    transitionName.value = '';
    return;
  }

  const toIndex = pageOrder.indexOf(to.path);
  const fromIndex = pageOrder.indexOf(from.path);

  // Apply slide transition between pages in the nav order
  if (toIndex !== -1 && fromIndex !== -1) {
    transitionName.value = toIndex > fromIndex ? 'slide-left' : 'slide-right';
  } else {
    transitionName.value = '';
  }
}, { immediate: true });
</script>

<style>
* {
  margin: 0;
  padding: 0;
  box-sizing: border-box;
}

html {
  overflow-x: hidden;
  overflow-y: auto;
  width: 100%;
}

body {
  font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Oxygen, Ubuntu, Cantarell, sans-serif;
  -webkit-font-smoothing: antialiased;
  -moz-osx-font-smoothing: grayscale;
  overflow-x: hidden;
  overflow-y: auto;
  -webkit-overflow-scrolling: touch;
  width: 100%;
  position: relative;
}

#app {
  min-height: 100vh;
  width: 100%;
  overflow-x: hidden;
  background: linear-gradient(180deg, #1a1a2e 0%, #16213e 50%, #0f3460 100%);
}

/* Slide transitions */
.slide-left-enter-active,
.slide-left-leave-active,
.slide-right-enter-active,
.slide-right-leave-active {
  transition: transform 0.3s ease-out, opacity 0.3s ease-out;
}

.slide-left-enter-from {
  transform: translateX(100%);
  opacity: 0;
}

.slide-left-leave-to {
  transform: translateX(-100%);
  opacity: 0;
}

.slide-right-enter-from {
  transform: translateX(-100%);
  opacity: 0;
}

.slide-right-leave-to {
  transform: translateX(100%);
  opacity: 0;
}

/* Disable inputs and save/delete buttons during long network operations */
body.has-network-activity .disable-on-load {
  pointer-events: none;
  opacity: 0.6;
  cursor: not-allowed;
}
</style>
