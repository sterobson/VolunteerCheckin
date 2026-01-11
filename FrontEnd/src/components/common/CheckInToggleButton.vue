<template>
  <div class="check-in-toggle-container">
    <button
      class="check-in-toggle"
      :class="{ 'is-checked-in': isCheckedIn }"
      :disabled="disabled || isLoading"
      @click="$emit('toggle')"
    >
      <span v-if="isLoading" class="toggle-spinner"></span>
      <span v-else-if="isCheckedIn" class="toggle-icon">✓</span>
      <span class="toggle-label">{{ buttonLabel }}</span>
    </button>
    <span v-if="isCheckedIn && checkInTime && !isLoading" class="check-in-details">
      {{ formatTime(checkInTime) }}
      <span v-if="detailsDisplay" class="check-in-method">({{ detailsDisplay }})</span>
    </span>
  </div>
</template>

<script setup>
import { computed, defineProps, defineEmits } from 'vue';

const props = defineProps({
  isCheckedIn: {
    type: Boolean,
    default: false,
  },
  checkInTime: {
    type: String,
    default: null,
  },
  checkInMethod: {
    type: String,
    default: null,
  },
  checkedInBy: {
    type: String,
    default: null,
  },
  marshalName: {
    type: String,
    default: null,
  },
  isLoading: {
    type: Boolean,
    default: false,
  },
  disabled: {
    type: Boolean,
    default: false,
  },
});

defineEmits(['toggle']);

const buttonLabel = computed(() => {
  if (props.isLoading) {
    return props.isCheckedIn ? 'Checking out...' : 'Checking in...';
  }
  return props.isCheckedIn ? 'Checked in' : 'Check in';
});

/**
 * Display who checked in or the method:
 * - If checkedInBy differs from marshal name, show who checked them in
 * - Otherwise show the check-in method (GPS, Manual)
 */
const detailsDisplay = computed(() => {
  const { checkedInBy, checkInMethod, marshalName } = props;

  // If someone else checked them in, show who
  if (checkedInBy && checkedInBy !== marshalName) {
    return checkedInBy;
  }

  // Otherwise show the method if available
  return checkInMethod || '';
});

const formatTime = (timeString) => {
  if (!timeString) return '';
  const date = new Date(timeString);
  return date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
};
</script>

<style scoped>
.check-in-toggle-container {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 0.25rem;
}

.check-in-toggle {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 0.375rem;
  padding: 0.5rem 1rem;
  border: 2px solid var(--accent-primary);
  border-radius: 20px;
  background: var(--accent-primary);
  color: white;
  font-size: 0.875rem;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.2s;
  min-width: 100px;
}

.check-in-toggle:hover:not(:disabled) {
  background: var(--accent-primary-hover);
  border-color: var(--accent-primary-hover);
}

.check-in-toggle:disabled {
  opacity: 0.7;
  cursor: not-allowed;
}

.check-in-toggle.is-checked-in {
  border-color: var(--accent-success);
  background: var(--accent-success);
  color: white;
}

.check-in-toggle.is-checked-in:hover:not(:disabled) {
  background: var(--accent-success-hover, #1e7e34);
  border-color: var(--accent-success-hover, #1e7e34);
}

.toggle-icon {
  font-size: 1rem;
  font-weight: bold;
}

.toggle-spinner {
  width: 14px;
  height: 14px;
  border: 2px solid rgba(255, 255, 255, 0.3);
  border-top-color: white;
  border-radius: 50%;
  animation: spin 0.8s linear infinite;
}

@keyframes spin {
  to {
    transform: rotate(360deg);
  }
}

.check-in-details {
  font-size: 0.75rem;
  color: var(--text-secondary);
}

.check-in-method {
  color: var(--text-muted);
}
</style>
