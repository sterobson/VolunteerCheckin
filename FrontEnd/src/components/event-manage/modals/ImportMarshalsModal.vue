<template>
  <BaseModal
    :show="show"
    :title="`Import ${termsLower.people} from CSV`"
    size="medium"
    :confirm-on-close="true"
    :is-dirty="isDirty"
    @close="handleClose"
  >
    <!-- Instruction text -->
    <p class="instruction">Upload a CSV file with columns: Name, Email (optional), Phone (optional), {{ terms.checkpoint }} (optional)</p>

    <!-- CSV Example -->
    <div class="csv-example">
      <strong>Example CSV format:</strong>
      <pre>Name,Email,Phone,Checkpoint
John Doe,john@example.com,555-1234,Checkpoint 1
Jane Smith,jane@example.com,555-5678,Checkpoint 2</pre>
    </div>

    <!-- Form -->
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
        <p>Imported {{ result.marshalsCreated }} {{ termsLower.people }} and {{ result.assignmentsCreated }} assignments</p>
        <div v-if="result.errors && result.errors.length > 0" class="import-errors">
          <strong>Errors:</strong>
          <ul>
            <li v-for="(err, index) in result.errors" :key="index">{{ err }}</li>
          </ul>
        </div>
      </div>
    </form>

    <!-- Action buttons -->
    <template #actions>
      <button type="button" @click="handleClose" class="btn btn-secondary">
        Cancel
      </button>
      <button type="button" @click="handleSubmit" class="btn btn-primary" :disabled="importing">
        {{ importing ? 'Importing...' : `Import ${termsLower.people}` }}
      </button>
    </template>
  </BaseModal>
</template>

<script setup>
import { ref, defineProps, defineEmits, watch } from 'vue';
import BaseModal from '../../BaseModal.vue';
import { useTerminology } from '../../../composables/useTerminology';

const { terms, termsLower } = useTerminology();

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
  margin-bottom: 1rem;
  font-size: 0.9rem;
}

.csv-example {
  background: var(--bg-secondary);
  padding: 1rem;
  border-radius: 4px;
  margin-bottom: 1.5rem;
}

.csv-example strong {
  display: block;
  margin-bottom: 0.5rem;
  color: var(--text-dark);
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

.import-result {
  padding: 0.75rem;
  background: var(--success-bg);
  border: 1px solid var(--success-border);
  border-radius: 4px;
  margin-bottom: 1rem;
}

.import-result p {
  margin: 0 0 0.5rem 0;
  color: var(--success-text);
  font-weight: 500;
}

.import-errors {
  margin-top: 0.5rem;
}

.import-errors strong {
  color: var(--danger-text);
  display: block;
  margin-bottom: 0.25rem;
}

.import-errors ul {
  margin: 0;
  padding-left: 1.5rem;
  color: var(--danger-text);
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
