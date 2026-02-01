<template>
  <div class="checkpoints-tab">
    <!-- Row 1: Action buttons + status pills -->
    <div class="tab-header">
      <div class="button-group">
        <button @click="$emit('add-checkpoint-manually')" class="btn btn-primary">
          Add {{ termsLower.checkpoint }}
        </button>
        <button @click="$emit('import-checkpoints')" class="btn btn-secondary">
          Import CSV
        </button>
      </div>
      <div class="status-pills" v-if="locations.length > 0">
        <!-- Total pill -->
        <div class="pill-group">
          <StatusPill
            variant="neutral"
            :active="isTotalActive"
            @click="clearAllFilters"
          >
            Total: {{ totalForStatusGroup }}
          </StatusPill>
        </div>

        <!-- Staffing status pills -->
        <div class="pill-group" v-if="showStaffingPills">
          <StatusPill
            v-if="fullCount > 0"
            variant="success"
            :active="activeFilter === 'full'"
            @click="setFilter('full')"
          >
            Fully staffed: {{ fullCount }}
          </StatusPill>
          <StatusPill
            v-if="partialCount > 0"
            variant="warning"
            :active="activeFilter === 'partial'"
            @click="setFilter('partial')"
          >
            Partially staffed: {{ partialCount }}
          </StatusPill>
          <StatusPill
            v-if="missingCount > 0"
            variant="danger"
            :active="activeFilter === 'missing'"
            @click="setFilter('missing')"
          >
            Unstaffed: {{ missingCount }}
          </StatusPill>
        </div>

        <!-- Check-in status pills -->
        <div class="pill-group" v-if="showCheckinPills">
          <StatusPill
            v-if="fullyCheckedInCount > 0"
            variant="success"
            :active="activeFilter === 'checkin-full'"
            @click="setFilter('checkin-full')"
          >
            Fully checked in: {{ fullyCheckedInCount }}
          </StatusPill>
          <StatusPill
            v-if="partiallyCheckedInCount > 0"
            variant="warning"
            :active="activeFilter === 'checkin-partial'"
            @click="setFilter('checkin-partial')"
          >
            Partially checked in: {{ partiallyCheckedInCount }}
          </StatusPill>
          <StatusPill
            v-if="notCheckedInCount > 0"
            variant="danger"
            :active="activeFilter === 'checkin-none'"
            @click="setFilter('checkin-none')"
          >
            Not checked in: {{ notCheckedInCount }}
          </StatusPill>
        </div>

        <!-- Area filter pills -->
        <div class="pill-group" v-if="showAreaFilters">
          <template v-for="area in sortedFilterAreas" :key="area.id">
            <StatusPill
              v-if="getAreaCheckpointCountFiltered(area.id) > 0"
              variant="neutral"
              :active="activeAreaFilter === area.id"
              class="area-pill"
              :style="{ backgroundColor: lightenColor(area.color || '#999'), borderColor: 'transparent', color: getLayerTextColor(area.color || '#999') }"
              @click="setAreaFilter(area.id)"
            >
              {{ area.name }}: {{ getAreaCheckpointCountFiltered(area.id) }}
            </StatusPill>
          </template>
        </div>

        <!-- Layer filter pills -->
        <div class="pill-group" v-if="showLayerFilters">
          <template v-for="layer in sortedLayers" :key="layer.id">
            <StatusPill
              v-if="getLayerCheckpointCountFiltered(layer.id) > 0"
              variant="neutral"
              :active="activeLayerFilter === layer.id"
              class="layer-pill"
              :style="{ backgroundColor: lightenColor(layer.routeColor || '#3388ff'), borderColor: 'transparent', color: getLayerTextColor(layer.routeColor || '#3388ff') }"
              @click="setLayerFilter(layer.id)"
            >
              {{ layer.name }}: {{ getLayerCheckpointCountFiltered(layer.id) }}
            </StatusPill>
          </template>
        </div>
      </div>
    </div>

    <!-- Row 2: Filters -->
    <div class="filters-section">
      <div class="search-group">
        <input
          v-model="searchQuery"
          type="text"
          class="search-input"
          :placeholder="`Search by name, description, ${termsLower.area}, or ${termsLower.person}...`"
        />
      </div>
    </div>

    <!-- Row 3: Content - Always grouped by area -->
    <template v-if="groupedByArea.length > 0">
      <div
        v-for="group in groupedByArea"
        :key="group.areaId"
        class="area-group"
      >
        <h3 class="area-group-header">{{ group.areaName }}</h3>
        <CheckpointsGrid
          :checkpoints="group.checkpoints"
          :areas="areas"
          :hide-area-pills="true"
          @select="$emit('select-location', $event)"
        />
      </div>
    </template>
    <div v-else class="empty-state">
      <p class="empty-message">{{ emptyMessage }}</p>
    </div>
  </div>
