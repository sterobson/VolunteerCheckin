<template>
  <div
    class="marshal-assignment-card"
    :class="{
      'is-checked-in': effectiveCheckInStatus && !isMarkedForRemoval,
      'is-pending': assignment.isPending,
      'is-marked-for-removal': isMarkedForRemoval,
      'is-clickable': isClickable,
    }"
    @click="handleSelectMarshal"
  >
    <div class="card-main">
      <div class="card-title">
        <span class="marshal-name">{{ assignment.marshalName }}</span>
      </div>

      <!-- Check-in toggle (not shown when pending, marked for removal, or read-only) -->
      <div v-if="!assignment.isPending && !isMarkedForRemoval && !readOnly" class="card-check-in">
        <button
          class="check-in-toggle"
          :class="{ 'is-checked-in': effectiveCheckInStatus }"
          @click.stop="$emit('toggle-check-in', assignment)"
        >
          <span class="toggle-icon">{{ effectiveCheckInStatus ? '✓' : '' }}</span>
          <span class="toggle-label">{{ effectiveCheckInStatus ? 'Checked in' : 'Check in' }}</span>
        </button>
        <span v-if="effectiveCheckInStatus && assignment.checkInTime && !hasUnsavedChanges" class="check-in-time">
          {{ formatTime(assignment.checkInTime) }}
          <span v-if="checkInByDisplay" class="check-in-method">({{ checkInByDisplay }})</span>
          <span v-if="formattedDistance" class="check-in-distance" :class="distanceClass">{{ formattedDistance }}</span>
        </span>
        <span v-else-if="hasUnsavedChanges" class="unsaved-indicator">
          Unsaved
        </span>
      </div>

      <!-- Read-only check-in status -->
      <div v-if="!assignment.isPending && !isMarkedForRemoval && readOnly" class="card-status-readonly">
        <span class="status-badge" :class="effectiveCheckInStatus ? 'status-checked-in' : 'status-not-checked-in'">
          <span class="status-icon">{{ effectiveCheckInStatus ? '✓' : '○' }}</span>
          {{ effectiveCheckInStatus ? 'Checked in' : 'Not checked in' }}
        </span>
        <span v-if="effectiveCheckInStatus && assignment.checkInTime" class="check-in-time">
          {{ formatTime(assignment.checkInTime) }}
          <span v-if="checkInByDisplay" class="check-in-method">({{ checkInByDisplay }})</span>
          <span v-if="formattedDistance" class="check-in-distance" :class="distanceClass">{{ formattedDistance }}</span>
        </span>
      </div>

      <!-- Status indicators -->
      <div v-if="assignment.isPending" class="card-badge">
        <span class="pending-indicator">Will be assigned on save</span>
      </div>

      <div v-if="isMarkedForRemoval" class="card-badge">
        <span class="removal-indicator">Will be removed on save</span>
      </div>
    </div>

    <button
      v-if="!readOnly && !isMarkedForRemoval"
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
</template>

<script setup>
import { defineProps, defineEmits, computed } from 'vue';
import { calculateDistance, formatDistance } from '../../utils/coordinateUtils';

const props = defineProps({
  assignment: {
    type: Object,
    required: true,
  },
  effectiveCheckInStatus: {
    type: Boolean,
    default: false,
  },
  hasUnsavedChanges: {
    type: Boolean,
    default: false,
  },
  isMarkedForRemoval: {
    type: Boolean,
    default: false,
  },
  readOnly: {
    type: Boolean,
    default: false,
  },
  location: {
    type: Object,
    default: null,
  },
});

const emit = defineEmits(['click', 'toggle-check-in', 'remove', 'undo-remove']);

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

const isClickable = computed(() => {
  return !props.assignment.isPending && !props.isMarkedForRemoval && !props.readOnly;
});

/**
 * Calculate distance from check-in location to checkpoint
 * Returns null if GPS data is not available
 */
const checkInDistance = computed(() => {
  const { checkInLatitude, checkInLongitude } = props.assignment;
  const location = props.location;

  // Need both check-in coordinates and location coordinates
  if (!checkInLatitude || !checkInLongitude || !location?.latitude || !location?.longitude) {
    return null;
  }

  return calculateDistance(
    checkInLatitude,
    checkInLongitude,
    location.latitude,
    location.longitude
  );
});

/**
 * Format the distance for display
 */
const formattedDistance = computed(() => {
  if (checkInDistance.value === null) return null;
  return formatDistance(checkInDistance.value);
});

