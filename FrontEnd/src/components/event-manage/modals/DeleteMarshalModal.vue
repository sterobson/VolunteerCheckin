<template>
  <div v-if="show" class="modal-overlay" @click.self="$emit('close')">
    <div class="delete-modal">
      <h3>Delete {{ terms.person }}</h3>
      <p class="message">
        This {{ termsLower.person }} is also listed as a contact. What would you like to delete?
      </p>

      <div class="options">
        <button
          class="option-btn"
          :class="{ selected: selectedOption === 'marshal' }"
          @click="selectedOption = 'marshal'"
        >
          <div class="option-radio">
            <div class="radio-inner" v-if="selectedOption === 'marshal'"></div>
          </div>
          <div class="option-content">
            <span class="option-title">Delete {{ termsLower.person }} only</span>
            <span class="option-description">Remove the {{ termsLower.person }} record but keep them as a contact</span>
          </div>
        </button>

        <button
          class="option-btn"
          :class="{ selected: selectedOption === 'contact' }"
          @click="selectedOption = 'contact'"
        >
          <div class="option-radio">
            <div class="radio-inner" v-if="selectedOption === 'contact'"></div>
          </div>
          <div class="option-content">
            <span class="option-title">Delete contact only</span>
            <span class="option-description">Remove them from the contacts list but keep the {{ termsLower.person }} record</span>
          </div>
        </button>

        <button
          class="option-btn"
          :class="{ selected: selectedOption === 'both' }"
          @click="selectedOption = 'both'"
        >
          <div class="option-radio">
            <div class="radio-inner" v-if="selectedOption === 'both'"></div>
          </div>
          <div class="option-content">
            <span class="option-title">Delete both</span>
            <span class="option-description">Remove both the {{ termsLower.person }} and contact records</span>
          </div>
        </button>
      </div>

      <div class="modal-actions">
        <button class="btn btn-secondary" @click="$emit('close')">Cancel</button>
        <button class="btn btn-danger" @click="handleConfirm" :disabled="!selectedOption">Delete</button>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, defineProps, defineEmits, watch } from 'vue';
import { useTerminology } from '../../../composables/useTerminology';

const { terms, termsLower } = useTerminology();

const props = defineProps({
  show: {
    type: Boolean,
    default: false,
  },
});

const emit = defineEmits(['close', 'delete']);

const selectedOption = ref('both'); // Default to 'both'

// Reset selection when modal opens
watch(() => props.show, (newVal) => {
  if (newVal) {
    selectedOption.value = 'both';
  }
});

const handleConfirm = () => {
  if (selectedOption.value) {
    emit('delete', selectedOption.value);
  }
};
</script>

<style scoped>
.modal-overlay {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background: var(--modal-overlay);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 3000;
}

.delete-modal {
  background: var(--modal-bg);
  color: var(--text-primary);
  padding: 1.5rem;
  border-radius: 12px;
  max-width: 450px;
  width: 90%;
  box-shadow: var(--shadow-lg);
}

.delete-modal h3 {
  margin: 0 0 0.75rem 0;
  color: var(--text-primary);
  font-size: 1.25rem;
}

.message {
  margin: 0 0 1.25rem 0;
  color: var(--text-secondary);
  line-height: 1.5;
}

.options {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  margin-bottom: 1.5rem;
}

.option-btn {
  display: flex;
  align-items: flex-start;
  gap: 0.75rem;
  padding: 0.875rem 1rem;
  background: var(--bg-secondary);
  border: 1px solid var(--border-color);
  border-radius: 8px;
  cursor: pointer;
  text-align: left;
  transition: all 0.2s;
  width: 100%;
}

.option-btn:hover {
  border-color: var(--accent-primary);
  background: var(--bg-tertiary);
}

.option-btn.selected {
  border-color: var(--danger);
  background: rgba(239, 68, 68, 0.1);
}

.option-radio {
  flex-shrink: 0;
  width: 18px;
  height: 18px;
  border: 2px solid var(--border-color);
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  margin-top: 2px;
  transition: border-color 0.2s;
}

.option-btn.selected .option-radio {
  border-color: var(--danger);
}

.radio-inner {
  width: 10px;
  height: 10px;
  background: var(--danger);
  border-radius: 50%;
}

.option-content {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.option-title {
  font-weight: 500;
  color: var(--text-primary);
}

.option-description {
  font-size: 0.85rem;
  color: var(--text-secondary);
}

.modal-actions {
  display: flex;
  gap: 1rem;
  justify-content: flex-end;
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

.btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.btn-danger {
  background: var(--danger);
  color: white;
}

.btn-danger:hover:not(:disabled) {
  background: var(--danger-hover);
}

.btn-secondary {
  background: var(--bg-tertiary);
  color: var(--text-primary);
}

.btn-secondary:hover {
  background: var(--bg-hover);
}
</style>