</template>

<script setup>
import { defineProps, defineEmits, computed, ref } from 'vue';
import { alphanumericCompare } from '../../../utils/sortUtils';
import { useTerminology } from '../../../composables/useTerminology';
import StatusPill from '../../StatusPill.vue';
import CheckpointsGrid from '../CheckpointsGrid.vue';

const { termsLower } = useTerminology();

const props = defineProps({
  locations: {
    type: Array,
    required: true,
  },
  areas: {
    type: Array,
    default: () => [],
  },
  layers: {
    type: Array,
    default: () => [],
  },
});

defineEmits(['add-checkpoint-manually', 'import-checkpoints', 'select-location']);

const searchQuery = ref('');
const activeFilter = ref('all');
const activeLayerFilter = ref(null);
const activeAreaFilter = ref(null);

const setFilter = (filter) => {
  activeFilter.value = filter;
};

const setLayerFilter = (layerId) => {
  // Toggle off if clicking same filter
  activeLayerFilter.value = activeLayerFilter.value === layerId ? null : layerId;
};

const setAreaFilter = (areaId) => {
  // Toggle off if clicking same filter
  activeAreaFilter.value = activeAreaFilter.value === areaId ? null : areaId;
};

// Clear all filters (called when clicking Total)
const clearAllFilters = () => {
  activeFilter.value = 'all';
  activeAreaFilter.value = null;
  activeLayerFilter.value = null;
};

// Total is active only when no other filters are selected
const isTotalActive = computed(() => {
  return activeFilter.value === 'all' && !activeAreaFilter.value && !activeLayerFilter.value;
});

// Get areaIds for a location (handles different casing)
const getLocationAreaIds = (location) => {
  return location.areaIds || location.AreaIds || [];
};

// Check if a location belongs to a specific area
const locationBelongsToArea = (location, areaId) => {
  const areaIds = getLocationAreaIds(location);
  return areaIds.includes(areaId);
};

// Areas that have checkpoints, sorted alphanumerically
const sortedFilterAreas = computed(() => {
  // Get areas that have at least one checkpoint
  const areasWithCheckpoints = props.areas.filter(area =>
    props.locations.some(loc => locationBelongsToArea(loc, area.id))
  );
  return [...areasWithCheckpoints].sort((a, b) => alphanumericCompare(a.name, b.name));
});

// Show area filters only if 2+ areas have non-zero filtered counts
const showAreaFilters = computed(() => {
  const areasWithCount = sortedFilterAreas.value.filter(area =>
    getAreaCheckpointCountFiltered(area.id) > 0
  );
  return areasWithCount.length >= 2;
});

// Get layerIds for a location (handles different casing)
const getLocationLayerIds = (location) => {
  return location.layerIds || location.LayerIds || null;
};

// Check if a location belongs to a specific layer
const locationBelongsToLayer = (location, layerId) => {
  const layerIds = getLocationLayerIds(location);
  // If layerIds is null, checkpoint belongs to all layers
  if (!layerIds) return true;
  return layerIds.includes(layerId);
};

