<template>
  <div class="responsive-tabs">
    <!-- Regular tabs - hidden when overflowing -->
    <div ref="visibleTabsRef" class="visible-tabs" :class="{ 'is-hidden': useHamburger }">
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

    <!-- Hamburger menu - shown when tabs overflow -->
    <div class="hamburger-only" :class="{ 'is-visible': useHamburger }">
      <div class="hamburger-menu" ref="hamburgerMenu">
        <button
          ref="hamburgerButton"
          class="hamburger-button"
          :class="{ active: menuOpen }"
          @click.stop="toggleMenu"
          type="button"
        >
          <span class="hamburger-icon">☰</span>
          <span v-if="activeTab?.icon" class="tab-icon" v-html="getIcon(activeTab.icon)"></span>
          <span class="hamburger-label">{{ activeTabLabel }}</span>
        </button>

        <div v-if="menuOpen" class="hamburger-dropdown" :style="dropdownStyle" @click.stop>
          <button
            v-for="tab in tabs"
            :key="tab.value"
            class="dropdown-item"
            :class="{ active: modelValue === tab.value }"
            @click.stop="selectTab(tab.value)"
            type="button"
          >
            <span v-if="tab.icon" class="tab-icon" v-html="getIcon(tab.icon)"></span>
            {{ tab.label }}
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted, onUnmounted, nextTick, watch } from 'vue';
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

const hamburgerMenu = ref(null);
const hamburgerButton = ref(null);
const visibleTabsRef = ref(null);
const menuOpen = ref(false);
const dropdownStyle = ref({});
const useHamburger = ref(false);

const activeTab = computed(() => {
  return props.tabs.find(t => t.value === props.modelValue);
});

const activeTabLabel = computed(() => {
  return activeTab.value ? activeTab.value.label : 'Menu';
});

const toggleMenu = () => {
  menuOpen.value = !menuOpen.value;

  // Calculate dropdown position when opening
  if (menuOpen.value && hamburgerButton.value) {
    const rect = hamburgerButton.value.getBoundingClientRect();
    dropdownStyle.value = {
      top: `${rect.bottom + 4}px`,
    };
  }
};

const selectTab = (tabValue) => {
  emit('update:modelValue', tabValue);
  menuOpen.value = false;
};

// Close menu when clicking outside
const handleClickOutside = (event) => {
  if (!hamburgerMenu.value) return;

  const clickedInside = hamburgerMenu.value.contains(event.target);

  if (menuOpen.value && !clickedInside) {
    menuOpen.value = false;
  }
};

// Check if tabs are overflowing and switch to hamburger if needed
const checkOverflow = () => {
  if (!visibleTabsRef.value) return;

  const container = visibleTabsRef.value;

  // Temporarily show tabs to measure them accurately
  const wasHidden = useHamburger.value;
  if (wasHidden) {
    container.style.visibility = 'visible';
    container.style.position = 'static';
  }

  // Check if content overflows the container
  const isOverflowing = container.scrollWidth > container.clientWidth;

  // Restore hidden state if it was hidden
  if (wasHidden) {
    container.style.visibility = '';
    container.style.position = '';
  }

  useHamburger.value = isOverflowing;
};

let resizeObserver = null;

onMounted(() => {
  document.addEventListener('click', handleClickOutside);

  // Check overflow on mount
  nextTick(() => {
    checkOverflow();
  });

  // Use ResizeObserver on the parent container to detect size changes
  nextTick(() => {
    const parent = visibleTabsRef.value?.parentElement;
    if (parent) {
      resizeObserver = new ResizeObserver(() => {
        checkOverflow();
      });
      resizeObserver.observe(parent);
    }
  });
});

onUnmounted(() => {
  document.removeEventListener('click', handleClickOutside);
  if (resizeObserver) {
    resizeObserver.disconnect();
  }
});

// Re-check overflow when tabs change (e.g., terminology changes)
watch(() => props.tabs, () => {
  nextTick(() => {
    checkOverflow();
  });
}, { deep: true });
</script>

<style scoped>
.responsive-tabs {
  width: 100%;
}

/* By default, show tabs and hide hamburger */
.visible-tabs {
  display: flex;
  gap: 0.25rem;
  align-items: center;
  overflow: hidden;
  flex: 1;
  min-width: 0;
}

.visible-tabs.is-hidden {
  visibility: hidden;
  position: absolute;
  pointer-events: none;
}

.hamburger-only {
  display: none;
}

.hamburger-only.is-visible {
  display: flex;
  align-items: center;
}

.tab-button {
  display: flex;
  align-items: center;
  gap: 0.25rem;
  padding: 0.75rem 0.6rem;
  border: none;
  background: transparent;
  color: #666;
  cursor: pointer;
  font-size: 0.9rem;
  font-weight: 500;
  border-bottom: 3px solid transparent;
  transition: all 0.2s;
  position: relative;
  bottom: -2px;
  white-space: nowrap;
}

.tab-icon {
  display: flex;
  align-items: center;
  justify-content: center;
}

.tab-icon :deep(svg) {
  width: 18px;
  height: 18px;
}

.tab-button:hover {
  color: #333;
  background: #f8f9fa;
}

.tab-button.active {
  color: #007bff;
  border-bottom-color: #007bff;
}

.hamburger-menu {
  position: relative;
}

.hamburger-button {
  padding: 1rem 1.5rem;
  border: none;
  background: transparent;
  color: #666;
  cursor: pointer;
  font-size: 1rem;
  font-weight: 500;
  border-bottom: 3px solid transparent;
  transition: all 0.2s;
  position: relative;
  bottom: -2px;
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.hamburger-button:hover {
  color: #333;
  background: #f8f9fa;
}

.hamburger-button.active {
  color: #007bff;
  background: #e7f3ff;
}

.hamburger-icon {
  font-size: 1.2rem;
}

.hamburger-label {
  font-size: 1rem;
}

.hamburger-dropdown {
  position: fixed;
  left: 1rem;
  right: 1rem;
  background: white;
  border: 1px solid #dee2e6;
  border-radius: 8px;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15);
  z-index: 10000;
  max-height: calc(100vh - 150px);
  overflow-y: auto;
}

.dropdown-item {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  width: 100%;
  padding: 1rem 1.25rem;
  border: none;
  background: white;
  color: #333;
  cursor: pointer;
  font-size: 1rem;
  text-align: left;
  transition: background-color 0.2s;
}

.dropdown-item:hover {
  background: #f8f9fa;
}

.dropdown-item.active {
  color: #007bff;
  background: #e7f3ff;
  font-weight: 500;
}

.dropdown-item:first-child {
  border-radius: 8px 8px 0 0;
}

.dropdown-item:last-child {
  border-radius: 0 0 8px 8px;
}

</style>
