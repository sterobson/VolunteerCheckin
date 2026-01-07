<template>
  <BaseModal
    :show="show"
    :title="`Shift ${terms.checkpoint} Times?`"
    size="medium"
    @close="handleCancel"
  >
    <div class="shift-times-content">
      <p class="info-text">
        You've changed the event date/time. Some {{ termsLower.checkpoints }} have custom times set.
      </p>

      <div class="time-delta-info">
        <strong>Event time change:</strong>
        <div class="delta-detail">
          Old: {{ formatDateTime(oldEventDate) }}
        </div>
        <div class="delta-detail">
          New: {{ formatDateTime(newEventDate) }}
        </div>
        <div class="delta-summary">
          Delta: {{ formatTimeDelta(timeDelta) }}
        </div>
      </div>

      <p class="affected-checkpoints">
        <strong>{{ affectedCheckpointsCount }} {{ affectedCheckpointsCount === 1 ? termsLower.checkpoint : termsLower.checkpoints }}</strong> with custom times will be shifted by the same amount.
      </p>

      <div class="option-selection">
        <label class="radio-option">
          <input
            v-model="selectedOption"
            type="radio"
            value="shift"
            name="shift-option"
          />
          <div>
            <strong>Shift all {{ termsLower.checkpoint }} times</strong>
            <small>Recommended: Maintains relative timing</small>
          </div>
        </label>

        <label class="radio-option">
          <input
            v-model="selectedOption"
            type="radio"
            value="keep"
            name="shift-option"
          />
          <div>
            <strong>Keep {{ termsLower.checkpoint }} times unchanged</strong>
            <small>{{ terms.checkpoint }} times will stay at their current values</small>
          </div>
        </label>
      </div>
    </div>

    <template #actions>
      <button type="button" @click="handleCancel" class="btn btn-secondary">
        Cancel
      </button>
      <button type="button" @click="handleConfirm" class="btn btn-primary">
        {{ selectedOption === 'shift' ? 'Shift Times' : 'Keep Times' }}
      </button>
    </template>
  </BaseModal>
</template>

<script setup>
import { ref, computed, defineProps, defineEmits } from 'vue';
import BaseModal from '../../BaseModal.vue';
import { useTerminology } from '../../../composables/useTerminology';

const { terms, termsLower } = useTerminology();

const props = defineProps({
  show: {
    type: Boolean,
    default: false,
  },
  oldEventDate: {
    type: String,
    required: true,
  },
  newEventDate: {
    type: String,
    required: true,
  },
  affectedCheckpointsCount: {
    type: Number,
    default: 0,
  },
});

const emit = defineEmits(['confirm', 'cancel']);

const selectedOption = ref('shift');

const timeDelta = computed(() => {
  const oldDate = new Date(props.oldEventDate);
  const newDate = new Date(props.newEventDate);
  return newDate - oldDate; // milliseconds
});

const formatDateTime = (dateString) => {
  const date = new Date(dateString);
  return date.toLocaleString([], {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
};

const formatTimeDelta = (deltaMs) => {
  const absDelta = Math.abs(deltaMs);
  const sign = deltaMs >= 0 ? '+' : '-';

  const days = Math.floor(absDelta / (1000 * 60 * 60 * 24));
  const hours = Math.floor((absDelta % (1000 * 60 * 60 * 24)) / (1000 * 60 * 60));
  const minutes = Math.floor((absDelta % (1000 * 60 * 60)) / (1000 * 60));

  const parts = [];
  if (days > 0) parts.push(`${days}d`);
  if (hours > 0) parts.push(`${hours}h`);
  if (minutes > 0) parts.push(`${minutes}m`);

  return `${sign}${parts.join(' ')}`;
};

const handleConfirm = () => {
  emit('confirm', {
    shouldShift: selectedOption.value === 'shift',
    timeDelta: timeDelta.value,
  });
};

const handleCancel = () => {
  emit('cancel');
};
</script>

<style scoped>
.shift-times-content {
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
}

.info-text {
  color: var(--text-dark);
  line-height: 1.5;
  margin: 0;
}

.time-delta-info {
  background: var(--bg-secondary);
  padding: 1rem;
  border-radius: 8px;
  border-left: 4px solid var(--accent-primary);
}

.delta-detail {
  color: var(--text-secondary);
  margin: 0.25rem 0;
}

.delta-summary {
  margin-top: 0.5rem;
  color: var(--accent-primary);
  font-weight: 600;
}

.affected-checkpoints {
  color: var(--text-dark);
  margin: 0;
  padding: 0.75rem;
  background: var(--warning-bg);
  border: 1px solid var(--warning);
  border-radius: 4px;
}

.option-selection {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.radio-option {
  display: flex;
  align-items: flex-start;
  gap: 0.75rem;
  padding: 1rem;
  border: 2px solid var(--border-color);
  border-radius: 8px;
  cursor: pointer;
  transition: all 0.2s;
}

.radio-option:hover {
  border-color: var(--accent-primary);
  background: var(--bg-secondary);
}

.radio-option input[type="radio"] {
  margin-top: 0.25rem;
  cursor: pointer;
  width: 1.2rem;
  height: 1.2rem;
}

.radio-option div {
  flex: 1;
}

.radio-option strong {
  display: block;
  color: var(--text-dark);
  margin-bottom: 0.25rem;
}

.radio-option small {
  display: block;
  color: var(--text-secondary);
  font-size: 0.85rem;
}

.btn {
  padding: 0.5rem 1.5rem;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  font-size: 0.9rem;
  font-weight: 500;
  transition: background-color 0.2s;
}

.btn-primary {
  background: var(--btn-primary-bg);
  color: var(--btn-primary-text);
}

.btn-primary:hover {
  background: var(--btn-primary-hover);
}

.btn-secondary {
  background: var(--btn-secondary-bg);
  color: var(--btn-secondary-text);
}

.btn-secondary:hover {
  background: var(--btn-secondary-hover);
}
</style>
