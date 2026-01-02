<template>
  <div class="responsive-tabs">
    <!-- Regular tabs - hidden on small screens -->
    <div class="visible-tabs">
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

    <!-- Hamburger menu - shown only on small screens -->
    <div class="hamburger-only">
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
import { ref, computed, onMounted, onUnmounted } from 'vue';
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
const menuOpen = ref(false);
const dropdownStyle = ref({});

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

onMounted(() => {
  document.addEventListener('click', handleClickOutside);
});

onUnmounted(() => {
  document.removeEventListener('click', handleClickOutside);
});
</script>

<style scoped>
.responsive-tabs {
  width: 100%;
}

/* By default, show tabs and hide hamburger */
.visible-tabs {
  display: flex;
  gap: 0.5rem;
  align-items: center;
}

.hamburger-only {
  display: none;
}

.tab-button {
  display: flex;
  align-items: center;
  gap: 0.35rem;
  padding: 1rem 1rem;
  border: none;
  background: transparent;
  color: #666;
  cursor: pointer;
  font-size: 0.95rem;
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

/* On smaller screens, hide tabs and show hamburger */
@media (max-width: 900px) {
  .visible-tabs {
    display: none;
  }

  .hamburger-only {
    display: flex;
    align-items: center;
  }
}
</style>
