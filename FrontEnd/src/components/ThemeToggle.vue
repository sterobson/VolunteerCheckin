<template>
  <div class="theme-dropdown" ref="dropdownRef">
    <button
      class="theme-toggle-btn"
      @click="toggleDropdown"
      type="button"
      :aria-expanded="isOpen"
    >
      <span class="theme-icon" v-html="currentIcon"></span>
      <span class="theme-label">{{ currentLabel }}</span>
      <svg class="chevron" :class="{ open: isOpen }" width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
        <polyline points="6 9 12 15 18 9"></polyline>
      </svg>
    </button>
    <div v-if="isOpen" class="dropdown-menu">
      <button
        v-for="option in options"
        :key="option.value"
        class="dropdown-item"
        :class="{ active: theme === option.value }"
        @click="selectTheme(option.value)"
      >
        <span class="option-icon" v-html="option.icon"></span>
        <span>{{ option.label }}</span>
      </button>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted, onUnmounted } from 'vue';
import { useAdminTheme } from '../composables/useAdminTheme';

const { theme, setTheme } = useAdminTheme();

const isOpen = ref(false);
const dropdownRef = ref(null);

const icons = {
  light: `<svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
    <circle cx="12" cy="12" r="5"></circle>
    <line x1="12" y1="1" x2="12" y2="3"></line>
    <line x1="12" y1="21" x2="12" y2="23"></line>
    <line x1="4.22" y1="4.22" x2="5.64" y2="5.64"></line>
    <line x1="18.36" y1="18.36" x2="19.78" y2="19.78"></line>
    <line x1="1" y1="12" x2="3" y2="12"></line>
    <line x1="21" y1="12" x2="23" y2="12"></line>
    <line x1="4.22" y1="19.78" x2="5.64" y2="18.36"></line>
    <line x1="18.36" y1="5.64" x2="19.78" y2="4.22"></line>
  </svg>`,
  dark: `<svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
    <path d="M21 12.79A9 9 0 1 1 11.21 3 7 7 0 0 0 21 12.79z"></path>
  </svg>`,
  system: `<svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
    <rect x="2" y="3" width="20" height="14" rx="2" ry="2"></rect>
    <line x1="8" y1="21" x2="16" y2="21"></line>
    <line x1="12" y1="17" x2="12" y2="21"></line>
  </svg>`,
};

const options = [
  { value: 'system', label: 'System', icon: icons.system },
  { value: 'light', label: 'Light', icon: icons.light },
  { value: 'dark', label: 'Dark', icon: icons.dark },
];

const currentIcon = computed(() => icons[theme.value] || icons.system);
const currentLabel = computed(() => {
  const option = options.find(o => o.value === theme.value);
  return option ? option.label : 'System';
});

const toggleDropdown = () => {
  isOpen.value = !isOpen.value;
};

const selectTheme = (value) => {
  setTheme(value);
  isOpen.value = false;
};

const handleClickOutside = (event) => {
  if (dropdownRef.value && !dropdownRef.value.contains(event.target)) {
    isOpen.value = false;
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
.theme-dropdown {
  position: relative;
}

.theme-toggle-btn {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.5rem 0.75rem;
  border: 1px solid var(--border-color);
  border-radius: 6px;
  background: var(--bg-secondary);
  color: var(--text-secondary);
  cursor: pointer;
  transition: all 0.2s;
  font-size: 0.875rem;
}

.theme-toggle-btn:hover {
  background: var(--bg-hover);
  color: var(--text-primary);
}

.theme-icon,
.option-icon {
  display: flex;
  align-items: center;
  justify-content: center;
}

.theme-label {
  min-width: 48px;
}

.chevron {
  transition: transform 0.2s;
}

.chevron.open {
  transform: rotate(180deg);
}

.dropdown-menu {
  position: absolute;
  top: 100%;
  right: 0;
  margin-top: 4px;
  background: var(--bg-primary);
  border: 1px solid var(--border-color);
  border-radius: 6px;
  box-shadow: var(--shadow-md);
  z-index: 1000;
  min-width: 140px;
  overflow: hidden;
}

.dropdown-item {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  width: 100%;
  padding: 0.625rem 0.875rem;
  border: none;
  background: transparent;
  color: var(--text-primary);
  cursor: pointer;
  font-size: 0.875rem;
  text-align: left;
  transition: background-color 0.15s;
}

.dropdown-item:hover {
  background: var(--bg-hover);
}

.dropdown-item.active {
  background: var(--bg-active);
  color: var(--accent-primary);
}
</style>
