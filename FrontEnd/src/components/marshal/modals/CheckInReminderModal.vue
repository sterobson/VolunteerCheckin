<template>
  <Transition name="modal">
    <div v-if="show && checkpoint" class="modal-overlay" @click.self="$emit('dismiss')">
      <div class="modal-content check-in-reminder-modal">
        <div class="modal-header warning">
          <h3>Check-in reminder</h3>
          <button class="modal-close" @click="$emit('dismiss')">&times;</button>
        </div>
        <div class="modal-body">
          <div class="reminder-icon">&#9888;&#65039;</div>
          <p class="reminder-message">
            You have not checked in yet to {{ termsLower.checkpoint }}
            <strong>{{ checkpoint.location?.name }}</strong><span v-if="checkpoint.location?.description"> - {{ checkpoint.location.description }}</span>.
          </p>
          <p class="reminder-hint">
            Please check in when you arrive at your {{ termsLower.checkpoint }}.
          </p>
        </div>
        <div class="modal-footer">
          <button @click="$emit('dismiss')" class="btn btn-secondary">
            Dismiss
          </button>
          <button
            @click="$emit('go-to-checkpoint')"
            class="btn btn-primary"
            :style="accentButtonStyle"
          >
            Go to {{ termsLower.checkpoint }}
          </button>
        </div>
      </div>
    </div>
  </Transition>
</template>

<script setup>
import { defineProps, defineEmits } from 'vue';
import { useTerminology } from '../../../composables/useTerminology';

const { terms, termsLower } = useTerminology();

defineProps({
  show: {
    type: Boolean,
    required: true,
  },
  checkpoint: {
    type: Object,
    default: null,
  },
  accentButtonStyle: {
    type: Object,
    default: () => ({}),
  },
});

defineEmits(['dismiss', 'go-to-checkpoint']);
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
  justify-content: center;
  align-items: center;
  z-index: 3000;
}

.modal-content {
  background: var(--card-bg);
  border-radius: 12px;
  max-width: 400px;
  width: 90%;
  max-height: 90vh;
  overflow-y: auto;
  box-shadow: var(--shadow-xl);
}

.modal-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 1rem 1.5rem;
  border-bottom: 1px solid var(--border-light);
}

.modal-header.warning {
  background: var(--warning-bg);
  border-bottom: 1px solid var(--warning);
}

.modal-header h3 {
  margin: 0;
  font-size: 1.1rem;
  color: var(--warning-text);
}

.modal-close {
  background: none;
  border: none;
  font-size: 1.5rem;
  cursor: pointer;
  color: var(--text-secondary);
  padding: 0;
  line-height: 1;
}

.modal-close:hover {
  color: var(--text-dark);
}

.modal-body {
  text-align: center;
  padding: 2rem 1.5rem;
}

.reminder-icon {
  font-size: 3rem;
  margin-bottom: 1rem;
}

.reminder-message {
  font-size: 1.1rem;
  color: var(--text-dark);
  margin-bottom: 0.75rem;
}

.reminder-hint {
  font-size: 0.9rem;
  color: var(--text-secondary);
  margin: 0;
}

.modal-footer {
  display: flex;
  justify-content: flex-end;
  gap: 0.75rem;
  padding: 1rem 1.5rem;
  border-top: 1px solid var(--border-light);
}

.btn {
  padding: 0.5rem 1rem;
  border: none;
  border-radius: 6px;
  cursor: pointer;
  font-weight: 500;
  font-size: 0.9rem;
  transition: all 0.2s;
}

.btn-secondary {
  background: var(--btn-cancel-bg);
  color: var(--btn-cancel-text);
}

.btn-secondary:hover {
  background: var(--btn-cancel-hover);
}

.btn-primary {
  background: var(--brand-primary);
  color: white;
}

.btn-primary:hover {
  background: var(--brand-primary-hover);
}

/* Modal transition */
.modal-enter-active,
.modal-leave-active {
  transition: opacity 0.3s ease;
}

.modal-enter-active .modal-content,
.modal-leave-active .modal-content {
  transition: transform 0.3s ease;
}

.modal-enter-from,
.modal-leave-to {
  opacity: 0;
}

.modal-enter-from .modal-content,
.modal-leave-to .modal-content {
  transform: scale(0.9);
}
</style>
