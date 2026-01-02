<template>
  <div class="tabs-container" ref="containerRef">
    <!-- Regular tabs when there's enough space -->
    <div v-if="!showHamburger" class="tabs">
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

const checkOverflow = () => {
  if (!containerRef.value) return;

  // Get the container width
  const containerWidth = containerRef.value.clientWidth;

  // Estimate the tabs width (rough calculation based on tab count and average width)
  const estimatedTabWidth = props.tabs.length * 100; // ~100px per tab on average

  // If tabs would overflow, use hamburger menu
  showHamburger.value = estimatedTabWidth > containerWidth || containerWidth < HAMBURGER_THRESHOLD;
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
  flex-wrap: wrap;
}

.tab-button {
  display: flex;
  align-items: center;
  gap: 0.4rem;
  padding: 0.5rem 1rem;
  border: none;
  background: transparent;
  color: #666;
  cursor: pointer;
  font-size: 0.9rem;
  border-bottom: 2px solid transparent;
  transition: all 0.2s;
  flex-shrink: 0;
  white-space: nowrap;
}

.tab-button:hover {
  color: #333;
}

.tab-button.active {
  color: #007bff;
  border-bottom-color: #007bff;
  font-weight: 500;
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

.hamburger-button {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  width: 100%;
  padding: 0.75rem 1rem;
  border: 1px solid #ddd;
  background: white;
  border-radius: 6px;
  cursor: pointer;
  font-size: 0.95rem;
  color: #333;
  transition: all 0.2s;
}

.hamburger-button:hover {
  background: #f8f9fa;
  border-color: #007bff;
}

.current-tab-icon {
  display: flex;
  align-items: center;
  justify-content: center;
  color: #667eea;
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
  color: #666;
  margin-left: auto;
}

.hamburger-menu {
  position: absolute;
  top: 100%;
  left: 0;
  right: 0;
  background: white;
  border: 1px solid #ddd;
  border-radius: 6px;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15);
  z-index: 100;
  margin-top: 0.25rem;
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
  background: white;
  text-align: left;
  cursor: pointer;
  font-size: 0.9rem;
  color: #333;
  transition: background-color 0.15s;
}

.menu-item:hover {
  background: #f5f5f5;
}

.menu-item.active {
  background: #e3f2fd;
  color: #007bff;
  font-weight: 500;
}

.menu-item .tab-icon {
  flex-shrink: 0;
}
</style>
