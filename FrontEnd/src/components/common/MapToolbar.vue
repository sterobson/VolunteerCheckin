<template>
  <div class="map-toolbar" :class="[`position-${position}`]">
    <!-- Fullscreen button -->
    <button
      v-if="showFullscreen"
      class="toolbar-btn"
      @click="$emit('fullscreen-click')"
      title="Fullscreen"
    >
      <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
        <path d="M8 3H5a2 2 0 0 0-2 2v3m18 0V5a2 2 0 0 0-2-2h-3m0 18h3a2 2 0 0 0 2-2v-3M3 16v3a2 2 0 0 0 2 2h3"/>
      </svg>
    </button>

    <!-- Filters button -->
    <div v-if="showFilters && !actionsExpanded" class="toolbar-btn-wrapper filters-wrapper">
      <!-- Mobile backdrop -->
      <div v-if="filtersExpanded" class="mobile-backdrop" @click="filtersExpanded = false"></div>
      <button
        class="toolbar-btn"
        :class="{ active: filtersExpanded }"
        @click="toggleFilters"
        title="Show/hide options"
      >
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
          <path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"/>
          <circle cx="12" cy="12" r="3"/>
        </svg>
      </button>

      <!-- Filters dropdown -->
      <div v-if="filtersExpanded" class="dropdown-content filters-dropdown">
        <div class="filters-header">
          <span class="filters-title">Map filters</span>
          <button class="close-filters-btn" @click="filtersExpanded = false" title="Close">
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <path d="M18 6L6 18M6 6l12 12"/>
            </svg>
          </button>
        </div>

        <div class="filter-section">
          <h4>Display</h4>
          <label class="filter-checkbox">
            <input type="checkbox" :checked="filters.showRoute" @change="updateFilter('showRoute', $event.target.checked)" />
            <span>{{ terms.course }} line</span>
          </label>
        </div>

        <div class="filter-section">
          <h4>{{ terms.checkpoints }}</h4>
          <label class="filter-checkbox">
            <input type="checkbox" :checked="filters.showUncheckedIn" @change="updateFilter('showUncheckedIn', $event.target.checked)" />
            <span class="status-indicator unchecked"></span>
            <span>Unchecked-in</span>
          </label>
          <label class="filter-checkbox">
            <input type="checkbox" :checked="filters.showPartiallyCheckedIn" @change="updateFilter('showPartiallyCheckedIn', $event.target.checked)" />
            <span class="status-indicator partial"></span>
            <span>Partially checked-in</span>
          </label>
          <label class="filter-checkbox">
            <input type="checkbox" :checked="filters.showFullyCheckedIn" @change="updateFilter('showFullyCheckedIn', $event.target.checked)" />
            <span class="status-indicator full"></span>
            <span>Fully checked-in</span>
          </label>
        </div>

        <div class="filter-section" v-if="areas.length > 0">
          <h4>{{ terms.areas }}</h4>
          <AreasSelection
            :areas="areas"
            :selected-area-ids="filters.selectedAreaIds"
            @update:selected-area-ids="updateFilter('selectedAreaIds', $event)"
          />
        </div>
      </div>
    </div>

    <!-- Action buttons -->
    <div
      v-for="action in actions"
      :key="action.id"
      class="toolbar-btn-wrapper"
      :class="{ hidden: filtersExpanded }"
    >
      <button
        class="toolbar-btn"
        :class="{ active: expandedActionId === action.id }"
        @click="handleActionClick(action)"
        :title="action.label || action.id"
      >
        <svg v-if="action.icon === 'plus'" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
          <line x1="12" y1="5" x2="12" y2="19"/>
          <line x1="5" y1="12" x2="19" y2="12"/>
        </svg>
        <svg v-else-if="action.icon === 'edit'" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
          <path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7"/>
          <path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z"/>
        </svg>
        <span v-else-if="action.icon === 'custom'" v-html="action.customIcon"></span>
      </button>

      <!-- Action dropdown (if items provided) -->
      <div v-if="action.items && expandedActionId === action.id" class="dropdown-content action-dropdown">
        <button
          v-for="item in action.items"
          :key="item.id"
          @click="handleMenuItemClick(action.id, item.id)"
          class="dropdown-item"
        >
          {{ item.label }}
        </button>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, defineProps, defineEmits, watch } from 'vue';
import AreasSelection from '../event-manage/AreasSelection.vue';
import { useTerminology } from '../../composables/useTerminology';

const { terms, termsLower } = useTerminology();

const props = defineProps({
  showFullscreen: {
    type: Boolean,
    default: false,
  },
  showFilters: {
    type: Boolean,
    default: false,
  },
  filters: {
    type: Object,
    default: () => ({
      showRoute: true,
      showUncheckedIn: true,
      showPartiallyCheckedIn: true,
      showFullyCheckedIn: true,
      showAreas: true,
      selectedAreaIds: [],
    }),
  },
  areas: {
    type: Array,
    default: () => [],
  },
  actions: {
    type: Array,
    default: () => [],
  },
  position: {
    type: String,
    default: 'top-right',
    validator: (v) => ['top-right', 'top-left'].includes(v),
  },
});

const emit = defineEmits([
  'fullscreen-click',
  'filter-change',
  'action-click',
]);

const filtersExpanded = ref(false);
const expandedActionId = ref(null);

const actionsExpanded = computed(() => expandedActionId.value !== null);

const toggleFilters = () => {
  filtersExpanded.value = !filtersExpanded.value;
  if (filtersExpanded.value) {
    expandedActionId.value = null;
  }
};

const updateFilter = (key, value) => {
  emit('filter-change', { ...props.filters, [key]: value });
};