// Sorted layers for display
const sortedLayers = computed(() => {
  return [...props.layers].sort((a, b) => (a.displayOrder || 0) - (b.displayOrder || 0));
});

// Lighten a hex color by mixing with white
const lightenColor = (hexColor, amount = 0.7) => {
  const hex = hexColor.replace('#', '');
  const r = parseInt(hex.substring(0, 2), 16);
  const g = parseInt(hex.substring(2, 4), 16);
  const b = parseInt(hex.substring(4, 6), 16);

  // Mix with white (255, 255, 255)
  const newR = Math.round(r + (255 - r) * amount);
  const newG = Math.round(g + (255 - g) * amount);
  const newB = Math.round(b + (255 - b) * amount);

  return `#${newR.toString(16).padStart(2, '0')}${newG.toString(16).padStart(2, '0')}${newB.toString(16).padStart(2, '0')}`;
};

// Get text color based on the original (intense) route color
const getLayerTextColor = (hexColor) => {
  const hex = hexColor.replace('#', '');
  const r = parseInt(hex.substring(0, 2), 16);
  const g = parseInt(hex.substring(2, 4), 16);
  const b = parseInt(hex.substring(4, 6), 16);

  // Darken the color for text (mix with black)
  const darkenAmount = 0.4;
  const newR = Math.round(r * (1 - darkenAmount));
  const newG = Math.round(g * (1 - darkenAmount));
  const newB = Math.round(b * (1 - darkenAmount));

  return `#${newR.toString(16).padStart(2, '0')}${newG.toString(16).padStart(2, '0')}${newB.toString(16).padStart(2, '0')}`;
};

// Determine if layer filters should be shown
// Show only if 2+ layers have non-zero filtered counts
const showLayerFilters = computed(() => {
  if (props.layers.length < 2) return false;

  const layersWithCount = sortedLayers.value.filter(layer =>
    getLayerCheckpointCountFiltered(layer.id) > 0
  );
  return layersWithCount.length >= 2;
});

// Staffing status helpers
const getAssignedCount = (loc) => (loc.assignments || []).length;
const getCheckedInCountForLoc = (loc) => (loc.assignments || []).filter(a => a.isCheckedIn).length;

// Staffing filter predicates
const isFullyStaffed = (loc) => getAssignedCount(loc) >= loc.requiredMarshals && loc.requiredMarshals > 0;
const isPartiallyStaffed = (loc) => getAssignedCount(loc) > 0 && getAssignedCount(loc) < loc.requiredMarshals;
const isUnstaffed = (loc) => getAssignedCount(loc) === 0 && loc.requiredMarshals > 0;

// Check-in filter predicates
const isFullyCheckedIn = (loc) => {
  const assigned = getAssignedCount(loc);
  const checkedIn = getCheckedInCountForLoc(loc);
  return assigned > 0 && checkedIn >= assigned;
};
const isPartiallyCheckedIn = (loc) => {
  const assigned = getAssignedCount(loc);
  const checkedIn = getCheckedInCountForLoc(loc);
  return assigned > 0 && checkedIn > 0 && checkedIn < assigned;
};
const isNotCheckedIn = (loc) => {
  const assigned = getAssignedCount(loc);
  const checkedIn = getCheckedInCountForLoc(loc);
  return assigned > 0 && checkedIn === 0;
};

// Helper to get area name for search
const getAreaName = (areaId) => {
  const area = props.areas.find((a) => a.id === areaId);
  return area ? area.name : null;
};

// Apply search filter
const applySearchFilter = (locations) => {
  if (!searchQuery.value.trim()) return locations;
  const searchTerms = searchQuery.value.toLowerCase().trim().split(/\s+/);
  return locations.filter(location => {
    const areaIds = location.areaIds || location.AreaIds || [];
    const areaNames = areaIds.map(id => getAreaName(id) || '').join(' ');
    const marshalNames = (location.assignments || []).map(a => a.marshalName || '').join(' ');
    const searchableText = `${location.name || ''} ${location.description || ''} ${areaNames} ${marshalNames}`.toLowerCase();
    return searchTerms.every(term => searchableText.includes(term));
  });
};

