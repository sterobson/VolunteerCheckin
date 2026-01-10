<template>
  <div class="tabs-container" ref="containerRef">
    <!-- Regular tabs when there's enough space -->
    <div v-if="!showHamburger" class="tabs" ref="tabsRef">
      <button
        v-for="tab in tabs"
        :key="tab.value"
        class="tab-button"
        :class="{ active: modelValue === tab.value }"
        @click="$emit('update:modelValue', tab.value)"
        type="button"
      >
        <span v-if="tab.icon" class="tab-icon" v-html="getIcon(tab.icon)"></span>
        {{ tab.label }}
      </button>
    </div>

    <!-- Hamburger menu when space is limited -->
    <div v-else class="hamburger-container" ref="hamburgerRef">
      <div class="hamburger-nav">
        <button
          class="nav-arrow"
          :class="{ invisible: !hasPrevTab }"
          @click="goToPrevTab"
          type="button"
          aria-label="Previous tab"
          :disabled="!hasPrevTab"
        >
          ‹
        </button>
        <button
          class="hamburger-button"
          @click="toggleMenu"
          type="button"
          aria-label="Tab menu"
        >
          <span v-if="currentTabIcon" class="current-tab-icon" v-html="getIcon(currentTabIcon)"></span>
          <span class="current-tab-label">{{ currentTabLabel }}</span>
          <span class="hamburger-chevron">▼</span>
        </button>
        <button
          class="nav-arrow"
          :class="{ invisible: !hasNextTab }"
          @click="goToNextTab"
          type="button"
          aria-label="Next tab"
          :disabled="!hasNextTab"
        >
          ›
        </button>
      </div>

      <div v-if="menuOpen" class="hamburger-menu">
        <button
          v-for="tab in tabs"
          :key="tab.value"
          class="menu-item"
          :class="{ active: modelValue === tab.value }"
          @click="selectTab(tab.value)"
          type="button"
        >
          <span v-if="tab.icon" class="tab-icon" v-html="getIcon(tab.icon)"></span>
          {{ tab.label }}
        </button>
      </div>
    </div>
  </div>
</template>

<script setup>
import { defineProps, defineEmits, ref, computed, onMounted, onUnmounted, nextTick, watch } from 'vue';
import { getIcon } from '../utils/icons';

const props = defineProps({
  tabs: {
    type: Array,
    required: true,
    // Array of { value: string, label: string, icon?: string }
  },
  modelValue: {
    type: String,
    required: true,
  },
});

const emit = defineEmits(['update:modelValue']);

const containerRef = ref(null);
const tabsRef = ref(null);
const hamburgerRef = ref(null);
const showHamburger = ref(false);
const menuOpen = ref(false);

// Threshold in pixels - if tabs would need more than this, use hamburger
const HAMBURGER_THRESHOLD = 400;

const currentTabLabel = computed(() => {
  const currentTab = props.tabs.find(t => t.value === props.modelValue);
  return currentTab ? currentTab.label : '';
});

const currentTabIcon = computed(() => {
  const currentTab = props.tabs.find(t => t.value === props.modelValue);
  return currentTab?.icon || null;
});

const currentTabIndex = computed(() => {
  return props.tabs.findIndex(t => t.value === props.modelValue);
});

const hasPrevTab = computed(() => {
  return currentTabIndex.value > 0;
});

const hasNextTab = computed(() => {
  return currentTabIndex.value < props.tabs.length - 1;
});

const goToPrevTab = () => {
  if (hasPrevTab.value) {
    emit('update:modelValue', props.tabs[currentTabIndex.value - 1].value);
  }
};

const goToNextTab = () => {
  if (hasNextTab.value) {
    emit('update:modelValue', props.tabs[currentTabIndex.value + 1].value);
  }
};

const checkOverflow = () => {
  if (!containerRef.value) return;

  const containerWidth = containerRef.value.clientWidth;

  // Small screen threshold - always use hamburger
  if (containerWidth < HAMBURGER_THRESHOLD) {
    showHamburger.value = true;
    return;
  }

  // If tabs are currently visible, check their actual width
  if (tabsRef.value && !showHamburger.value) {
    const tabsWidth = tabsRef.value.scrollWidth;
    if (tabsWidth > containerWidth) {
      showHamburger.value = true;
    }
    return;
  }

  // If currently showing hamburger, check if tabs would fit now
  // Use a more accurate estimate: ~90px per tab without icon, ~110px with icon
  const avgTabWidth = props.tabs.some(t => t.icon) ? 110 : 90;
  const estimatedTabWidth = props.tabs.length * avgTabWidth;
  const gapWidth = (props.tabs.length - 1) * 8; // 0.5rem gap

  if (estimatedTabWidth + gapWidth <= containerWidth) {
    // Tabs might fit - switch to tabs and re-check
    showHamburger.value = false;
    nextTick(() => {
      if (tabsRef.value && tabsRef.value.scrollWidth > containerWidth) {
        showHamburger.value = true;
      }
    });
  }
};

