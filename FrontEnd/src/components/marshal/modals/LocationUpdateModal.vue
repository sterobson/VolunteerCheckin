<template>
  <Transition name="modal">
    <div v-if="show" class="modal-overlay" @click.self="$emit('close')">
      <div class="modal-content location-update-modal">
        <div class="modal-header">
          <h3>Update {{ checkpointTermLower }} location</h3>
          <button class="modal-close" @click="$emit('close')">&times;</button>
        </div>

        <div class="modal-body">
          <div v-if="success" class="success-message">
            {{ success }}
          </div>
          <div v-else>
            <p class="modal-description">
              Update the position of <strong>{{ assignment?.location?.name }}</strong>
            </p>

            <!-- Last update info -->
            <div v-if="assignment?.location?.lastLocationUpdate" class="last-update-info">
              Last updated: {{ formatDateTime(assignment.location.lastLocationUpdate) }}
            </div>

            <!-- GPS Status -->
            <div v-if="userLocation" class="gps-status active">
              <svg class="status-icon" viewBox="0 0 24 24" fill="currentColor">
                <path d="M12 8c-2.21 0-4 1.79-4 4s1.79 4 4 4 4-1.79 4-4-1.79-4-4-4zm8.94 3c-.46-4.17-3.77-7.48-7.94-7.94V1h-2v2.06C6.83 3.52 3.52 6.83 3.06 11H1v2h2.06c.46 4.17 3.77 7.48 7.94 7.94V23h2v-2.06c4.17-.46 7.48-3.77 7.94-7.94H23v-2h-2.06zM12 19c-3.87 0-7-3.13-7-7s3.13-7 7-7 7 3.13 7 7-3.13 7-7 7z"/>
              </svg>
              GPS active
            </div>

            <!-- GPS Option -->
            <div class="update-option">
              <button
                @click="$emit('update-gps')"
                class="btn btn-primary btn-full"
                :disabled="updating"
                :style="accentButtonStyle"
              >
                <svg class="btn-icon" viewBox="0 0 24 24" fill="currentColor">
                  <path d="M12 8c-2.21 0-4 1.79-4 4s1.79 4 4 4 4-1.79 4-4-1.79-4-4-4zm8.94 3c-.46-4.17-3.77-7.48-7.94-7.94V1h-2v2.06C6.83 3.52 3.52 6.83 3.06 11H1v2h2.06c.46 4.17 3.77 7.48 7.94 7.94V23h2v-2.06c4.17-.46 7.48-3.77 7.94-7.94H23v-2h-2.06zM12 19c-3.87 0-7-3.13-7-7s3.13-7 7-7 7 3.13 7 7-3.13 7-7 7z"/>
                </svg>
                {{ updating ? 'Updating...' : 'Use current GPS location' }}
              </button>
            </div>

            <!-- Auto-update Option -->
            <div class="update-option auto-update-option">
              <label class="checkbox-label">
                <input
                  type="checkbox"
                  :checked="autoUpdateEnabled"
                  @change="$emit('toggle-auto-update')"
                />
                <span>Auto-update every 60 seconds</span>
              </label>
              <p class="option-hint">Automatically updates the {{ checkpointTermLower }} location using your GPS every minute.</p>
            </div>

            <!-- Select on Map Option -->
            <div class="update-option">
              <button
                @click="$emit('select-on-map')"
                class="btn btn-secondary btn-full"
                :disabled="updating"
              >
                <svg class="btn-icon" viewBox="0 0 24 24" fill="currentColor">
                  <path d="M20.5 3l-.16.03L15 5.1 9 3 3.36 4.9c-.21.07-.36.25-.36.48V20.5c0 .28.22.5.5.5l.16-.03L9 18.9l6 2.1 5.64-1.9c.21-.07.36-.25.36-.48V3.5c0-.28-.22-.5-.5-.5zM15 19l-6-2.11V5l6 2.11V19z"/>
                </svg>
                Select location on map
              </button>
            </div>

            <!-- Copy from Checkpoint Option -->
            <div class="update-option">
              <label class="option-label">Or copy location from another {{ checkpointTermLower }}:</label>
              <div class="checkpoint-list" v-if="availableCheckpoints.length > 0">
                <button
                  v-for="loc in availableCheckpoints"
                  :key="loc.id"
                  @click="$emit('copy-from-checkpoint', loc.id)"
                  class="btn btn-secondary btn-checkpoint-source"
                  :disabled="updating"
                >
                  {{ loc.name }}
                </button>
              </div>
              <p v-else class="no-checkpoints">
                No other {{ checkpointTermPluralLower }} available to copy from.
              </p>
            </div>

            <div v-if="error" class="error">{{ error }}</div>
          </div>
        </div>

        <div class="modal-footer">
          <button @click="$emit('close')" class="btn btn-secondary">
            Cancel
          </button>
        </div>
      </div>
    </div>
  </Transition>
