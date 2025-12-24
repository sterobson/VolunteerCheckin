<template>
  <div v-if="show" class="modal" @click.self="close">
    <div class="modal-content">
      <button @click="close" class="modal-close-btn">✕</button>
      <h2>Import marshals from CSV</h2>
      <p class="instruction">Upload a CSV file with columns: Name, Email (optional), Phone (optional), Checkpoint (optional)</p>

      <div class="csv-example">
        <strong>Example CSV format:</strong>
        <pre>Name,Email,Phone,Checkpoint
John Doe,john@example.com,555-1234,Checkpoint 1
Jane Smith,jane@example.com,555-5678,Checkpoint 2</pre>
      </div>

      <form @submit.prevent="handleSubmit">
        <div class="form-group">
          <label>CSV file</label>
          <input
            type="file"
            accept=".csv"
            @change="handleFileChange"
            required
            class="form-input"
          />
        </div>

        <div v-if="error" class="error">{{ error }}</div>
        <div v-if="result" class="import-result">
          <p>Imported {{ result.marshalsCreated }} marshals and {{ result.assignmentsCreated }} assignments</p>
          <div v-if="result.errors && result.errors.length > 0" class="import-errors">
            <strong>Errors:</strong>
            <ul>
              <li v-for="(err, index) in result.errors" :key="index">{{ err }}</li>
            </ul>
          </div>
        </div>

        <div class="modal-actions">
          <button type="button" @click="close" class="btn btn-secondary">
            Cancel
          </button>
          <button type="submit" class="btn btn-primary" :disabled="importing">
            {{ importing ? 'Importing...' : 'Import marshals' }}
          </button>
        </div>
      </form>
    </div>

    <ConfirmModal
      :show="showConfirmModal"
      title="Unsaved Changes"
      message="You have unsaved changes. Are you sure you want to close?"
      @confirm="handleConfirmClose"
      @cancel="handleCancelClose"
    />
  </div>
</template>

<script setup>
import { ref, defineProps, defineEmits, watch } from 'vue';
import ConfirmModal from '../../ConfirmModal.vue';

const props = defineProps({
  show: {
    type: Boolean,
    default: false,
  },
  importing: {
    type: Boolean,
    default: false,
  },
  error: {
    type: String,
    default: null,
  },
  result: {
    type: Object,
    default: null,
  },
  isDirty: {
    type: Boolean,
    default: false,
  },
});

const emit = defineEmits(['close', 'submit', 'update:isDirty']);

const selectedFile = ref(null);
const showConfirmModal = ref(false);

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

const close = () => {
  if (props.isDirty) {
    showConfirmModal.value = true;
  } else {
    emit('close');
  }
};

const handleConfirmClose = () => {
  showConfirmModal.value = false;
  emit('close');
};

const handleCancelClose = () => {
  showConfirmModal.value = false;
};
</script>

<style scoped>
.modal {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background: rgba(0, 0, 0, 0.5);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1000;
}

.modal-content {
  background: white;
  padding: 2rem;
  border-radius: 12px;
  max-width: 600px;
  width: 90%;
  max-height: 90vh;
  overflow-y: auto;
  position: relative;
}

.modal-close-btn {
  position: absolute;
  top: 1rem;
  right: 1rem;
  background: none;
  border: none;
  font-size: 1.5rem;
  cursor: pointer;
  color: #666;
  width: 32px;
  height: 32px;
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: 4px;
}

.modal-close-btn:hover {
  background: #f0f0f0;
  color: #333;
}

h2 {
  margin: 0 0 1rem 0;
  color: #333;
}

.instruction {
  color: #666;
  margin-bottom: 1rem;
  font-size: 0.9rem;
}

.csv-example {
  background: #f8f9fa;
  padding: 1rem;
  border-radius: 4px;
  margin-bottom: 1.5rem;
}

.csv-example strong {
  display: block;
  margin-bottom: 0.5rem;
  color: #333;
}

.csv-example pre {
  margin: 0;
  font-size: 0.85rem;
  overflow-x: auto;
}

.form-group {
  margin-bottom: 1.5rem;
}

.form-group label {
  display: block;
  margin-bottom: 0.5rem;
  font-weight: 500;
  color: #333;
}

.form-input {
  width: 100%;
  padding: 0.5rem;
  border: 1px solid #ddd;
  border-radius: 4px;
  font-size: 0.9rem;
  box-sizing: border-box;
}

.error {
  color: #dc3545;
  padding: 0.75rem;
  background: #f8d7da;
  border: 1px solid #f5c6cb;
  border-radius: 4px;
  margin-bottom: 1rem;
}

.import-result {
  padding: 0.75rem;
  background: #d4edda;
  border: 1px solid #c3e6cb;
  border-radius: 4px;
  margin-bottom: 1rem;
}

.import-result p {
  margin: 0 0 0.5rem 0;
  color: #155724;
  font-weight: 500;
}

.import-errors {
  margin-top: 0.5rem;
}

.import-errors strong {
  color: #721c24;
  display: block;
  margin-bottom: 0.25rem;
}

.import-errors ul {
  margin: 0;
  padding-left: 1.5rem;
  color: #721c24;
}

.modal-actions {
  display: flex;
  gap: 1rem;
  justify-content: flex-end;
  margin-top: 2rem;
  padding-top: 1.5rem;
  background: white;
  position: sticky;
  bottom: 0;
  border-top: 1px solid #e0e0e0;
  margin-left: -2rem;
  margin-right: -2rem;
  margin-bottom: -2rem;
  padding-left: 2rem;
  padding-right: 2rem;
  padding-bottom: 2rem;
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
  background: #007bff;
  color: white;
}

.btn-primary:hover:not(:disabled) {
  background: #0056b3;
}

.btn-primary:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.btn-secondary {
  background: #6c757d;
  color: white;
}

.btn-secondary:hover {
  background: #545b62;
}
</style>
