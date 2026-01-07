<template>
  <BaseModal
    :show="show"
    :title="showCreateMode ? `Create new ${termsLower.person}` : `Assign ${termsLower.person} to ${locationName}`"
    size="medium"
    @close="handleClose"
  >
    <!-- Select existing marshal - only show if not creating new -->
    <div v-if="!showCreateMode" class="assign-marshal-section">
      <h3>Select existing {{ termsLower.person }}</h3>
      <select v-model="selectedMarshalId" class="form-input">
        <option value="">Choose a {{ termsLower.person }}...</option>
        <option
          v-for="marshal in marshals"
          :key="marshal.id"
          :value="marshal.id"
        >
          {{ marshal.name }}
          <template v-if="marshal.assignedLocationIds && marshal.assignedLocationIds.length > 0">
            (assigned to {{ marshal.assignedLocationIds.length }} {{ marshal.assignedLocationIds.length > 1 ? termsLower.checkpoints : termsLower.checkpoint }})
          </template>
        </option>
      </select>
      <button
        @click="handleAssignExisting"
        class="btn btn-primary btn-full"
        style="margin-top: 1rem;"
        :disabled="!selectedMarshalId"
      >
        Assign selected {{ termsLower.person }}
      </button>

      <div class="divider">OR</div>

      <button @click="toggleCreateMode" class="btn btn-secondary btn-full">
        Create new {{ termsLower.person }}
      </button>
    </div>

    <!-- Create new marshal form - only show when creating -->
    <div v-else class="assign-marshal-section create-mode">
      <h3>Create new {{ termsLower.person }}</h3>
      <div class="form-group">
        <label>Name *</label>
        <input
          v-model="newMarshalForm.name"
          type="text"
          required
          class="form-input"
        />
      </div>
      <div class="form-group">
        <label>Email</label>
        <input
          v-model="newMarshalForm.email"
          type="email"
          class="form-input"
        />
      </div>
      <div class="form-group">
        <label>Phone</label>
        <input
          v-model="newMarshalForm.phoneNumber"
          type="tel"
          class="form-input"
        />
      </div>
    </div>

    <!-- Action buttons -->
    <template #actions>
      <template v-if="showCreateMode">
        <button @click="handleCancelCreate" class="btn btn-secondary">
          Cancel
        </button>
        <button @click="handleCreateAndAssign" class="btn btn-primary">
          Create & assign
        </button>
      </template>
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
  locationName: {
    type: String,
    default: '',
  },
  marshals: {
    type: Array,
    default: () => [],
  },
});

const emit = defineEmits(['close', 'assign-existing', 'create-and-assign']);

const showCreateMode = ref(false);
const selectedMarshalId = ref('');
const newMarshalForm = ref({
  name: '',
  email: '',
  phoneNumber: '',
});

watch(() => props.show, (newVal) => {
  if (!newVal) {
    // Reset state when modal closes
    showCreateMode.value = false;
    selectedMarshalId.value = '';
    newMarshalForm.value = {
      name: '',
      email: '',
      phoneNumber: '',
    };
  }
});

const toggleCreateMode = () => {
  showCreateMode.value = true;
};

const handleAssignExisting = () => {
  if (selectedMarshalId.value) {
    emit('assign-existing', selectedMarshalId.value);
  }
};

const handleCreateAndAssign = () => {
  if (newMarshalForm.value.name) {
    emit('create-and-assign', { ...newMarshalForm.value });
  }
};

const handleCancelCreate = () => {
  showCreateMode.value = false;
  newMarshalForm.value = {
    name: '',
    email: '',
    phoneNumber: '',
  };
};

const handleClose = () => {
  emit('close');
};
</script>

<style scoped>
.assign-marshal-section {
  margin-bottom: 1rem;
}

.assign-marshal-section h3 {
  margin: 0 0 1rem 0;
  color: var(--text-dark);
  font-size: 1rem;
  font-weight: 600;
}

.create-mode {
  background: var(--bg-secondary);
  padding: 1.5rem;
  border-radius: 8px;
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
  background: var(--input-bg);
  color: var(--text-primary);
}

.divider {
  text-align: center;
  margin: 1.5rem 0;
  color: var(--text-secondary);
  font-weight: 600;
}

.btn {
  padding: 0.5rem 1rem;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  font-size: 0.9rem;
  transition: background-color 0.2s;
}

.btn-full {
  width: 100%;
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
