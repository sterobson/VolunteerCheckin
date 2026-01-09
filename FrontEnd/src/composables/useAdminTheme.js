/**
 * Composable for admin theme management (light/dark/system)
 *
 * Usage:
 * const { theme, colorScheme, setTheme, cycleTheme } = useAdminTheme();
 *
 * Apply colorScheme to your container:
 * <div :style="{ colorScheme }">
 */

import { ref, computed } from 'vue';

const STORAGE_KEY = 'admin-theme-preference';

// Shared state across all instances
const theme = ref(loadTheme());

function loadTheme() {
  if (typeof window === 'undefined') return 'system';
  return localStorage.getItem(STORAGE_KEY) || 'system';
}

function saveTheme(value) {
  localStorage.setItem(STORAGE_KEY, value);
}

export function useAdminTheme() {
  /**
   * The CSS color-scheme value to apply
   * - 'light dark' for system preference
   * - 'light' for forced light
   * - 'dark' for forced dark
   */
  const colorScheme = computed(() => {
    if (theme.value === 'system') return 'light dark';
    return theme.value;
  });

  /**
   * Set the theme preference
   * @param {'light' | 'dark' | 'system'} value
   */
  const setTheme = (value) => {
    theme.value = value;
    saveTheme(value);
  };

  /**
   * Cycle through themes: system -> light -> dark -> system
   */
  const cycleTheme = () => {
    const order = ['system', 'light', 'dark'];
    const currentIndex = order.indexOf(theme.value);
    const nextIndex = (currentIndex + 1) % order.length;
    setTheme(order[nextIndex]);
  };

  /**
   * Check if currently displaying dark mode
   * (accounts for system preference)
   */
  const isDark = computed(() => {
    if (theme.value === 'dark') return true;
    if (theme.value === 'light') return false;
    // System preference
    if (typeof window !== 'undefined') {
      return window.matchMedia('(prefers-color-scheme: dark)').matches;
    }
    return false;
  });

  return {
    theme,
    colorScheme,
    isDark,
    setTheme,
    cycleTheme,
  };
}