// Apply staffing filter
const applyStaffingFilter = (locations) => {
  if (activeFilter.value === 'full') return locations.filter(isFullyStaffed);
  if (activeFilter.value === 'partial') return locations.filter(isPartiallyStaffed);
  if (activeFilter.value === 'missing') return locations.filter(isUnstaffed);
  return locations;
};

// Apply check-in filter
const applyCheckinFilter = (locations) => {
  if (activeFilter.value === 'checkin-full') return locations.filter(isFullyCheckedIn);
  if (activeFilter.value === 'checkin-partial') return locations.filter(isPartiallyCheckedIn);
  if (activeFilter.value === 'checkin-none') return locations.filter(isNotCheckedIn);
  return locations;
};

// Apply area filter
const applyAreaFilter = (locations) => {
  if (!activeAreaFilter.value) return locations;
  return locations.filter(loc => locationBelongsToArea(loc, activeAreaFilter.value));
};

// Apply layer filter
const applyLayerFilter = (locations) => {
  if (!activeLayerFilter.value) return locations;
  return locations.filter(loc => locationBelongsToLayer(loc, activeLayerFilter.value));
};

// Get filtered locations excluding a specific filter category
// Categories: 'staffing', 'checkin', 'area', 'layer'
const getFilteredExcluding = (excludeCategory) => {
  let result = props.locations;
  result = applySearchFilter(result);
  if (excludeCategory !== 'staffing') result = applyStaffingFilter(result);
  if (excludeCategory !== 'checkin') result = applyCheckinFilter(result);
  if (excludeCategory !== 'area') result = applyAreaFilter(result);
  if (excludeCategory !== 'layer') result = applyLayerFilter(result);
  return result;
};

// Base for staffing counts (filtered by everything except staffing)
const staffingBase = computed(() => getFilteredExcluding('staffing'));

// Base for check-in counts (filtered by everything except check-in)
const checkinBase = computed(() => getFilteredExcluding('checkin'));

// Base for area counts (filtered by everything except area)
const areaBase = computed(() => getFilteredExcluding('area'));

// Base for layer counts (filtered by everything except layer)
const layerBase = computed(() => getFilteredExcluding('layer'));

// Total for the status group (filtered by area, layer, search - excludes staffing/checkin)
const totalForStatusGroup = computed(() => staffingBase.value.length);

// Staffing counts based on filtered data
const fullCount = computed(() => staffingBase.value.filter(isFullyStaffed).length);
const partialCount = computed(() => staffingBase.value.filter(isPartiallyStaffed).length);
const missingCount = computed(() => staffingBase.value.filter(isUnstaffed).length);

// Check-in counts based on filtered data
const fullyCheckedInCount = computed(() => checkinBase.value.filter(isFullyCheckedIn).length);
const partiallyCheckedInCount = computed(() => checkinBase.value.filter(isPartiallyCheckedIn).length);
const notCheckedInCount = computed(() => checkinBase.value.filter(isNotCheckedIn).length);

// Area counts based on filtered data
const getAreaCheckpointCountFiltered = (areaId) => {
  return areaBase.value.filter(loc => locationBelongsToArea(loc, areaId)).length;
};

// Layer counts based on filtered data
const getLayerCheckpointCountFiltered = (layerId) => {
  return layerBase.value.filter(loc => locationBelongsToLayer(loc, layerId)).length;
};

// Total count (all filters applied)
const totalFiltered = computed(() => {
  let result = props.locations;
  result = applySearchFilter(result);
  result = applyStaffingFilter(result);
  result = applyCheckinFilter(result);
  result = applyAreaFilter(result);
  result = applyLayerFilter(result);
  return result.length;
});

// Show staffing pills only if 2+ pills would have non-zero counts
const showStaffingPills = computed(() => {
  let visibleCount = 0;
  if (fullCount.value > 0) visibleCount++;
  if (partialCount.value > 0) visibleCount++;
  if (missingCount.value > 0) visibleCount++;
  return visibleCount >= 2;
});

