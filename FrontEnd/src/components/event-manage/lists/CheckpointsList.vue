<template>
  <div>
    <h3>{{ terms.checkpoints }} ({{ locations.length }})</h3>
    <div class="button-group">
      <div class="add-menu-wrapper" ref="menuWrapperRef">
        <button
          ref="addButtonRef"
          @click="toggleAddMenu"
          class="btn btn-small btn-primary"
        >
          Add...
        </button>
        <div v-if="showAddMenu" class="add-dropdown">
          <button @click="handleAddManually" class="dropdown-item">
            Add {{ termsLower.checkpoint }} manually
          </button>
          <button @click="handleImport" class="dropdown-item">
            Import from CSV...
          </button>
        </div>
      </div>
    </div>

    <div v-if="areas.length > 0" class="filter-group">
      <h4>Filter by {{ terms.area.toLowerCase() }}:</h4>
      <label class="filter-checkbox">
        <input
          type="checkbox"
          :checked="showAllAreas"
          @change="handleAllAreasToggle"
        />
        All {{ terms.areas.toLowerCase() }} ({{ locations.length }})
      </label>
      <AreasSelection
        v-if="!showAllAreas"
        :areas="areas"
        :selected-area-ids="selectedAreaIds"
        @update:selected-area-ids="selectedAreaIds = $event"
      />
    </div>

    <div class="locations-list">
      <div
        v-for="location in sortedLocations"
        :key="location.id"
        class="location-item"
        :class="{
          'location-full': location.checkedInCount >= location.requiredMarshals,
          'location-missing': location.checkedInCount === 0
        }"
        @click="$emit('select-location', location)"
      >
        <div class="location-info">
          <div class="location-header">
            <span class="checkpoint-icon" v-html="getCheckpointIconSvg(location)"></span>
            <div class="location-name-desc">
              <strong>{{ location.name }}</strong><span v-if="location.description" class="location-description"> - {{ location.description }}</span>
            </div>
            <span
              v-for="areaId in (location.areaIds || location.AreaIds || [])"
              :key="areaId"
              class="area-badge"
              :style="{ backgroundColor: getAreaColor(areaId) }"
            >
              {{ getAreaName(areaId) }}
            </span>
            <span class="location-status">
              {{ location.checkedInCount }}/{{ location.requiredMarshals }}
            </span>
          </div>
        </div>
        <div class="location-assignments">
          <span
            v-for="assignment in location.assignments"
            :key="assignment.id"
            class="assignment-badge"
            :class="{ 'checked-in': assignment.isCheckedIn }"
          >
            {{ assignment.marshalName }}
          </span>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { defineProps, defineEmits, computed, ref, onMounted, onUnmounted } from 'vue';
import { alphanumericCompare } from '../../../utils/sortUtils';
import AreasSelection from '../AreasSelection.vue';
import { useTerminology } from '../../../composables/useTerminology';
import { generateCheckpointSvg } from '../../../constants/checkpointIcons';

const { terms, termsLower } = useTerminology();

const props = defineProps({
  locations: {
    type: Array,
    required: true,
  },
  areas: {
    type: Array,
    default: () => [],
  },
});

const emit = defineEmits(['add-checkpoint-manually', 'import-checkpoints', 'select-location']);

const showAllAreas = ref(true);
const selectedAreaIds = ref([]);
const addButtonRef = ref(null);
const menuWrapperRef = ref(null);
const showAddMenu = ref(false);

const toggleAddMenu = () => {
  showAddMenu.value = !showAddMenu.value;
};

const handleAddManually = () => {
  showAddMenu.value = false;
  emit('add-checkpoint-manually');
};

const handleImport = () => {
  showAddMenu.value = false;
  emit('import-checkpoints');
};

// Close dropdown when clicking outside
const handleClickOutside = (event) => {
  if (menuWrapperRef.value && !menuWrapperRef.value.contains(event.target)) {
    showAddMenu.value = false;
  }
};

onMounted(() => {
  document.addEventListener('click', handleClickOutside);
});

onUnmounted(() => {
  document.removeEventListener('click', handleClickOutside);
});

const handleAllAreasToggle = () => {
  showAllAreas.value = !showAllAreas.value;
  if (!showAllAreas.value && selectedAreaIds.value.length === 0) {
    // Select all areas by default when unchecking "All areas"
    selectedAreaIds.value = props.areas.map(a => a.id);
  }
};

const sortedLocations = computed(() => {
  let filtered = props.locations;

  // Filter by area
  if (!showAllAreas.value && selectedAreaIds.value.length > 0) {
    filtered = filtered.filter((loc) => {
      const areaIds = loc.areaIds || loc.AreaIds || [];
      return areaIds.some(id => selectedAreaIds.value.includes(id));
    });
  }

  return [...filtered].sort((a, b) => alphanumericCompare(a.name, b.name));
});

const getAreaName = (areaId) => {
  const area = props.areas.find((a) => a.id === areaId);
  return area ? area.name : null;
};

const getAreaColor = (areaId) => {
  const area = props.areas.find((a) => a.id === areaId);
  return area ? area.color : '#999';
};

