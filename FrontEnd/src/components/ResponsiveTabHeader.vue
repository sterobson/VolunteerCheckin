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

// SVG icons for tabs
const icons = {
  // Course/Route - path with waypoints (looks like a route/track)
  course: `<svg xmlns="http://www.w3.org/2000/svg" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="6" cy="19" r="3"/><path d="M9 19h8.5a3.5 3.5 0 0 0 0-7h-11a3.5 3.5 0 0 1 0-7H15"/><circle cx="18" cy="5" r="3"/></svg>`,

  // Checkpoint - map pin
  checkpoint: `<svg xmlns="http://www.w3.org/2000/svg" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M20 10c0 6-8 12-8 12s-8-6-8-12a8 8 0 0 1 16 0Z"/><circle cx="12" cy="10" r="3"/></svg>`,

  // Area - polygon shape
  area: `<svg xmlns="http://www.w3.org/2000/svg" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><polygon points="3 6 9 3 15 6 21 3 21 18 15 21 9 18 3 21"/><line x1="9" y1="3" x2="9" y2="18"/><line x1="15" y1="6" x2="15" y2="21"/></svg>`,

  // Marshal/Person - simple person/user icon
  marshal: `<svg xmlns="http://www.w3.org/2000/svg" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M19 21v-2a4 4 0 0 0-4-4H9a4 4 0 0 0-4 4v2"/><circle cx="12" cy="7" r="4"/></svg>`,

  // Checklist - list with checkboxes
  checklist: `<svg xmlns="http://www.w3.org/2000/svg" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M9 11l3 3L22 4"/><path d="M21 12v7a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11"/></svg>`,

  // Notes - paper with lines
  notes: `<svg xmlns="http://www.w3.org/2000/svg" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M14.5 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V7.5L14.5 2z"/><polyline points="14 2 14 8 20 8"/><line x1="8" y1="13" x2="16" y2="13"/><line x1="8" y1="17" x2="16" y2="17"/></svg>`,

  // Event details - calendar/settings
  details: `<svg xmlns="http://www.w3.org/2000/svg" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect x="3" y="4" width="18" height="18" rx="2" ry="2"/><line x1="16" y1="2" x2="16" y2="6"/><line x1="8" y1="2" x2="8" y2="6"/><line x1="3" y1="10" x2="21" y2="10"/><path d="M9 16h6"/></svg>`,

  // Location/coordinates - crosshairs
  location: `<svg xmlns="http://www.w3.org/2000/svg" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="10"/><line x1="22" y1="12" x2="18" y2="12"/><line x1="6" y1="12" x2="2" y2="12"/><line x1="12" y1="6" x2="12" y2="2"/><line x1="12" y1="22" x2="12" y2="18"/></svg>`,
};

const getIcon = (iconName) => {
  return icons[iconName] || '';
};

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