// Show check-in pills only if 2+ pills would have non-zero counts
const showCheckinPills = computed(() => {
  let visibleCount = 0;
  if (fullyCheckedInCount.value > 0) visibleCount++;
  if (partiallyCheckedInCount.value > 0) visibleCount++;
  if (notCheckedInCount.value > 0) visibleCount++;
  return visibleCount >= 2;
});

// Filtering by all active filters
const filteredLocations = computed(() => {
  let result = props.locations;
  result = applySearchFilter(result);
  result = applyStaffingFilter(result);
  result = applyCheckinFilter(result);
  result = applyAreaFilter(result);
  result = applyLayerFilter(result);
  return result;
});

const sortedLocations = computed(() => {
  return [...filteredLocations.value].sort((a, b) => alphanumericCompare(a.name, b.name));
});

// Group checkpoints by area
// Areas appear in the order of their first checkpoint (when sorted alphanumerically)
const groupedByArea = computed(() => {
  const sorted = sortedLocations.value;
  if (sorted.length === 0) return [];

  // Track which areas we've seen and in what order
  const areaOrder = [];
  const areaGroups = new Map();

  // Special key for checkpoints with no area
  const NO_AREA_KEY = '__no_area__';

  for (const checkpoint of sorted) {
    const areaIds = checkpoint.areaIds || checkpoint.AreaIds || [];

    if (areaIds.length === 0) {
      // Checkpoint has no area
      if (!areaGroups.has(NO_AREA_KEY)) {
        areaOrder.push(NO_AREA_KEY);
        areaGroups.set(NO_AREA_KEY, []);
      }
      areaGroups.get(NO_AREA_KEY).push(checkpoint);
    } else {
      // Add checkpoint to each area it belongs to
      for (const areaId of areaIds) {
        if (!areaGroups.has(areaId)) {
          areaOrder.push(areaId);
          areaGroups.set(areaId, []);
        }
        areaGroups.get(areaId).push(checkpoint);
      }
    }
  }

  // Build result array in area order
  return areaOrder.map(areaId => {
    if (areaId === NO_AREA_KEY) {
      return {
        areaId: NO_AREA_KEY,
        areaName: `No ${termsLower.value.area}`,
        checkpoints: areaGroups.get(NO_AREA_KEY),
      };
    }
    const area = props.areas.find(a => a.id === areaId);
    return {
      areaId,
      areaName: area ? area.name : `Unknown ${termsLower.value.area}`,
      checkpoints: areaGroups.get(areaId),
    };
  });
});

// Helper to get active layer name
const getActiveLayerName = () => {
  if (!activeLayerFilter.value) return null;
  const layer = props.layers.find(l => l.id === activeLayerFilter.value);
  return layer ? layer.name : null;
};

// Helper to get active area name
const getActiveAreaName = () => {
  if (!activeAreaFilter.value) return null;
  const area = props.areas.find(a => a.id === activeAreaFilter.value);
  return area ? area.name : null;
};

