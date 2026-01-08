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
        <StatusPill
          variant="neutral"
          :active="activeFilter === 'all'"
          @click="setFilter('all')"
        >
          {{ locations.length }} total
        </StatusPill>
        <StatusPill
          v-if="fullCount > 0"
          variant="success"
          :active="activeFilter === 'full'"
          @click="setFilter('full')"
        >
          {{ fullCount }} fully staffed
        </StatusPill>
        <StatusPill
          v-if="partialCount > 0"
          variant="warning"
          :active="activeFilter === 'partial'"
          @click="setFilter('partial')"
        >
          {{ partialCount }} partial
        </StatusPill>
        <StatusPill
          v-if="missingCount > 0"
          variant="danger"
          :active="activeFilter === 'missing'"
          @click="setFilter('missing')"
        >
          {{ missingCount }} unstaffed
        </StatusPill>
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

    <!-- Row 3: Content -->
    <div class="locations-list">
      <div
        v-for="location in sortedLocations"
        :key="location.id"
        class="location-item"
        :style="{ borderLeftColor: getLocationBorderColor(location) }"
        @click="$emit('select-location', location)"
      >
        <div class="location-info">
          <div class="location-header">
            <span class="checkpoint-icon" v-html="getCheckpointIconSvg(location)"></span>
            <strong>{{ location.name }}</strong>
            <span
              v-for="areaId in (location.areaIds || location.AreaIds || [])"
              :key="areaId"
              class="area-badge"
              :style="{ backgroundColor: getAreaColor(areaId) }"
            >
              {{ getAreaName(areaId) }}
            </span>
          </div>
          <p v-if="location.description" class="location-description">
            {{ location.description }}
          </p>
          <div class="location-stats">
            <span class="stat-badge" :class="getStaffingClass(location)">
              {{ location.checkedInCount }}/{{ location.requiredMarshals }} staffed
            </span>
            <span
              v-for="assignment in location.assignments"
              :key="assignment.id"
              class="stat-badge"
              :class="{ 'checked-in': assignment.isCheckedIn }"
            >
              {{ assignment.marshalName }}
            </span>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { defineProps, defineEmits, computed, ref } from 'vue';
import { alphanumericCompare } from '../../../utils/sortUtils';
import { useTerminology } from '../../../composables/useTerminology';
import { generateCheckpointSvg } from '../../../constants/checkpointIcons';
import StatusPill from '../../StatusPill.vue';

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
});

defineEmits(['add-checkpoint-manually', 'import-checkpoints', 'select-location']);

const searchQuery = ref('');
const activeFilter = ref('all');

const setFilter = (filter) => {
  activeFilter.value = filter;
};

// Status pill counts
const fullCount = computed(() => {
  return props.locations.filter(loc => loc.checkedInCount >= loc.requiredMarshals && loc.requiredMarshals > 0).length;
});

const partialCount = computed(() => {
  return props.locations.filter(loc => loc.checkedInCount > 0 && loc.checkedInCount < loc.requiredMarshals).length;
});

const missingCount = computed(() => {
  return props.locations.filter(loc => loc.checkedInCount === 0 && loc.requiredMarshals > 0).length;
});

// Filtering by status pill and search query
const filteredLocations = computed(() => {
  let result = props.locations;

  // Filter by status pill
  if (activeFilter.value === 'full') {
    result = result.filter(loc => loc.checkedInCount >= loc.requiredMarshals && loc.requiredMarshals > 0);
  } else if (activeFilter.value === 'partial') {
    result = result.filter(loc => loc.checkedInCount > 0 && loc.checkedInCount < loc.requiredMarshals);
  } else if (activeFilter.value === 'missing') {
    result = result.filter(loc => loc.checkedInCount === 0 && loc.requiredMarshals > 0);
  }

  // Filter by search query
  if (searchQuery.value.trim()) {
    const searchTerms = searchQuery.value.toLowerCase().trim().split(/\s+/);

    result = result.filter(location => {
      // Build searchable text
      const areaIds = location.areaIds || location.AreaIds || [];
      const areaNames = areaIds.map(id => getAreaName(id) || '').join(' ');
      const marshalNames = (location.assignments || []).map(a => a.marshalName || '').join(' ');

      const searchableText = `${location.name || ''} ${location.description || ''} ${areaNames} ${marshalNames}`.toLowerCase();

      // All search terms must match
      return searchTerms.every(term => searchableText.includes(term));
    });
  }

  return result;
});

const sortedLocations = computed(() => {
  return [...filteredLocations.value].sort((a, b) => alphanumericCompare(a.name, b.name));
});

const getAreaName = (areaId) => {
  const area = props.areas.find((a) => a.id === areaId);
  return area ? area.name : null;
};

const getAreaColor = (areaId) => {
  const area = props.areas.find((a) => a.id === areaId);
  return area ? area.color : '#999';
};

// Get border color based on checkpoint's resolved style or first area color
const getLocationBorderColor = (location) => {
  const resolvedBgColor = location.resolvedStyleBackgroundColor || location.ResolvedStyleBackgroundColor || location.resolvedStyleColor || location.ResolvedStyleColor;
  if (resolvedBgColor) {
    return resolvedBgColor;
  }

  const areaIds = location.areaIds || location.AreaIds || [];
  if (areaIds.length > 0) {
    return getAreaColor(areaIds[0]);
  }

  return '#667eea';
};

// Get staffing status class
const getStaffingClass = (location) => {
  if (location.requiredMarshals === 0) return '';
  if (location.checkedInCount >= location.requiredMarshals) return 'staffed-full';
  if (location.checkedInCount > 0) return 'staffed-partial';
  return 'staffed-none';
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

.locations-list {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.location-item {
  padding: 1rem;
  border: 2px solid var(--border-color);
  border-left: 4px solid var(--accent-primary);
  border-radius: 6px;
  cursor: pointer;
  transition: all 0.3s;
  background: var(--card-bg);
}

.location-item:hover {
  border-color: var(--accent-primary);
  box-shadow: var(--shadow-md);
}

.location-info {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.location-header {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.location-header strong {
  font-size: 1rem;
  color: var(--text-primary);
}

.location-description {
  margin: 0;
  color: var(--text-secondary);
  font-size: 0.9rem;
}

.location-stats {
  display: flex;
  gap: 0.5rem;
  flex-wrap: wrap;
}

.stat-badge {
  padding: 0.25rem 0.75rem;
  background: var(--bg-tertiary);
  border-radius: 12px;
  font-size: 0.75rem;
  color: var(--text-secondary);
  font-weight: 500;
}

.stat-badge.staffed-full {
  background: var(--status-success-bg);
  color: var(--accent-success);
}

.stat-badge.staffed-partial {
  background: var(--status-warning-bg);
  color: var(--warning-text, #92400e);
}

.stat-badge.staffed-none {
  background: var(--status-danger-bg);
  color: var(--accent-danger);
}

.stat-badge.checked-in {
  background: var(--accent-success);
  color: white;
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