// Generate checkpoint icon SVG based on resolved style
const getCheckpointIconSvg = (location) => {
  const resolvedType = location.resolvedStyleType || location.ResolvedStyleType;
  const resolvedBgColor = location.resolvedStyleBackgroundColor || location.ResolvedStyleBackgroundColor || location.resolvedStyleColor || location.ResolvedStyleColor;
  const resolvedBorderColor = location.resolvedStyleBorderColor || location.ResolvedStyleBorderColor;
  const resolvedIconColor = location.resolvedStyleIconColor || location.ResolvedStyleIconColor;
  const resolvedShape = location.resolvedStyleBackgroundShape || location.ResolvedStyleBackgroundShape;

  // Check if there's any resolved styling - type, colors, or shape
  const hasResolvedStyle = (resolvedType && resolvedType !== 'default')
    || resolvedBgColor
    || resolvedBorderColor
    || resolvedIconColor
    || (resolvedShape && resolvedShape !== 'circle');

  if (hasResolvedStyle) {
    // Use resolved style from hierarchy
    return generateCheckpointSvg({
      type: resolvedType || 'circle',
      backgroundShape: resolvedShape || 'circle',
      backgroundColor: resolvedBgColor || '#667eea',
      borderColor: resolvedBorderColor || '#ffffff',
      iconColor: resolvedIconColor || '#ffffff',
      size: '75',
      outputSize: 24,
    });
  }

  // Default: use neutral colored circle (not status-based)
  return `<svg width="24" height="24" viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg">
    <circle cx="12" cy="12" r="10" fill="#667eea" stroke="#fff" stroke-width="2"/>
  </svg>`;
};
</script>

<style scoped>
h3 {
  margin: 0 0 1rem 0;
  color: var(--text-primary);
}

.button-group {
  display: flex;
  gap: 0.5rem;
  margin-bottom: 1rem;
}

.btn {
  padding: 0.5rem 1rem;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  font-size: 0.9rem;
  transition: background-color 0.2s;
}

.btn-small {
  padding: 0.4rem 0.8rem;
  font-size: 0.85rem;
}

.btn-primary {
  background: var(--accent-primary);
  color: white;
}

.btn-primary:hover {
  background: var(--accent-primary-hover);
}

.btn-secondary {
  background: var(--bg-tertiary);
  color: var(--text-primary);
}

.btn-secondary:hover {
  background: var(--bg-hover);
}

.add-menu-wrapper {
  position: relative;
}

.add-dropdown {
  position: absolute;
  top: 100%;
  left: 0;
  background: var(--bg-primary);
  border-radius: 6px;
  box-shadow: var(--shadow-lg);
  z-index: 100;
  min-width: 180px;
  margin-top: 0.25rem;
  overflow: hidden;
}

.dropdown-item {
  display: block;
  width: 100%;
  padding: 0.75rem 1rem;
  border: none;
  background: var(--bg-primary);
  text-align: left;
  cursor: pointer;
  font-size: 0.9rem;
  color: var(--text-primary);
  transition: background-color 0.15s;
}

.dropdown-item:hover {
  background: var(--bg-tertiary);
}

.dropdown-item:first-child {
  border-radius: 6px 6px 0 0;
}

.dropdown-item:last-child {
  border-radius: 0 0 6px 6px;
}

.locations-list {
  margin-top: 1rem;
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.location-item {
  padding: 1rem;
  border: 2px solid var(--border-color);
  border-radius: 6px;
  cursor: pointer;
  transition: all 0.3s;
  background: var(--card-bg);
}

.location-item:hover {
  border-color: var(--accent-primary);
}

.location-item.location-full {
  border-color: var(--accent-success);
  background: var(--status-success-bg);
}

.location-item.location-missing {
  border-color: var(--accent-danger);
  background: var(--status-danger-bg);
}

.location-info {
  margin-bottom: 0.5rem;
}

.location-name-desc {
  flex: 1;
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.location-description {
  font-weight: 400;
  color: var(--text-muted);
}

.location-status {
  font-weight: 600;
  color: var(--accent-primary);
  white-space: nowrap;
  margin-left: auto;
}

.location-assignments {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
}

.assignment-badge {
  padding: 0.25rem 0.75rem;
  background: var(--bg-tertiary);
  border-radius: 12px;
  font-size: 0.75rem;
  color: var(--text-secondary);
}

.assignment-badge.checked-in {
  background: var(--accent-success);
  color: white;
}

.filter-group {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
  margin: 1rem 0;
  padding: 1rem;
  background: var(--bg-tertiary);
  border-radius: 4px;
}

.filter-group h4 {
  margin: 0 0 0.5rem 0;
  font-size: 0.85rem;
  font-weight: 600;
  color: var(--text-primary);
  text-transform: uppercase;
  letter-spacing: 0.5px;
}

.filter-checkbox {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  font-size: 0.9rem;
  cursor: pointer;
  color: var(--text-primary);
}

.filter-checkbox input[type="checkbox"] {
  cursor: pointer;
  width: 1rem;
  height: 1rem;
}

.location-header {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  flex-wrap: wrap;
}

.checkpoint-icon {
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
  width: 24px;
  height: 24px;
}

.checkpoint-icon :deep(svg) {
  width: 24px;
  height: 24px;
}

.area-badge {
  padding: 0.2rem 0.6rem;
  border-radius: 12px;
  font-size: 0.7rem;
  font-weight: 600;
  color: white;
  text-shadow: 0 1px 2px rgba(0, 0, 0, 0.2);
}
</style>