// Empty message based on filters
const emptyMessage = computed(() => {
  const hasSearch = searchQuery.value.trim();
  const layerName = getActiveLayerName();
  const areaName = getActiveAreaName();

  // Build suffix for area/layer filtering
  const parts = [];
  if (areaName) parts.push(`in ${areaName}`);
  if (layerName) parts.push(`on ${layerName}`);
  const filterSuffix = parts.length > 0 ? ` ${parts.join(' ')}` : '';

  if (activeFilter.value === 'full') {
    if (hasSearch) {
      return `No fully staffed ${termsLower.value.checkpoints}${filterSuffix} match your search.`;
    }
    return `No ${termsLower.value.checkpoints}${filterSuffix} are fully staffed.`;
  }
  if (activeFilter.value === 'partial') {
    if (hasSearch) {
      return `No partially staffed ${termsLower.value.checkpoints}${filterSuffix} match your search.`;
    }
    return `No ${termsLower.value.checkpoints}${filterSuffix} are partially staffed.`;
  }
  if (activeFilter.value === 'missing') {
    if (hasSearch) {
      return `No unstaffed ${termsLower.value.checkpoints}${filterSuffix} match your search.`;
    }
    return `No ${termsLower.value.checkpoints}${filterSuffix} are unstaffed.`;
  }
  if (activeFilter.value === 'checkin-full') {
    if (hasSearch) {
      return `No fully checked in ${termsLower.value.checkpoints}${filterSuffix} match your search.`;
    }
    return `No ${termsLower.value.checkpoints}${filterSuffix} are fully checked in.`;
  }
  if (activeFilter.value === 'checkin-partial') {
    if (hasSearch) {
      return `No partially checked in ${termsLower.value.checkpoints}${filterSuffix} match your search.`;
    }
    return `No ${termsLower.value.checkpoints}${filterSuffix} are partially checked in.`;
  }
  if (activeFilter.value === 'checkin-none') {
    if (hasSearch) {
      return `No ${termsLower.value.checkpoints}${filterSuffix} without check-ins match your search.`;
    }
    return `No ${termsLower.value.checkpoints}${filterSuffix} are without check-ins.`;
  }
  if (filterSuffix) {
    if (hasSearch) {
      return `No ${termsLower.value.checkpoints}${filterSuffix} match your search.`;
    }
    return `No ${termsLower.value.checkpoints}${filterSuffix}.`;
  }
  if (hasSearch) {
    return `No ${termsLower.value.checkpoints} match your search.`;
  }
  return `No ${termsLower.value.checkpoints} yet. Add one to get started!`;
});
</script>

<style scoped>
.checkpoints-tab {
  width: 100%;
}

.tab-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 1rem;
  flex-wrap: wrap;
  gap: 1rem;
}

.button-group {
  display: flex;
  gap: 0.75rem;
}

.status-pills {
  display: flex;
  gap: 0.5rem;
  flex-wrap: wrap;
  align-items: center;
}

.pill-group {
  display: flex;
  gap: 0.5rem;
  flex-wrap: nowrap;
  align-items: center;
}

.pill-group:not(:first-child)::before {
  content: '';
  width: 1px;
  height: 1.25rem;
  background: var(--border-medium);
  margin-right: 0.25rem;
}

.pill-group:empty {
  display: none;
}

.pill-separator {
  width: 1px;
  height: 1.25rem;
  background: var(--border-medium);
  margin: 0 0.25rem;
}

.status-pills :deep(.layer-pill),
.status-pills :deep(.area-pill) {
  border: none;
}

.area-group {
  margin-bottom: 1.5rem;
}

.area-group:last-child {
  margin-bottom: 0;
}

.area-group-header {
  font-size: 1rem;
  font-weight: 600;
  color: var(--text-primary);
  margin: 0 0 0.75rem 0;
  padding-bottom: 0.5rem;
  border-bottom: 1px solid var(--border-light);
}

.empty-state {
  text-align: center;
  padding: 3rem 1rem;
  color: var(--text-secondary);
}

.empty-message {
  margin: 0;
  font-size: 1rem;
}

.btn {
  padding: 0.5rem 1rem;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  font-size: 0.9rem;
  transition: background-color 0.2s;
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

.filters-section {
  margin-bottom: 1rem;
  padding: 0.75rem 1rem;
  background: var(--bg-tertiary);
  border-radius: 8px;
}

.search-group {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.search-input {
  flex: 1;
  max-width: 400px;
  padding: 0.5rem 0.75rem;
  border: 1px solid var(--input-border);
  border-radius: 4px;
  font-size: 0.9rem;
  background: var(--input-bg);
  color: var(--text-primary);
}

.search-input:focus {
  outline: none;
  border-color: var(--accent-primary);
}

.search-input::placeholder {
  color: var(--text-muted);
}
</style>