const toggleMenu = () => {
  menuOpen.value = !menuOpen.value;
};

const selectTab = (tabValue) => {
  emit('update:modelValue', tabValue);
  menuOpen.value = false;
};

// Close menu when clicking outside
const handleClickOutside = (event) => {
  if (hamburgerRef.value && !hamburgerRef.value.contains(event.target)) {
    menuOpen.value = false;
  }
};

let resizeObserver = null;

onMounted(() => {
  nextTick(() => {
    checkOverflow();

    // Watch for size changes
    if (containerRef.value && typeof ResizeObserver !== 'undefined') {
      resizeObserver = new ResizeObserver(() => {
        checkOverflow();
      });
      resizeObserver.observe(containerRef.value);
    }

    document.addEventListener('click', handleClickOutside);
  });
});

onUnmounted(() => {
  if (resizeObserver) {
    resizeObserver.disconnect();
  }
  document.removeEventListener('click', handleClickOutside);
});

// Re-check overflow when tabs change
watch(() => props.tabs, () => {
  nextTick(() => {
    checkOverflow();
  });
}, { deep: true });
</script>

<style scoped>
.tabs-container {
  display: flex;
  align-items: center;
  position: relative;
  min-width: 0;
}

.tabs {
  display: flex;
  gap: 0.5rem;
  padding: 0.5rem 0;
  flex-wrap: nowrap;
  position: relative;
}

.tab-button {
  display: flex;
  align-items: center;
  gap: 0.4rem;
  padding: 0.5rem 1rem;
  border: none;
  background: transparent;
  color: var(--text-secondary);
  cursor: pointer;
  font-size: 0.9rem;
  transition: color 0.2s, border-color 0.2s;
  flex-shrink: 0;
  white-space: nowrap;
  border-bottom: 2px solid transparent;
  margin-bottom: -1px;
}

.tab-button:hover {
  color: var(--text-primary);
}

.tab-button.active {
  color: var(--accent-primary);
  font-weight: 500;
  border-bottom-color: var(--accent-primary);
}

.tab-icon {
  display: flex;
  align-items: center;
  justify-content: center;
}

.tab-icon :deep(svg) {
  width: 16px;
  height: 16px;
}

/* Hamburger menu styles */
.hamburger-container {
  position: relative;
  width: 100%;
}

.hamburger-nav {
  display: flex;
  align-items: center;
  gap: 0;
  margin: 0 -2rem;
}

.nav-arrow {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 44px;
  height: 44px;
  border: none;
  background: var(--bg-tertiary);
  cursor: pointer;
  font-size: 1.5rem;
  color: var(--text-primary);
  transition: all 0.2s;
  flex-shrink: 0;
}

.nav-arrow:hover:not(:disabled) {
  background: var(--bg-hover);
  color: var(--accent-primary);
}

.nav-arrow.invisible {
  opacity: 0;
  pointer-events: none;
}

.hamburger-button {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  flex: 1;
  min-width: 0;
  padding: 0.75rem 1rem;
  border: none;
  background: transparent;
  cursor: pointer;
  font-size: 0.95rem;
  color: var(--text-primary);
  transition: all 0.2s;
}

.hamburger-button:hover {
  background: var(--bg-tertiary);
}

.current-tab-icon {
  display: flex;
  align-items: center;
  justify-content: center;
  color: var(--accent-primary);
}

.current-tab-icon :deep(svg) {
  width: 18px;
  height: 18px;
}

.current-tab-label {
  font-weight: 500;
  flex: 1;
  text-align: left;
}

.hamburger-chevron {
  font-size: 0.7rem;
  color: var(--text-secondary);
  margin-left: auto;
}

.hamburger-menu {
  position: absolute;
  top: 100%;
  left: 0;
  right: 0;
  background: var(--card-bg);
  border: 1px solid var(--border-color);
  border-radius: 6px;
  box-shadow: var(--shadow-md);
  z-index: 2000;
  margin-top: 0;
  overflow: hidden;
  max-height: 300px;
  overflow-y: auto;
}

.menu-item {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  width: 100%;
  padding: 0.75rem 1rem;
  border: none;
  background: var(--card-bg);
  text-align: left;
  cursor: pointer;
  font-size: 0.9rem;
  color: var(--text-primary);
  transition: background-color 0.15s;
}

.menu-item:hover {
  background: var(--bg-tertiary);
}

.menu-item.active {
  background: var(--bg-secondary);
  color: var(--accent-primary);
  font-weight: 500;
}

.menu-item .tab-icon {
  flex-shrink: 0;
}
</style>