/**
 * Get CSS class for distance color coding
 * ≤50m: green, ≤100m: orange, >100m: red
 */
const distanceClass = computed(() => {
  if (checkInDistance.value === null) return '';
  if (checkInDistance.value <= 50) return 'distance-close';
  if (checkInDistance.value <= 100) return 'distance-medium';
  return 'distance-far';
});

const handleSelectMarshal = () => {
  if (isClickable.value) {
    emit('click', props.assignment);
  }
};
</script>

<style scoped>
.marshal-assignment-card {
  background: var(--card-bg);
  border: 1px solid var(--border-color);
  border-radius: 8px;
  padding: 0.875rem;
  display: flex;
  flex-direction: row;
  align-items: flex-start;
  gap: 0.5rem;
  transition: border-color 0.2s, box-shadow 0.2s;
}

.card-main {
  flex: 1;
  min-width: 0;
  display: flex;
  flex-direction: column;
  gap: 0.375rem;
}

/* Compact single-row layout for narrow screens */
@media (max-width: 640px) {
  .marshal-assignment-card {
    align-items: center;
    padding: 0.5rem 0.75rem;
  }

  .card-main {
    flex-direction: row;
    flex-wrap: wrap;
    align-items: center;
    gap: 0.375rem 0.625rem;
  }

  .card-title {
    flex: 1;
    min-width: 0;
  }
}

.marshal-assignment-card.is-checked-in {
  background: var(--status-success-bg);
  border-color: var(--accent-success);
}

.marshal-assignment-card.is-pending {
  border-style: dashed;
  border-color: var(--warning);
  background: var(--warning-bg-light);
}

.marshal-assignment-card.is-marked-for-removal {
  border-style: dashed;
  border-color: var(--danger);
  background: var(--status-danger-bg);
  opacity: 0.8;
}

.marshal-assignment-card.is-marked-for-removal .marshal-name {
  text-decoration: line-through;
  color: var(--text-muted);
}

.marshal-assignment-card.is-clickable {
  cursor: pointer;
}

.marshal-assignment-card.is-clickable:hover {
  border-color: var(--accent-primary);
  box-shadow: var(--shadow-sm);
}

.marshal-assignment-card.is-clickable:hover .marshal-name {
  color: var(--accent-primary);
}

.marshal-assignment-card.is-clickable.is-checked-in:hover {
  border-color: var(--accent-success-hover);
}

.marshal-name {
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

.card-check-in {
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  gap: 0.25rem;
}

@media (max-width: 640px) {
  .card-check-in {
    align-items: flex-end;
  }
}

.check-in-toggle {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 0.375rem;
  padding: 0.375rem 0.75rem;
  border: 2px solid var(--accent-primary);
  border-radius: 20px;
  background: var(--accent-primary);
  color: white;
  font-size: 0.8rem;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.5s;
}

.check-in-toggle:hover {
  background: var(--accent-primary-hover);
  border-color: var(--accent-primary-hover);
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
}

.toggle-icon {
  font-size: 0.9rem;
}

.toggle-icon:empty {
  display: none;
}

.card-status-readonly {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  flex-wrap: wrap;
}

.status-badge {
  display: inline-flex;
  align-items: center;
  gap: 0.25rem;
  padding: 0.2rem 0.6rem;
  border-radius: 12px;
  font-size: 0.75rem;
  font-weight: 500;
}

.status-icon {
  font-size: 0.85rem;
}

.status-badge.status-checked-in {
  background: var(--accent-success);
  color: white;
}

.status-badge.status-not-checked-in {
  background: var(--bg-tertiary);
  color: var(--text-secondary);
}

.check-in-time {
  font-size: 0.75rem;
  color: var(--text-secondary);
}

.check-in-method {
  color: var(--text-muted);
}

.check-in-distance {
  margin-left: 0.5rem;
  padding: 0.1rem 0.4rem;
  border-radius: 8px;
  font-size: 0.7rem;
  font-weight: 600;
}

.check-in-distance.distance-close {
  background: var(--status-success-bg);
  color: var(--accent-success);
}

.check-in-distance.distance-medium {
  background: var(--status-warning-bg);
  color: var(--warning-text, #92400e);
}

.check-in-distance.distance-far {
  background: var(--status-danger-bg);
  color: var(--accent-danger);
}

.unsaved-indicator {
  font-size: 0.75rem;
  color: var(--warning-text, #92400e);
  font-style: italic;
}

.card-badge {
  margin-top: 0.25rem;
}

@media (max-width: 640px) {
  .card-badge {
    margin-top: 0;
  }
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