</template>

<script setup>
import { defineProps, defineEmits, computed } from 'vue';

const props = defineProps({
  show: {
    type: Boolean,
    required: true,
  },
  assignment: {
    type: Object,
    default: null,
  },
  userLocation: {
    type: Object,
    default: null,
  },
  autoUpdateEnabled: {
    type: Boolean,
    default: false,
  },
  updating: {
    type: Boolean,
    default: false,
  },
  error: {
    type: String,
    default: '',
  },
  success: {
    type: String,
    default: '',
  },
  availableCheckpoints: {
    type: Array,
    default: () => [],
  },
  checkpointTerm: {
    type: String,
    default: 'Checkpoint',
  },
  checkpointTermPlural: {
    type: String,
    default: 'Checkpoints',
  },
  accentButtonStyle: {
    type: Object,
    default: () => ({}),
  },
});

defineEmits(['close', 'update-gps', 'toggle-auto-update', 'select-on-map', 'copy-from-checkpoint']);

const checkpointTermLower = computed(() => props.checkpointTerm.toLowerCase());
const checkpointTermPluralLower = computed(() => props.checkpointTermPlural.toLowerCase());

const formatDateTime = (dateString) => {
  if (!dateString) return '';
  return new Date(dateString).toLocaleString();
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
  justify-content: center;
  align-items: center;
  z-index: 3000;
}

.modal-content {
  background: var(--card-bg);
  border-radius: 12px;
  max-width: 450px;
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

.modal-header h3 {
  margin: 0;
  font-size: 1.1rem;
  color: var(--text-dark);
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
  padding: 1.5rem;
}

.modal-description {
  margin: 0 0 1.5rem 0;
  color: var(--text-darker);
}

.last-update-info {
  font-size: 0.85rem;
  color: var(--text-secondary);
  margin-bottom: 1rem;
  padding: 0.5rem;
  background: var(--bg-muted);
  border-radius: 4px;
}

.gps-status {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  font-size: 0.85rem;
  color: var(--text-secondary);
  margin-bottom: 1rem;
}

.gps-status.active {
  color: var(--success-dark);
}

.gps-status .status-icon {
  width: 16px;
  height: 16px;
}

.update-option {
  margin-bottom: 1.5rem;
}

.option-label {
  display: block;
  font-size: 0.9rem;
  color: var(--text-darker);
  margin-bottom: 0.5rem;
}

.btn {
  padding: 0.75rem 1rem;
  border: none;
  border-radius: 6px;
  cursor: pointer;
  font-weight: 500;
  font-size: 0.9rem;
  transition: all 0.2s;
  display: inline-flex;
  align-items: center;
  gap: 0.5rem;
}

.btn:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.btn-full {
  width: 100%;
  justify-content: center;
}

.btn-primary {
  background: var(--brand-primary);
  color: white;
}

.btn-primary:hover:not(:disabled) {
  background: var(--brand-primary-hover);
}

.btn-secondary {
  background: var(--btn-cancel-bg);
  color: var(--btn-cancel-text);
}

.btn-secondary:hover:not(:disabled) {
  background: var(--btn-cancel-hover);
}

.btn-icon {
  width: 18px;
  height: 18px;
}

.checkpoint-list {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.btn-checkpoint-source {
  text-align: left;
  padding: 0.75rem 1rem;
}

.no-checkpoints {
  font-size: 0.85rem;
  color: var(--text-light);
  font-style: italic;
}

.auto-update-option {
  background: var(--bg-secondary);
  padding: 1rem;
  border-radius: 8px;
}

.checkbox-label {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  cursor: pointer;
  font-weight: 500;
}

.checkbox-label input[type="checkbox"] {
  width: 18px;
  height: 18px;
  cursor: pointer;
}

.option-hint {
  font-size: 0.8rem;
  color: var(--text-secondary);
  margin: 0.5rem 0 0 0;
  padding-left: 27px;
}

.success-message {
  padding: 1rem;
  background: var(--success-bg-light);
  color: var(--success-dark);
  border-radius: 8px;
  text-align: center;
  font-weight: 500;
}

.error {
  padding: 1rem;
  background: var(--danger-bg-lighter);
  color: var(--danger);
  border-radius: 6px;
  font-size: 0.875rem;
}

.modal-footer {
  display: flex;
  justify-content: flex-end;
  padding: 1rem 1.5rem;
  border-top: 1px solid var(--border-light);
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
