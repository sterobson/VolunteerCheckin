<template>
  <div
    class="checkpoint-card"
    :class="{
      'is-clickable': clickable,
      'staffed-full': staffingStatus === 'full',
      'staffed-partial': staffingStatus === 'partial',
      'staffed-none': staffingStatus === 'none',
    }"
    :style="{ borderLeftColor: borderColor }"
    @click="handleClick"
  >
    <div class="checkpoint-card-header">
      <div class="checkpoint-card-title" :class="{ clickable }">
        <span class="checkpoint-name-row">
          <span class="checkpoint-icon" v-html="checkpointIconSvg"></span>
          <span class="checkpoint-name">{{ checkpoint.name }}</span><span v-if="checkpoint.description" class="checkpoint-description">
            — {{ checkpoint.description }}</span>
        </span>
      </div>
    </div>

    <div v-if="areaIds.length > 0 && !hideAreaPills" class="checkpoint-card-areas">
      <span
        v-for="areaId in areaIds"
        :key="areaId"
        class="area-badge"
        :style="{ backgroundColor: getAreaColor(areaId) }"
      >
        {{ getAreaName(areaId) }}
      </span>
    </div>

    <div class="checkpoint-card-stats">
      <span class="stat-badge staffing-badge" :class="staffingClass">
        {{ assignedCount }}/{{ checkpoint.requiredMarshals }} staffed
      </span>
      <span v-if="assignedCount > 0" class="stat-badge" :class="checkInStatusClass">
        {{ checkedInAssignedCount }}/{{ assignedCount }} checked in
      </span>
    </div>

    <div v-if="sortedAssignments.length > 0" class="checkpoint-card-marshals">
      <span
        v-for="assignment in sortedAssignments"
        :key="assignment.id"
        class="marshal-badge"
        :class="{ 'checked-in': assignment.isCheckedIn }"
      >
        {{ assignment.marshalName }}
      </span>
    </div>
  </div>
</template>

<script setup>
import { computed, defineProps, defineEmits } from 'vue';
import { generateCheckpointSvg } from '../../constants/checkpointIcons';

const props = defineProps({
  checkpoint: {
    type: Object,
    required: true,
  },
  areas: {
    type: Array,
    default: () => [],
  },
  clickable: {
    type: Boolean,
    default: true,
  },
  hideAreaPills: {
    type: Boolean,
    default: false,
  },
});

const emit = defineEmits(['click']);

