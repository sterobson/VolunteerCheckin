<template>
  <div
    class="marshal-checkpoint-card"
    :class="{
      'is-checked-in': effectiveCheckInStatus && !isMarkedForRemoval,
      'is-pending': assignment.isPending,
      'is-marked-for-removal': isMarkedForRemoval,
      'is-locked': isLocked,
    }"
    :style="{ borderLeftColor: isMarkedForRemoval ? undefined : borderColor }"
  >
    <div class="card-header">
      <div class="card-title" @click="handleSelectCheckpoint">
        <span class="checkpoint-name-row">
          <span class="checkpoint-icon" v-html="checkpointIconSvg"></span>
          <span class="checkpoint-name">{{ assignment.locationName }}</span><span v-if="assignment.locationDescription" class="checkpoint-description">
            — {{ assignment.locationDescription }}</span>
        </span>
      </div>
      <button
        v-if="!isLocked && !isMarkedForRemoval"
        type="button"
        class="remove-btn"
        title="Remove assignment"
        @click.stop="$emit('remove', assignment)"
      >
        &times;
      </button>
      <button
        v-if="isMarkedForRemoval"
        type="button"
        class="undo-btn"
        title="Cancel removal"
        @click.stop="$emit('undo-remove', assignment)"
      >
        Undo
      </button>
    </div>

    <div v-if="areaNames.length > 0 && !isMarkedForRemoval" class="card-areas">
      <span
        v-for="(areaName, index) in areaNames"
        :key="index"
        class="area-badge"
        :style="{ backgroundColor: areaColors[index] }"
      >
        {{ areaName }}
      </span>
    </div>

    <!-- Check-in toggle (not shown when pending or marked for removal) -->
    <div v-if="!assignment.isPending && !isMarkedForRemoval && !isLocked" class="card-check-in">
      <button
        class="check-in-toggle"
        :class="{ 'is-checked-in': effectiveCheckInStatus }"
        @click="$emit('toggle-check-in', assignment)"
      >
        <span class="toggle-icon">✓</span>
        <span class="toggle-label-wrapper">
          <span class="toggle-label toggle-label-check-in">Check in</span>
          <span class="toggle-label toggle-label-checked-in">Checked in</span>
        </span>
      </button>
      <span v-if="effectiveCheckInStatus && assignment.checkInTime && !hasUnsavedChanges" class="check-in-time">
        {{ formatTime(assignment.checkInTime) }}
        <span v-if="checkInByDisplay" class="check-in-method">({{ checkInByDisplay }})</span>
      </span>
      <span v-else-if="hasUnsavedChanges" class="unsaved-indicator">
        Unsaved
      </span>
    </div>

    <!-- Locked state -->
    <div v-if="isLocked && !isMarkedForRemoval" class="card-locked">
      <span class="locked-badge">Viewing from this {{ termsLower.checkpoint }}</span>
    </div>

    <!-- Status indicators -->
    <div v-if="assignment.isPending" class="card-badge">
      <span class="pending-indicator">Will be assigned on save</span>
    </div>

    <div v-if="isMarkedForRemoval" class="card-badge">
      <span class="removal-indicator">Will be removed on save</span>
    </div>
  </div>
</template>

<script setup>
import { computed, defineProps, defineEmits } from 'vue';
import { generateCheckpointSvg } from '../../constants/checkpointIcons';
import { useTerminology } from '../../composables/useTerminology';

const { termsLower } = useTerminology();

const props = defineProps({
  assignment: {
    type: Object,
    required: true,
  },
  location: {
    type: Object,
    default: null,
  },
  areas: {
    type: Array,
    default: () => [],
  },
  effectiveCheckInStatus: {
    type: Boolean,
    default: false,
  },
  hasUnsavedChanges: {
    type: Boolean,
    default: false,
  },
  isLocked: {
    type: Boolean,
    default: false,
  },
  isMarkedForRemoval: {
    type: Boolean,
    default: false,
  },
});

const emit = defineEmits(['click', 'toggle-check-in', 'remove', 'undo-remove']);

// Get area IDs from location
const areaIds = computed(() => {
  if (!props.location) return [];
  return props.location.areaIds || props.location.AreaIds || [];
});

// Get area names for display
const areaNames = computed(() => {
  return areaIds.value
    .map(id => {
      const area = props.areas.find(a => a.id === id);
      return area ? area.name : null;
    })
    .filter(Boolean);
});

// Get area colors for badges
const areaColors = computed(() => {
  return areaIds.value
    .map(id => {
      const area = props.areas.find(a => a.id === id);
      return area ? area.color : '#999';
    });
});

// Border color based on resolved style or area
const borderColor = computed(() => {
  if (!props.location) return '#667eea';

  const loc = props.location;
  const resolvedBgColor = loc.resolvedStyleBackgroundColor || loc.ResolvedStyleBackgroundColor || loc.resolvedStyleColor || loc.ResolvedStyleColor;
  if (resolvedBgColor) {
    return resolvedBgColor;
  }

  if (areaIds.value.length > 0) {
    const area = props.areas.find(a => a.id === areaIds.value[0]);
    if (area) return area.color;
  }

  return '#667eea';
});

