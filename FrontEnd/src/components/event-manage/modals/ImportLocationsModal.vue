<template>
  <BaseModal
    :show="show"
    :title="`Import ${termsLower.checkpoints} from CSV`"
    size="medium"
    :confirm-on-close="true"
    :is-dirty="isDirty"
    @close="handleClose"
  >
    <!-- Instruction text -->
    <p class="instruction">Upload a CSV file with columns: Label, Lat/Latitude, Long/Longitude, What3Words (optional), {{ terms.people }} (optional)</p>

    <!-- CSV Example -->
    <div class="csv-example">
      <strong>Example CSV format:</strong>
      <pre>Label,Latitude,Longitude,What3Words,Marshals
Checkpoint 1,51.505,-0.09,filled.count.soap,"John Doe, Jane Smith"
Checkpoint 2,51.510,-0.10,index.home.raft,Bob Wilson</pre>
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

      <div class="form-group">
        <label class="checkbox-label">
          <input type="checkbox" v-model="deleteExisting" @change="handleInput" />
          Delete existing {{ termsLower.checkpoints }} before import
        </label>
      </div>

      <div v-if="error" class="error">{{ error }}</div>
      <div v-if="result" class="import-result">
        <p>Created {{ result.locationsCreated }} {{ termsLower.checkpoints }} and {{ result.assignmentsCreated }} assignments</p>
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
        {{ importing ? 'Importing...' : `Import ${termsLower.checkpoints}` }}
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
const deleteExisting = ref(false);

watch(() => props.show, (newVal) => {
  if (!newVal) {
    selectedFile.value = null;
    deleteExisting.value = false;
  }
});

const handleFileChange = (event) => {
  selectedFile.value = event.target.files[0];
  emit('update:isDirty', true);
};

const handleInput = () => {
  emit('update:isDirty', true);
};

const handleSubmit = () => {
  if (!selectedFile.value) {
    return;
  }
  emit('submit', { file: selectedFile.value, deleteExisting: deleteExisting.value });
};

const handleClose = () => {
  emit('close');
};
</script>

<style scoped>
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

.checkbox-label {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  font-weight: normal;
  cursor: pointer;
}

.checkbox-label input[type="checkbox"] {
  cursor: pointer;
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
