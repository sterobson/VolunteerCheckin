<template>
  <BaseModal
    :show="show"
    title="Upload GPX route"
    size="medium"
    :confirm-on-close="true"
    :is-dirty="isDirty"
    @close="handleClose"
  >
    <!-- Instruction text -->
    <p class="instruction">Upload a GPX file to display the event route on the map</p>

    <!-- Form -->
    <form @submit.prevent="handleSubmit">
      <div class="form-group">
        <label>GPX file</label>
        <input
          type="file"
          accept=".gpx"
          @change="handleFileChange"
          required
          class="form-input"
        />
      </div>

      <div v-if="error" class="error">{{ error }}</div>
    </form>

    <!-- Action buttons -->
    <template #actions>
      <button type="button" @click="handleClose" class="btn btn-secondary">
        Cancel
      </button>
      <button type="button" @click="handleSubmit" class="btn btn-primary" :disabled="uploading">
        {{ uploading ? 'Uploading...' : 'Upload route' }}
      </button>
    </template>
  </BaseModal>
</template>

<script setup>
import { ref, defineProps, defineEmits, watch } from 'vue';
import BaseModal from '../../BaseModal.vue';

const props = defineProps({
  show: {
    type: Boolean,
    default: false,
  },
  uploading: {
    type: Boolean,
    default: false,
  },
  error: {
    type: String,
    default: null,
  },
  isDirty: {
    type: Boolean,
    default: false,
  },
});

const emit = defineEmits(['close', 'submit', 'update:isDirty']);

const selectedFile = ref(null);

watch(() => props.show, (newVal) => {
  if (!newVal) {
    selectedFile.value = null;
  }
});

const handleFileChange = (event) => {
  selectedFile.value = event.target.files[0];
  emit('update:isDirty', true);
};

const handleSubmit = () => {
  if (!selectedFile.value) {
    return;
  }
  emit('submit', selectedFile.value);
};

const handleClose = () => {
  emit('close');
};
</script>

<style scoped>
.instruction {
  color: var(--text-secondary);
  margin-bottom: 1.5rem;
}

.form-group {
  margin-bottom: 1.5rem;
}

.form-group label {
  display: block;
  margin-bottom: 0.5rem;
  font-weight: 500;
  color: var(--text-dark);
}

.form-input {
  width: 100%;
  padding: 0.5rem;
  border: 1px solid var(--border-medium);
  border-radius: 4px;
  font-size: 0.9rem;
  box-sizing: border-box;
}

.error {
  color: var(--danger);
  padding: 0.75rem;
  background: var(--danger-bg);
  border: 1px solid var(--danger-border);
  border-radius: 4px;
  margin-bottom: 1rem;
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
  background: var(--btn-primary-bg);
  color: var(--btn-primary-text);
}

.btn-primary:hover:not(:disabled) {
  background: var(--btn-primary-hover);
}

.btn-primary:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.btn-secondary {
  background: var(--btn-secondary-bg);
  color: var(--btn-secondary-text);
}

.btn-secondary:hover {
  background: var(--btn-secondary-hover);
}
</style>