// Generate checkpoint icon SVG
const checkpointIconSvg = computed(() => {
  if (!props.location) {
    return `<svg width="24" height="24" viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg">
      <circle cx="12" cy="12" r="10" fill="#667eea" stroke="#fff" stroke-width="2"/>
    </svg>`;
  }

  const loc = props.location;
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

const formatTime = (timeString) => {
  if (!timeString) return '';
  const date = new Date(timeString);
  return date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
};

/**
 * Display who checked in the marshal:
 * - If checkedInBy differs from marshal name, show who checked them in
 * - Otherwise show the check-in method (GPS, Manual)
 */
const checkInByDisplay = computed(() => {
  const { checkedInBy, checkInMethod, marshalName } = props.assignment;

  // If someone else checked them in, show who
  if (checkedInBy && checkedInBy !== marshalName) {
    return checkedInBy;
  }

  // Otherwise show the method if available
  return checkInMethod || '';
});

const handleSelectCheckpoint = () => {
  if (!props.assignment.isPending && !props.isMarkedForRemoval && !props.isLocked) {
    emit('click', props.assignment);
  }
};
</script>

<style scoped>
.marshal-checkpoint-card {
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

.marshal-checkpoint-card.is-checked-in {
  background: var(--status-success-bg);
  border-color: var(--accent-success);
}

.marshal-checkpoint-card.is-pending {
  border-style: dashed;
  border-color: var(--warning);
  background: var(--warning-bg-light);
}

.marshal-checkpoint-card.is-marked-for-removal {
  border-style: dashed;
  border-color: var(--danger);
  border-left-width: 1px;
  background: var(--status-danger-bg);
  opacity: 0.8;
}

.marshal-checkpoint-card.is-marked-for-removal .checkpoint-name,
.marshal-checkpoint-card.is-marked-for-removal .checkpoint-description {
  text-decoration: line-through;
  color: var(--text-muted);
}

.card-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: 0.5rem;
}

.card-title {
  flex: 1;
  min-width: 0;
  cursor: pointer;
}

.card-title:hover .checkpoint-name {
  color: var(--accent-primary);
}

.marshal-checkpoint-card.is-marked-for-removal .card-title,
.marshal-checkpoint-card.is-locked .card-title {
  cursor: default;
}

.marshal-checkpoint-card.is-marked-for-removal .card-title:hover .checkpoint-name {
  color: var(--text-muted);
}

.marshal-checkpoint-card.is-locked .card-title:hover .checkpoint-name {
  color: var(--text-primary);
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

.remove-btn {
  width: 24px;
  height: 24px;
  border: none;
  background: var(--bg-tertiary);
  color: var(--text-secondary);
  border-radius: 4px;
  cursor: pointer;
  font-size: 1.1rem;
  line-height: 1;
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
  transition: all 0.15s;
}

.remove-btn:hover {
  background: var(--danger);
  color: white;
}

.undo-btn {
  padding: 0.25rem 0.5rem;
  border: none;
  background: var(--bg-tertiary);
  color: var(--text-secondary);
  border-radius: 4px;
  cursor: pointer;
  font-size: 0.75rem;
  font-weight: 500;
  flex-shrink: 0;
  transition: all 0.15s;
}

.undo-btn:hover {
  background: var(--accent-primary);
  color: white;
}

.card-areas {
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

.card-check-in {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  flex-wrap: wrap;
}

.check-in-toggle {
  display: inline-flex;
  align-items: center;
  gap: 0.375rem;
  padding: 0.375rem 0.75rem;
  border: 2px solid var(--accent-primary);
  border-radius: 20px;
  background: var(--accent-primary);
  color: white;
  font-size: 0.8rem;
  font-weight: 500;
  cursor: pointer;
  transition: background-color 0.5s, border-color 0.5s;
}

.check-in-toggle:hover {
  background: var(--accent-primary-hover);
  border-color: var(--accent-primary-hover);
  transition: background-color 0.15s, border-color 0.15s;
}

.check-in-toggle.is-checked-in {
  border-color: var(--accent-success);
  background: var(--accent-success);
  color: white;
}

.check-in-toggle.is-checked-in:hover {
  background: var(--accent-success-hover);
  border-color: var(--accent-success-hover);
  color: white;
  transition: background-color 0.15s, border-color 0.15s;
}

.toggle-icon {
  font-size: 0.9rem;
  width: 0;
  overflow: hidden;
  transition: width 0.5s;
}

.check-in-toggle.is-checked-in .toggle-icon {
  width: 0.9rem;
}

.toggle-label-wrapper {
  position: relative;
  display: inline-block;
}

.toggle-label {
  transition: opacity 0.5s;
  white-space: nowrap;
}

/* "Checked in" is longer, so it determines the width */
.toggle-label-checked-in {
  opacity: 0;
}

/* "Check in" is positioned on top */
.toggle-label-check-in {
  position: absolute;
  left: 50%;
  top: 50%;
  transform: translate(-50%, -50%);
  opacity: 1;
}

.check-in-toggle.is-checked-in .toggle-label-check-in {
  opacity: 0;
}

.check-in-toggle.is-checked-in .toggle-label-checked-in {
  opacity: 1;
}

.check-in-time {
  font-size: 0.75rem;
  color: var(--text-secondary);
}

.check-in-method {
  color: var(--text-muted);
}

.unsaved-indicator {
  font-size: 0.75rem;
  color: var(--warning-text, #92400e);
  font-style: italic;
}

.card-locked {
  margin-top: 0.25rem;
}

.locked-badge {
  color: var(--text-secondary);
  font-size: 0.8rem;
  font-style: italic;
}

.card-badge {
  margin-top: 0.25rem;
}

.pending-indicator {
  font-size: 0.75rem;
  color: var(--warning-orange);
  font-style: italic;
}

.removal-indicator {
  font-size: 0.75rem;
  color: var(--danger);
  font-style: italic;
}
</style>