const areaIds = computed(() => {
  return props.checkpoint.areaIds || props.checkpoint.AreaIds || [];
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
const borderColor = computed(() => {
  const loc = props.checkpoint;
  const resolvedBgColor = loc.resolvedStyleBackgroundColor || loc.ResolvedStyleBackgroundColor || loc.resolvedStyleColor || loc.ResolvedStyleColor;
  if (resolvedBgColor) {
    return resolvedBgColor;
  }

  if (areaIds.value.length > 0) {
    return getAreaColor(areaIds.value[0]);
  }

  return '#667eea';
});

// Staffing status (based on assignments, not check-ins)
const staffingStatus = computed(() => {
  const loc = props.checkpoint;
  const assigned = (loc.assignments || []).length;
  if (loc.requiredMarshals === 0) return null;
  if (assigned >= loc.requiredMarshals) return 'full';
  if (assigned > 0) return 'partial';
  return 'none';
});

// Sorted marshal assignments (alphabetical by name)
const sortedAssignments = computed(() => {
  const assignments = props.checkpoint.assignments || [];
  return [...assignments].sort((a, b) =>
    (a.marshalName || '').localeCompare(b.marshalName || '', undefined, { sensitivity: 'base' })
  );
});

// Checked-in count of assigned marshals
const assignedCount = computed(() => {
  return (props.checkpoint.assignments || []).length;
});

const checkedInAssignedCount = computed(() => {
  return (props.checkpoint.assignments || []).filter(a => a.isCheckedIn).length;
});

const staffingClass = computed(() => {
  if (!staffingStatus.value) return '';
  return `staffing-${staffingStatus.value}`;
});

// Check-in status class for assigned marshals
const checkInStatusClass = computed(() => {
  const total = assignedCount.value;
  const checkedIn = checkedInAssignedCount.value;
  if (total === 0) return '';
  if (checkedIn === 0) return 'checkin-none';
  if (checkedIn >= total) return 'checkin-full';
  return 'checkin-partial';
});

// Generate checkpoint icon SVG based on resolved style
const checkpointIconSvg = computed(() => {
  const loc = props.checkpoint;
  const resolvedType = loc.resolvedStyleType || loc.ResolvedStyleType;
  const resolvedBgColor = loc.resolvedStyleBackgroundColor || loc.ResolvedStyleBackgroundColor || loc.resolvedStyleColor || loc.ResolvedStyleColor;
  const resolvedBorderColor = loc.resolvedStyleBorderColor || loc.ResolvedStyleBorderColor;
  const resolvedIconColor = loc.resolvedStyleIconColor || loc.ResolvedStyleIconColor;
  const resolvedShape = loc.resolvedStyleBackgroundShape || loc.ResolvedStyleBackgroundShape;

  const hasResolvedStyle = (resolvedType && resolvedType !== 'default')
    || resolvedBgColor
    || resolvedBorderColor
    || resolvedIconColor
    || (resolvedShape && resolvedShape !== 'circle');

  if (hasResolvedStyle) {
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

  return `<svg width="24" height="24" viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg">
    <circle cx="12" cy="12" r="10" fill="#667eea" stroke="#fff" stroke-width="2"/>
  </svg>`;
});

const handleClick = () => {
  if (props.clickable) {
    emit('click', props.checkpoint);
  }
};
</script>

<style scoped>
.checkpoint-card {
  background: var(--card-bg);
  border: 1px solid var(--border-color);
  border-left: 4px solid var(--accent-primary);
  border-radius: 8px;
  padding: 0.875rem;
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  transition: border-color 0.2s, box-shadow 0.2s;
}

.checkpoint-card.is-clickable {
  cursor: pointer;
}

.checkpoint-card.is-clickable:hover {
  border-color: var(--accent-primary);
  box-shadow: var(--shadow-sm);
}

.checkpoint-card-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: 0.5rem;
}

.checkpoint-card-title {
  flex: 1;
  min-width: 0;
}

.checkpoint-card-title.clickable:hover .checkpoint-name {
  color: var(--accent-primary);
}

.checkpoint-name-row {
  display: inline;
}

.checkpoint-icon {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  vertical-align: middle;
  width: 24px;
  height: 24px;
  margin-right: 0.5rem;
}

.checkpoint-icon :deep(svg) {
  width: 24px;
  height: 24px;
}

.checkpoint-name {
  font-weight: 600;
  color: var(--text-primary);
  transition: color 0.15s;
}

.checkpoint-description {
  font-size: 0.85rem;
  color: var(--text-secondary);
}

.checkpoint-card-areas {
  display: flex;
  gap: 0.375rem;
  flex-wrap: wrap;
}

.area-badge {
  padding: 0.15rem 0.5rem;
  border-radius: 10px;
  font-size: 0.7rem;
  font-weight: 600;
  color: white;
  text-shadow: 0 1px 2px rgba(0, 0, 0, 0.2);
}

.checkpoint-card-stats {
  display: flex;
  gap: 0.5rem;
  flex-wrap: wrap;
}

.stat-badge {
  padding: 0.2rem 0.6rem;
  background: var(--bg-tertiary);
  border-radius: 12px;
  font-size: 0.75rem;
  color: var(--text-secondary);
  font-weight: 500;
}

.staffing-badge.staffing-full {
  background: var(--status-success-bg);
  color: var(--accent-success);
}

.staffing-badge.staffing-partial {
  background: var(--status-warning-bg);
  color: var(--warning-text, #92400e);
}

.staffing-badge.staffing-none {
  background: var(--status-danger-bg);
  color: var(--accent-danger);
}

.stat-badge.checkin-full {
  background: var(--status-success-bg);
  color: var(--accent-success);
}

.stat-badge.checkin-partial {
  background: var(--status-warning-bg);
  color: var(--warning-text, #92400e);
}

.stat-badge.checkin-none {
  background: var(--status-danger-bg);
  color: var(--accent-danger);
}

.checkpoint-card-marshals {
  display: flex;
  gap: 0.375rem;
  flex-wrap: wrap;
  margin-top: 0.25rem;
}

.marshal-badge {
  padding: 0.15rem 0.5rem;
  background: var(--bg-tertiary);
  border-radius: 10px;
  font-size: 0.7rem;
  font-weight: 500;
  color: var(--text-secondary);
}

.marshal-badge.checked-in {
  background: var(--accent-success);
  color: white;
}
</style>