const handleActionClick = (action) => {
  if (action.items && action.items.length > 0) {
    // Toggle dropdown
    if (expandedActionId.value === action.id) {
      expandedActionId.value = null;
    } else {
      expandedActionId.value = action.id;
      filtersExpanded.value = false;
    }
  } else {
    // Direct action
    emit('action-click', { actionId: action.id });
  }
};

const handleMenuItemClick = (actionId, itemId) => {
  expandedActionId.value = null;
  emit('action-click', { actionId, itemId });
};

// Close dropdowns when clicking outside
const closeDropdowns = () => {
  filtersExpanded.value = false;
  expandedActionId.value = null;
};

defineExpose({ closeDropdowns });
</script>

<style scoped>
.map-toolbar {
  position: absolute;
  z-index: 1000;
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.map-toolbar.position-top-right {
  top: 8px;
  right: 8px;
}

.map-toolbar.position-top-left {
  top: 8px;
  left: 8px;
}

.toolbar-btn-wrapper {
  position: relative;
}

.toolbar-btn-wrapper.hidden {
  display: none;
}

.toolbar-btn {
  background: var(--card-bg, white);
  border: none;
  border-radius: 4px;
  padding: 6px;
  cursor: pointer;
  color: var(--text-secondary, #666);
  box-shadow: 0 1px 4px rgba(0, 0, 0, 0.15);
  display: flex;
  align-items: center;
  justify-content: center;
  transition: all 0.15s ease;
  min-width: 32px;
  min-height: 32px;
}

.toolbar-btn:hover {
  background: var(--bg-hover, #f5f5f5);
  color: var(--text-primary, #333);
  box-shadow: 0 2px 6px rgba(0, 0, 0, 0.2);
}

.toolbar-btn:active {
  transform: scale(0.95);
}

.toolbar-btn.active {
  background: var(--text-primary, #333);
  color: white;
}

.toolbar-btn svg {
  display: block;
  width: 18px;
  height: 18px;
}

/* Dropdown content shared styles */
.dropdown-content {
  position: absolute;
  top: 0;
  right: calc(100% + 8px);
  background: var(--card-bg);
  border-radius: 8px;
  box-shadow: var(--shadow-lg);
  z-index: 1001;
  min-width: 200px;
}

.filters-dropdown {
  padding: 0;
  min-width: 240px;
}

.filters-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0.75rem 1rem;
  border-bottom: 1px solid var(--border-color);
  background: var(--bg-secondary);
  border-radius: 8px 8px 0 0;
}

.filters-title {
  font-weight: 600;
  font-size: 0.9rem;
  color: var(--text-primary);
}

.close-filters-btn {
  background: none;
  border: none;
  padding: 0.25rem;
  cursor: pointer;
  color: var(--text-secondary);
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: 4px;
  transition: all 0.15s;
}

.close-filters-btn:hover {
  background: var(--bg-hover);
  color: var(--text-primary);
}

.close-filters-btn svg {
  width: 16px;
  height: 16px;
}

.filter-section {
  padding: 0.75rem 1rem;
  border-bottom: 1px solid var(--border-color);
}

.filter-section:last-child {
  border-bottom: none;
}

.filter-section h4 {
  margin: 0 0 0.5rem 0;
  font-size: 0.75rem;
  color: var(--text-secondary);
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.5px;
}

.filter-checkbox {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.4rem 0;
  font-size: 0.875rem;
  cursor: pointer;
  color: var(--text-primary);
}

.filter-checkbox:hover {
  color: var(--accent-primary);
}

.filter-checkbox input[type="checkbox"] {
  cursor: pointer;
  width: 1rem;
  height: 1rem;
  flex-shrink: 0;
}

.status-indicator {
  width: 10px;
  height: 10px;
  border-radius: 50%;
  flex-shrink: 0;
}

.status-indicator.unchecked {
  background: var(--status-danger-bg, #fee2e2);
  border: 2px solid var(--accent-danger, #ef4444);
}

.status-indicator.partial {
  background: var(--status-warning-bg, #fef3c7);
  border: 2px solid var(--accent-warning, #f59e0b);
}

.status-indicator.full {
  background: var(--status-success-bg, #dcfce7);
  border: 2px solid var(--accent-success, #22c55e);
}

/* Action dropdown */
.action-dropdown {
  padding: 0.5rem 0;
}

.dropdown-item {
  display: block;
  width: 100%;
  padding: 0.75rem 1rem;
  background: none;
  border: none;
  text-align: left;
  font-size: 0.9rem;
  color: var(--text-dark);
  cursor: pointer;
  transition: background-color 0.2s;
}

.dropdown-item:hover {
  background: var(--bg-tertiary);
}

.dropdown-item:first-child {
  border-radius: 8px 8px 0 0;
}

.dropdown-item:last-child {
  border-radius: 0 0 8px 8px;
}

/* Mobile backdrop - hidden on desktop */
.mobile-backdrop {
  display: none;
}

@media (max-width: 768px) {
  .mobile-backdrop {
    display: block;
    position: fixed;
    top: 0;
    left: 0;
    right: 0;
    bottom: 0;
    background: rgba(0, 0, 0, 0.4);
    z-index: 1000;
  }

  .dropdown-content {
    position: fixed;
    top: auto;
    bottom: 0;
    left: 0;
    right: 0;
    max-height: 70vh;
    overflow-y: auto;
    border-radius: 16px 16px 0 0;
    box-shadow: 0 -4px 20px rgba(0, 0, 0, 0.15);
    z-index: 1001;
  }

  .filters-dropdown {
    min-width: auto;
  }

  .filters-header {
    border-radius: 16px 16px 0 0;
    padding: 1rem;
    position: sticky;
    top: 0;
    z-index: 1;
  }

  .filter-section {
    padding: 1rem;
  }

  .filter-checkbox {
    padding: 0.6rem 0;
  }
}
</style>
