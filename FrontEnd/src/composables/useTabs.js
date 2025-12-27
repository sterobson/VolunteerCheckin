/**
 * Composable for tab navigation functionality
 * Provides a reusable way to manage tab state and switching
 */

import { ref, computed } from 'vue';

export function useTabs(defaultTab = 'details', tabNames = []) {
  const activeTab = ref(defaultTab);

  /**
   * Switch to a specific tab
   * @param {string} tabName - The name of the tab to switch to
   */
  const switchTab = (tabName) => {
    activeTab.value = tabName;
  };

  /**
   * Reset to default tab
   */
  const resetTab = () => {
    activeTab.value = defaultTab;
  };

  /**
   * Check if a specific tab is active
   * @param {string} tabName - The name of the tab to check
   * @returns {boolean} True if the tab is active
   */
  const isActiveTab = (tabName) => {
    return activeTab.value === tabName;
  };

  /**
   * Get the index of the current active tab
   * @returns {number} Index of active tab, or -1 if not found
   */
  const activeTabIndex = computed(() => {
    if (tabNames.length === 0) return -1;
    return tabNames.indexOf(activeTab.value);
  });

  /**
   * Navigate to the next tab
   * @param {boolean} wrap - If true, wrap to first tab after last (default: false)
   */
  const nextTab = (wrap = false) => {
    if (tabNames.length === 0) return;

    const currentIndex = activeTabIndex.value;
    if (currentIndex === -1) return;

    const nextIndex = currentIndex + 1;

    if (nextIndex < tabNames.length) {
      activeTab.value = tabNames[nextIndex];
    } else if (wrap) {
      activeTab.value = tabNames[0];
    }
  };

  /**
   * Navigate to the previous tab
   * @param {boolean} wrap - If true, wrap to last tab before first (default: false)
   */
  const previousTab = (wrap = false) => {
    if (tabNames.length === 0) return;

    const currentIndex = activeTabIndex.value;
    if (currentIndex === -1) return;

    const prevIndex = currentIndex - 1;

    if (prevIndex >= 0) {
      activeTab.value = tabNames[prevIndex];
    } else if (wrap) {
      activeTab.value = tabNames[tabNames.length - 1];
    }
  };

  /**
   * Check if there is a next tab
   * @returns {boolean} True if there is a next tab
   */
  const hasNextTab = computed(() => {
    if (tabNames.length === 0) return false;
    const currentIndex = activeTabIndex.value;
    return currentIndex !== -1 && currentIndex < tabNames.length - 1;
  });

  /**
   * Check if there is a previous tab
   * @returns {boolean} True if there is a previous tab
   */
  const hasPreviousTab = computed(() => {
    if (tabNames.length === 0) return false;
    const currentIndex = activeTabIndex.value;
    return currentIndex > 0;
  });

  return {
    // State
    activeTab,
    activeTabIndex,
    hasNextTab,
    hasPreviousTab,

    // Methods
    switchTab,
    resetTab,
    isActiveTab,
    nextTab,
    previousTab,
  };
}
